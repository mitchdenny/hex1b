using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>
/// Represents the lifecycle state of a terminal session.
/// </summary>
public enum TerminalState
{
    /// <summary>
    /// The terminal session has not started yet.
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// The terminal session is currently running.
    /// </summary>
    Running,
    
    /// <summary>
    /// The terminal session has completed (process exited).
    /// </summary>
    Completed
}

/// <summary>
/// A handle that connects a Hex1bTerminal to a TerminalWidget for embedding
/// child terminal sessions within a TUI application.
/// </summary>
/// <remarks>
/// <para>
/// The TerminalWidgetHandle implements <see cref="ICellImpactAwarePresentationAdapter"/>
/// to receive pre-processed cell impacts directly from the terminal's ANSI parsing logic.
/// This eliminates the need to duplicate parsing code while maintaining a screen buffer 
/// that the TerminalWidget can render from.
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
public sealed class TerminalWidgetHandle : ICellImpactAwarePresentationAdapter, IAsyncDisposable
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
    
    // Mouse tracking state - tracks whether the child process has enabled mouse
    // These are set when we receive PrivateModeToken from the child's output
    private bool _mouseTrackingEnabled;  // Mode 1000, 1002, or 1003
    private bool _sgrMouseModeEnabled;   // Mode 1006
    
    // Cursor shape (from CursorShapeToken)
    private CursorShape _cursorShape = CursorShape.Default;
    
    // Terminal title (OSC 0/2) and icon name (OSC 0/1)
    private string _windowTitle = "";
    private string _iconName = "";
    
    // Title stack for OSC 22/23 (push/pop)
    private readonly Stack<(string Title, string IconName)> _titleStack = new();
    
    // Reference to the owning terminal for forwarding input
    private Hex1bTerminal? _terminal;
    
    // Terminal lifecycle state
    private TerminalState _state = TerminalState.NotStarted;
    private int? _exitCode;
    
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
    /// Gets the current cursor shape requested by the child process.
    /// </summary>
    public CursorShape CursorShape => _cursorShape;
    
    /// <summary>
    /// Gets whether the child process has enabled mouse tracking.
    /// Mouse events are only forwarded to the child when this is true.
    /// </summary>
    public bool MouseTrackingEnabled => _mouseTrackingEnabled;
    
    /// <summary>
    /// Gets the current lifecycle state of the terminal session.
    /// </summary>
    public TerminalState State => _state;
    
    /// <summary>
    /// Gets the exit code of the terminal process, if it has completed.
    /// </summary>
    public int? ExitCode => _exitCode;
    
    /// <summary>
    /// Gets whether the terminal is currently running.
    /// </summary>
    public bool IsRunning => _state == TerminalState.Running;
    
    /// <summary>
    /// Gets the current window title set by OSC 0 or OSC 2 sequences from the child process.
    /// </summary>
    public string WindowTitle => _windowTitle;

    /// <summary>
    /// Gets the current icon name set by OSC 0 or OSC 1 sequences from the child process.
    /// </summary>
    public string IconName => _iconName;
    
    /// <summary>
    /// Event raised when the terminal state changes.
    /// </summary>
    public event Action<TerminalState>? StateChanged;
    
    /// <summary>
    /// Event raised when new output has been written to the buffer.
    /// TerminalNode subscribes to this to trigger re-renders.
    /// </summary>
    public event Action? OutputReceived;
    
    /// <summary>
    /// Event raised when the window title changes (OSC 0 or OSC 2 from child process).
    /// </summary>
    public event Action<string>? WindowTitleChanged;

    /// <summary>
    /// Event raised when the icon name changes (OSC 0 or OSC 1 from child process).
    /// </summary>
    public event Action<string>? IconNameChanged;
    
    /// <summary>
    /// Sets the terminal that owns this handle. Called by Hex1bTerminal constructor.
    /// </summary>
    internal void SetTerminal(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }
    
    /// <summary>
    /// Marks the terminal as started/running. Called by Hex1bTerminal.RunAsync.
    /// </summary>
    internal void NotifyStarted()
    {
        if (_state == TerminalState.NotStarted)
        {
            _state = TerminalState.Running;
            StateChanged?.Invoke(_state);
            OutputReceived?.Invoke(); // Trigger re-render to switch from fallback to terminal
        }
    }
    
    /// <summary>
    /// Marks the terminal as completed. Called by Hex1bTerminal.RunAsync when the process exits.
    /// </summary>
    /// <param name="exitCode">The exit code of the process.</param>
    internal void NotifyCompleted(int exitCode)
    {
        if (_state != TerminalState.Completed)
        {
            _exitCode = exitCode;
            _state = TerminalState.Completed;
            StateChanged?.Invoke(_state);
            OutputReceived?.Invoke(); // Trigger re-render to switch from terminal to fallback
        }
    }
    
    /// <summary>
    /// Resets the terminal state to NotStarted. Used when restarting a terminal.
    /// </summary>
    public void Reset()
    {
        lock (_bufferLock)
        {
            _state = TerminalState.NotStarted;
            _exitCode = null;
            ClearBuffer();
        }
        StateChanged?.Invoke(_state);
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Sends an input event (key or mouse) to the terminal's workload (e.g., child process).
    /// Mouse events are only forwarded if the child process has enabled mouse tracking.
    /// </summary>
    /// <param name="evt">The event to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event has been sent.</returns>
    public async Task SendEventAsync(Hex1bEvent evt, CancellationToken ct = default)
    {
        if (_terminal == null || _disposed) return;
        
        // Only forward mouse events if the child has enabled mouse tracking
        if (evt is Hex1bMouseEvent && !_mouseTrackingEnabled)
        {
            return;
        }
        
        await _terminal.SendEventAsync(evt, ct);
    }
    
    /// <summary>
    /// Handles private mode tokens to track mouse and cursor state changes.
    /// </summary>
    private void HandlePrivateModeToken(PrivateModeToken pm)
    {
        switch (pm.Mode)
        {
            // Cursor visibility
            case 25:
                _cursorVisible = pm.Enable;
                break;
            
            // Mouse tracking modes
            case 1000: // X11 mouse - basic button tracking
            case 1002: // Button event tracking (motion while button held)
            case 1003: // Any event tracking (all motion)
                _mouseTrackingEnabled = pm.Enable;
                break;
            
            // SGR extended mouse mode (extended coordinates)
            case 1006:
                _sgrMouseModeEnabled = pm.Enable;
                break;
        }
    }
    
    /// <summary>
    /// Handles OSC tokens for title sequences.
    /// </summary>
    private void HandleOscToken(OscToken osc)
    {
        switch (osc.Command)
        {
            case "0":
                // OSC 0: Set both icon name and window title
                SetIconName(osc.Payload);
                SetWindowTitle(osc.Payload);
                break;
                
            case "1":
                // OSC 1: Set icon name only
                SetIconName(osc.Payload);
                break;
                
            case "2":
                // OSC 2: Set window title only
                SetWindowTitle(osc.Payload);
                break;
                
            case "22":
                // OSC 22: Push title onto stack
                PushTitleStack(osc.Payload);
                break;
                
            case "23":
                // OSC 23: Pop title from stack
                PopTitleStack();
                break;
        }
    }
    
    /// <summary>
    /// Sets the window title and fires the WindowTitleChanged event if changed.
    /// </summary>
    private void SetWindowTitle(string title)
    {
        if (_windowTitle != title)
        {
            _windowTitle = title;
            WindowTitleChanged?.Invoke(title);
        }
    }
    
    /// <summary>
    /// Sets the icon name and fires the IconNameChanged event if changed.
    /// </summary>
    private void SetIconName(string name)
    {
        if (_iconName != name)
        {
            _iconName = name;
            IconNameChanged?.Invoke(name);
        }
    }
    
    /// <summary>
    /// Pushes the current title and icon name onto the stack (OSC 22).
    /// </summary>
    private void PushTitleStack(string payload)
    {
        _titleStack.Push((_windowTitle, _iconName));
        
        // If payload is not empty and not just a mode specifier, treat it as a new title
        if (!string.IsNullOrEmpty(payload) && payload != "0" && payload != "1" && payload != "2")
        {
            SetWindowTitle(payload);
            SetIconName(payload);
        }
    }
    
    /// <summary>
    /// Pops the title and icon name from the stack (OSC 23).
    /// </summary>
    private void PopTitleStack()
    {
        if (_titleStack.Count == 0) return;
        
        var (savedTitle, savedIconName) = _titleStack.Pop();
        SetWindowTitle(savedTitle);
        SetIconName(savedIconName);
    }

    /// <summary>
    /// Sends a key event to the terminal's workload (e.g., child process).
    /// </summary>
    /// <param name="keyEvent">The key event to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the event has been sent.</returns>
    [Obsolete("Use SendEventAsync instead.")]
    public Task SendKeyEventAsync(Hex1bKeyEvent keyEvent, CancellationToken ct = default)
        => SendEventAsync(keyEvent, ct);
    
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
    /// Gets a snapshot of the current screen buffer with its dimensions.
    /// This is atomic - the dimensions will always match the buffer.
    /// </summary>
    /// <returns>A tuple containing the buffer copy, width, and height.</returns>
    public (TerminalCell[,] Buffer, int Width, int Height) GetScreenBufferSnapshot()
    {
        lock (_bufferLock)
        {
            var copy = new TerminalCell[_height, _width];
            Array.Copy(_screenBuffer, copy, _screenBuffer.Length);
            return (copy, _width, _height);
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
    
    /// <summary>
    /// Fallback for when the terminal doesn't support cell impacts.
    /// This should rarely be called since ICellImpactAwarePresentationAdapter.WriteOutputWithImpactsAsync
    /// is the preferred path.
    /// </summary>
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // This is the fallback path - the terminal should prefer WriteOutputWithImpactsAsync
        // when it detects we implement ICellImpactAwarePresentationAdapter.
        // For now, just notify that something happened (display may be incomplete).
        if (!_disposed && !data.IsEmpty)
        {
            OutputReceived?.Invoke();
        }
        return ValueTask.CompletedTask;
    }
    
    /// <summary>
    /// Receives pre-processed cell impacts from the terminal's ANSI parsing logic.
    /// This is the main entry point for output - the terminal calls this instead of
    /// WriteOutputAsync when it detects we implement ICellImpactAwarePresentationAdapter.
    /// </summary>
    public ValueTask WriteOutputWithImpactsAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default)
    {
        if (_disposed || appliedTokens.Count == 0) 
            return ValueTask.CompletedTask;
        
        int maxY = -1;
        int droppedCount = 0;
        lock (_bufferLock)
        {
            foreach (var applied in appliedTokens)
            {
                // Check for mode changes from the child process
                if (applied.Token is PrivateModeToken pm)
                {
                    HandlePrivateModeToken(pm);
                }
                else if (applied.Token is CursorShapeToken cst)
                {
                    _cursorShape = cst.Shape switch
                    {
                        1 => CursorShape.BlinkingBlock,
                        2 => CursorShape.SteadyBlock,
                        3 => CursorShape.BlinkingUnderline,
                        4 => CursorShape.SteadyUnderline,
                        5 => CursorShape.BlinkingBar,
                        6 => CursorShape.SteadyBar,
                        _ => CursorShape.Default
                    };
                }
                else if (applied.Token is OscToken osc)
                {
                    HandleOscToken(osc);
                }
                
                // Apply each cell impact to our buffer
                foreach (var impact in applied.CellImpacts)
                {
                    if (impact.X >= 0 && impact.X < _width && impact.Y >= 0 && impact.Y < _height)
                    {
                        _screenBuffer[impact.Y, impact.X] = impact.Cell;
                        if (impact.Y > maxY) maxY = impact.Y;
                    }
                    else if (impact.Y >= _height)
                    {
                        droppedCount++;
                    }
                }
                
                // Update cursor position from the last token
                _cursorX = Math.Clamp(applied.CursorXAfter, 0, _width - 1);
                _cursorY = Math.Clamp(applied.CursorYAfter, 0, _height - 1);
            }
        }
        
        // Only notify bound nodes when there are actual cell changes
        // Cursor-only updates don't require a full repaint - RenderCursor handles that
        if (maxY >= 0)
        {
            OutputReceived?.Invoke();
        }
        
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
        if (newWidth == _width && newHeight == _height) return;
        
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
}
