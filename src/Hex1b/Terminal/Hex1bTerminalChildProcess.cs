using System.Diagnostics;

namespace Hex1b.Terminal;

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
    
    private int _width;
    private int _height;
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
    {
        _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        _arguments = arguments ?? [];
        _workingDirectory = workingDirectory;
        _environment = environment;
        _inheritEnvironment = inheritEnvironment;
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
    /// Gets the process ID of the child process. Returns -1 if not started.
    /// </summary>
    public int ProcessId => _ptyHandle?.ProcessId ?? -1;
    
    /// <summary>
    /// Gets whether the process has been started.
    /// </summary>
    public bool HasStarted => _started;
    
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
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_started)
            throw new InvalidOperationException("Process has already been started.");
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(Hex1bTerminalChildProcess));
        
        _started = true;
        
        // Create platform-specific PTY
        _ptyHandle = CreatePtyHandle();
        
        // Build environment
        var env = BuildEnvironment();
        
        // Start the process
        await _ptyHandle.StartAsync(_fileName, _arguments, _workingDirectory, env, _width, _height, ct);
        
        // Signal that the process has started (allows ReadOutputAsync to proceed)
        _startedTcs.TrySetResult();
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
        if (!_started || _disposed || _ptyHandle == null)
            return;
        
        try
        {
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
        _width = width;
        _height = height;
        
        if (_started && !_exited && _ptyHandle != null)
        {
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
        
        // Set TERM if not already set
        if (!env.ContainsKey("TERM"))
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
    
    private static IPtyHandle CreatePtyHandle()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixPtyHandle();
        }
        else if (OperatingSystem.IsWindows())
        {
            return new WindowsPtyHandle();
        }
        else
        {
            throw new PlatformNotSupportedException(
                $"PTY is not supported on {Environment.OSVersion.Platform}");
        }
    }
    
    // === Disposal ===
    
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        if (_ptyHandle != null)
        {
            await _ptyHandle.DisposeAsync();
        }
        
        Disconnected?.Invoke();
    }
}

/// <summary>
/// Platform-specific PTY handle abstraction.
/// </summary>
internal interface IPtyHandle : IAsyncDisposable
{
    /// <summary>
    /// Gets the process ID.
    /// </summary>
    int ProcessId { get; }
    
    /// <summary>
    /// Starts the process with the given parameters.
    /// </summary>
    Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct);
    
    /// <summary>
    /// Reads output from the PTY master.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);
    
    /// <summary>
    /// Writes input to the PTY master.
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    
    /// <summary>
    /// Resizes the PTY.
    /// </summary>
    void Resize(int width, int height);
    
    /// <summary>
    /// Sends a signal to the process.
    /// </summary>
    void Kill(int signal);
    
    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    Task<int> WaitForExitAsync(CancellationToken ct);
}
