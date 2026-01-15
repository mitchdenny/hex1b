using System.Text;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// A handle that connects a Hex1bTerminal to a TerminalWidget for embedding
/// child terminal sessions within a TUI application.
/// </summary>
/// <remarks>
/// <para>
/// The TerminalWidgetHandle acts as an <see cref="IHex1bTerminalPresentationAdapter"/>
/// for the child terminal while maintaining a screen buffer that the TerminalWidget
/// can render from. This allows the child terminal to continue running and capturing
/// output even when the widget is not mounted in the tree.
/// </para>
/// <para>
/// Usage:
/// <code>
/// var terminal = Hex1bTerminal.CreateBuilder()
///     .WithPtyProcess("bash")
///     .WithTerminalWidget(out var bashHandle)
///     .Build();
/// 
/// _ = terminal.RunAsync(appCt);
/// 
/// // In widget tree:
/// ctx.Terminal(bashHandle);
/// </code>
/// </para>
/// </remarks>
public sealed class TerminalWidgetHandle : IHex1bTerminalPresentationAdapter, IAsyncDisposable
{
    private readonly object _bufferLock = new();
    private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    
    private TerminalCell[,] _screenBuffer;
    private int _width;
    private int _height;
    private int _cursorX;
    private int _cursorY;
    private bool _cursorVisible = true;
    private bool _disposed;
    
    // Current SGR state for parsing
    private Hex1b.Theming.Hex1bColor? _currentForeground;
    private Hex1b.Theming.Hex1bColor? _currentBackground;
    private CellAttributes _currentAttributes;
    
    // Scroll region (0-based)
    private int _scrollTop;
    private int _scrollBottom;
    
