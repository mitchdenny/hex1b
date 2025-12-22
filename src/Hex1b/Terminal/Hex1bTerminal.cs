#pragma warning disable HEX1B_SIXEL // Sixel API is experimental - internal usage is allowed

using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A virtual terminal that bridges workload and presentation layers, with optional
/// screen buffer capture for testing and debugging.
/// </summary>
/// <remarks>
/// <para>
/// Hex1bTerminal serves as a mediator between:
/// <list type="bullet">
///   <item><see cref="IHex1bTerminalPresentationAdapter"/> - the raw I/O layer (console, WebSocket, etc.)</item>
///   <item><see cref="IHex1bTerminalWorkloadAdapter"/> - the workload (app, process, etc.)</item>
/// </list>
/// </para>
/// <para>
/// The workload adapter is provided by the caller. For Hex1bApp, use <see cref="Hex1bAppWorkloadAdapter"/>.
/// This design keeps the terminal decoupled from specific workload types.
/// </para>
/// <para>
/// When no presentation adapter is provided (null), the terminal operates in "headless" mode,
/// capturing all output to the screen buffer. This is ideal for testing.
/// </para>
/// </remarks>
/// <example>
/// <para>Production usage:</para>
/// <code>
/// var presentation = new ConsolePresentationAdapter();
/// var workload = new Hex1bAppWorkloadAdapter(presentation.Width, presentation.Height);
/// var terminal = new Hex1bTerminal(presentation, workload);
/// var app = new Hex1bApp(workload, ctx => ctx.Text("Hello"));
/// await app.RunAsync();
/// </code>
/// <para>Testing usage:</para>
/// <code>
/// var workload = new Hex1bAppWorkloadAdapter(80, 24);
/// var terminal = new Hex1bTerminal(workload);  // headless
/// var app = new Hex1bApp(workload, ctx => ctx.Text("Hello"));
/// // ... run and test ...
/// Assert.True(terminal.ContainsText("Hello"));
/// </code>
/// </example>
public sealed class Hex1bTerminal : IDisposable
{
    private readonly IHex1bTerminalPresentationAdapter? _presentation;
    private readonly IHex1bTerminalWorkloadAdapter _workload;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly StringBuilder _rawOutput;
    private TerminalCell[,] _screenBuffer;
    private int _cursorX;
    private int _cursorY;
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;
    private bool _disposed;
    private bool _inAlternateScreen;
    private Task? _inputProcessingTask;
    private Task? _outputProcessingTask;

    /// <summary>
    /// Creates a new headless terminal for testing with the specified dimensions.
    /// This constructor creates an internal <see cref="Hex1bAppWorkloadAdapter"/> that
    /// can be accessed via the <see cref="WorkloadAdapter"/> property.
    /// </summary>
    /// <param name="width">Terminal width in characters.</param>
    /// <param name="height">Terminal height in lines.</param>
    public Hex1bTerminal(int width, int height)
        : this(null, new Hex1bAppWorkloadAdapter(width, height))
    {
        _width = width;
        _height = height;
        _screenBuffer = new TerminalCell[height, width];
        ClearBuffer();
    }

    /// <summary>
    /// Creates a new headless terminal for testing with the specified workload adapter.
    /// </summary>
    /// <param name="workload">The workload adapter (e.g., Hex1bAppWorkloadAdapter).</param>
    public Hex1bTerminal(IHex1bTerminalWorkloadAdapter workload)
        : this(presentation: null, workload: workload)
    {
    }

    /// <summary>
    /// Creates a new terminal with the specified presentation and workload adapters.
    /// </summary>
    /// <param name="presentation">The presentation adapter for actual I/O. Pass null for headless/test mode.</param>
    /// <param name="workload">The workload adapter (e.g., Hex1bAppWorkloadAdapter).</param>
    public Hex1bTerminal(
        IHex1bTerminalPresentationAdapter? presentation,
        IHex1bTerminalWorkloadAdapter workload)
    {
        _presentation = presentation;
        _workload = workload ?? throw new ArgumentNullException(nameof(workload));
        _width = presentation?.Width ?? 80;
        _height = presentation?.Height ?? 24;
        _rawOutput = new StringBuilder();
        _screenBuffer = new TerminalCell[_height, _width];
        
        ClearBuffer();

        // Subscribe to presentation events if present
        if (_presentation != null)
        {
            _presentation.Resized += OnPresentationResized;
            _presentation.Disconnected += OnPresentationDisconnected;
        }

        // Subscribe to workload disconnect
        _workload.Disconnected += OnWorkloadDisconnected;
    }

