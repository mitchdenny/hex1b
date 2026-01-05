using System.Text;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;

namespace TerminalMcp;

/// <summary>
/// Represents a terminal session that manages a child process attached to a virtual terminal.
/// Provides methods for input, output capture, and lifecycle management.
/// </summary>
public sealed class TerminalSession : IAsyncDisposable
{
    private readonly Hex1bTerminalChildProcess _process;
    private readonly Hex1bTerminal _terminal;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _outputPumpTask;
    private bool _disposed;
    private int _width;
    private int _height;

    /// <summary>
    /// Gets the unique identifier for this session.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the terminal width in columns.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Gets the terminal height in rows.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Gets when this session was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the command that was executed.
    /// </summary>
    public string Command { get; }

    /// <summary>
    /// Gets the arguments passed to the command.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the working directory for the process.
    /// </summary>
    public string? WorkingDirectory { get; }

    /// <summary>
    /// Gets the process ID of the child process. Returns -1 if not started.
    /// </summary>
    public int ProcessId => _process.ProcessId;

    /// <summary>
    /// Gets whether the underlying process has exited.
    /// </summary>
    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Gets the exit code of the process. Only valid when <see cref="HasExited"/> is true.
    /// </summary>
    public int ExitCode => _process.ExitCode;

    private TerminalSession(
        string id,
        Hex1bTerminalChildProcess process,
        Hex1bTerminal terminal,
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        int width,
        int height)
    {
        Id = id;
        _process = process;
        _terminal = terminal;
        _width = width;
        _height = height;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        StartedAt = DateTimeOffset.UtcNow;

        // Start pumping output from process to terminal
        _outputPumpTask = PumpOutputAsync(_cts.Token);
    }

    /// <summary>
    /// Creates and starts a new terminal session.
    /// </summary>
    /// <param name="id">Unique session identifier.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <param name="environment">Additional environment variables.</param>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A started terminal session.</returns>
    public static async Task<TerminalSession> StartAsync(
        string id,
        string command,
        string[] arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environment = null,
        int width = 80,
        int height = 24,
        CancellationToken ct = default)
    {
        // Create the child process with PTY
        var process = new Hex1bTerminalChildProcess(
            command,
            arguments,
            workingDirectory,
            environment,
            inheritEnvironment: true,
            initialWidth: width,
            initialHeight: height);

        // Create the virtual terminal that processes the output
        var terminal = new Hex1bTerminal(process, width, height);

        // Start the process
        await process.StartAsync(ct);

        return new TerminalSession(id, process, terminal, command, arguments, workingDirectory, width, height);
    }

    /// <summary>
    /// Sends text input to the terminal.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendInputAsync(string text, CancellationToken ct = default)
    {
        if (_disposed || _process.HasExited)
            return;

        var bytes = Encoding.UTF8.GetBytes(text);
        await _process.WriteInputAsync(bytes, ct);
    }

    /// <summary>
    /// Sends a special key to the terminal.
    /// </summary>
    /// <param name="key">The key to send (e.g., "Enter", "Tab", "Escape", "Up", "Down", "Left", "Right").</param>
    /// <param name="modifiers">Key modifiers (e.g., "Ctrl", "Alt", "Shift").</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SendKeyAsync(string key, string[]? modifiers = null, CancellationToken ct = default)
    {
        if (_disposed || _process.HasExited)
            return;

        var bytes = TranslateKey(key, modifiers);
        if (bytes.Length > 0)
        {
            await _process.WriteInputAsync(bytes, ct);
        }
    }

    /// <summary>
    /// Resizes the terminal.
    /// </summary>
    /// <param name="width">New width in columns.</param>
    /// <param name="height">New height in rows.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_disposed || _process.HasExited)
            return;

