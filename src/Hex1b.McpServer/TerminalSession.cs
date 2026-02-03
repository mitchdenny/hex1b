using System.Text;
using Hex1b.Automation;
using Hex1b.Theming;

namespace Hex1b.McpServer;

/// <summary>
/// Represents a terminal session that manages a child process attached to a virtual terminal.
/// Provides methods for input, output capture, and lifecycle management.
/// </summary>
public sealed class TerminalSession : IAsyncDisposable
{
    private readonly Hex1bTerminalChildProcess _process;
    private readonly Hex1bTerminal _terminal;
    private readonly CapturingPresentationAdapter _presentation;
    private readonly AsciinemaRecorder _asciinemaRecorder;
    private readonly CancellationTokenSource _cts = new();
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

    /// <summary>
    /// Gets the path to the asciinema recording file that was specified at session start, if any.
    /// For dynamic recordings, use <see cref="ActiveRecordingPath"/> instead.
    /// </summary>
    public string? AsciinemaFilePath { get; }

    /// <summary>
    /// Gets whether the session is currently recording to an asciinema file.
    /// </summary>
    public bool IsRecording => _asciinemaRecorder.IsRecording;

    /// <summary>
    /// Gets the path to the currently active asciinema recording, or null if not recording.
    /// </summary>
    public string? ActiveRecordingPath => _asciinemaRecorder.FilePath;

    private TerminalSession(
        string id,
        Hex1bTerminalChildProcess process,
        Hex1bTerminal terminal,
        CapturingPresentationAdapter presentation,
        AsciinemaRecorder asciinemaRecorder,
        string command,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        string? initialAsciinemaFilePath,
        int width,
        int height)
    {
        Id = id;
        _process = process;
        _terminal = terminal;
        _presentation = presentation;
        _asciinemaRecorder = asciinemaRecorder;
        _width = width;
        _height = height;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        AsciinemaFilePath = initialAsciinemaFilePath;
        StartedAt = DateTimeOffset.UtcNow;
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
    /// <param name="asciinemaFilePath">Optional path to save an asciinema recording from session start.</param>
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
        string? asciinemaFilePath = null,
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

        // Create a capturing presentation adapter so the terminal's output pump runs
        var presentation = new CapturingPresentationAdapter(width, height);

        // Create asciinema recorder - either in recording mode (if path provided) or idle mode
        AsciinemaRecorder asciinemaRecorder;
        if (!string.IsNullOrWhiteSpace(asciinemaFilePath))
        {
            asciinemaRecorder = new AsciinemaRecorder(asciinemaFilePath, new AsciinemaRecorderOptions
            {
                AutoFlush = true,
                Title = $"{command} session",
                Command = command
            });
        }
        else
        {
            // Create in idle mode for dynamic recording later
            asciinemaRecorder = new AsciinemaRecorder();
        }

        // Create the virtual terminal with presentation adapter to enable output pumping
        var terminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = presentation,
            WorkloadAdapter = process,
            Width = width,
            Height = height
        };

        // Always add asciinema recorder as a workload filter (it will filter events based on IsRecording)
        terminalOptions.WorkloadFilters.Add(asciinemaRecorder);

        var terminal = new Hex1bTerminal(terminalOptions);

        // Start the process
        await process.StartAsync(ct);

        return new TerminalSession(id, process, terminal, presentation, asciinemaRecorder, command, arguments, workingDirectory, asciinemaFilePath, width, height);
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