    /// <summary>
    /// Creates a new TerminalWidgetHandle with the specified dimensions.
    /// </summary>
    /// <param name="width">Initial width in columns.</param>
    /// <param name="height">Initial height in rows.</param>
    public TerminalWidgetHandle(int width = 80, int height = 24)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        
        _width = width;
        _height = height;
        _scrollBottom = height - 1;
        _screenBuffer = new TerminalCell[height, width];
        ClearBuffer();
    }
    
    /// <summary>
    /// Gets the current width of the terminal in columns.
    /// </summary>
    public int Width => _width;
    
    /// <summary>
    /// Gets the current height of the terminal in rows.
    /// </summary>
    public int Height => _height;
    
    /// <summary>
    /// Gets the current cursor X position (0-based).
    /// </summary>
    public int CursorX => _cursorX;
    
    /// <summary>
    /// Gets the current cursor Y position (0-based).
    /// </summary>
    public int CursorY => _cursorY;
    
    /// <summary>
    /// Gets whether the cursor is currently visible.
    /// </summary>
    public bool CursorVisible => _cursorVisible;
    
    /// <summary>
    /// Event raised when new output has been written to the buffer.
    /// TerminalNode subscribes to this to trigger re-renders.
    /// </summary>
    public event Action? OutputReceived;
    
    /// <summary>
    /// Gets a copy of the current screen buffer.
    /// </summary>
    /// <returns>A copy of the screen buffer cells.</returns>
    public TerminalCell[,] GetScreenBuffer()
    {
        lock (_bufferLock)
        {
            var copy = new TerminalCell[_height, _width];
            Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
            return copy;
        }
    }
    
    /// <summary>
    /// Gets the cell at the specified position.
    /// </summary>
    public TerminalCell GetCell(int x, int y)
    {
        lock (_bufferLock)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return TerminalCell.Empty;
            return _screenBuffer[y, x];
        }
    }
    
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
        _currentAttributes = CellAttributes.None;
    }
    
    // === IHex1bTerminalPresentationAdapter Implementation ===
    
    int IHex1bTerminalPresentationAdapter.Width => _width;
    int IHex1bTerminalPresentationAdapter.Height => _height;
    
    /// <inheritdoc />
    public TerminalCapabilities Capabilities { get; } = new()
    {
        SupportsMouse = true,
        Supports256Colors = true,
        SupportsTrueColor = true,
    };
    
    /// <inheritdoc />
    public event Action<int, int>? Resized;
    
    /// <inheritdoc />
    public event Action? Disconnected;
    
    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || data.IsEmpty) return ValueTask.CompletedTask;
        
        var text = Encoding.UTF8.GetString(data.Span);
        var tokens = AnsiTokenizer.Tokenize(text);
        
        lock (_bufferLock)
        {
            foreach (var token in tokens)
            {
                ApplyToken(token);
            }
        }
        
        // Notify any bound nodes that output is available
        OutputReceived?.Invoke();
        
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;
        
        // Wait indefinitely until cancelled or disposed
        try
        {
            await _disconnected.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        
        return ReadOnlyMemory<byte>.Empty;
    }
    
    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    
    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    
    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    
    /// <summary>
    /// Resizes the terminal buffer.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0) return;
        
        lock (_bufferLock)
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
            
            // Copy existing content that fits
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
            _scrollBottom = newHeight - 1;
        }
        
        Resized?.Invoke(newWidth, newHeight);
    }
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        
        Disconnected?.Invoke();
        _disconnected.TrySetResult();
        
        return ValueTask.CompletedTask;
    }
    
    // === Token Processing (simplified version of Hex1bTerminal's logic) ===
    
    private void ApplyToken(AnsiToken token)
    {
        switch (token)
        {
            case TextToken textToken:
                ApplyText(textToken.Text);
                break;
                
            case ControlCharacterToken controlToken:
                ApplyControlCharacter(controlToken.Character);
                break;
                
            case SgrToken sgrToken:
                ProcessSgr(sgrToken.Parameters);
                break;
                
            case CursorPositionToken cursorToken:
                _cursorY = Math.Clamp(cursorToken.Row - 1, 0, _height - 1);
                _cursorX = Math.Clamp(cursorToken.Column - 1, 0, _width - 1);
                break;
                
            case ClearScreenToken clearToken:
                ApplyClearScreen(clearToken.Mode);
                break;
                
            case ClearLineToken clearLineToken:
                ApplyClearLine(clearLineToken.Mode);
                break;
                
            case CursorMoveToken moveToken:
                ApplyCursorMove(moveToken);
                break;
                
            case CursorColumnToken columnToken:
                _cursorX = Math.Clamp(columnToken.Column - 1, 0, _width - 1);
                break;
                
            case ScrollRegionToken scrollToken:
                if (scrollToken.Bottom == 0)
                {
                    _scrollTop = 0;
                    _scrollBottom = _height - 1;
                }
                else
                {
                    _scrollTop = Math.Clamp(scrollToken.Top - 1, 0, _height - 1);
                    _scrollBottom = Math.Clamp(scrollToken.Bottom - 1, _scrollTop, _height - 1);
                }
                _cursorX = 0;
                _cursorY = 0;
                break;
                
            case ScrollUpToken scrollUpToken:
                for (int i = 0; i < scrollUpToken.Count; i++)
                    ScrollUp();
                break;
                
            case ScrollDownToken scrollDownToken:
                for (int i = 0; i < scrollDownToken.Count; i++)
                    ScrollDown();
                break;
                
            case PrivateModeToken privateModeToken:
                if (privateModeToken.Mode == 25) // Cursor visibility
                {
                    _cursorVisible = privateModeToken.Enable;
                }
                else if (privateModeToken.Mode == 1049) // Alternate screen
                {
                    if (privateModeToken.Enable)
                    {
                        ClearBuffer();
                        _cursorX = 0;
                        _cursorY = 0;
                    }
                }
                break;
                
            case DeleteCharacterToken deleteCharToken:
                DeleteCharacters(deleteCharToken.Count);
                break;
                
            case InsertCharacterToken insertCharToken:
                InsertCharacters(insertCharToken.Count);
                break;
                
            case EraseCharacterToken eraseCharToken:
                EraseCharacters(eraseCharToken.Count);
                break;
                
            case InsertLinesToken insertLinesToken:
                InsertLines(insertLinesToken.Count);
                break;
                
            case DeleteLinesToken deleteLinesToken:
                DeleteLines(deleteLinesToken.Count);
                break;
        }
    }
    
    private void ApplyText(string text)
    {
        foreach (var c in text)
        {
            if (_cursorX >= _width)
            {
                _cursorX = 0;
                _cursorY++;
                if (_cursorY > _scrollBottom)
                {
                    ScrollUp();
                    _cursorY = _scrollBottom;
                }
            }
            
            if (_cursorY >= 0 && _cursorY < _height && _cursorX >= 0 && _cursorX < _width)
            {
                _screenBuffer[_cursorY, _cursorX] = new TerminalCell(
                    c.ToString(),
                    _currentForeground,
                    _currentBackground,
                    _currentAttributes);
                _cursorX++;
            }
        }
    }
    
    private void ApplyControlCharacter(char c)
    {
        switch (c)
        {
            case '\n':
                if (_cursorY >= _scrollBottom)
                {
                    ScrollUp();
                }
                else if (_cursorY < _height - 1)
                {
                    _cursorY++;
                }
                break;
                
            case '\r':
                _cursorX = 0;
                break;
                
            case '\t':
                _cursorX = Math.Min((_cursorX / 8 + 1) * 8, _width - 1);
                break;
                
            case '\b':
                if (_cursorX > 0)
                    _cursorX--;
                break;
        }
    }
    
    private void ApplyCursorMove(CursorMoveToken token)
    {
        switch (token.Direction)
        {
            case CursorMoveDirection.Up:
                _cursorY = Math.Max(0, _cursorY - token.Count);
                break;
            case CursorMoveDirection.Down:
                _cursorY = Math.Min(_height - 1, _cursorY + token.Count);
                break;
            case CursorMoveDirection.Forward:
                _cursorX = Math.Min(_width - 1, _cursorX + token.Count);
                break;
            case CursorMoveDirection.Back:
                _cursorX = Math.Max(0, _cursorX - token.Count);
                break;
            case CursorMoveDirection.NextLine:
                _cursorY = Math.Min(_height - 1, _cursorY + token.Count);
                _cursorX = 0;
                break;
            case CursorMoveDirection.PreviousLine:
                _cursorY = Math.Max(0, _cursorY - token.Count);
                _cursorX = 0;
                break;
        }
    }
    
    private void ApplyClearScreen(ClearMode mode)
    {
        switch (mode)
        {
            case ClearMode.ToEnd:
                // Clear from cursor to end
                for (int x = _cursorX; x < _width; x++)
                    _screenBuffer[_cursorY, x] = TerminalCell.Empty;
                for (int y = _cursorY + 1; y < _height; y++)
                    for (int x = 0; x < _width; x++)
                        _screenBuffer[y, x] = TerminalCell.Empty;
                break;
                
            case ClearMode.ToStart:
                // Clear from start to cursor
                for (int y = 0; y < _cursorY; y++)
                    for (int x = 0; x < _width; x++)
                        _screenBuffer[y, x] = TerminalCell.Empty;
                for (int x = 0; x <= _cursorX; x++)
                    _screenBuffer[_cursorY, x] = TerminalCell.Empty;
                break;
                
            case ClearMode.All:
            case ClearMode.AllAndScrollback:
                ClearBuffer();
                break;
        }
    }
    
    private void ApplyClearLine(ClearMode mode)
    {
        switch (mode)
        {
            case ClearMode.ToEnd:
                for (int x = _cursorX; x < _width; x++)
                    _screenBuffer[_cursorY, x] = TerminalCell.Empty;
                break;
                
            case ClearMode.ToStart:
                for (int x = 0; x <= _cursorX; x++)
                    _screenBuffer[_cursorY, x] = TerminalCell.Empty;
                break;
                
            case ClearMode.All:
                for (int x = 0; x < _width; x++)
                    _screenBuffer[_cursorY, x] = TerminalCell.Empty;
                break;
        }
    }
    
    private void ScrollUp()
    {
        for (int y = _scrollTop; y < _scrollBottom; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = _screenBuffer[y + 1, x];
            }
        }
        for (int x = 0; x < _width; x++)
        {
            _screenBuffer[_scrollBottom, x] = TerminalCell.Empty;
        }
    }
    
    private void ScrollDown()
    {
        for (int y = _scrollBottom; y > _scrollTop; y--)
        {
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[y, x] = _screenBuffer[y - 1, x];
            }
        }
        for (int x = 0; x < _width; x++)
        {
            _screenBuffer[_scrollTop, x] = TerminalCell.Empty;
        }
    }
    
    private void DeleteCharacters(int count)
    {
        for (int i = 0; i < count && _cursorX + i < _width; i++)
        {
            for (int x = _cursorX; x < _width - 1; x++)
            {
                _screenBuffer[_cursorY, x] = _screenBuffer[_cursorY, x + 1];
            }
            _screenBuffer[_cursorY, _width - 1] = TerminalCell.Empty;
        }
    }
    
    private void InsertCharacters(int count)
    {
        for (int i = 0; i < count; i++)
        {
            for (int x = _width - 1; x > _cursorX; x--)
            {
                _screenBuffer[_cursorY, x] = _screenBuffer[_cursorY, x - 1];
            }
            _screenBuffer[_cursorY, _cursorX] = TerminalCell.Empty;
        }
    }
    
    private void EraseCharacters(int count)
    {
        for (int i = 0; i < count && _cursorX + i < _width; i++)
        {
            _screenBuffer[_cursorY, _cursorX + i] = TerminalCell.Empty;
        }
    }
    
    private void InsertLines(int count)
    {
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom) return;
        
        for (int i = 0; i < count; i++)
        {
            for (int y = _scrollBottom; y > _cursorY; y--)
            {
                for (int x = 0; x < _width; x++)
                {
                    _screenBuffer[y, x] = _screenBuffer[y - 1, x];
                }
            }
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[_cursorY, x] = TerminalCell.Empty;
            }
        }
    }
    
    private void DeleteLines(int count)
    {
        if (_cursorY < _scrollTop || _cursorY > _scrollBottom) return;
        
        for (int i = 0; i < count; i++)
        {
            for (int y = _cursorY; y < _scrollBottom; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    _screenBuffer[y, x] = _screenBuffer[y + 1, x];
                }
            }
            for (int x = 0; x < _width; x++)
            {
                _screenBuffer[_scrollBottom, x] = TerminalCell.Empty;
            }
        }
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
                case 2:
                    _currentAttributes |= CellAttributes.Dim;
                    break;
                case 3:
                    _currentAttributes |= CellAttributes.Italic;
                    break;
                case 4:
                    _currentAttributes |= CellAttributes.Underline;
                    break;
                case 7:
                    _currentAttributes |= CellAttributes.Reverse;
                    break;
                case 9:
                    _currentAttributes |= CellAttributes.Strikethrough;
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
                case 27:
                    _currentAttributes &= ~CellAttributes.Reverse;
                    break;
                case 29:
                    _currentAttributes &= ~CellAttributes.Strikethrough;
                    break;
                    
                // Basic foreground colors (30-37)
                case >= 30 and <= 37:
                    _currentForeground = Hex1b.Theming.Hex1bColor.FromAnsi256(code - 30);
                    break;
                case 39:
                    _currentForeground = null;
                    break;
                    
                // Basic background colors (40-47)
                case >= 40 and <= 47:
                    _currentBackground = Hex1b.Theming.Hex1bColor.FromAnsi256(code - 40);
                    break;
                case 49:
                    _currentBackground = null;
                    break;
                    
                // Bright foreground colors (90-97)
                case >= 90 and <= 97:
                    _currentForeground = Hex1b.Theming.Hex1bColor.FromAnsi256(code - 90 + 8);
                    break;
                    
                // Bright background colors (100-107)
                case >= 100 and <= 107:
                    _currentBackground = Hex1b.Theming.Hex1bColor.FromAnsi256(code - 100 + 8);
                    break;
                    
                // 256-color and RGB colors
                case 38:
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentForeground = Hex1b.Theming.Hex1bColor.FromAnsi256(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentForeground = Hex1b.Theming.Hex1bColor.FromRgb(r, g, b);
                        }
                        i += 4;
                    }
                    break;
                    
                case 48:
                    if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var colorIndex))
                        {
                            _currentBackground = Hex1b.Theming.Hex1bColor.FromAnsi256(colorIndex);
                        }
                        i += 2;
                    }
                    else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                    {
                        if (int.TryParse(parts[i + 2], out var r) &&
                            int.TryParse(parts[i + 3], out var g) &&
                            int.TryParse(parts[i + 4], out var b))
                        {
                            _currentBackground = Hex1b.Theming.Hex1bColor.FromRgb(r, g, b);
                        }
                        i += 4;
                    }
                    break;
            }
        }
    }
}