        _width = width;
        _height = height;
        await _process.ResizeAsync(width, height, ct);
    }

    /// <summary>
    /// Captures the current terminal screen as text.
    /// </summary>
    /// <returns>The terminal screen content as text.</returns>
    public string CaptureText()
    {
        using var snapshot = _terminal.CreateSnapshot();
        return snapshot.GetText();
    }

    /// <summary>
    /// Captures the current terminal screen as SVG.
    /// </summary>
    /// <param name="options">Optional SVG rendering options.</param>
    /// <returns>An SVG representation of the terminal screen.</returns>
    public string CaptureSvg(TerminalSvgOptions? options = null)
    {
        using var snapshot = _terminal.CreateSnapshot();
        return snapshot.ToSvg(options);
    }

    /// <summary>
    /// Waits for specific text to appear on the terminal screen.
    /// </summary>
    /// <param name="text">The text to wait for.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the text appeared, false if timeout occurred.</returns>
    public async Task<bool> WaitForTextAsync(string text, TimeSpan timeout, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                using var snapshot = _terminal.CreateSnapshot();
                if (snapshot.ContainsText(text))
                    return true;

                await Task.Delay(100, cts.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout occurred
        }

        return false;
    }

    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public async Task<int> WaitForExitAsync(CancellationToken ct = default)
    {
        return await _process.WaitForExitAsync(ct);
    }

    /// <summary>
    /// Kills the process.
    /// </summary>
    /// <param name="signal">Signal to send (Unix only). Default is SIGTERM (15).</param>
    public void Kill(int signal = 15)
    {
        if (!_disposed && !_process.HasExited)
        {
            _process.Kill(signal);
        }
    }

    private async Task PumpOutputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !_process.HasExited)
            {
                // The terminal automatically pumps output when reading from the workload adapter
                // We just need to keep the pump running
                await Task.Delay(50, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    private static byte[] TranslateKey(string key, string[]? modifiers)
    {
        var hasCtrl = modifiers?.Contains("Ctrl", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasAlt = modifiers?.Contains("Alt", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasShift = modifiers?.Contains("Shift", StringComparer.OrdinalIgnoreCase) ?? false;

        // Handle special keys
        return key.ToLowerInvariant() switch
        {
            "enter" or "return" => "\r"u8.ToArray(),
            "tab" => hasShift ? "\x1b[Z"u8.ToArray() : "\t"u8.ToArray(),
            "escape" or "esc" => "\x1b"u8.ToArray(),
            "backspace" => "\x7f"u8.ToArray(),
            "delete" => "\x1b[3~"u8.ToArray(),
            "up" => "\x1b[A"u8.ToArray(),
            "down" => "\x1b[B"u8.ToArray(),
            "right" => "\x1b[C"u8.ToArray(),
            "left" => "\x1b[D"u8.ToArray(),
            "home" => "\x1b[H"u8.ToArray(),
            "end" => "\x1b[F"u8.ToArray(),
            "pageup" => "\x1b[5~"u8.ToArray(),
            "pagedown" => "\x1b[6~"u8.ToArray(),
            "insert" => "\x1b[2~"u8.ToArray(),
            "f1" => "\x1bOP"u8.ToArray(),
            "f2" => "\x1bOQ"u8.ToArray(),
            "f3" => "\x1bOR"u8.ToArray(),
            "f4" => "\x1bOS"u8.ToArray(),
            "f5" => "\x1b[15~"u8.ToArray(),
            "f6" => "\x1b[17~"u8.ToArray(),
            "f7" => "\x1b[18~"u8.ToArray(),
            "f8" => "\x1b[19~"u8.ToArray(),
            "f9" => "\x1b[20~"u8.ToArray(),
            "f10" => "\x1b[21~"u8.ToArray(),
            "f11" => "\x1b[23~"u8.ToArray(),
            "f12" => "\x1b[24~"u8.ToArray(),
            "space" => " "u8.ToArray(),
            _ when key.Length == 1 && hasCtrl => [(byte)(char.ToUpper(key[0]) - 'A' + 1)],
            _ when key.Length == 1 => Encoding.UTF8.GetBytes(key),
            _ => []
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the output pump
        await _cts.CancelAsync();

        try
        {
            // Wait briefly for pump to finish
            await _outputPumpTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch (TimeoutException)
        {
            // Ignore
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Kill the process if still running
        if (!_process.HasExited)
        {
            _process.Kill();
        }

        // Dispose process and terminal
        await _process.DisposeAsync();
        _terminal.Dispose();
        _cts.Dispose();
    }
}
