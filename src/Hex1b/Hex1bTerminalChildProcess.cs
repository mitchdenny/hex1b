using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Hex1b;

/// <summary>
/// Represents a child process attached to a pseudo-terminal (PTY).
/// The process's stdin/stdout/stderr are connected to the PTY, making
/// it believe it's running in an interactive terminal.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IHex1bTerminalWorkloadAdapter"/>, allowing it to be
/// used directly with <see cref="Hex1bTerminal"/> as the workload source.
/// </para>
/// <para>
/// Platform support:
/// <list type="bullet">
///   <item>Linux/macOS: Uses POSIX PTY APIs (posix_openpt, forkpty, etc.)</item>
///   <item>Windows: Uses ConPTY APIs (CreatePseudoConsole, etc.)</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Launch bash with a PTY attached
/// await using var process = new Hex1bTerminalChildProcess("/bin/bash", "-l");
/// await process.StartAsync();
/// 
/// // Connect to Hex1bTerminal
/// using var terminal = new Hex1bTerminal(process, 80, 24);
/// 
/// // Wait for process to exit
/// var exitCode = await process.WaitForExitAsync();
/// </code>
/// </example>
public sealed class Hex1bTerminalChildProcess : IHex1bTerminalWorkloadAdapter
{
    private readonly string _fileName;
    private readonly string[] _arguments;
    private readonly string? _workingDirectory;
    private readonly Dictionary<string, string>? _environment;
    private readonly bool _inheritEnvironment;
    private readonly Func<TimeSpan, IPtyHandle> _ptyHandleFactory;
    private readonly object _lifecycleLock = new();
    private readonly CancellationTokenSource _startupCancellation = new();
    private readonly TaskCompletionSource _startupFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _disposeTask;
    private TimeSpan _unixPtyStartupTimeout = UnixPtyStartupOptions.DefaultTimeout;
    
    private int _width;
    private int _height;
    private bool _startInitiated;
    private bool _started;
    private bool _exited;
    private int _exitCode;
    private bool _disposed;
    private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    // Platform-specific PTY handle (will be implemented per-platform)
    private IPtyHandle? _ptyHandle;
    
    /// <summary>
    /// Creates a new child process configuration.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Command-line arguments.</param>
    public Hex1bTerminalChildProcess(string fileName, params string[] arguments)
        : this(fileName, arguments, workingDirectory: null, environment: null, inheritEnvironment: true)
    {
    }
    
    /// <summary>
    /// Creates a new child process configuration with full options.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="workingDirectory">Working directory for the process. Null uses current directory.</param>
    /// <param name="environment">Additional environment variables. Null uses none.</param>
    /// <param name="inheritEnvironment">Whether to inherit the parent's environment variables.</param>
    /// <param name="initialWidth">Initial terminal width in columns.</param>
    /// <param name="initialHeight">Initial terminal height in rows.</param>
    public Hex1bTerminalChildProcess(
        string fileName,
        string[] arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environment = null,
        bool inheritEnvironment = true,
        int initialWidth = 80,
        int initialHeight = 24)
        : this(
            fileName,
            arguments,
            workingDirectory,
            environment,
            inheritEnvironment,
            initialWidth,
            initialHeight,
            timeout => CreatePtyHandle(unixPtyStartupTimeout: timeout))
    {
    }

    internal Hex1bTerminalChildProcess(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string>? environment,
        bool inheritEnvironment,
        int initialWidth,
        int initialHeight,
        Func<TimeSpan, IPtyHandle> ptyHandleFactory)
    {
        _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        _arguments = arguments ?? [];
        _workingDirectory = workingDirectory;
        _environment = environment;
        _inheritEnvironment = inheritEnvironment;
        _ptyHandleFactory = ptyHandleFactory ?? throw new ArgumentNullException(nameof(ptyHandleFactory));
        _width = initialWidth;
        _height = initialHeight;
    }
    
    // === Process Information ===
    
    /// <summary>
    /// Gets the file name (executable path) of the process.
    /// </summary>
    public string FileName => _fileName;
    
