using System.Text;
using Hex1b.Theming;

namespace Hex1b.Terminal;

/// <summary>
/// A presentation adapter wrapper that optimizes ANSI output by suppressing redundant updates.
/// </summary>
/// <remarks>
/// <para>
/// This adapter wraps another presentation adapter and maintains a snapshot of the last
/// terminal state sent to the presentation layer. Before forwarding output, it simulates
/// the ANSI sequences to determine if any terminal cells would actually change. If no
/// cells change, the output is suppressed, dramatically reducing unnecessary updates.
/// </para>
/// <para>
/// This is particularly useful for:
/// <list type="bullet">
///   <item>Reducing flicker in fast-updating displays</item>
///   <item>Minimizing network traffic for remote terminals</item>
///   <item>Improving performance for terminals with high latency</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OptimizedPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly IHex1bTerminalPresentationAdapter _inner;
    private TerminalCell[,] _lastSentState;
    private int _width;
    private int _height;
    private bool _disposed;
    private bool _hasSeenFirstWrite;
    private string? _lastNonCellSequence;  // Track last non-cell-modifying sequence

    /// <summary>
    /// Creates a new optimized presentation adapter wrapping the specified adapter.
    /// </summary>
    /// <param name="inner">The underlying presentation adapter to wrap.</param>
    public OptimizedPresentationAdapter(IHex1bTerminalPresentationAdapter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _width = inner.Width;
        _height = inner.Height;
        _lastSentState = new TerminalCell[_height, _width];
        _hasSeenFirstWrite = false;
        
        // Initialize with empty cells
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _lastSentState[y, x] = TerminalCell.Empty;
            }
        }

        // Forward resize events
        _inner.Resized += OnInnerResized;
        _inner.Disconnected += OnInnerDisconnected;
    }

    private void OnInnerResized(int width, int height)
    {
        // Resize our cached state
        var newState = new TerminalCell[height, width];
        
        // Initialize with empty cells
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                newState[y, x] = TerminalCell.Empty;
            }
        }
        
        // Copy existing content that fits
        var copyHeight = Math.Min(_height, height);
        var copyWidth = Math.Min(_width, width);
        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                newState[y, x] = _lastSentState[y, x];
            }
        }
        
        _lastSentState = newState;
        _width = width;
        _height = height;
        
        Resized?.Invoke(width, height);
    }

    private void OnInnerDisconnected()
    {
        Disconnected?.Invoke();
    }

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || data.IsEmpty) return;

        var text = Encoding.UTF8.GetString(data.Span);

        // Always forward the first write to establish baseline state
        if (!_hasSeenFirstWrite)
        {
            _hasSeenFirstWrite = true;
            await _inner.WriteOutputAsync(data, ct);
            UpdateCachedState(text);
            _lastNonCellSequence = text;
            return;
        }

        // Check if this output would actually change any cells
        if (WouldChangeAnyCells(text))
        {
            // Forward to the inner adapter
            await _inner.WriteOutputAsync(data, ct);
            UpdateCachedState(text);
            _lastNonCellSequence = text;
        }
        else
        {
            // No cell changes, but check if this is a different non-cell-modifying sequence
            // Forward it the first time, suppress repeats
            if (text != _lastNonCellSequence)
            {
                await _inner.WriteOutputAsync(data, ct);
                _lastNonCellSequence = text;
                UpdateCachedState(text);
            }
            // else: suppress - same non-cell-modifying sequence sent twice
        }
    }

    /// <summary>
    /// Determines if the given ANSI output would change any terminal cells.
    /// </summary>
    private bool WouldChangeAnyCells(string text)
    {
        // Create a temporary state to simulate the output
        var testState = new TerminalCell[_height, _width];
        Array.Copy(_lastSentState, testState, _lastSentState.Length);
        
        // Create a temporary terminal emulator to process the output
        var emulator = new AnsiEmulator(_width, _height, testState);
        emulator.ProcessOutput(text);
        
        // Compare the test state with our cached state
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (!CellsEqual(testState[y, x], _lastSentState[y, x]))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// Updates the cached state after output has been sent.
    /// </summary>
    private void UpdateCachedState(string text)
    {
        var emulator = new AnsiEmulator(_width, _height, _lastSentState);
        emulator.ProcessOutput(text);
    }

    /// <summary>
    /// Compares two terminal cells for equality.
    /// </summary>
    private static bool CellsEqual(TerminalCell a, TerminalCell b)
    {
        return a.Character == b.Character &&
               Equals(a.Foreground, b.Foreground) &&
               Equals(a.Background, b.Background) &&
               a.Attributes == b.Attributes;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        return _inner.ReadInputAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        return _inner.FlushAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
    {
        // Reset our cached state and first write flag when entering TUI mode
        _hasSeenFirstWrite = false;
        _lastNonCellSequence = null;
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _lastSentState[y, x] = TerminalCell.Empty;
            }
        }
        
        return _inner.EnterTuiModeAsync(ct);
    }

    /// <inheritdoc />
    public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
    {
        return _inner.ExitTuiModeAsync(ct);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _inner.Resized -= OnInnerResized;
        _inner.Disconnected -= OnInnerDisconnected;

        await _inner.DisposeAsync();
    }

    /// <summary>
    /// Simple ANSI emulator for tracking terminal cell state.
    /// </summary>
    private class AnsiEmulator
    {
        private readonly int _width;
        private readonly int _height;
        private readonly TerminalCell[,] _cells;
        private int _cursorX;
        private int _cursorY;
        private Hex1bColor? _currentForeground;
        private Hex1bColor? _currentBackground;
        private CellAttributes _currentAttributes;

        public AnsiEmulator(int width, int height, TerminalCell[,] cells)
        {
            _width = width;
            _height = height;
            _cells = cells;
        }

        public void ProcessOutput(string text)
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
                else if (text[i] >= 32) // Printable character
                {
                    if (_cursorY >= _height)
                    {
                        ScrollUp();
                        _cursorY = _height - 1;
                    }
                    
                    if (_cursorX < _width && _cursorY < _height)
                    {
                        _cells[_cursorY, _cursorX] = new TerminalCell(
                            text[i].ToString(), 
                            _currentForeground, 
                            _currentBackground, 
                            _currentAttributes);
                        
                        _cursorX++;
                        if (_cursorX >= _width)
                        {
                            _cursorX = 0;
                            _cursorY++;
                        }
                    }
                    i++;
                }
                else
                {
                    i++; // Skip control characters we don't handle
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
                case 'K':
                    ProcessClearLine(parameters);
                    break;
                case 'h':
                case 'l':
                    if (parameters.Contains("?1049"))
                    {
                        if (command == 'h')
                        {
                            // Enter alternate screen
                            ClearScreen();
                        }
                    }
                    break;
            }

            return end + 1;
        }

        private void ProcessSgr(string parameters)
        {
            if (string.IsNullOrEmpty(parameters) || parameters == "0")
            {
                _currentForeground = null;
                _currentBackground = null;
                _currentAttributes = CellAttributes.None;
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
                        _currentAttributes = CellAttributes.None;
                        break;
                    case 1:
                        _currentAttributes |= CellAttributes.Bold;
                        break;
                    case 3:
                        _currentAttributes |= CellAttributes.Italic;
                        break;
                    case 4:
                        _currentAttributes |= CellAttributes.Underline;
                        break;
                    case 22:
                        _currentAttributes &= ~(CellAttributes.Bold | CellAttributes.Dim);
                        break;
                    case 23:
                        _currentAttributes &= ~CellAttributes.Italic;
                        break;
                    case 24:
                        _currentAttributes &= ~CellAttributes.Underline;
                        break;
                    
                    // Foreground colors
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
        }

        private void ProcessClearScreen(string parameters)
        {
            var mode = string.IsNullOrEmpty(parameters) ? 0 : int.TryParse(parameters, out var m) ? m : 0;

            switch (mode)
            {
                case 0: // Clear from cursor to end
                    ClearFromCursor();
                    break;
                case 1: // Clear from start to cursor
                    ClearToCursor();
                    break;
                case 2:
                case 3: // Clear entire screen
                    ClearScreen();
                    break;
            }
        }

        private void ProcessClearLine(string parameters)
        {
            var mode = string.IsNullOrEmpty(parameters) ? 0 : int.TryParse(parameters, out var m) ? m : 0;

            switch (mode)
            {
                case 0: // Clear from cursor to end of line
                    for (int x = _cursorX; x < _width; x++)
                    {
                        _cells[_cursorY, x] = TerminalCell.Empty;
                    }
                    break;
                case 1: // Clear from start of line to cursor
                    for (int x = 0; x <= _cursorX && x < _width; x++)
                    {
                        _cells[_cursorY, x] = TerminalCell.Empty;
                    }
                    break;
                case 2: // Clear entire line
                    for (int x = 0; x < _width; x++)
                    {
                        _cells[_cursorY, x] = TerminalCell.Empty;
                    }
                    break;
            }
        }

        private void ClearScreen()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _cells[y, x] = TerminalCell.Empty;
                }
            }
            _currentForeground = null;
            _currentBackground = null;
        }

        private void ClearFromCursor()
        {
            for (int x = _cursorX; x < _width; x++)
            {
                _cells[_cursorY, x] = TerminalCell.Empty;
            }
            for (int y = _cursorY + 1; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _cells[y, x] = TerminalCell.Empty;
                }
            }
        }

        private void ClearToCursor()
        {
            for (int y = 0; y < _cursorY; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _cells[y, x] = TerminalCell.Empty;
                }
            }
            for (int x = 0; x <= _cursorX && x < _width; x++)
            {
                _cells[_cursorY, x] = TerminalCell.Empty;
            }
        }

        private void ScrollUp()
        {
            for (int y = 0; y < _height - 1; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _cells[y, x] = _cells[y + 1, x];
                }
            }
            for (int x = 0; x < _width; x++)
            {
                _cells[_height - 1, x] = TerminalCell.Empty;
            }
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
    }
}
