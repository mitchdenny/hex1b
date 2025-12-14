using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Represents a single cell in the virtual terminal screen buffer.
/// </summary>
public readonly record struct TerminalCell(char Character, Hex1bColor? Foreground, Hex1bColor? Background)
{
    public static readonly TerminalCell Empty = new(' ', null, null);
}

/// <summary>
/// A virtual terminal emulator that captures all output to an in-memory screen buffer
/// and allows programmatic injection of input events. Useful for testing TUI applications
/// and for scenarios like screen recording, remote terminals, or headless operation.
/// </summary>
public sealed class Hex1bTerminal : IHex1bTerminal, IDisposable
{
    private readonly Channel<Hex1bEvent> _inputChannel;
    private TerminalCell[,] _screenBuffer;
    private readonly StringBuilder _rawOutput;
    private int _cursorX;
    private int _cursorY;
    private bool _inAlternateScreen;
    private Hex1bColor? _currentForeground;
    private Hex1bColor? _currentBackground;

    /// <summary>
    /// Creates a new virtual terminal with the specified dimensions.
    /// </summary>
    /// <param name="width">Terminal width in characters.</param>
    /// <param name="height">Terminal height in lines.</param>
    public Hex1bTerminal(int width = 80, int height = 24)
    {
        Width = width;
        Height = height;
        _screenBuffer = new TerminalCell[height, width];
        _rawOutput = new StringBuilder();
        _inputChannel = Channel.CreateUnbounded<Hex1bEvent>();
        ClearBuffer();
    }

    /// <inheritdoc />
    public int Width { get; private set; }

    /// <inheritdoc />
    public int Height { get; private set; }

    /// <inheritdoc />
    public ChannelReader<Hex1bEvent> InputEvents => _inputChannel.Reader;

    /// <summary>
    /// Gets whether the terminal is currently in alternate screen mode.
    /// </summary>
    public bool InAlternateScreen => _inAlternateScreen;

    /// <summary>
    /// Gets the current cursor X position (0-based).
    /// </summary>
    public int CursorX => _cursorX;

    /// <summary>
    /// Gets the current cursor Y position (0-based).
    /// </summary>
    public int CursorY => _cursorY;

    /// <summary>
    /// Gets the raw output written to this terminal, including ANSI escape sequences.
    /// </summary>
    public string RawOutput => _rawOutput.ToString();

    /// <summary>
    /// Gets a copy of the current screen buffer.
    /// </summary>
    public TerminalCell[,] GetScreenBuffer()
    {
        var copy = new TerminalCell[Height, Width];
        Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
        return copy;
    }