    /// <summary>
    /// Gets the arguments passed to the process.
    /// </summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>
    /// Gets or initializes the Unix PTY startup-handshake timeout. Defaults to 10 seconds.
    /// </summary>
    /// <remarks>
    /// Accepts any positive duration or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// Infinite waiting still honors cancellation and disposal. This bounds the Unix
    /// pre-exec/exec handshake, not shell-prompt or application readiness, and has no
    /// effect on Windows. Cleanup can outlast the deadline when kernel operations block.
    /// This experimental option's name, scope, and policy may change.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative other than infinite.</exception>
    [Experimental("HEX1B_UNIX_PTY_STARTUP")]
    public TimeSpan UnixPtyStartupTimeout
    {
        get => _unixPtyStartupTimeout;
        init => _unixPtyStartupTimeout = UnixPtyStartupOptions.ValidateTimeout(value);
    }
    
    /// <summary>
    /// Gets the process ID of the child process. Returns -1 if not started.
    /// </summary>
    public int ProcessId { get { lock (_lifecycleLock) return _started ? _ptyHandle?.ProcessId ?? -1 : -1; } }
    
    /// <summary>
    /// Gets whether the process has been started.
    /// </summary>
    public bool HasStarted { get { lock (_lifecycleLock) return _started; } }
    
    /// <summary>
    /// Gets whether the process has exited.
    /// </summary>
    public bool HasExited => _exited;
    
    /// <summary>
    /// Gets the exit code of the process. Only valid after <see cref="HasExited"/> is true.
    /// </summary>
    public int ExitCode => _exitCode;
    
    /// <summary>
    /// Gets the current terminal width.
    /// </summary>
    public int Width => _width;
    
    /// <summary>
    /// Gets the current terminal height.
    /// </summary>
    public int Height => _height;
    
    // === Lifecycle ===
    
