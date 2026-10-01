using System.Runtime.InteropServices;
using System.Globalization;

namespace Hex1b;

/// <summary>
/// Unix (Linux/macOS) PTY implementation using native library.
/// Uses proper setsid/TIOCSCTTY for controlling terminal setup,
/// which is required for programs like tmux and screen to work correctly.
/// </summary>
internal sealed partial class UnixPtyHandle : IPtyHandle
{
    private int _masterFd = -1;
    private int _childPid = -1;
    private bool _disposed;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly TimeSpan _startupTimeout;
    private readonly TimeProvider _timeProvider;
    private readonly IUnixPtyStartupInterop _startupInterop;
    private Task? _startupTask;
    private Task? _disposeTask;
    private int? _exitStatus;
    private bool _childReaped;
    private readonly byte[] _readBuffer = new byte[4096];
    private readonly byte[] _readFds = new byte[128];
    
    public int ProcessId => _childPid;

    public UnixPtyHandle(TimeSpan? startupTimeout = null)
        : this(startupTimeout ?? UnixPtyStartupOptions.DefaultTimeout, TimeProvider.System, new NativeUnixPtyStartupInterop())
    {
    }

    internal UnixPtyHandle(TimeSpan startupTimeout, TimeProvider timeProvider, IUnixPtyStartupInterop startupInterop)
    {
        _startupTimeout = UnixPtyStartupOptions.ValidateTimeout(startupTimeout);
        _timeProvider = timeProvider;
        _startupInterop = startupInterop;
    }
    
