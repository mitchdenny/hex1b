using System.Diagnostics;
using System.Text;

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
    private readonly Func<IPtyHandle> _ptyHandleFactory;
    
    private int _width;
    private int _height;
    private bool _startInitiated;
    private bool _started;
    private bool _exited;
    private int _exitCode;
    private bool _disposed;
    private readonly TaskCompletionSource _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Queue<byte[]> _startupOutputQueue = new();
    private readonly object _startupOutputLock = new();
    
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
            CreatePtyHandle)
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
        Func<IPtyHandle> ptyHandleFactory)
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
        if (_startInitiated)
            throw new InvalidOperationException("Process has already been started.");
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(Hex1bTerminalChildProcess));

        _startInitiated = true;
        
        // Create platform-specific PTY
        _ptyHandle = _ptyHandleFactory();
        
        // Build environment
        var env = BuildEnvironment();
        
        // Null is documented to mean "use the current directory". Resolve it here so
        // every platform handle, including the Windows shim path, gets the same cwd.
        var workingDirectory = string.IsNullOrWhiteSpace(_workingDirectory)
            ? Environment.CurrentDirectory
            : _workingDirectory;

        // Start the process
        await _ptyHandle.StartAsync(_fileName, _arguments, workingDirectory, env, _width, _height, ct);

        // Capture a short startup burst up-front so the first consumer read gets the
        // initial title/prompt output immediately. Handle-level warmup owns any
        // Windows-specific nudging so we do not synthesize an extra Enter here.
        await CaptureStartupOutputBurstAsync(ct).ConfigureAwait(false);

        _started = true;

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

        lock (_startupOutputLock)
        {
            if (_startupOutputQueue.Count > 0)
            {
                return _startupOutputQueue.Dequeue();
            }
        }
        
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
        if (_disposed)
            return;
        
        try
        {
            await _startedTcs.Task.WaitAsync(ct);
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

    private async Task<byte[]> CaptureStartupOutputBurstAsync(CancellationToken ct)
    {
        if (_ptyHandle == null)
        {
            return [];
        }

        var captured = new List<byte[]>();
        var captureDeadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(500);
        var quietWindow = TimeSpan.FromMilliseconds(75);
        DateTime? quietDeadline = null;

        while (DateTime.UtcNow < captureDeadline)
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(TimeSpan.FromMilliseconds(50));

            ReadOnlyMemory<byte> data;
            try
            {
                data = await _ptyHandle.ReadAsync(readCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (quietDeadline.HasValue && DateTime.UtcNow >= quietDeadline.Value)
                {
                    break;
                }

                continue;
            }

            if (data.IsEmpty)
            {
                if (quietDeadline.HasValue && DateTime.UtcNow >= quietDeadline.Value)
                {
                    break;
                }

                continue;
            }

            lock (_startupOutputLock)
            {
                var chunk = data.ToArray();
                _startupOutputQueue.Enqueue(chunk);
                captured.Add(chunk);
            }

            quietDeadline = DateTime.UtcNow + quietWindow;
        }

        return [.. captured.SelectMany(static chunk => chunk)];
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
    
    private static IPtyHandle CreatePtyHandle()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixPtyHandle();
        }
        else if (OperatingSystem.IsWindows())
        {
            return new WindowsProxyPtyHandle();
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
