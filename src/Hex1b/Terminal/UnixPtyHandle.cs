using System.Runtime.InteropServices;

namespace Hex1b.Terminal;

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
    private readonly byte[] _readBuffer = new byte[4096];
    private readonly byte[] _readFds = new byte[128];
    
    public int ProcessId => _childPid;
    
    public async Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        // Check if native library is available - it's REQUIRED for proper PTY operation
        if (!IsNativeLibraryAvailable())
        {
            throw new InvalidOperationException(
                "Native hex1binterop library not found. This library is required for proper PTY operation. " +
                "Programs like tmux and screen require a proper controlling terminal which can only be " +
                "established via the native library. Please ensure libhex1binterop.so (Linux) or " +
                "libhex1binterop.dylib (macOS) is in the application directory or a standard library path.");
        }
        
        string resolvedPath = ResolveExecutablePath(fileName);
        
        // The native pty_forkpty functions inherit the parent's environment.
        // We need to temporarily set HEX1B_NESTING_LEVEL so the child inherits the correct value,
        // then restore the original value after fork.
        // Note: This is not thread-safe, but acceptable for this diagnostic variable.
        const string nestingLevelKey = "HEX1B_NESTING_LEVEL";
        string? originalNestingLevel = System.Environment.GetEnvironmentVariable(nestingLevelKey);
        
        try
        {
            // Set the nesting level from the environment dictionary (which has the incremented value)
            if (environment.TryGetValue(nestingLevelKey, out var newNestingLevel))
            {
                System.Environment.SetEnvironmentVariable(nestingLevelKey, newNestingLevel);
            }
            
            int result;
            
            // If arguments are provided, use the exec function which passes them through
            // Otherwise, use the shell function which creates a login shell
            if (arguments.Length > 0)
            {
                // Build argv array: [fileName, ...arguments, null]
                var argv = new string[arguments.Length + 2];
                argv[0] = resolvedPath;
                for (int i = 0; i < arguments.Length; i++)
                {
                    argv[i + 1] = arguments[i];
                }
                // Last element is implicitly null for strings array in marshalling
                
                result = pty_forkpty_exec(
                    resolvedPath,
                    argv,
                    arguments.Length + 1, // argc includes argv[0]
                    workingDirectory ?? System.Environment.CurrentDirectory,
                    width,
                    height,
                    out _masterFd,
                    out _childPid);
                
                if (result < 0)
                {
                    throw new InvalidOperationException($"pty_forkpty_exec failed with error: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                // No arguments - start as login shell
                result = pty_forkpty_shell(
                    resolvedPath,
                    workingDirectory ?? System.Environment.CurrentDirectory,
                    width,
                    height,
                    out _masterFd,
                    out _childPid);
                
                if (result < 0)
                {
                    throw new InvalidOperationException($"pty_forkpty_shell failed with error: {Marshal.GetLastWin32Error()}");
                }
            }
        }
        finally
        {
            // Restore the original nesting level in the parent process
            System.Environment.SetEnvironmentVariable(nestingLevelKey, originalNestingLevel);
        }
        
        // Small delay to let child process initialize
        await Task.Delay(50, ct);
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
    
    private static bool IsNativeLibraryAvailable()
    {
        try
        {
            return NativeLibrary.TryLoad("hex1binterop", typeof(UnixPtyHandle).Assembly, null, out _);
        }
        catch
        {
            return false;
        }
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
        
        _ = pty_resize(_masterFd, width, height);
    }
    
    public void Kill(int signal = 15)
    {
        if (_childPid > 0 && IsChildRunning(_childPid))
        {
            _ = KillProcess(_childPid, signal);
        }
    }
    
    private static bool IsChildRunning(int pid)
    {
        return KillProcess(pid, 0) == 0;
    }
    
    public async Task<int> WaitForExitAsync(CancellationToken ct)
    {
        if (_childPid <= 0)
            return -1;
        
        while (!ct.IsCancellationRequested)
        {
            int status;
            int result = pty_wait(_childPid, 100, out status);
            if (result == 0)
            {
                return status;
            }
            else if (result < 0)
            {
                return -1;
            }
            await Task.Delay(10, ct);
        }
        
        return -1;
    }
    
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;
        
        _disposed = true;
        
        if (_masterFd >= 0)
        {
            close(_masterFd);
            _masterFd = -1;
        }
        
        if (_childPid > 0 && IsChildRunning(_childPid))
        {
            _ = KillProcess(_childPid, SIGKILL);
            
            for (int i = 0; i < 10 && IsChildRunning(_childPid); i++)
            {
                Thread.Sleep(10);
            }
            
            _ = pty_wait(_childPid, 100, out _);
        }
        
        return ValueTask.CompletedTask;
    }
    
    // === P/Invoke declarations ===
    
    private const int SIGKILL = 9;
    
    [LibraryImport("hex1binterop", EntryPoint = "hex1b_forkpty_shell", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int pty_forkpty_shell(
        string shellPath,
        string workingDir,
        int width,
        int height,
        out int masterFd,
        out int childPid);
    
    [LibraryImport("hex1binterop", EntryPoint = "hex1b_forkpty_exec", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int pty_forkpty_exec(
        string execPath,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] argv,
        int argc,
        string workingDir,
        int width,
        int height,
        out int masterFd,
        out int childPid);
    
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
    
    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int close(int fd);
    
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int KillProcess(int pid, int sig);
}