    public Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_startupTask is not null)
                throw new InvalidOperationException("The Unix PTY has already been started.");
            return _startupTask = StartCoreAsync(fileName, arguments, workingDirectory, environment, width, height, ct);
        }
    }

    private async Task StartCoreAsync(string fileName, string[] arguments, string? workingDirectory,
        Dictionary<string, string> environment, int width, int height, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetimeCancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        _startupInterop.ValidateLibrary();
        string resolvedPath = ResolveExecutablePath(fileName);
        var cwd = workingDirectory ?? Environment.CurrentDirectory;
        if (cwd.Contains('\0'))
            throw new ArgumentException("The working directory must not contain a NUL character.", nameof(workingDirectory));
        
        // Pass a complete, null-terminated environment without mutating the hosting process.
        var envp = new string[environment.Count + 1];
        var environmentIndex = 0;
        foreach (var (key, value) in environment)
        {
            if (key.Length == 0 || key.Contains('=') || key.Contains('\0') || value.Contains('\0'))
                throw new ArgumentException("Environment names and values must be valid execve strings.", nameof(environment));
            envp[environmentIndex++] = $"{key}={value}";
        }

        try
        {
            await Task.Run(() => StartAndConfirm(resolvedPath, arguments, cwd, envp, width, height, cancellation.Token),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }

    private void StartAndConfirm(string executable, string[] arguments, string cwd, string[] environment,
        int width, int height, CancellationToken ct)
    {
        UnixPtyStartupHandles? pending = null;
        var state = new UnixPtyStartupState();
        var startedAt = _timeProvider.GetTimestamp();
        try
        {
            ct.ThrowIfCancellationRequested();
            pending = _startupInterop.Begin(executable, arguments, cwd, environment, width, height);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var elapsed = _timeProvider.GetElapsedTime(startedAt);
                ThrowIfStartupExpired(executable, cwd, elapsed, state);
                var pollMilliseconds = _startupTimeout == Timeout.InfiniteTimeSpan
                    ? 50
                    : (int)Math.Min(50, Math.Ceiling((_startupTimeout - elapsed).TotalMilliseconds));
                var result = _startupInterop.Poll(pending.Value.StartupFd, pollMilliseconds, ref state, out var stage, out var error);
                if (result < 0)
                    throw NativeUnixPtyStartupInterop.CreateStartupException(executable, cwd, stage, error);

                if (result != 0)
                    continue;

                ct.ThrowIfCancellationRequested();
                ThrowIfStartupExpired(executable, cwd, _timeProvider.GetElapsedTime(startedAt), state);
                var startupFd = pending.Value.StartupFd;
                pending = pending.Value with { StartupFd = -1 };
                _startupInterop.CloseStartup(startupFd);
                lock (_lifecycleLock)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    ct.ThrowIfCancellationRequested();
                    _masterFd = pending.Value.MasterFd;
                    _childPid = pending.Value.ChildPid;
                    pending = null;
                }
                return;
            }
        }
        catch (Exception failure)
        {
            if (pending is { } handles)
            {
                try
                {
                    _startupInterop.Abort(handles);
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException("Unix PTY startup failed and child cleanup also failed.", failure, cleanupFailure);
                }
                if (failure is TimeoutException)
                {
                    throw new TimeoutException(
                        failure.Message + " The child was terminated if still running and reaped.", failure);
                }
            }
            throw;
        }
    }

    private void ThrowIfStartupExpired(string executable, string cwd, TimeSpan elapsed, UnixPtyStartupState state)
    {
        if (_startupTimeout == Timeout.InfiniteTimeSpan || elapsed < _startupTimeout)
            return;
        var checkpoint = state.Ready == 0
            ? "No pre-exec confirmation was received."
            : "Pre-exec confirmation was received, but exec completion was not confirmed.";
        throw new TimeoutException(
            $"Unix PTY startup handshake for '{executable}' in working directory '{cwd}' exceeded the configured timeout " +
            $"of {_startupTimeout.TotalSeconds.ToString("G", CultureInfo.InvariantCulture)} seconds " +
            $"(elapsed {elapsed.TotalSeconds.ToString("G", CultureInfo.InvariantCulture)} seconds). {checkpoint} " +
            "Configure UnixPtyStartupTimeout to change this limit.");
    }
    
    private static string ResolveExecutablePath(string fileName)
    {
        if (fileName.Contains('/'))
        {
            return Path.GetFullPath(fileName);
        }
        
        var pathEnv = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(':'))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        
        return fileName;
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        if (_masterFd < 0 || _disposed)
            return ReadOnlyMemory<byte>.Empty;
        
        try
        {
            return await Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    Array.Clear(_readFds);
                    FD_SET(_masterFd, _readFds);
                    
                    int result = select(_masterFd + 1, _readFds, IntPtr.Zero, IntPtr.Zero, 100);
                    
                    if (result < 0)
                    {
                        int errno = Marshal.GetLastPInvokeError();
                        if (errno == 4) continue; // EINTR
                        return ReadOnlyMemory<byte>.Empty;
                    }
                    
                    if (result == 0)
                    {
                        if (!IsChildRunning(_childPid))
                            return ReadOnlyMemory<byte>.Empty;
                        continue;
                    }
                    
                    if (FD_ISSET(_masterFd, _readFds))
                    {
                        nint bytesRead = read(_masterFd, _readBuffer, (nuint)_readBuffer.Length);
                        if (bytesRead <= 0)
                            return ReadOnlyMemory<byte>.Empty;
                        
                        var resultBuf = new byte[bytesRead];
                        Array.Copy(_readBuffer, resultBuf, (int)bytesRead);
                        
                        return new ReadOnlyMemory<byte>(resultBuf);
                    }
                }
                return ReadOnlyMemory<byte>.Empty;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }
    
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_masterFd < 0 || _disposed || data.IsEmpty)
            return ValueTask.CompletedTask;
        
        try
        {
            var buffer = data.ToArray();
            nint remaining = buffer.Length;
            nint offset = 0;
            
            while (remaining > 0)
            {
                nint written = write(_masterFd, buffer, offset, remaining);
                if (written < 0)
                {
                    int errno = Marshal.GetLastPInvokeError();
                    if (errno == 4) continue; // EINTR
                    break;
                }
                offset += written;
                remaining -= written;
            }
        }
        catch
        {
            // Write error - ignore
        }
        
        return ValueTask.CompletedTask;
    }
    
    private static void FD_SET(int fd, byte[] fdset)
    {
        int index = fd / 8;
        int bit = fd % 8;
        if (index < fdset.Length)
            fdset[index] |= (byte)(1 << bit);
    }
    
    private static bool FD_ISSET(int fd, byte[] fdset)
    {
        int index = fd / 8;
        int bit = fd % 8;
        if (index >= fdset.Length) return false;
        return (fdset[index] & (1 << bit)) != 0;
    }
    
    public void Resize(int width, int height)
    {
        if (_masterFd < 0 || _disposed)
            return;
        
        pty_resize(_masterFd, width, height);
    }
    
    public void Kill(int signal = 15)
    {
        lock (_lifecycleLock)
        {
            if (_childPid > 0 && !_childReaped)
                _ = KillProcess(_childPid, signal);
        }
    }
    
    private static bool IsChildRunning(int pid)
    {
        return pid > 0 && KillProcess(pid, 0) == 0;
    }
    
    public async Task<int> WaitForExitAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            lock (_lifecycleLock)
            {
                if (_exitStatus is { } exitStatus)
                    return exitStatus;
                if (_childPid <= 0)
                    return -1;
                var result = pty_wait(_childPid, 100, out var status);
                if (result == 0)
                {
                    _childReaped = true;
                    _exitStatus = status;
                    return status;
                }
                if (result < 0)
                {
                    var error = Marshal.GetLastPInvokeError();
                    if (error == 4)
                        continue;
                    if (error == 10) // ECHILD: another owner has already reaped it.
                        _childReaped = true;
                    return -1;
                }
            }
            await Task.Delay(10, ct);
        }
        
        return -1;
    }
    
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                _disposeTask = Task.Run(DisposeCoreAsync);
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            _lifetimeCancellation.Cancel();
            if (_startupTask is not null)
                await _startupTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            UnixPtyStartupHandles handles;
            lock (_lifecycleLock)
            {
                handles = new(_masterFd, _childReaped ? -1 : _childPid, -1);
                _masterFd = -1;
                _childPid = -1;
            }
            if (handles.MasterFd >= 0 || handles.ChildPid > 0)
                _startupInterop.Abort(handles);
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    // === P/Invoke declarations ===
    
    [LibraryImport("hex1binterop", EntryPoint = "hex1b_wait", SetLastError = true)]
    private static partial int pty_wait(int pid, int timeoutMs, out int status);
    
    [LibraryImport("hex1binterop", EntryPoint = "hex1b_resize", SetLastError = true)]
    private static partial int pty_resize(int masterFd, int width, int height);
    
    [LibraryImport("libc", EntryPoint = "select", SetLastError = true)]
    private static partial int select(int nfds, byte[] readfds, IntPtr writefds, IntPtr exceptfds, ref Timeval timeout);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct Timeval
    {
        public long tv_sec;
        public long tv_usec;
    }
    
    private static int select(int nfds, byte[] readfds, IntPtr writefds, IntPtr exceptfds, int timeoutMs)
    {
        var tv = new Timeval
        {
            tv_sec = timeoutMs / 1000,
            tv_usec = (timeoutMs % 1000) * 1000
        };
        return select(nfds, readfds, writefds, exceptfds, ref tv);
    }
    
    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static partial nint read(int fd, byte[] buf, nuint count);
    
    private static nint write(int fd, byte[] buf, nint offset, nint count)
    {
        unsafe
        {
            fixed (byte* ptr = buf)
            {
                return writePtr(fd, ptr + offset, (nuint)count);
            }
        }
    }
    
    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    private static unsafe partial nint writePtr(int fd, byte* buf, nuint count);
    
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int KillProcess(int pid, int sig);
}