    /// <summary>
    /// Gets the text content of the screen buffer as a string, with lines separated by newlines.
    /// ANSI escape sequences are stripped, leaving only visible characters.
    /// </summary>
    public string GetScreenText()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                sb.Append(_screenBuffer[y, x].Character);
            }
            if (y < Height - 1)
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
        if (lineIndex < 0 || lineIndex >= Height)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        var sb = new StringBuilder(Width);
        for (int x = 0; x < Width; x++)
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
        for (int y = 0; y < Height; y++)
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
        for (int y = 0; y < Height; y++)
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

    /// <inheritdoc />
    public void Write(string text)
    {
        _rawOutput.Append(text);
        ProcessOutput(text);
    }

    /// <inheritdoc />
    public void Clear()
    {
        ClearBuffer();
        _cursorX = 0;
        _cursorY = 0;
    }

    /// <inheritdoc />
    public void SetCursorPosition(int left, int top)
    {
        _cursorX = Math.Clamp(left, 0, Width - 1);
        _cursorY = Math.Clamp(top, 0, Height - 1);
    }

    /// <inheritdoc />
    public void EnterAlternateScreen()
    {
        _inAlternateScreen = true;
        ClearBuffer();
        _cursorX = 0;
        _cursorY = 0;
    }

    /// <inheritdoc />
    public void ExitAlternateScreen()
    {
        _inAlternateScreen = false;
    }

    /// <summary>
    /// Injects a key input event into the terminal.
    /// </summary>
    public async ValueTask SendKeyAsync(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        var evt = KeyMapper.ToHex1bKeyEvent(key, keyChar, shift, alt, control);
        await _inputChannel.Writer.WriteAsync(evt);
    }

    /// <summary>
    /// Injects a key input event into the terminal synchronously.
    /// </summary>
    public void SendKey(ConsoleKey key, char keyChar = '\0', bool shift = false, bool alt = false, bool control = false)
    {
        var evt = KeyMapper.ToHex1bKeyEvent(key, keyChar, shift, alt, control);
        _inputChannel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Injects a key input event using the new Hex1bKey type.
    /// </summary>
    public async ValueTask SendKeyAsync(Hex1bKey key, char keyChar = '\0', Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        var evt = new Hex1bKeyEvent(key, keyChar, modifiers);
        await _inputChannel.Writer.WriteAsync(evt);
    }

    /// <summary>
    /// Injects a key input event using the new Hex1bKey type synchronously.
    /// </summary>
    public void SendKey(Hex1bKey key, char keyChar = '\0', Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        var evt = new Hex1bKeyEvent(key, keyChar, modifiers);
        _inputChannel.Writer.TryWrite(evt);
    }

    /// <summary>
    /// Types a string of characters into the terminal.
    /// </summary>
    public async ValueTask TypeTextAsync(string text)
    {
        foreach (var c in text)
        {
            var key = CharToConsoleKey(c);
            var shift = char.IsUpper(c);
            await SendKeyAsync(key, c, shift: shift);
        }
    }

    /// <summary>
    /// Types a string of characters into the terminal synchronously.
    /// </summary>
    public void TypeText(string text)
    {
        foreach (var c in text)
        {
            var key = CharToConsoleKey(c);
            var shift = char.IsUpper(c);
            SendKey(key, c, shift: shift);
        }
    }

    /// <summary>
    /// Completes the input channel, signaling end of input.
    /// </summary>
    public void CompleteInput()
    {
        _inputChannel.Writer.Complete();
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
        var copyHeight = Math.Min(Height, newHeight);
        var copyWidth = Math.Min(Width, newWidth);
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                newBuffer[y, x] = _screenBuffer[y, x];
            }
        }

        _screenBuffer = newBuffer;
        Width = newWidth;
        Height = newHeight;
        _cursorX = Math.Min(_cursorX, newWidth - 1);
        _cursorY = Math.Min(_cursorY, newHeight - 1);
    }

    /// <summary>
    /// Clears the raw output buffer.
    /// </summary>
    public void ClearRawOutput() => _rawOutput.Clear();

    /// <inheritdoc />
    public void Dispose()
    {
        _inputChannel.Writer.TryComplete();
    }

    private void ClearBuffer()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
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
                // ANSI escape sequence
                i = ProcessAnsiSequence(text, i);
            }
            else if (text[i] == '\n')
            {
                _cursorY++;
                _cursorX = 0;
                if (_cursorY >= Height)
                {
                    ScrollUp();
                    _cursorY = Height - 1;
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
                // Regular character
                if (_cursorX < Width && _cursorY < Height)
                {
                    _screenBuffer[_cursorY, _cursorX] = new TerminalCell(text[i], _currentForeground, _currentBackground);
                    _cursorX++;
                    if (_cursorX >= Width)
                    {
                        _cursorX = 0;
                        _cursorY++;
                        if (_cursorY >= Height)
                        {
                            ScrollUp();
                            _cursorY = Height - 1;
                        }
                    }
                }
                i++;
            }
        }
    }

    private int ProcessAnsiSequence(string text, int start)
    {
        // Find end of sequence (letter or 'm' for SGR)
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
            case 'm': // SGR (Select Graphic Rendition)
                ProcessSgr(parameters);
                break;
            case 'H': // Cursor position
                ProcessCursorPosition(parameters);
                break;
            case 'J': // Clear screen
                ProcessClearScreen(parameters);
                break;
            case 'h': // Set mode (including alternate screen)
            case 'l': // Reset mode
                // Handle alternate screen buffer commands
                if (parameters.Contains("?1049"))
                {
                    if (command == 'h')
                        EnterAlternateScreen();
                    else
                        ExitAlternateScreen();
                }
                break;
        }

        return end + 1;
    }

    private void ProcessSgr(string parameters)
    {
        if (string.IsNullOrEmpty(parameters) || parameters == "0")
        {
            // Reset
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
                case 38: // Extended foreground color
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
                case 48: // Extended background color
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
                // ANSI positions are 1-based
                _cursorY = Math.Clamp(row - 1, 0, Height - 1);
                _cursorX = Math.Clamp(col - 1, 0, Width - 1);
            }
        }
        else if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var row))
            {
                _cursorY = Math.Clamp(row - 1, 0, Height - 1);
            }
        }
    }

    private void ProcessClearScreen(string parameters)
    {
        var mode = string.IsNullOrEmpty(parameters) ? 0 : int.TryParse(parameters, out var m) ? m : 0;

        switch (mode)
        {
            case 0: // Clear from cursor to end
                ClearFromCursor();
                break;
            case 1: // Clear from beginning to cursor
                ClearToCursor();
                break;
            case 2: // Clear entire screen
            case 3: // Clear entire screen and scrollback
                ClearBuffer();
                break;
        }
    }

    private void ClearFromCursor()
    {
        // Clear current line from cursor
        for (int x = _cursorX; x < Width; x++)
        {
            _screenBuffer[_cursorY, x] = TerminalCell.Empty;
        }
        // Clear all lines below
        for (int y = _cursorY + 1; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
    }

    private void ClearToCursor()
    {
        // Clear all lines above
        for (int y = 0; y < _cursorY; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _screenBuffer[y, x] = TerminalCell.Empty;
            }
        }
        // Clear current line up to cursor
        for (int x = 0; x <= _cursorX && x < Width; x++)
        {
            _screenBuffer[_cursorY, x] = TerminalCell.Empty;
        }
    }

    private void ScrollUp()
    {
        // Move all lines up by one
        for (int y = 0; y < Height - 1; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _screenBuffer[y, x] = _screenBuffer[y + 1, x];
            }
        }
        // Clear the bottom line
        for (int x = 0; x < Width; x++)
        {
            _screenBuffer[Height - 1, x] = TerminalCell.Empty;
        }
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
        0 => Hex1bColor.FromRgb(0, 0, 0),       // Black
        1 => Hex1bColor.FromRgb(128, 0, 0),     // Red
        2 => Hex1bColor.FromRgb(0, 128, 0),     // Green
        3 => Hex1bColor.FromRgb(128, 128, 0),   // Yellow
        4 => Hex1bColor.FromRgb(0, 0, 128),     // Blue
        5 => Hex1bColor.FromRgb(128, 0, 128),   // Magenta
        6 => Hex1bColor.FromRgb(0, 128, 128),   // Cyan
        7 => Hex1bColor.FromRgb(192, 192, 192), // White
        _ => Hex1bColor.FromRgb(128, 128, 128)
    };

    private static Hex1bColor BrightColorFromCode(int code) => code switch
    {
        0 => Hex1bColor.FromRgb(128, 128, 128), // Bright Black (Gray)
        1 => Hex1bColor.FromRgb(255, 0, 0),     // Bright Red
        2 => Hex1bColor.FromRgb(0, 255, 0),     // Bright Green
        3 => Hex1bColor.FromRgb(255, 255, 0),   // Bright Yellow
        4 => Hex1bColor.FromRgb(0, 0, 255),     // Bright Blue
        5 => Hex1bColor.FromRgb(255, 0, 255),   // Bright Magenta
        6 => Hex1bColor.FromRgb(0, 255, 255),   // Bright Cyan
        7 => Hex1bColor.FromRgb(255, 255, 255), // Bright White
        _ => Hex1bColor.FromRgb(192, 192, 192)
    };

    private static Hex1bColor Color256FromIndex(int index)
    {
        if (index < 16)
        {
            // Standard colors
            return index < 8 ? StandardColorFromCode(index) : BrightColorFromCode(index - 8);
        }
        else if (index < 232)
        {
            // 216 color cube
            index -= 16;
            var r = (index / 36) * 51;
            var g = ((index / 6) % 6) * 51;
            var b = (index % 6) * 51;
            return Hex1bColor.FromRgb((byte)r, (byte)g, (byte)b);
        }
        else
        {
            // Grayscale
            var gray = (index - 232) * 10 + 8;
            return Hex1bColor.FromRgb((byte)gray, (byte)gray, (byte)gray);
        }
    }
}