    private int _width;
    private int _height;

    private void OnPresentationResized(int width, int height)
    {
        // IMPORTANT: Call Resize() first before updating _width/_height
        // because Resize() needs the OLD dimensions to know how much to copy
        Resize(width, height);
        // Notify workload of resize
        _ = _workload.ResizeAsync(width, height);
    }

    private void OnPresentationDisconnected()
    {
        Disconnected?.Invoke();
    }

    private void OnWorkloadDisconnected()
    {
        Disconnected?.Invoke();
    }

    // === Configuration ===

    /// <summary>
    /// Terminal width.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Terminal height.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Gets the workload adapter. For headless terminals created with (width, height),
    /// this returns the internal <see cref="Hex1bAppWorkloadAdapter"/>.
    /// For terminals created with explicit adapters, this returns the provided workload.
    /// </summary>
    /// <remarks>
    /// This property is primarily for testing scenarios where the terminal is created
    /// with dimensions and the workload adapter is needed for Hex1bApp or context creation.
    /// </remarks>
    public IHex1bAppTerminalWorkloadAdapter WorkloadAdapter => 
        _workload as IHex1bAppTerminalWorkloadAdapter 
        ?? throw new InvalidOperationException("Workload adapter is not an IHex1bAppTerminalWorkloadAdapter");

    /// <summary>
    /// Raised when the terminal disconnects (either presentation or workload).
    /// </summary>
    public event Action? Disconnected;

    // === Terminal Control ===

    /// <summary>
    /// Starts the terminal's I/O pump loops.
    /// Call this after constructing the terminal to begin processing I/O.
    /// </summary>
    /// <remarks>
    /// If a presentation adapter is present, this also enters TUI mode on the presentation
    /// (raw mode, alternate screen, etc.) so that input can be captured properly.
    /// </remarks>
    public void Start()
    {
        // Enter TUI mode on presentation if present (enables raw mode for input capture)
        if (_presentation != null)
        {
            // Fire and forget - EnterTuiModeAsync is typically synchronous for console
            _ = _presentation.EnterTuiModeAsync();
        }

        // Start pumping presentation input → workload (if presentation exists)
        if (_presentation != null && _inputProcessingTask == null)
        {
            _inputProcessingTask = Task.Run(() => PumpPresentationInputAsync(_disposeCts.Token));
        }

        // Start pumping workload output → presentation
        if (_outputProcessingTask == null)
        {
            _outputProcessingTask = Task.Run(() => PumpWorkloadOutputAsync(_disposeCts.Token));
        }
    }

    /// <summary>
    /// Synchronously drains any pending output from the workload and processes it
    /// into the screen buffer. Useful for testing scenarios.
    /// </summary>
    /// <remarks>
    /// In headless testing mode, the app writes output to the workload adapter's channel.
    /// This method reads all pending output and processes it immediately, allowing
    /// tests to assert on screen content without needing async pump loops.
    /// </remarks>
    public void FlushOutput()
    {
        if (_workload is not Hex1bAppWorkloadAdapter appWorkload)
            return;

        // Drain all available output synchronously using non-blocking reads
        while (appWorkload.TryReadOutput(out var data))
        {
            if (data.IsEmpty)
            {
                break;
            }

            var text = Encoding.UTF8.GetString(data.Span);
            _rawOutput.Append(text);
            ProcessOutput(text);
        }
    }

    // === I/O Pump Tasks ===

