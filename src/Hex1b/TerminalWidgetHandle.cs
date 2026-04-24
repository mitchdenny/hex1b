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
public sealed class TerminalWidgetHandle : ICellImpactAwarePresentationAdapter, ITerminalLifecycleAwarePresentationAdapter, IAsyncDisposable
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
    
    // Alternate screen tracking - set when child sends mode 1049
    private bool _inAlternateScreen;
    
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
    
    // Copy mode state: when active, output is queued instead of applied
    private bool _inCopyMode;
    private TerminalSelection? _selection;
    private List<IReadOnlyList<AppliedToken>>? _outputQueue;
    
    // Scrollback offset tracked by the TerminalNode, synced here for mouse coordinate translation
    private int _scrollbackOffset;
    
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
    /// Gets whether the child process is currently using the alternate screen buffer (mode 1049).
    /// When true, scrollback viewing is disabled because alternate screen programs (vim, less, etc.)
    /// manage their own scrolling.
    /// </summary>
    public bool InAlternateScreen => _inAlternateScreen;
    
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
    /// Gets whether copy mode is currently active. When active, output from the child
    /// process is queued rather than applied to the screen buffer, and the selection
    /// cursor can be navigated independently.
    /// </summary>
    public bool IsInCopyMode => _inCopyMode;
    
    /// <summary>
    /// Gets the current text selection, or null if copy mode is not active.
    /// </summary>
    public TerminalSelection? Selection => _selection;
    
    /// <summary>
    /// Raised when copy mode is entered or exited.
    /// </summary>
    public event Action<bool>? CopyModeChanged;
    
    /// <summary>
    /// Gets or sets the current scrollback offset, synced from the TerminalNode.
    /// Used for translating mouse coordinates to virtual buffer positions.
    /// </summary>
    public int CurrentScrollbackOffset
    {
        get => _scrollbackOffset;
        set => _scrollbackOffset = value;
    }
    
    /// <summary>
    /// Raised when text is copied via copy mode. Subscribers should send the text
    /// to the system clipboard (e.g., via OSC 52).
    /// </summary>
    public event Action<string>? TextCopied;
    
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
    
    /// <inheritdoc />
    void ITerminalLifecycleAwarePresentationAdapter.TerminalCreated(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }
    
    /// <inheritdoc />
    void ITerminalLifecycleAwarePresentationAdapter.TerminalStarted()
    {
        if (_state == TerminalState.NotStarted)
        {
            _state = TerminalState.Running;
            StateChanged?.Invoke(_state);
            OutputReceived?.Invoke(); // Trigger re-render to switch from fallback to terminal
        }
    }
    
    /// <inheritdoc />
    void ITerminalLifecycleAwarePresentationAdapter.TerminalCompleted(int exitCode)
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
            
            // Alternate screen buffer
            case 1049:
                _inAlternateScreen = pm.Enable;
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
        // Prefer the terminal's authoritative buffer when available
        if (_terminal is { } terminal)
        {
            var (buffer, _, _, _, _) = terminal.GetScreenBufferSnapshot();
            return buffer;
        }
        
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
    /// <remarks>
    /// When a backing <see cref="Hex1bTerminal"/> is connected, this returns the terminal's
    /// authoritative buffer rather than the handle's local copy. This ensures the snapshot
    /// reflects the correct content after resize/reflow operations, where the terminal's
    /// buffer is reflowed but the handle's local buffer may still have the old layout.
    /// </remarks>
    /// <returns>A tuple containing the buffer copy, width, and height.</returns>
    public (TerminalCell[,] Buffer, int Width, int Height) GetScreenBufferSnapshot()
    {
        // Prefer the terminal's authoritative buffer when available.
        // The handle's local buffer can be stale after resize/reflow because:
        // 1. Handle.Resize() does a simple copy of old content
        // 2. Terminal.Resize() does a full reflow
        // 3. The handle's buffer doesn't get the reflowed content
        if (_terminal is { } terminal)
        {
            var (buffer, width, height, cursorX, cursorY) = terminal.GetScreenBufferSnapshot();
            // Sync cursor position from the terminal's authoritative state
            _cursorX = cursorX;
            _cursorY = cursorY;
            return (buffer, width, height);
        }
        
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
    
    /// <summary>
    /// Gets a snapshot of the scrollback buffer rows from the underlying terminal.
    /// Returns rows ordered oldest to newest.
    /// </summary>
    /// <param name="count">Maximum number of scrollback rows to return.</param>
    /// <returns>Array of scrollback rows, or empty if scrollback is not enabled.</returns>
    public ScrollbackRow[] GetScrollbackSnapshot(int count)
    {
        return _terminal?.GetScrollbackRows(count) ?? [];
    }
    
    /// <summary>
    /// Gets the number of rows currently in the scrollback buffer.
    /// Returns 0 if scrollback is not enabled.
    /// </summary>
    public int ScrollbackCount => _terminal?.ScrollbackCount ?? 0;
    
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
        
        lock (_bufferLock)
        {
            if (_inCopyMode && _outputQueue != null)
            {
                // Queue output for later application — buffer is frozen during copy mode.
                // Still process mode tokens (mouse tracking, alternate screen) immediately
                // so state is correct when copy mode exits.
                foreach (var applied in appliedTokens)
                {
                    if (applied.Token is PrivateModeToken pm)
                        HandlePrivateModeToken(pm);
                    else if (applied.Token is CursorShapeToken cst)
                        HandleCursorShapeToken(cst);
                    else if (applied.Token is OscToken osc)
                        HandleOscToken(osc);
                }
                _outputQueue.Add(appliedTokens);
                return ValueTask.CompletedTask;
            }
        }
        
        ApplyTokensToBuffer(appliedTokens);
        return ValueTask.CompletedTask;
    }
    
    private void ApplyTokensToBuffer(IReadOnlyList<AppliedToken> appliedTokens)
    {
        int maxY = -1;
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
                    HandleCursorShapeToken(cst);
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
                }
                
                // Update cursor position from the last token
                _cursorX = Math.Clamp(applied.CursorXAfter, 0, _width - 1);
                _cursorY = Math.Clamp(applied.CursorYAfter, 0, _height - 1);
            }
        }
        
        // Only notify bound nodes when there are actual cell changes
        if (maxY >= 0)
        {
            OutputReceived?.Invoke();
        }
    }
    
    private void HandleCursorShapeToken(CursorShapeToken cst)
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

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition() => (0, 0);
    
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
    
    // === Copy Mode ===
    
    /// <summary>
    /// Enters copy mode: freezes the screen buffer and begins queuing output.
    /// The copy mode cursor starts at the bottom-right of the visible screen.
    /// </summary>
    public void EnterCopyMode()
    {
        lock (_bufferLock)
        {
            if (_inCopyMode) return;
            
            _inCopyMode = true;
            _outputQueue = new List<IReadOnlyList<AppliedToken>>();
            
            // Position cursor at the terminal's current cursor position in virtual coordinates
            int scrollbackCount = ScrollbackCount;
            var initialPosition = new BufferPosition(scrollbackCount + _cursorY, _cursorX);
            _selection = new TerminalSelection(initialPosition);
        }
        
        CopyModeChanged?.Invoke(true);
        OutputReceived?.Invoke(); // trigger re-render to show copy mode cursor
    }
    
    /// <summary>
    /// Exits copy mode: flushes all queued output to the screen buffer and clears the selection.
    /// </summary>
    public void ExitCopyMode()
    {
        StopDragScrollTimer();
        List<IReadOnlyList<AppliedToken>>? pendingQueue;
        
        lock (_bufferLock)
        {
            if (!_inCopyMode) return;
            
            _inCopyMode = false;
            _selection = null;
            pendingQueue = _outputQueue;
            _outputQueue = null;
        }
        
        // Flush queued output outside the lock
        if (pendingQueue != null)
        {
            foreach (var tokens in pendingQueue)
            {
                // Re-enter the normal output path
                ApplyTokensToBuffer(tokens);
            }
        }
        
        CopyModeChanged?.Invoke(false);
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Copies the currently selected text and exits copy mode.
    /// Raises <see cref="TextCopied"/> with the selected text.
    /// </summary>
    /// <returns>The selected text, or null if no selection is active.</returns>
    public string? CopySelection()
    {
        string? text;
        
        lock (_bufferLock)
        {
            if (!_inCopyMode || _selection == null || !_selection.IsSelecting)
            {
                text = null;
            }
            else
            {
                text = _selection.ExtractText(GetVirtualCellUnlocked, _width);
            }
        }
        
        ExitCopyMode();
        
        if (text != null)
        {
            TextCopied?.Invoke(text);
        }
        
        return text;
    }
    
    /// <summary>
    /// Raised when a key is pressed while in copy mode. The consumer handles the key
    /// mapping (e.g., vi keys, arrow keys) and calls the appropriate navigation/selection
    /// methods on this handle. The bool parameter should be set to true if the key was handled.
    /// </summary>
    public event Func<Hex1bEvent, bool>? CopyModeInput;
    
    /// <summary>
    /// Invokes the <see cref="CopyModeInput"/> handler for the given input event.
    /// Returns true if the event was handled by a subscriber.
    /// </summary>
    internal bool RaiseCopyModeInput(Hex1bEvent inputEvent)
    {
        return CopyModeInput?.Invoke(inputEvent) ?? false;
    }
    
    /// <summary>
    /// Moves the copy mode cursor by the specified row and column deltas.
    /// Clamps to buffer bounds and scrolls the viewport to keep the cursor visible.
    /// </summary>
    public void MoveCopyModeCursor(int rowDelta, int colDelta)
    {
        if (_selection == null) return;
        var pos = _selection.Cursor;
        int maxRow = VirtualBufferHeight - 1;
        int maxCol = _width - 1;
        var newPos = new BufferPosition(
            Math.Clamp(pos.Row + rowDelta, 0, maxRow),
            Math.Clamp(pos.Column + colDelta, 0, maxCol));
        _selection.MoveCursor(newPos);
        EnsureCopyModeCursorVisible();
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Moves the copy mode cursor to an absolute position.
    /// Clamps to buffer bounds and scrolls the viewport to keep the cursor visible.
    /// </summary>
    public void SetCopyModeCursorPosition(int row, int column)
    {
        if (_selection == null) return;
        int maxRow = VirtualBufferHeight - 1;
        int maxCol = _width - 1;
        _selection.MoveCursor(new BufferPosition(
            Math.Clamp(row, 0, maxRow),
            Math.Clamp(column, 0, maxCol)));
        EnsureCopyModeCursorVisible();
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Starts or toggles selection in the specified mode.
    /// If already selecting in the same mode, clears the selection.
    /// </summary>
    public void StartOrToggleSelection(SelectionMode mode)
    {
        if (_selection == null) return;
        if (_selection.IsSelecting && _selection.Mode == mode)
            _selection.ClearSelection();
        else if (_selection.IsSelecting)
            _selection.ToggleMode(mode);
        else
            _selection.StartSelection(mode);
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Moves the copy mode cursor forward to the next word boundary.
    /// </summary>
    public void MoveWordForward()
    {
        if (_selection == null) return;
        var pos = _selection.Cursor;
        int maxRow = VirtualBufferHeight - 1;
        int row = pos.Row, col = pos.Column;
        
        // Skip current word (non-space characters)
        while (row <= maxRow)
        {
            var cell = GetVirtualCell(row, col);
            if (cell == null || string.IsNullOrWhiteSpace(cell.Value.Character)) break;
            col++;
            if (col >= _width) { col = 0; row++; }
        }
        // Skip whitespace
        while (row <= maxRow)
        {
            var cell = GetVirtualCell(row, col);
            if (cell == null) break;
            if (!string.IsNullOrWhiteSpace(cell.Value.Character)) break;
            col++;
            if (col >= _width) { col = 0; row++; }
        }
        
        _selection.MoveCursor(new BufferPosition(Math.Min(row, maxRow), col));
        EnsureCopyModeCursorVisible();
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Moves the copy mode cursor backward to the previous word boundary.
    /// </summary>
    public void MoveWordBackward()
    {
        if (_selection == null) return;
        var pos = _selection.Cursor;
        int row = pos.Row, col = pos.Column;
        
        col--;
        if (col < 0) { col = _width - 1; row--; }
        if (row < 0) { _selection.MoveCursor(new BufferPosition(0, 0)); OutputReceived?.Invoke(); return; }
        
        // Skip whitespace
        while (row >= 0)
        {
            var cell = GetVirtualCell(row, col);
            if (cell == null) break;
            if (!string.IsNullOrWhiteSpace(cell.Value.Character)) break;
            col--;
            if (col < 0) { col = _width - 1; row--; }
        }
        // Skip word
        while (row >= 0)
        {
            var cell = GetVirtualCell(row, col);
            if (cell == null || string.IsNullOrWhiteSpace(cell.Value.Character)) { col++; break; }
            col--;
            if (col < 0) { col = _width - 1; row--; }
        }
        
        _selection.MoveCursor(new BufferPosition(Math.Max(row, 0), Math.Clamp(col, 0, _width - 1)));
        EnsureCopyModeCursorVisible();
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Adjusts the scrollback offset so the copy mode cursor is within the visible viewport.
    /// </summary>
    private void EnsureCopyModeCursorVisible()
    {
        if (_selection == null) return;
        int cursorRow = _selection.Cursor.Row;
        int scrollbackCount = ScrollbackCount;
        
        // Visible virtual row range: [viewStart, viewStart + _height - 1]
        int viewStart = scrollbackCount - _scrollbackOffset;
        int viewEnd = viewStart + _height - 1;
        
        if (cursorRow < viewStart)
        {
            _scrollbackOffset = scrollbackCount - cursorRow;
        }
        else if (cursorRow > viewEnd)
        {
            _scrollbackOffset = scrollbackCount - (cursorRow - _height + 1);
        }
        _scrollbackOffset = Math.Max(0, _scrollbackOffset);
    }
    
    // Pending mouse selection anchor — set on Down, used on first Drag
    private BufferPosition? _pendingMouseAnchor;
    
    // Auto-scroll timer for drag-outside-bounds
    private Timer? _dragScrollTimer;
    private int _dragScrollDirection; // -1 = up, +1 = down, 0 = none
    private int _dragLastColumn;
    private SelectionMode _dragLastMode;
    
    /// <summary>
    /// Handles mouse-driven selection. Translates local terminal coordinates to virtual
    /// buffer positions and manages the selection state machine (down/drag/up).
    /// Only enters copy mode when the user actually drags (not on a single click).
    /// When dragging outside the viewport, auto-scrolls every 500ms.
    /// </summary>
    /// <param name="localX">X coordinate relative to the terminal widget bounds.</param>
    /// <param name="localY">Y coordinate relative to the terminal widget bounds.</param>
    /// <param name="action">The mouse action (Down records anchor, Drag starts selection, Up finalizes).</param>
    /// <param name="mode">The selection mode to use.</param>
    public void MouseSelect(int localX, int localY, Input.MouseAction action, SelectionMode mode)
    {
        int scrollbackCount = ScrollbackCount;
        int virtualRow = scrollbackCount - _scrollbackOffset + localY;
        int column = Math.Clamp(localX, 0, _width - 1);
        virtualRow = Math.Clamp(virtualRow, 0, VirtualBufferHeight - 1);
        
        switch (action)
        {
            case Input.MouseAction.Down:
                // Record anchor position — don't enter copy mode yet (wait for drag)
                _pendingMouseAnchor = new BufferPosition(virtualRow, column);
                StopDragScrollTimer();
                break;
                
            case Input.MouseAction.Drag:
                if (_pendingMouseAnchor is { } anchor)
                {
                    // First drag after mouse down — now enter copy mode and start selection
                    if (!_inCopyMode)
                    {
                        EnterCopyMode();
                    }
                    _selection?.MoveCursor(anchor);
                    _selection?.StartSelection(mode);
                    _pendingMouseAnchor = null;
                }
                
                if (_selection != null && _inCopyMode)
                {
                    // If selection mode changed (modifier changed mid-drag), update it
                    if (_selection.IsSelecting && _selection.Mode != mode)
                    {
                        _selection.ToggleMode(mode);
                    }
                    _selection.MoveCursor(new BufferPosition(virtualRow, column));
                    EnsureCopyModeCursorVisible();
                    OutputReceived?.Invoke();
                    
                    // Start/stop auto-scroll timer based on whether mouse is out of bounds
                    if (localY < 0)
                    {
                        StartDragScrollTimer(-1, column, mode);
                    }
                    else if (localY >= _height)
                    {
                        StartDragScrollTimer(1, column, mode);
                    }
                    else
                    {
                        StopDragScrollTimer();
                    }
                }
                break;
                
            case Input.MouseAction.Up:
                _pendingMouseAnchor = null;
                StopDragScrollTimer();
                // Selection stays active — user can refine with keyboard or press y to copy
                OutputReceived?.Invoke();
                break;
        }
    }
    
    private void StartDragScrollTimer(int direction, int column, SelectionMode mode)
    {
        _dragScrollDirection = direction;
        _dragLastColumn = column;
        _dragLastMode = mode;
        
        if (_dragScrollTimer == null)
        {
            _dragScrollTimer = new Timer(DragScrollTick, null, 500, 500);
        }
    }
    
    private void StopDragScrollTimer()
    {
        _dragScrollTimer?.Dispose();
        _dragScrollTimer = null;
        _dragScrollDirection = 0;
    }
    
    private void DragScrollTick(object? state)
    {
        if (!_inCopyMode || _selection == null || _dragScrollDirection == 0) 
        {
            StopDragScrollTimer();
            return;
        }
        
        // Move cursor one row in the scroll direction
        var pos = _selection.Cursor;
        int newRow = Math.Clamp(pos.Row + _dragScrollDirection, 0, VirtualBufferHeight - 1);
        _selection.MoveCursor(new BufferPosition(newRow, _dragLastColumn));
        EnsureCopyModeCursorVisible();
        OutputReceived?.Invoke();
    }
    
    /// <summary>
    /// Gets the copy mode cursor position in virtual buffer coordinates, or null if not in copy mode.
    /// </summary>
    public BufferPosition? CopyModeCursorPosition
    {
        get
        {
            lock (_bufferLock)
            {
                return _selection?.Cursor;
            }
        }
    }
    
    /// <summary>
    /// Gets a cell from the virtual buffer (scrollback + screen unified view).
    /// Virtual row 0 is the oldest scrollback row; scrollbackCount+screenHeight-1 is the last screen row.
    /// </summary>
    /// <param name="virtualRow">The virtual row index.</param>
    /// <param name="column">The column index.</param>
    /// <returns>The cell at that position, or null if out of bounds.</returns>
    public TerminalCell? GetVirtualCell(int virtualRow, int column)
    {
        lock (_bufferLock)
        {
            return GetVirtualCellUnlocked(virtualRow, column);
        }
    }
    
    private TerminalCell? GetVirtualCellUnlocked(int virtualRow, int column)
    {
        int scrollbackCount = ScrollbackCount;
        
        if (virtualRow < 0) return null;
        if (column < 0 || column >= _width) return null;
        
        if (virtualRow < scrollbackCount)
        {
            // Scrollback region
            var rows = _terminal?.GetScrollbackRows(scrollbackCount);
            if (rows == null || virtualRow >= rows.Length) return null;
            var cells = rows[virtualRow].Cells;
            if (column >= cells.Length) return TerminalCell.Empty;
            return cells[column];
        }
        else
        {
            // Screen region
            int screenRow = virtualRow - scrollbackCount;
            if (screenRow >= _height) return null;
            return _screenBuffer[screenRow, column];
        }
    }
    
    /// <summary>
    /// Gets the total number of rows in the virtual buffer (scrollback + screen).
    /// </summary>
    public int VirtualBufferHeight => ScrollbackCount + _height;
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        
        StopDragScrollTimer();
        Disconnected?.Invoke();
        _disconnected.TrySetResult();
        
        return ValueTask.CompletedTask;
    }
}