    /// <summary>
    /// Starts the child process with an attached PTY.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The process has already been started.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <exception cref="TimeoutException">The Unix startup handshake exceeded its configured timeout.</exception>
    /// <exception cref="OperationCanceledException">Startup was canceled.</exception>
    /// <remarks>
    /// A failed attempt is single-use and cannot be retried on this instance.
    /// Failure to enter the requested working directory is fatal. Unix success confirms
    /// the pre-exec/exec handshake, not application readiness. Cancellation or timeout
    /// racing with exec cannot guarantee that the target never briefly ran.
    /// </remarks>
    public async Task StartAsync(CancellationToken ct = default)
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_startInitiated)
                throw new InvalidOperationException("Process has already been started.");
            _startInitiated = true;
        }

        IPtyHandle? handle = null;
        try
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, _startupCancellation.Token);
            var startupToken = linkedCancellation.Token;
            startupToken.ThrowIfCancellationRequested();
            handle = _ptyHandleFactory(_unixPtyStartupTimeout);
            var env = BuildEnvironment();
            var workingDirectory = string.IsNullOrWhiteSpace(_workingDirectory)
                ? Environment.CurrentDirectory
                : _workingDirectory;
            int startWidth, startHeight;
            lock (_lifecycleLock)
            {
                startWidth = _width;
                startHeight = _height;
            }

            await handle.StartAsync(_fileName, _arguments, workingDirectory, env, startWidth, startHeight, startupToken);

            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                startupToken.ThrowIfCancellationRequested();
                // Serialize the catch-up resize and publication with concurrent resizes.
                if (_width != startWidth || _height != startHeight)
                    handle.Resize(_width, _height);
                startupToken.ThrowIfCancellationRequested();
                _ptyHandle = handle;
                _started = true;
                _startedTcs.TrySetResult();
            }
        }
        catch (Exception error)
        {
            if (handle is not null)
                await CleanupFailedStartAsync(handle, error);

            if (error is OperationCanceledException)
            {
                _startedTcs.TrySetCanceled(ct.IsCancellationRequested ? ct : new CancellationToken(true));
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(error.Message, error, ct);
            }
            else
                _startedTcs.TrySetException(error);
            throw;
        }
        finally
        {
            _startupFinished.TrySetResult();
        }
    }
    
    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code of the process.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        if (!_started)
            throw new InvalidOperationException("Process has not been started.");
        
        if (_exited)
            return _exitCode;
        
        _exitCode = await _ptyHandle!.WaitForExitAsync(ct);
        _exited = true;
        Disconnected?.Invoke();
        
        return _exitCode;
    }
    
    /// <summary>
    /// Sends a signal to the process (Unix) or terminates it (Windows).
    /// </summary>
    /// <param name="signal">The signal number (Unix only). Default is SIGTERM (15).</param>
    public void Kill(int signal = 15)
    {
        if (!_started || _exited)
            return;
        
        _ptyHandle?.Kill(signal);
    }
    
    // === IHex1bTerminalWorkloadAdapter Implementation ===
    
    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        // Wait for process to start (allows terminal to be created before process starts)
        await _startedTcs.Task.WaitAsync(ct);
        
        if (_disposed || _ptyHandle == null)
            return ReadOnlyMemory<byte>.Empty;

        try
        {
            return await _ptyHandle.ReadAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (Exception) when (_exited || _disposed)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }
    
    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        await _startedTcs.Task.WaitAsync(ct);

        try
        {
            if (_disposed || _exited || _ptyHandle == null)
                return;

            await _ptyHandle.WriteAsync(data, ct);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception) when (_exited || _disposed)
        {
            // Ignore writes after exit
        }
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        lock (_lifecycleLock)
        {
            if (_width == width && _height == height)
                return ValueTask.CompletedTask;

            _width = width;
            _height = height;
            if (_started && !_exited && !_disposed && _ptyHandle != null)
                _ptyHandle.Resize(width, height);
        }
        
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public event Action? Disconnected;
    
    // === Private Helpers ===
    
    private Dictionary<string, string> BuildEnvironment()
    {
        var env = new Dictionary<string, string>();
        
        if (_inheritEnvironment)
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    env[key] = value;
                }
            }
        }
        
        // TERM helps Unix-side shells and TUI tools pick sensible capabilities.
        // On Windows/ConPTY it can change cmd.exe / PowerShell prompt behavior in
        // unhelpful ways, so only inject it on Unix-like platforms.
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && !env.ContainsKey("TERM"))
        {
            env["TERM"] = "xterm-256color";
        }
        
        // Set HEX1B_NESTING_LEVEL to track nested terminal depth
        // If already set, increment it; otherwise set to 1
        const string nestingLevelKey = "HEX1B_NESTING_LEVEL";
        int nestingLevel = 1;
        if (env.TryGetValue(nestingLevelKey, out var existingLevel) && 
            int.TryParse(existingLevel, out var parsedLevel))
        {
            nestingLevel = parsedLevel + 1;
        }
        env[nestingLevelKey] = nestingLevel.ToString();
        
        // Apply custom environment (can override nesting level if explicitly set)
        if (_environment != null)
        {
            foreach (var (key, value) in _environment)
            {
                env[key] = value;
            }
        }
        
        return env;
    }
    
    internal static IPtyHandle CreatePtyHandle(
        WindowsPtyMode windowsPtyMode = WindowsPtyMode.RequireProxy,
        string? windowsPtyHostPath = null,
        TimeSpan? unixPtyStartupTimeout = null,
        string? windowsPtyProxySocketPath = null)
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixPtyHandle(unixPtyStartupTimeout ?? UnixPtyStartupOptions.DefaultTimeout);
        }
        else if (OperatingSystem.IsWindows())
        {
            return new WindowsProxyPtyHandle(windowsPtyMode, windowsPtyHostPath, windowsPtyProxySocketPath);
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"PTY is not supported on {Environment.OSVersion.Platform}");
        }
    }

    // === Disposal ===
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                if (!_startInitiated)
                {
                    _startedTcs.TrySetException(new ObjectDisposedException(nameof(Hex1bTerminalChildProcess)));
                    _startupFinished.TrySetResult();
                }
                // Run cancellation callbacks outside the lifecycle lock.
                _disposeTask = Task.Run(DisposeCoreAsync);
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            Exception? cancellationError = null;
            try
            {
                await _startupCancellation.CancelAsync();
            }
            catch (Exception error)
            {
                cancellationError = error;
            }
            await _startupFinished.Task;
            if (_ptyHandle is not null)
                await _ptyHandle.DisposeAsync();
            if (cancellationError is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellationError).Throw();
            Disconnected?.Invoke();
        }
        finally
        {
            _startupCancellation.Dispose();
        }
    }

    private static async Task CleanupFailedStartAsync(IPtyHandle handle, Exception primaryError)
    {
        List<Exception>? cleanupErrors = null;
        try
        {
            if (handle.ProcessId > 0)
            {
                handle.Kill(9);
                await handle.WaitForExitAsync(CancellationToken.None);
            }
        }
        catch (Exception error)
        {
            (cleanupErrors ??= []).Add(error);
        }
        try
        {
            await handle.DisposeAsync();
        }
        catch (Exception error)
        {
            (cleanupErrors ??= []).Add(error);
        }
        if (cleanupErrors is not null)
            primaryError.Data["Hex1b.StartupCleanupErrors"] = new AggregateException(cleanupErrors);
    }
}