    private async Task PumpPresentationInputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _presentation != null)
            {
                var data = await _presentation.ReadInputAsync(ct);
                if (data.IsEmpty)
                {
                    break;
                }

                // For Hex1bAppWorkloadAdapter, we parse input and send events directly
                if (_workload is Hex1bAppWorkloadAdapter appWorkload)
                {
                    await ParseAndDispatchInputAsync(data, appWorkload, ct);
                }
                else
                {
                    // For other workloads, forward raw bytes
                    await _workload.WriteInputAsync(data, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task PumpWorkloadOutputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var data = await _workload.ReadOutputAsync(ct);
                if (data.IsEmpty)
                {
                    // Small delay to prevent busy-waiting in headless mode
                    await Task.Delay(10, ct);
                    continue;
                }

                var text = Encoding.UTF8.GetString(data.Span);

                // Capture raw output and parse into screen buffer
                _rawOutput.Append(text);
                ProcessOutput(text);

                // Forward to presentation if present
                if (_presentation != null)
                {
                    await _presentation.WriteOutputAsync(data);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task ParseAndDispatchInputAsync(ReadOnlyMemory<byte> data, Hex1bAppWorkloadAdapter workload, CancellationToken ct)
    {
        var message = Encoding.UTF8.GetString(data.Span);
        var i = 0;

        while (i < message.Length && !ct.IsCancellationRequested)
        {
            // Check for DA1 response
            if (TryParseDA1Response(message, i, out var da1Consumed))
            {
                i += da1Consumed;
                continue;
            }

            // Check for ANSI escape sequence
            if (message[i] == '\x1b' && i + 1 < message.Length)
            {
                if (message[i + 1] == '[')
                {
                    // CSI sequence
                    if (i + 2 < message.Length && message[i + 2] == '<')
                    {
                        var (mouseEvent, mouseConsumed) = ParseSgrMouseSequence(message, i);
                        if (mouseEvent != null)
                        {
                            await workload.WriteInputEventAsync(mouseEvent, ct);
                            i += mouseConsumed;
                            continue;
                        }
                    }

                    var (csiEvent, csiConsumed) = ParseCsiSequence(message, i);
                    if (csiEvent != null)
                    {
                        await workload.WriteInputEventAsync(csiEvent, ct);
                    }
                    i += csiConsumed;
                }
                else if (message[i + 1] == 'O')
                {
                    var (ss3Event, ss3Consumed) = ParseSS3Sequence(message, i);
                    if (ss3Event != null)
                    {
                        await workload.WriteInputEventAsync(ss3Event, ct);
                    }
                    i += ss3Consumed;
                }
                else
                {
                    await workload.WriteInputEventAsync(
                        new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), ct);
                    i++;
                }
            }
            else if (char.IsHighSurrogate(message[i]) && i + 1 < message.Length && char.IsLowSurrogate(message[i + 1]))
            {
                var text = message.Substring(i, 2);
                await workload.WriteInputEventAsync(Hex1bKeyEvent.FromText(text), ct);
                i += 2;
            }
            else
            {
                var keyEvent = ParseKeyInput(message[i]);
                if (keyEvent != null)
                {
                    await workload.WriteInputEventAsync(keyEvent, ct);
                }
                i++;
            }
        }
    }

    // === Screen Buffer APIs (Testing/Debugging) ===

    /// <summary>
    /// Gets the current cursor X position (0-based).
    /// </summary>
    public int CursorX => _cursorX;

    /// <summary>
    /// Gets the current cursor Y position (0-based).
    /// </summary>
    public int CursorY => _cursorY;

    /// <summary>
    /// Gets whether the terminal is currently in alternate screen mode.
    /// </summary>
    public bool InAlternateScreen => _inAlternateScreen;

    /// <summary>
    /// Gets the raw output written to this terminal, including ANSI escape sequences.
    /// </summary>
    public string RawOutput => _rawOutput.ToString();

    /// <summary>
    /// Enters alternate screen mode (for testing purposes).
    /// In headless mode, this just sets the flag and clears the buffer.
    /// </summary>
    public void EnterAlternateScreen()
    {
        ProcessOutput("\x1b[?1049h");
    }

    /// <summary>
    /// Exits alternate screen mode (for testing purposes).
    /// In headless mode, this just clears the flag.
    /// </summary>
    public void ExitAlternateScreen()
    {
        ProcessOutput("\x1b[?1049l");
    }

    /// <summary>
    /// Gets a copy of the current screen buffer.
    /// </summary>
    public TerminalCell[,] GetScreenBuffer()
    {
        var copy = new TerminalCell[_height, _width];
        Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
        return copy;
    }

    /// <summary>
    /// Gets the text content of the screen buffer as a string, with lines separated by newlines.
    /// </summary>
    public string GetScreenText()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                sb.Append(_screenBuffer[y, x].Character);
            }
            if (y < _height - 1)
            {
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the text content of a specific line (0-based).
    /// </summary>
    public string GetLine(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _height)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var sb = new StringBuilder(_width);
        for (int x = 0; x < _width; x++)
        {
            sb.Append(_screenBuffer[lineIndex, x].Character);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Gets the text content of a specific line, trimmed of trailing whitespace.
    /// </summary>
    public string GetLineTrimmed(int lineIndex) => GetLine(lineIndex).TrimEnd();

    /// <summary>
    /// Gets all non-empty lines from the screen buffer.
    /// </summary>
    public IEnumerable<string> GetNonEmptyLines()
    {
        for (int y = 0; y < _height; y++)
        {
            var line = GetLineTrimmed(y);
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// Checks if the screen contains the specified text anywhere.
    /// </summary>
    public bool ContainsText(string text)
    {
        var screenText = GetScreenText();
        return screenText.Contains(text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Finds all occurrences of the specified text on the screen.
    /// Returns a list of (line, column) positions.
    /// </summary>
    public List<(int Line, int Column)> FindText(string text)
    {
        var results = new List<(int, int)>();
        for (int y = 0; y < _height; y++)
        {
            var line = GetLine(y);
            var index = 0;
            while ((index = line.IndexOf(text, index, StringComparison.Ordinal)) >= 0)
            {
                results.Add((y, index));
                index++;
            }
        }
        return results;
    }

    /// <summary>
    /// Clears the raw output buffer.
    /// </summary>
    public void ClearRawOutput() => _rawOutput.Clear();

    // === Input Injection APIs (Testing) ===

    /// <summary>
    /// Injects a key input event into the terminal (for testing).
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    public void SendKey(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.SendKey(key, keyChar, shift, alt, control);
        }
    }

    /// <summary>
    /// Injects a key input event using the Hex1bKey type (for testing).
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    public void SendKey(Hex1bKey key, char keyChar = '\0', Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.SendKey(key, keyChar, modifiers);
        }
    }

    /// <summary>
    /// Injects a mouse input event (for testing).
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    public void SendMouse(MouseButton button, MouseAction action, int x, int y, Hex1bModifiers modifiers = Hex1bModifiers.None, int clickCount = 1)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.SendMouse(button, action, x, y, modifiers, clickCount);
        }
    }

    /// <summary>
    /// Types a string of characters into the terminal (for testing).
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    public void TypeText(string text)
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.TypeText(text);
        }
    }

    /// <summary>
    /// Completes the input channel, signaling end of input (for testing).
    /// Only works with Hex1bAppWorkloadAdapter.
    /// </summary>
    public void CompleteInput()
    {
        if (_workload is Hex1bAppWorkloadAdapter appWorkload)
        {
            appWorkload.CompleteInput();
        }
    }

    /// <summary>
    /// Resizes the terminal, preserving content where possible.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        var newBuffer = new TerminalCell[newHeight, newWidth];
        
        // Initialize with empty cells
        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                newBuffer[y, x] = TerminalCell.Empty;
            }
        }

        // Copy existing content
        var copyHeight = Math.Min(_height, newHeight);
        var copyWidth = Math.Min(_width, newWidth);
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                newBuffer[y, x] = _screenBuffer[y, x];
            }
        }

        _screenBuffer = newBuffer;
        _width = newWidth;
        _height = newHeight;
        _cursorX = Math.Min(_cursorX, newWidth - 1);
        _cursorY = Math.Min(_cursorY, newHeight - 1);
    }

    // === Screen Buffer Parsing ===

    private void ClearBuffer()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
        _currentForeground = null;
        _currentBackground = null;
    }

    private void ProcessOutput(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                i = ProcessAnsiSequence(text, i);
            }
            else if (text[i] == '\n')
            {
                _cursorY++;
                _cursorX = 0;
                if (_cursorY >= _height)
                {
                    ScrollUp();
                    _cursorY = _height - 1;
                }
                i++;
            }
            else if (text[i] == '\r')
            {
                _cursorX = 0;
                i++;
            }
            else
            {
                if (_cursorX < _width && _cursorY < _height)
                {
                    _screenBuffer[_cursorY, _cursorX] = new TerminalCell(text[i], _currentForeground, _currentBackground);
                    _cursorX++;
                    if (_cursorX >= _width)
                    {
                        _cursorX = 0;
                        _cursorY++;
                        if (_cursorY >= _height)
                        {
                            ScrollUp();
                            _cursorY = _height - 1;
                        }
                    }
                }
                i++;
            }
        }
    }

    private int ProcessAnsiSequence(string text, int start)
    {
        int end = start + 2;
        while (end < text.Length && !char.IsLetter(text[end]))
        {
            end++;
        }

        if (end >= text.Length)
            return end;

        var command = text[end];
        var parameters = text[(start + 2)..end];

        switch (command)
        {
            case 'm':
                ProcessSgr(parameters);
                break;
            case 'H':
                ProcessCursorPosition(parameters);
                break;
            case 'J':
                ProcessClearScreen(parameters);
                break;
            case 'h':
            case 'l':
                if (parameters.Contains("?1049"))
                {
                    if (command == 'h')
                        DoEnterAlternateScreen();
                    else
                        DoExitAlternateScreen();
                }
                break;
        }

        return end + 1;
    }

    private void DoEnterAlternateScreen()
    {
        _inAlternateScreen = true;
        ClearBuffer();
        _cursorX = 0;
        _cursorY = 0;
    }

    private void DoExitAlternateScreen()
    {
        _inAlternateScreen = false;
    }

    private void ProcessSgr(string parameters)
    {
        if (string.IsNullOrEmpty(parameters) || parameters == "0")
        {
            _currentForeground = null;
            _currentBackground = null;
            return;
        }

        var parts = parameters.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code))
                continue;

            switch (code)
            {
                case 0:
                    _currentForeground = null;
                    _currentBackground = null;
                    break;
                case >= 30 and <= 37:
                    _currentForeground = StandardColorFromCode(code - 30);
                    break;
                case >= 40 and <= 47:
                    _currentBackground = StandardColorFromCode(code - 40);
                    break;
                case >= 90 and <= 97:
                    _currentForeground = BrightColorFromCode(code - 90);
                    break;
                case >= 100 and <= 107:
                    _currentBackground = BrightColorFromCode(code - 100);
                    break;
                case 38:
                    if (i + 2 < parts.Length && parts[i + 1] == "5")
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentForeground = Color256FromIndex(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 4 < parts.Length && parts[i + 1] == "2")
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentForeground = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                        }
                        i += 4;
                    }
                    break;
                case 48:
                    if (i + 2 < parts.Length && parts[i + 1] == "5")
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentBackground = Color256FromIndex(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 4 < parts.Length && parts[i + 1] == "2")
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentBackground = Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
                        }
                        i += 4;
                    }
                    break;
            }
        }
    }

    private void ProcessCursorPosition(string parameters)
    {
        if (string.IsNullOrEmpty(parameters))
        {
            _cursorX = 0;
            _cursorY = 0;
            return;
        }

        var parts = parameters.Split(';');
        if (parts.Length >= 2)
        {
            if (int.TryParse(parts[0], out var row) && int.TryParse(parts[1], out var col))
            {
                _cursorY = Math.Clamp(row - 1, 0, _height - 1);
                _cursorX = Math.Clamp(col - 1, 0, _width - 1);
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var row))
            {
                _cursorY = Math.Clamp(row - 1, 0, _height - 1);
            }
        }
    }

    private void ProcessClearScreen(string parameters)
    {
        var mode = string.IsNullOrEmpty(parameters) ? 0 : int.TryParse(parameters, out var m) ? m : 0;

        switch (mode)
        {
            case 0:
                ClearFromCursor();
                break;
            case 1:
                ClearToCursor();
                break;
            case 2:
            case 3:
                ClearBuffer();
                break;
        }
    }

    private void ClearFromCursor()
    {
        for (int x = _cursorX; x < _width; x++)
        {
            _screenBuffer[_cursorY, x] = TerminalCell.Empty;
        }
        for (int y = _cursorY + 1; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
    }

    private void ClearToCursor()
    {
        for (int y = 0; y < _cursorY; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
        for (int x = 0; x <= _cursorX && x < _width; x++)
        {
            _screenBuffer[_cursorY, x] = TerminalCell.Empty;
        }
    }

    private void ScrollUp()
    {
        for (int y = 0; y < _height - 1; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = _screenBuffer[y + 1, x];
            }
        }
        for (int x = 0; x < _width; x++)
        {
            _screenBuffer[_height - 1, x] = TerminalCell.Empty;
        }
    }

    // === Static Input Parsing Helpers ===

    private static bool TryParseDA1Response(string message, int start, out int consumed)
    {
        consumed = 0;
        
        if (start + 3 < message.Length && 
            message[start] == '\x1b' && 
            message[start + 1] == '[' && 
            message[start + 2] == '?')
        {
            for (var j = start + 3; j < message.Length; j++)
            {
                if (message[j] == 'c')
                {
                    var response = message.Substring(start, j - start + 1);
                    Nodes.SixelNode.HandleDA1Response(response);
                    consumed = j - start + 1;
                    return true;
                }
            }
        }
        
        return false;
    }

    private static Hex1bKeyEvent? ParseKeyInput(char c)
    {
        return c switch
        {
            '\r' or '\n' => new Hex1bKeyEvent(Hex1bKey.Enter, c, Hex1bModifiers.None),
            '\t' => new Hex1bKeyEvent(Hex1bKey.Tab, c, Hex1bModifiers.None),
            '\x1b' => new Hex1bKeyEvent(Hex1bKey.Escape, c, Hex1bModifiers.None),
            '\x7f' or '\b' => new Hex1bKeyEvent(Hex1bKey.Backspace, c, Hex1bModifiers.None),
            ' ' => new Hex1bKeyEvent(Hex1bKey.Spacebar, c, Hex1bModifiers.None),
            >= 'a' and <= 'z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'a'))), c, Hex1bModifiers.None),
            >= 'A' and <= 'Z' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - 'A'))), c, Hex1bModifiers.Shift),
            >= '0' and <= '9' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.D0 + (c - '0'))), c, Hex1bModifiers.None),
            >= '\x01' and <= '\x1a' => new Hex1bKeyEvent(
                KeyMapper.ToHex1bKey((ConsoleKey)((int)ConsoleKey.A + (c - '\x01'))), c, Hex1bModifiers.Control),
            _ when !char.IsControl(c) => new Hex1bKeyEvent(Hex1bKey.None, c, Hex1bModifiers.None),
            _ => null
        };
    }

    private static (Hex1bKeyEvent? Event, int Consumed) ParseCsiSequence(string message, int start)
    {
        if (start + 2 >= message.Length)
            return (null, 1);

        var i = start + 2;
        var param1 = 0;
        var param2 = 0;
        var hasParam2 = false;

        while (i < message.Length && char.IsDigit(message[i]))
        {
            param1 = param1 * 10 + (message[i] - '0');
            i++;
        }

        if (i < message.Length && message[i] == ';')
        {
            i++;
            while (i < message.Length && char.IsDigit(message[i]))
            {
                param2 = param2 * 10 + (message[i] - '0');
                hasParam2 = true;
                i++;
            }
        }

        if (i >= message.Length)
            return (null, i - start);

        var finalChar = message[i];
        i++;

        var modifiers = Hex1bModifiers.None;
        if (hasParam2 && param2 >= 2)
        {
            var modifierBits = param2 - 1;
            if ((modifierBits & 1) != 0) modifiers |= Hex1bModifiers.Shift;
            if ((modifierBits & 2) != 0) modifiers |= Hex1bModifiers.Alt;
            if ((modifierBits & 4) != 0) modifiers |= Hex1bModifiers.Control;
        }

        var key = finalChar switch
        {
            'A' => Hex1bKey.UpArrow,
            'B' => Hex1bKey.DownArrow,
            'C' => Hex1bKey.RightArrow,
            'D' => Hex1bKey.LeftArrow,
            'H' => Hex1bKey.Home,
            'F' => Hex1bKey.End,
            'Z' => Hex1bKey.Tab,
            '~' => ParseTildeSequence(param1),
            _ => Hex1bKey.None
        };

        if (key == Hex1bKey.None)
            return (null, i - start);

        if (finalChar == 'Z')
            modifiers |= Hex1bModifiers.Shift;

        return (new Hex1bKeyEvent(key, '\0', modifiers), i - start);
    }

    private static Hex1bKey ParseTildeSequence(int param)
    {
        return param switch
        {
            1 => Hex1bKey.Home,
            2 => Hex1bKey.Insert,
            3 => Hex1bKey.Delete,
            4 => Hex1bKey.End,
            5 => Hex1bKey.PageUp,
            6 => Hex1bKey.PageDown,
            _ => Hex1bKey.None
        };
    }

    private static (Hex1bKeyEvent? Event, int Consumed) ParseSS3Sequence(string message, int start)
    {
        if (start + 2 >= message.Length)
            return (null, 1);

        var finalChar = message[start + 2];

        var key = finalChar switch
        {
            'A' => Hex1bKey.UpArrow,
            'B' => Hex1bKey.DownArrow,
            'C' => Hex1bKey.RightArrow,
            'D' => Hex1bKey.LeftArrow,
            'H' => Hex1bKey.Home,
            'F' => Hex1bKey.End,
            'P' => Hex1bKey.F1,
            'Q' => Hex1bKey.F2,
            'R' => Hex1bKey.F3,
            'S' => Hex1bKey.F4,
            _ => Hex1bKey.None
        };

        if (key == Hex1bKey.None)
            return (null, 3);

        return (new Hex1bKeyEvent(key, '\0', Hex1bModifiers.None), 3);
    }

    private static (Hex1bMouseEvent? Event, int Consumed) ParseSgrMouseSequence(string message, int start)
    {
        if (start + 8 >= message.Length)
            return (null, 3);

        var i = start + 3;

        var terminatorIdx = -1;
        for (var j = i; j < message.Length; j++)
        {
            if (message[j] == 'M' || message[j] == 'm')
            {
                terminatorIdx = j;
                break;
            }
            if (!char.IsDigit(message[j]) && message[j] != ';')
            {
                return (null, 3);
            }
        }

        if (terminatorIdx < 0)
            return (null, 3);

        var sgrPart = message.Substring(i, terminatorIdx - i + 1);
        if (MouseParser.TryParseSgr(sgrPart, out var mouseEvent))
        {
            return (mouseEvent, terminatorIdx - start + 1);
        }

        return (null, 3);
    }

    private static ConsoleKey CharToConsoleKey(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            >= 'A' and <= 'Z' => (ConsoleKey)(c - 'a' + (int)ConsoleKey.A),
            >= '0' and <= '9' => (ConsoleKey)(c - '0' + (int)ConsoleKey.D0),
            ' ' => ConsoleKey.Spacebar,
            '\t' => ConsoleKey.Tab,
            '\n' or '\r' => ConsoleKey.Enter,
            _ => ConsoleKey.NoName
        };
    }

    private static Hex1bColor StandardColorFromCode(int code) => code switch
    {
        0 => Hex1bColor.FromRgb(0, 0, 0),
        1 => Hex1bColor.FromRgb(128, 0, 0),
        2 => Hex1bColor.FromRgb(0, 128, 0),
        3 => Hex1bColor.FromRgb(128, 128, 0),
        4 => Hex1bColor.FromRgb(0, 0, 128),
        5 => Hex1bColor.FromRgb(128, 0, 128),
        6 => Hex1bColor.FromRgb(0, 128, 128),
        7 => Hex1bColor.FromRgb(192, 192, 192),
        _ => Hex1bColor.FromRgb(128, 128, 128)
    };

    private static Hex1bColor BrightColorFromCode(int code) => code switch
    {
        0 => Hex1bColor.FromRgb(128, 128, 128),
        1 => Hex1bColor.FromRgb(255, 0, 0),
        2 => Hex1bColor.FromRgb(0, 255, 0),
        3 => Hex1bColor.FromRgb(255, 255, 0),
        4 => Hex1bColor.FromRgb(0, 0, 255),
        5 => Hex1bColor.FromRgb(255, 0, 255),
        6 => Hex1bColor.FromRgb(0, 255, 255),
        7 => Hex1bColor.FromRgb(255, 255, 255),
        _ => Hex1bColor.FromRgb(192, 192, 192)
    };

    private static Hex1bColor Color256FromIndex(int index)
    {
        if (index < 16)
        {
            return index < 8 ? StandardColorFromCode(index) : BrightColorFromCode(index - 8);
        }
        else if (index < 232)
        {
            index -= 16;
            var r = (index / 36) * 51;
            var g = ((index / 6) % 6) * 51;
            var b = (index % 6) * 51;
            return Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
        }
        else
        {
            var gray = (index - 232) * 10 + 8;
            return Hex1bColor.FromRgb((byte)gray, (byte)gray, (byte)gray);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Exit TUI mode before disposing
        if (_presentation != null)
        {
            // Fire and forget - ExitTuiModeAsync is typically synchronous for console
            _ = _presentation.ExitTuiModeAsync();
            _presentation.Resized -= OnPresentationResized;
            _presentation.Disconnected -= OnPresentationDisconnected;
        }

        _workload.Disconnected -= OnWorkloadDisconnected;

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_presentation != null)
        {
            // Exit TUI mode before disposing
            await _presentation.ExitTuiModeAsync();
            _presentation.Resized -= OnPresentationResized;
            _presentation.Disconnected -= OnPresentationDisconnected;
            await _presentation.DisposeAsync();
        }

        _workload.Disconnected -= OnWorkloadDisconnected;
        await _workload.DisposeAsync();

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
