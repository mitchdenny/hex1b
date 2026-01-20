using System.Text;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A diagnostic shell workload that simulates a shell environment without PTY infrastructure.
/// Useful for testing terminal control codes and debugging terminal behavior.
/// </summary>
/// <remarks>
/// <para>
/// The diagnostic shell provides a simulated shell environment with built-in commands
/// for testing various terminal features including:
/// </para>
/// <list type="bullet">
///   <item>Control code output testing (colors, cursor movement, etc.)</item>
///   <item>Input capture and hex dump functionality</item>
///   <item>Command history with arrow key navigation</item>
/// </list>
/// <para>
/// Commands:
/// <list type="bullet">
///   <item><c>help</c> - Show available commands</item>
///   <item><c>echo &lt;text&gt;</c> - Echo text back</item>
///   <item><c>colors</c> - Display color test patterns</item>
///   <item><c>cursor</c> - Test cursor movement sequences</item>
///   <item><c>scroll</c> - Test scroll region</item>
///   <item><c>clear</c> - Clear screen</item>
///   <item><c>title &lt;text&gt;</c> - Set window title via OSC</item>
///   <item><c>capture</c> - Start capturing input</item>
///   <item><c>dump</c> - Dump captured input as hex table</item>
///   <item><c>exit</c> - Exit the shell</item>
/// </list>
/// </para>
/// </remarks>
public sealed class DiagnosticShellWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Channel<byte[]> _outputChannel;
    private readonly List<byte> _capturedInput = [];
    private readonly List<string> _commandHistory = [];
    private readonly object _inputLock = new();
    
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _exited;
    private bool _capturing;
    private int _historyIndex = -1;
    private string _currentLine = "";
    private int _cursorPosition;

    /// <summary>
    /// Creates a new diagnostic shell workload adapter.
    /// </summary>
    /// <param name="width">Initial terminal width.</param>
    /// <param name="height">Initial terminal height.</param>
    public DiagnosticShellWorkloadAdapter(int width = 80, int height = 24)
    {
        _width = width;
        _height = height;
        
        // Unbounded channel - output should never block
        _outputChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false // Multiple commands might write
        });
    }

    /// <summary>
    /// Gets whether the shell has exited.
    /// </summary>
    public bool HasExited => _exited;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed || _exited) return ReadOnlyMemory<byte>.Empty;

        try
        {
            // Wait for output from the channel
            if (await _outputChannel.Reader.WaitToReadAsync(ct))
            {
                if (_outputChannel.Reader.TryRead(out var data))
                {
                    return data;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (ChannelClosedException)
        {
            // Shell exited
        }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _exited) return ValueTask.CompletedTask;

        var bytes = data.ToArray();
        
        // Record captured input if capturing
        if (_capturing)
        {
            lock (_inputLock)
            {
                _capturedInput.AddRange(bytes);
            }
        }

        // Process input byte by byte
        ProcessInput(bytes);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        _width = width;
        _height = height;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _exited = true;
        
        // Close the channel to unblock any readers
        _outputChannel.Writer.TryComplete();
        
        Disconnected?.Invoke();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Starts the diagnostic shell and shows the initial prompt.
    /// </summary>
    public void Start()
    {
        WriteOutput("\x1b[2J\x1b[H"); // Clear screen and home cursor
        WriteOutput("\x1b[1;36mHex1b Diagnostic Shell\x1b[0m\r\n");
        WriteOutput($"Terminal size: {_width}x{_height}\r\n");
        WriteOutput("Type 'help' for available commands.\r\n");
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void ProcessInput(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            // Check for escape sequences
            if (bytes[i] == 0x1b && i + 1 < bytes.Length)
            {
                // CSI sequences (ESC [)
                if (bytes[i + 1] == '[')
                {
                    // Parse CSI sequence
                    if (i + 2 < bytes.Length)
                    {
                        switch (bytes[i + 2])
                        {
                            case (byte)'A': // Up arrow
                                HandleUpArrow();
                                i += 3;
                                continue;
                            case (byte)'B': // Down arrow
                                HandleDownArrow();
                                i += 3;
                                continue;
                            case (byte)'C': // Right arrow
                                HandleRightArrow();
                                i += 3;
                                continue;
                            case (byte)'D': // Left arrow
                                HandleLeftArrow();
                                i += 3;
                                continue;
                            case (byte)'H': // Home
                                HandleHome();
                                i += 3;
                                continue;
                            case (byte)'F': // End
                                HandleEnd();
                                i += 3;
                                continue;
                            case (byte)'3': // Delete (ESC [ 3 ~)
                                if (i + 3 < bytes.Length && bytes[i + 3] == '~')
                                {
                                    HandleDelete();
                                    i += 4;
                                    continue;
                                }
                                break;
                        }
                    }
                }
                
                // Skip unknown escape sequence
                i += 2;
                continue;
            }

            // Handle regular characters
            var b = bytes[i];
            switch (b)
            {
                case 0x0D: // Enter (CR)
                case 0x0A: // LF
                    HandleEnter();
                    break;
                case 0x7F: // Backspace (DEL)
                case 0x08: // Backspace (BS)
                    HandleBackspace();
                    break;
                case 0x03: // Ctrl+C
                    HandleCtrlC();
                    break;
                case 0x04: // Ctrl+D
                    HandleCtrlD();
                    break;
                case 0x15: // Ctrl+U - clear line
                    ClearCurrentLine();
                    break;
                case 0x0C: // Ctrl+L - clear screen
                    WriteOutput("\x1b[2J\x1b[H");
                    ShowPrompt();
                    RedrawCurrentLine();
                    break;
                default:
                    if (b >= 0x20 && b < 0x7F) // Printable ASCII
                    {
                        InsertChar((char)b);
                    }
                    break;
            }

            i++;
        }
    }

    private void HandleUpArrow()
    {
        if (_commandHistory.Count == 0) return;

        if (_historyIndex == -1)
        {
            _historyIndex = _commandHistory.Count - 1;
        }
        else if (_historyIndex > 0)
        {
            _historyIndex--;
        }
        else
        {
            return; // Already at beginning
        }

        SetCurrentLine(_commandHistory[_historyIndex]);
    }

    private void HandleDownArrow()
    {
        if (_historyIndex == -1) return;

        if (_historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            SetCurrentLine(_commandHistory[_historyIndex]);
        }
        else
        {
            _historyIndex = -1;
            SetCurrentLine("");
        }
    }

    private void HandleRightArrow()
    {
        if (_cursorPosition < _currentLine.Length)
        {
            _cursorPosition++;
            WriteOutput("\x1b[C"); // Move cursor right
        }
    }

    private void HandleLeftArrow()
    {
        if (_cursorPosition > 0)
        {
            _cursorPosition--;
            WriteOutput("\x1b[D"); // Move cursor left
        }
    }

    private void HandleHome()
    {
        if (_cursorPosition > 0)
        {
            WriteOutput($"\x1b[{_cursorPosition}D"); // Move cursor left by position
            _cursorPosition = 0;
        }
    }

    private void HandleEnd()
    {
        if (_cursorPosition < _currentLine.Length)
        {
            var move = _currentLine.Length - _cursorPosition;
            WriteOutput($"\x1b[{move}C"); // Move cursor right
            _cursorPosition = _currentLine.Length;
        }
    }

    private void HandleDelete()
    {
        if (_cursorPosition < _currentLine.Length)
        {
            _currentLine = _currentLine.Remove(_cursorPosition, 1);
            // Rewrite from cursor position
            var remaining = _currentLine[_cursorPosition..];
            WriteOutput(remaining + " \x1b[" + (remaining.Length + 1) + "D");
        }
    }

    private void HandleBackspace()
    {
        if (_cursorPosition > 0)
        {
            _cursorPosition--;
            _currentLine = _currentLine.Remove(_cursorPosition, 1);
            
            // Move cursor back, rewrite rest of line, erase last char
            var remaining = _currentLine[_cursorPosition..];
            WriteOutput("\x1b[D" + remaining + " \x1b[" + (remaining.Length + 1) + "D");
        }
    }

    private void HandleEnter()
    {
        WriteOutput("\r\n");
        
        var command = _currentLine.Trim();
        if (!string.IsNullOrEmpty(command))
        {
            _commandHistory.Add(command);
        }
        
        _historyIndex = -1;
        _currentLine = "";
        _cursorPosition = 0;

        if (!string.IsNullOrEmpty(command))
        {
            ExecuteCommand(command);
        }
        else
        {
            ShowPrompt();
        }
    }

    private void HandleCtrlC()
    {
        WriteOutput("^C\r\n");
        _currentLine = "";
        _cursorPosition = 0;
        _historyIndex = -1;
        ShowPrompt();
    }

    private void HandleCtrlD()
    {
        if (string.IsNullOrEmpty(_currentLine))
        {
            WriteOutput("exit\r\n");
            Exit();
        }
    }

    private void InsertChar(char c)
    {
        if (_cursorPosition == _currentLine.Length)
        {
            _currentLine += c;
            _cursorPosition++;
            WriteOutput(c.ToString());
        }
        else
        {
            _currentLine = _currentLine.Insert(_cursorPosition, c.ToString());
            _cursorPosition++;
            var remaining = _currentLine[(_cursorPosition - 1)..];
            WriteOutput(remaining + "\x1b[" + (remaining.Length - 1) + "D");
        }
    }

    private void SetCurrentLine(string line)
    {
        // Clear current line
        ClearCurrentLine();
        
        // Set new line
        _currentLine = line;
        _cursorPosition = line.Length;
        WriteOutput(line);
    }

    private void ClearCurrentLine()
    {
        if (_cursorPosition > 0)
        {
            WriteOutput($"\x1b[{_cursorPosition}D"); // Move to start
        }
        WriteOutput("\x1b[K"); // Erase to end of line
        _currentLine = "";
        _cursorPosition = 0;
    }

    private void RedrawCurrentLine()
    {
        WriteOutput(_currentLine);
        if (_cursorPosition < _currentLine.Length)
        {
            WriteOutput($"\x1b[{_currentLine.Length - _cursorPosition}D");
        }
    }

    private void ShowPrompt()
    {
        WriteOutput("\x1b[1;32mdiag\x1b[0m> ");
    }

    private void ExecuteCommand(string command)
    {
        var parts = command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : "";

        switch (cmd)
        {
            case "help":
                ShowHelp();
                break;
            case "echo":
                WriteOutput(args + "\r\n");
                ShowPrompt();
                break;
            case "colors":
                ShowColors();
                break;
            case "colors256":
                ShowColors256();
                break;
            case "colorsrgb":
                ShowColorsRgb();
                break;
            case "cursor":
                TestCursor();
                break;
            case "scroll":
                TestScroll();
                break;
            case "clear":
                WriteOutput("\x1b[2J\x1b[H");
                ShowPrompt();
                break;
            case "title":
                WriteOutput($"\x1b]0;{args}\x07");
                WriteOutput($"Window title set to: {args}\r\n");
                ShowPrompt();
                break;
            case "capture":
                StartCapture();
                break;
            case "dump":
                DumpCaptured();
                break;
            case "size":
                WriteOutput($"Terminal size: {_width}x{_height}\r\n");
                ShowPrompt();
                break;
            case "bell":
                WriteOutput("\x07"); // BEL character
                WriteOutput("Bell sent.\r\n");
                ShowPrompt();
                break;
            case "styles":
                ShowStyles();
                break;
            case "box":
                DrawBox();
                break;
            case "unicode":
                ShowUnicode();
                break;
            case "history":
                ShowHistory();
                break;
            case "ping":
                // Immediate single-line output for testing
                WriteOutput($"PONG @ {DateTime.Now:HH:mm:ss.fff}\r\n");
                ShowPrompt();
                break;
            case "timer":
                // Async output test - writes output from a background task
                StartAsyncTimer(args);
                break;
            case "flood":
                // Flood test - rapid synchronous output
                FloodTest(args);
                break;
            case "exit":
            case "quit":
                Exit();
                break;
            default:
                WriteOutput($"\x1b[31mUnknown command: {cmd}\x1b[0m\r\n");
                WriteOutput("Type 'help' for available commands.\r\n");
                ShowPrompt();
                break;
        }
    }

    private void ShowHelp()
    {
        WriteOutput("\x1b[1;36mAvailable Commands:\x1b[0m\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mGeneral:\x1b[0m\r\n");
        WriteOutput("    help          Show this help message\r\n");
        WriteOutput("    echo <text>   Echo text back\r\n");
        WriteOutput("    clear         Clear screen\r\n");
        WriteOutput("    size          Show terminal size\r\n");
        WriteOutput("    history       Show command history\r\n");
        WriteOutput("    exit          Exit the shell\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mControl Code Tests:\x1b[0m\r\n");
        WriteOutput("    colors        Show 16 ANSI colors\r\n");
        WriteOutput("    colors256     Show 256-color palette\r\n");
        WriteOutput("    colorsrgb     Show RGB color gradients\r\n");
        WriteOutput("    styles        Show text styles (bold, italic, etc.)\r\n");
        WriteOutput("    cursor        Test cursor movement\r\n");
        WriteOutput("    scroll        Test scroll region\r\n");
        WriteOutput("    box           Draw a box using line drawing chars\r\n");
        WriteOutput("    unicode       Show Unicode test patterns\r\n");
        WriteOutput("    bell          Send terminal bell\r\n");
        WriteOutput("    title <text>  Set window title\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mCapture:\x1b[0m\r\n");
        WriteOutput("    capture       Start capturing raw input\r\n");
        WriteOutput("    dump          Dump captured input as hex\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mDiagnostic:\x1b[0m\r\n");
        WriteOutput("    ping          Immediate output test (should appear instantly)\r\n");
        WriteOutput("    timer [n]     Async timer: outputs n times with 500ms delay (default: 5)\r\n");
        WriteOutput("    flood [n]     Rapid sync output: n lines (default: 20)\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mKeys:\x1b[0m Up/Down=history, Left/Right=cursor, Ctrl+C=cancel, Ctrl+L=clear\r\n");
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void ShowColors()
    {
        WriteOutput("\x1b[1;36m16 ANSI Colors:\x1b[0m\r\n");
        WriteOutput("\r\n");
        
        // Standard colors (0-7)
        WriteOutput("Standard:  ");
        for (int i = 0; i < 8; i++)
        {
            WriteOutput($"\x1b[48;5;{i}m  \x1b[0m");
        }
        WriteOutput("\r\n");
        
        // Bright colors (8-15)
        WriteOutput("Bright:    ");
        for (int i = 8; i < 16; i++)
        {
            WriteOutput($"\x1b[48;5;{i}m  \x1b[0m");
        }
        WriteOutput("\r\n");
        
        // Foreground colors
        WriteOutput("\r\nForeground:\r\n");
        for (int i = 0; i < 8; i++)
        {
            WriteOutput($"  \x1b[3{i}m Color {i} \x1b[0m");
            WriteOutput($"  \x1b[9{i}m Bright {i} \x1b[0m\r\n");
        }
        
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void ShowColors256()
    {
        WriteOutput("\x1b[1;36m256-Color Palette:\x1b[0m\r\n");
        WriteOutput("\r\n");
        
        // 16-231: 6x6x6 color cube
        WriteOutput("Color cube (16-231):\r\n");
        for (int green = 0; green < 6; green++)
        {
            for (int red = 0; red < 6; red++)
            {
                for (int blue = 0; blue < 6; blue++)
                {
                    int color = 16 + (red * 36) + (green * 6) + blue;
                    WriteOutput($"\x1b[48;5;{color}m \x1b[0m");
                }
                WriteOutput(" ");
            }
            WriteOutput("\r\n");
        }
        
        // 232-255: Grayscale
        WriteOutput("\r\nGrayscale (232-255):\r\n");
        for (int i = 232; i < 256; i++)
        {
            WriteOutput($"\x1b[48;5;{i}m \x1b[0m");
        }
        WriteOutput("\r\n\r\n");
        
        ShowPrompt();
    }

    private void ShowColorsRgb()
    {
        WriteOutput("\x1b[1;36mRGB Color Gradients:\x1b[0m\r\n");
        WriteOutput("\r\n");
        
        // Red gradient
        WriteOutput("Red:   ");
        for (int i = 0; i < 32; i++)
        {
            int r = (i * 255) / 31;
            WriteOutput($"\x1b[48;2;{r};0;0m \x1b[0m");
        }
        WriteOutput("\r\n");
        
        // Green gradient
        WriteOutput("Green: ");
        for (int i = 0; i < 32; i++)
        {
            int g = (i * 255) / 31;
            WriteOutput($"\x1b[48;2;0;{g};0m \x1b[0m");
        }
        WriteOutput("\r\n");
        
        // Blue gradient
        WriteOutput("Blue:  ");
        for (int i = 0; i < 32; i++)
        {
            int b = (i * 255) / 31;
            WriteOutput($"\x1b[48;2;0;0;{b}m \x1b[0m");
        }
        WriteOutput("\r\n");
        
        // Rainbow
        WriteOutput("Rainbow: ");
        for (int i = 0; i < 32; i++)
        {
            double hue = (i * 360.0) / 31;
            var (r, g, b) = HsvToRgb(hue, 1.0, 1.0);
            WriteOutput($"\x1b[48;2;{r};{g};{b}m \x1b[0m");
        }
        WriteOutput("\r\n\r\n");
        
        ShowPrompt();
    }

    private static (int r, int g, int b) HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = (h / 60) - (int)(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private void ShowStyles()
    {
        WriteOutput("\x1b[1;36mText Styles:\x1b[0m\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1mBold text\x1b[0m            (SGR 1)\r\n");
        WriteOutput("  \x1b[2mDim text\x1b[0m             (SGR 2)\r\n");
        WriteOutput("  \x1b[3mItalic text\x1b[0m          (SGR 3)\r\n");
        WriteOutput("  \x1b[4mUnderline text\x1b[0m       (SGR 4)\r\n");
        WriteOutput("  \x1b[5mBlink text\x1b[0m           (SGR 5)\r\n");
        WriteOutput("  \x1b[7mReverse text\x1b[0m         (SGR 7)\r\n");
        WriteOutput("  \x1b[8mHidden text\x1b[0m          (SGR 8)\r\n");
        WriteOutput("  \x1b[9mStrikethrough\x1b[0m        (SGR 9)\r\n");
        WriteOutput("  \x1b[21mDouble underline\x1b[0m     (SGR 21)\r\n");
        WriteOutput("  \x1b[53mOverline\x1b[0m             (SGR 53)\r\n");
        WriteOutput("\r\n");
        WriteOutput("  \x1b[1;3;4mBold + Italic + Underline\x1b[0m\r\n");
        WriteOutput("  \x1b[31;1;4mRed + Bold + Underline\x1b[0m\r\n");
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void TestCursor()
    {
        WriteOutput("\x1b[1;36mCursor Movement Test:\x1b[0m\r\n\r\n");
        
        // Save cursor, move around, restore
        WriteOutput("Testing save/restore cursor...\r\n");
        WriteOutput("Start -> ");
        WriteOutput("\x1b[s"); // Save cursor
        WriteOutput("SAVED");
        WriteOutput("\x1b[5C"); // Move right 5
        WriteOutput("MOVED");
        WriteOutput("\x1b[u"); // Restore cursor
        WriteOutput("RESTORED");
        WriteOutput("\r\n\r\n");
        
        // Cursor movement
        WriteOutput("Position test (should show X at row 10, col 20):\r\n");
        WriteOutput("\x1b[s"); // Save
        WriteOutput("\x1b[10;20H"); // Move to row 10, col 20
        WriteOutput("X");
        WriteOutput("\x1b[u"); // Restore
        WriteOutput("\r\n");
        
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void TestScroll()
    {
        WriteOutput("\x1b[1;36mScroll Region Test:\x1b[0m\r\n");
        WriteOutput("Setting scroll region to rows 5-10...\r\n");
        WriteOutput("\x1b[5;10r"); // Set scroll region
        WriteOutput("\x1b[5;1H"); // Move to top of region
        
        for (int i = 1; i <= 12; i++)
        {
            WriteOutput($"Scroll line {i}\r\n");
            System.Threading.Thread.Sleep(100);
        }
        
        WriteOutput("\x1b[r"); // Reset scroll region
        WriteOutput("\x1b[12;1H"); // Move below region
        WriteOutput("\r\nScroll test complete.\r\n\r\n");
        ShowPrompt();
    }

    private void DrawBox()
    {
        WriteOutput("\x1b[1;36mBox Drawing Characters:\x1b[0m\r\n\r\n");
        
        // Single line box
        WriteOutput("Single line:\r\n");
        WriteOutput("┌────────┐\r\n");
        WriteOutput("│ Hello! │\r\n");
        WriteOutput("└────────┘\r\n\r\n");
        
        // Double line box
        WriteOutput("Double line:\r\n");
        WriteOutput("╔════════╗\r\n");
        WriteOutput("║ Hello! ║\r\n");
        WriteOutput("╚════════╝\r\n\r\n");
        
        // Mixed
        WriteOutput("Mixed:\r\n");
        WriteOutput("╔═══╤═══╗\r\n");
        WriteOutput("║ A │ B ║\r\n");
        WriteOutput("╟───┼───╢\r\n");
        WriteOutput("║ C │ D ║\r\n");
        WriteOutput("╚═══╧═══╝\r\n\r\n");
        
        ShowPrompt();
    }

    private void ShowUnicode()
    {
        WriteOutput("\x1b[1;36mUnicode Test:\x1b[0m\r\n\r\n");
        
        WriteOutput("Arrows: ← ↑ → ↓ ↔ ↕ ⇐ ⇑ ⇒ ⇓\r\n");
        WriteOutput("Blocks: ▀ ▁ ▂ ▃ ▄ ▅ ▆ ▇ █ ░ ▒ ▓\r\n");
        WriteOutput("Shapes: ● ○ ◆ ◇ ■ □ ▲ △ ▼ ▽\r\n");
        WriteOutput("Math:   ∞ ≠ ≤ ≥ ± × ÷ √ ∑ ∏ ∫\r\n");
        WriteOutput("Greek:  α β γ δ ε ζ η θ λ μ π σ\r\n");
        WriteOutput("Emoji:  😀 🎉 ✨ 🚀 💻 🔥 ⚡ 🌈\r\n");
        WriteOutput("CJK:    日本語 中文 한국어\r\n");
        WriteOutput("Wide:   Ｆｕｌｌｗｉｄｔｈ\r\n");
        WriteOutput("\r\n");
        
        ShowPrompt();
    }

    private void ShowHistory()
    {
        WriteOutput("\x1b[1;36mCommand History:\x1b[0m\r\n");
        if (_commandHistory.Count == 0)
        {
            WriteOutput("  (empty)\r\n");
        }
        else
        {
            for (int i = 0; i < _commandHistory.Count; i++)
            {
                WriteOutput($"  {i + 1}: {_commandHistory[i]}\r\n");
            }
        }
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void StartAsyncTimer(string args)
    {
        var count = 5;
        if (!string.IsNullOrEmpty(args) && int.TryParse(args, out var parsed))
        {
            count = Math.Clamp(parsed, 1, 20);
        }
        
        WriteOutput($"Starting async timer ({count} ticks, 500ms interval)...\r\n");
        
        // Fire and forget - writes output asynchronously
        _ = Task.Run(async () =>
        {
            for (int i = 1; i <= count && !_exited; i++)
            {
                await Task.Delay(500);
                if (_exited) break;
                WriteOutput($"  Timer tick {i}/{count} @ {DateTime.Now:HH:mm:ss.fff}\r\n");
            }
            
            if (!_exited)
            {
                WriteOutput("Timer complete.\r\n");
                ShowPrompt();
            }
        });
    }

    private void FloodTest(string args)
    {
        var count = 20;
        if (!string.IsNullOrEmpty(args) && int.TryParse(args, out var parsed))
        {
            count = Math.Clamp(parsed, 1, 100);
        }
        
        WriteOutput($"Flood test ({count} lines):\r\n");
        for (int i = 1; i <= count; i++)
        {
            WriteOutput($"  Line {i:D3}: The quick brown fox jumps over the lazy dog.\r\n");
        }
        WriteOutput("\r\n");
        ShowPrompt();
    }

    private void StartCapture()
    {
        lock (_inputLock)
        {
            _capturedInput.Clear();
            _capturing = true;
        }
        WriteOutput("\x1b[33mCapture started. Type any keys, then use 'dump' to see hex.\x1b[0m\r\n");
        ShowPrompt();
    }

    private void DumpCaptured()
    {
        byte[] captured;
        lock (_inputLock)
        {
            captured = [.. _capturedInput];
            _capturing = false;
        }

        WriteOutput("\x1b[1;36mCaptured Input Hex Dump:\x1b[0m\r\n");
        
        if (captured.Length == 0)
        {
            WriteOutput("  (no data captured)\r\n");
            WriteOutput("\r\n");
            ShowPrompt();
            return;
        }

        // Hex dump format: offset | hex bytes | ASCII
        WriteOutput("\r\n");
        WriteOutput("Offset   00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F   ASCII\r\n");
        WriteOutput("─────────────────────────────────────────────────────────────────\r\n");

        for (int offset = 0; offset < captured.Length; offset += 16)
        {
            // Offset
            WriteOutput($"{offset:X8} ");

            // Hex bytes
            var sb = new StringBuilder();
            var ascii = new StringBuilder();
            
            for (int i = 0; i < 16; i++)
            {
                if (i == 8) sb.Append(' ');
                
                if (offset + i < captured.Length)
                {
                    var b = captured[offset + i];
                    sb.Append($"{b:X2} ");
                    ascii.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
                }
                else
                {
                    sb.Append("   ");
                    ascii.Append(' ');
                }
            }

            WriteOutput(sb.ToString());
            WriteOutput("  ");
            WriteOutput(ascii.ToString());
            WriteOutput("\r\n");
        }

        WriteOutput($"\r\nTotal bytes: {captured.Length}\r\n\r\n");
        ShowPrompt();
    }

    private void Exit()
    {
        WriteOutput("\r\nGoodbye!\r\n");
        _exited = true;
        
        // Close the channel to signal completion
        _outputChannel.Writer.TryComplete();
        
        Disconnected?.Invoke();
    }

    private void WriteOutput(string text)
    {
        if (_exited || _disposed) return;
        
        var bytes = Encoding.UTF8.GetBytes(text);
        _outputChannel.Writer.TryWrite(bytes);
    }
}

/// <summary>
/// Extension methods for adding diagnostic shell to <see cref="Hex1bTerminalBuilder"/>.
/// </summary>
public static class DiagnosticShellBuilderExtensions
{
    /// <summary>
    /// Configures the terminal to run the diagnostic shell workload.
    /// </summary>
    /// <param name="builder">The terminal builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// The diagnostic shell provides a simulated shell environment for testing
    /// terminal control codes without requiring PTY infrastructure.
    /// </para>
    /// <para>
    /// Available commands include: help, echo, colors, cursor, scroll, capture, dump, exit.
    /// Use up/down arrows to navigate command history.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var terminal = Hex1bTerminal.CreateBuilder()
    ///     .WithDiagnosticShell()
    ///     .Build();
    /// 
    /// await terminal.RunAsync();
    /// </code>
    /// </example>
    public static Hex1bTerminalBuilder WithDiagnosticShell(this Hex1bTerminalBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetWorkloadFactory(presentation =>
        {
            var width = presentation?.Width ?? 80;
            var height = presentation?.Height ?? 24;

            var adapter = new DiagnosticShellWorkloadAdapter(width, height);

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                adapter.Start();
                
                // Wait for shell to exit or cancellation
                var tcs = new TaskCompletionSource<int>();
                
                adapter.Disconnected += () => tcs.TrySetResult(0);
                
                using var registration = ct.Register(() => tcs.TrySetCanceled());
                
                try
                {
                    return await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    return 130; // Standard exit code for Ctrl+C
                }
            };

            return new Hex1bTerminalBuildContext(adapter, runCallback);
        });

        return builder;
    }
}