    /// <summary>
    /// Starts recording the terminal session to an asciinema file.
    /// </summary>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="options">Optional recording options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if already recording.</exception>
    /// <remarks>
    /// The current terminal state is synthesized and written as the first event,
    /// so the recording starts with the current screen content.
    /// </remarks>
    public async Task StartRecordingAsync(string filePath, AsciinemaRecorderOptions? options = null, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSession));

        // Start recording - dimensions are automatically inferred from the session
        _asciinemaRecorder.StartRecording(filePath, options ?? new AsciinemaRecorderOptions
        {
            AutoFlush = true,
            Title = $"{Command} session",
            Command = Command
        });

        // Synthesize current terminal state and write as initial event
        var initialState = SynthesizeTerminalState();
        await _asciinemaRecorder.WriteInitialStateAsync(initialState, ct);
    }

    /// <summary>
    /// Stops the current asciinema recording.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path to the completed recording file, or null if not recording.</returns>
    public async Task<string?> StopRecordingAsync(CancellationToken ct = default)
    {
        return await _asciinemaRecorder.StopRecordingAsync(ct);
    }

    /// <summary>
    /// Synthesizes the current terminal state as ANSI escape sequences.
    /// </summary>
    private string SynthesizeTerminalState()
    {
        using var snapshot = _terminal.CreateSnapshot();
        var sb = new StringBuilder();
        
        // Clear screen and move cursor to home
        sb.Append("\x1b[2J\x1b[H");
        
        for (int y = 0; y < snapshot.Height; y++)
        {
            // Position cursor at start of each row
            if (y > 0)
            {
                sb.Append($"\x1b[{y + 1};1H");
            }
            
            for (int x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                
                // Build SGR sequence for this cell
                sb.Append("\x1b[0"); // Reset
                
                // Add attributes
                if ((cell.Attributes & CellAttributes.Bold) != 0) sb.Append(";1");
                if ((cell.Attributes & CellAttributes.Dim) != 0) sb.Append(";2");
                if ((cell.Attributes & CellAttributes.Italic) != 0) sb.Append(";3");
                if ((cell.Attributes & CellAttributes.Underline) != 0) sb.Append(";4");
                if ((cell.Attributes & CellAttributes.Blink) != 0) sb.Append(";5");
                if ((cell.Attributes & CellAttributes.Reverse) != 0) sb.Append(";7");
                if ((cell.Attributes & CellAttributes.Hidden) != 0) sb.Append(";8");
                if ((cell.Attributes & CellAttributes.Strikethrough) != 0) sb.Append(";9");
                
                // Add foreground color
                if (cell.Foreground is { } fg && !fg.IsDefault)
                {
                    sb.Append($";38;2;{fg.R};{fg.G};{fg.B}");
                }
                
                // Add background color
                if (cell.Background is { } bg && !bg.IsDefault)
                {
                    sb.Append($";48;2;{bg.R};{bg.G};{bg.B}");
                }
                
                sb.Append('m');
                
                // Print the character (or space if empty)
                var ch = string.IsNullOrEmpty(cell.Character) ? " " : cell.Character;
                sb.Append(ch);
            }
        }
        
        // Reset attributes at the end
        sb.Append("\x1b[0m");
        
        return sb.ToString();
    }

    private static byte[] TranslateKey(string key, string[]? modifiers)
    {
        var hasCtrl = modifiers?.Contains("Ctrl", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasAlt = modifiers?.Contains("Alt", StringComparer.OrdinalIgnoreCase) ?? false;
        var hasShift = modifiers?.Contains("Shift", StringComparer.OrdinalIgnoreCase) ?? false;

        // Handle special keys
        var baseKey = key.ToLowerInvariant() switch
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

        // Alt modifier sends ESC prefix followed by the key
        if (hasAlt && baseKey.Length > 0)
        {
            var result = new byte[baseKey.Length + 1];
            result[0] = 0x1b; // ESC
            baseKey.CopyTo(result, 1);
            return result;
        }

        return baseKey;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Kill the process if still running
        if (!_process.HasExited)
        {
            _process.Kill();
        }

        // Dispose asciinema recorder first to flush any remaining events
        await _asciinemaRecorder.DisposeAsync();

        // Dispose process, terminal, and presentation adapter
        await _process.DisposeAsync();
        _terminal.Dispose();
        await _presentation.DisposeAsync();
        _cts.Dispose();
    }
}
