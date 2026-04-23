using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for displaying an embedded terminal session.
/// </summary>
/// <remarks>
/// <para>
/// The TerminalNode renders the screen buffer from a <see cref="TerminalWidgetHandle"/>.
/// It subscribes to the handle's OutputReceived event to trigger re-renders when
/// new output arrives.
/// </para>
/// <para>
/// The node is focusable and forwards all keyboard input to the child terminal.
/// Mouse clicks within the terminal's bounds will focus the terminal.
/// </para>
/// <para>
/// When the terminal has a scrollback buffer configured, the user can scroll up
/// through previous output using keyboard shortcuts or mouse wheel. Scrollback
/// viewing is disabled when the child process is using the alternate screen buffer
/// (mode 1049), and mouse scroll events are forwarded to the child when it has
/// enabled mouse tracking.
/// </para>
/// </remarks>
public sealed class TerminalNode : Hex1bNode
{
    private TerminalWidgetHandle? _handle;
    private bool _isBound;
    private Action? _outputReceivedHandler;
    private Action? _invalidateCallback;
    private Action<Hex1bNode>? _captureInputCallback;
    private Action? _releaseCaptureCallback;
    private bool _isFocused;
    private bool _handleChanged; // Tracks if handle was changed since last Arrange
    
    // Tracks output version to detect if output arrived during render
    // This prevents the race condition where output arrives after snapshot
    // but before ClearDirtyFlags, causing the new content to be lost
    private volatile int _outputVersion;
    private int _lastRenderedVersion;
    
    // Scrollback view state
    // When _scrollbackOffset > 0, the view is scrolled up into the scrollback buffer
    // and keyboard input is intercepted for scrollback navigation instead of forwarded to the child
    private int _scrollbackOffset;
    
    /// <summary>
    /// Number of rows to scroll per mouse wheel tick.
    /// </summary>
    internal int MouseWheelScrollAmount { get; set; } = 3;
    
    /// <summary>
    /// Gets or sets the terminal handle this node renders from.
    /// </summary>
    public TerminalWidgetHandle? Handle
    {
        get => _handle;
        set
        {
            if (_handle != value)
            {
                _handle = value;
                _handleChanged = true; // Mark that we need to resize the new handle
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Gets or sets the source widget for this node.
    /// </summary>
    public TerminalWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// Gets or sets the callback that builds a fallback widget when the terminal is not running.
    /// </summary>
    internal Func<TerminalNotRunningArgs, Hex1bWidget>? NotRunningBuilder { get; set; }
    
    /// <summary>
    /// Gets or sets the reconciled fallback child node when the terminal is not running.
    /// </summary>
    internal Hex1bNode? FallbackChild { get; set; }
    
    /// <inheritdoc />
    /// <remarks>
    /// When the terminal is not running and a fallback child is displayed,
    /// this returns false so that focus navigation can reach the fallback's children.
    /// </remarks>
    public override bool IsFocusable => _handle == null || _handle.State == TerminalState.Running || FallbackChild == null;
    
    /// <inheritdoc />
    /// <remarks>
    /// Returns true when showing the fallback widget tree, so this node manages focus for its children.
    /// </remarks>
    public override bool ManagesChildFocus => FallbackChild != null && _handle?.State != TerminalState.Running;
    
    /// <inheritdoc />
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
                
                // Manage input capture based on focus and terminal state
                UpdateInputCapture();
            }
        }
    }
    
    /// <summary>
    /// Updates input capture based on current focus and terminal state.
    /// Captures input when focused and terminal is running, releases when not.
    /// </summary>
    private void UpdateInputCapture()
    {
        var shouldCapture = _isFocused && _handle != null && _handle.State == TerminalState.Running;
        
        if (shouldCapture)
        {
            _captureInputCallback?.Invoke(this);
        }
        else
        {
            _releaseCaptureCallback?.Invoke();
        }
    }
    
    /// <summary>
    /// Sets the callback to invoke when the terminal needs to be re-rendered.
    /// Typically set to <c>app.Invalidate</c> by the framework.
    /// </summary>
    internal void SetInvalidateCallback(Action callback)
    {
        _invalidateCallback = callback;
    }
    
    /// <summary>
    /// Sets the callbacks to invoke for input capture management.
    /// </summary>
    internal void SetCaptureCallbacks(Action<Hex1bNode>? captureInput, Action? releaseCapture)
    {
        _captureInputCallback = captureInput;
        _releaseCaptureCallback = releaseCapture;
    }
    
    /// <summary>
    /// Gets whether the terminal is currently in scrollback mode (scrolled up from live view).
    /// </summary>
    public bool IsInScrollbackMode => _scrollbackOffset > 0;
    
    /// <summary>
    /// Gets the current scrollback offset (number of lines scrolled up from live view).
    /// </summary>
    public int ScrollbackOffset => _scrollbackOffset;
    
    /// <inheritdoc />
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Keyboard scrollback navigation
        // Note: Ctrl+Shift combinations are not supported by the input binding system,
        // so we use Shift+Arrow/PageUp/PageDown/Home/End which matches GNOME Terminal conventions
        bindings.Shift().Key(Hex1bKey.UpArrow).Triggers(TerminalWidget.ScrollUpLine, ScrollUpLineHandler, "Scroll up one line");
        bindings.Shift().Key(Hex1bKey.DownArrow).Triggers(TerminalWidget.ScrollDownLine, ScrollDownLineHandler, "Scroll down one line");
        bindings.Shift().Key(Hex1bKey.PageUp).Triggers(TerminalWidget.ScrollUpPage, ScrollUpPageHandler, "Scroll up one page");
        bindings.Shift().Key(Hex1bKey.PageDown).Triggers(TerminalWidget.ScrollDownPage, ScrollDownPageHandler, "Scroll down one page");
        bindings.Shift().Key(Hex1bKey.Home).Triggers(TerminalWidget.ScrollToTop, ScrollToTopHandler, "Scroll to top of scrollback");
        bindings.Shift().Key(Hex1bKey.End).Triggers(TerminalWidget.ScrollToBottom, ScrollToBottomHandler, "Scroll to bottom (live view)");
        
        // Mouse wheel scrollback (only when child has NOT enabled mouse tracking)
        bindings.Mouse(MouseButton.ScrollUp).Triggers(TerminalWidget.ScrollUpLine, MouseScrollUpHandler, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Triggers(TerminalWidget.ScrollDownLine, MouseScrollDownHandler, "Scroll down");
    }
    
    private Task ScrollUpLineHandler(InputBindingActionContext ctx)
    {
        AdjustScrollbackOffset(1);
        return Task.CompletedTask;
    }
    
    private Task ScrollDownLineHandler(InputBindingActionContext ctx)
    {
        AdjustScrollbackOffset(-1);
        return Task.CompletedTask;
    }
    
    private Task MouseScrollUpHandler(InputBindingActionContext ctx)
    {
        // When child has enabled mouse tracking, forward scroll to the terminal
        // instead of controlling the scrollback view
        if (_handle?.MouseTrackingEnabled == true)
        {
            // Convert absolute screen coordinates to terminal-local coordinates
            var localX = ctx.MouseX - Bounds.X;
            var localY = ctx.MouseY - Bounds.Y;
            var scrollEvent = new Hex1bMouseEvent(MouseButton.ScrollUp, MouseAction.Down, localX, localY, Hex1bModifiers.None);
            _ = _handle.SendEventAsync(scrollEvent);
            return Task.CompletedTask;
        }
        AdjustScrollbackOffset(MouseWheelScrollAmount);
        return Task.CompletedTask;
    }
    
    private Task MouseScrollDownHandler(InputBindingActionContext ctx)
    {
        // When child has enabled mouse tracking, forward scroll to the terminal
        if (_handle?.MouseTrackingEnabled == true)
        {
            var localX = ctx.MouseX - Bounds.X;
            var localY = ctx.MouseY - Bounds.Y;
            var scrollEvent = new Hex1bMouseEvent(MouseButton.ScrollDown, MouseAction.Down, localX, localY, Hex1bModifiers.None);
            _ = _handle.SendEventAsync(scrollEvent);
            return Task.CompletedTask;
        }
        AdjustScrollbackOffset(-MouseWheelScrollAmount);
        return Task.CompletedTask;
    }
    
    private Task ScrollUpPageHandler(InputBindingActionContext ctx)
    {
        var pageSize = Math.Max(1, Bounds.Height - 1);
        AdjustScrollbackOffset(pageSize);
        return Task.CompletedTask;
    }
    
    private Task ScrollDownPageHandler(InputBindingActionContext ctx)
    {
        var pageSize = Math.Max(1, Bounds.Height - 1);
        AdjustScrollbackOffset(-pageSize);
        return Task.CompletedTask;
    }
    
    private Task ScrollToTopHandler(InputBindingActionContext ctx)
    {
        if (_handle == null) return Task.CompletedTask;
        var maxOffset = _handle.ScrollbackCount;
        if (maxOffset > 0)
        {
            _scrollbackOffset = maxOffset;
            MarkDirty();
            _invalidateCallback?.Invoke();
        }
        return Task.CompletedTask;
    }
    
    private Task ScrollToBottomHandler(InputBindingActionContext ctx)
    {
        if (_scrollbackOffset > 0)
        {
            _scrollbackOffset = 0;
            MarkDirty();
            _invalidateCallback?.Invoke();
        }
        return Task.CompletedTask;
    }
    
    private void AdjustScrollbackOffset(int delta)
    {
        if (_handle == null) return;
        
        // Don't allow scrollback when in alternate screen
        if (_handle.InAlternateScreen) return;
        
        var maxOffset = _handle.ScrollbackCount;
        var newOffset = Math.Clamp(_scrollbackOffset + delta, 0, maxOffset);
        if (newOffset != _scrollbackOffset)
        {
            _scrollbackOffset = newOffset;
            MarkDirty();
            _invalidateCallback?.Invoke();
        }
    }
    
    /// <inheritdoc />
    public override InputResult HandleInput(Hex1bEvent inputEvent)
    {
        // If terminal is not running and we have a fallback, don't forward to terminal
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            // Let the input router handle it - it will route to focused nodes in the fallback tree
            return InputResult.NotHandled;
        }
        
        // Forward input to the terminal
        if (_handle == null) return InputResult.NotHandled;
        
        // When in copy mode, intercept all keyboard input.
        // Delegate to the handle's CopyModeInput event for key mapping.
        if (_handle.IsInCopyMode && inputEvent is Hex1bKeyEvent)
        {
            _handle.RaiseCopyModeInput(inputEvent);
            MarkDirty();
            _invalidateCallback?.Invoke();
            return InputResult.Handled;
        }
        
        // When NOT in copy mode, give the CopyModeInput handler a chance to intercept
        // (e.g., to enter copy mode via a custom key like F6). If it handles the key,
        // don't forward to the child terminal.
        if (inputEvent is Hex1bKeyEvent && _handle.RaiseCopyModeInput(inputEvent))
        {
            MarkDirty();
            _invalidateCallback?.Invoke();
            return InputResult.Handled;
        }
        
        // When in scrollback mode and a non-scrollback key is pressed (no binding matched),
        // snap back to live view and forward the keystroke to the terminal.
        if (IsInScrollbackMode && inputEvent is Hex1bKeyEvent)
        {
            _scrollbackOffset = 0;
            MarkDirty();
            _invalidateCallback?.Invoke();
        }
        
        // Fire and forget - we don't want to block the input loop
        _ = _handle.SendEventAsync(inputEvent);
        return InputResult.Handled;
    }
    
    /// <inheritdoc />
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // If terminal is not running and we have a fallback, let mouse events go to fallback
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            // Let the input router handle it - it will route to nodes in the fallback tree
            return InputResult.NotHandled;
        }
        
        // When the child has enabled mouse tracking, forward all mouse events to it
        // (including scroll wheel). The child app manages its own scrolling (e.g., vim).
        if (_handle != null && _handle.MouseTrackingEnabled)
        {
            var localEvent = mouseEvent with { X = localX, Y = localY };
            return HandleInput(localEvent);
        }
        
        // When the child has NOT enabled mouse tracking, scroll wheel events
        // control the scrollback view instead of being forwarded.
        // The scroll bindings are handled by ConfigureDefaultBindings.
        // Return NotHandled so the input binding system can match them.
        if (mouseEvent.Button is MouseButton.ScrollUp or MouseButton.ScrollDown)
        {
            return InputResult.NotHandled;
        }
        
        // Non-scroll mouse events: forward to the child terminal
        var evt = mouseEvent with { X = localX, Y = localY };
        return HandleInput(evt);
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// When the terminal is not running and a fallback child is displayed,
    /// this returns the fallback child so that focus navigation and input routing work.
    /// </remarks>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            yield return FallbackChild;
        }
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// When the terminal is not running, returns the fallback child's focusable nodes.
    /// When running, returns this node if focusable.
    /// </remarks>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            foreach (var focusable in FallbackChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
        else if (IsFocusable)
        {
            yield return this;
        }
    }
    
    /// <summary>
    /// Binds this node to the current handle, subscribing to output events.
    /// </summary>
    /// <remarks>
    /// After subscribing to the handle's OutputReceived event, we immediately
    /// mark the node dirty and trigger an invalidation. This ensures that any
    /// output that arrived before binding (e.g., bash prompt) will be rendered
    /// on the next frame. Without this, the terminal would appear empty until
    /// user interaction triggers a re-render.
    /// </remarks>
    internal void Bind()
    {
        if (_isBound || _handle == null) return;
        
        _outputReceivedHandler = OnOutputReceived;
        _handle.OutputReceived += _outputReceivedHandler;
        _isBound = true;
        
        // Mark dirty and invalidate to render any output that arrived before binding.
        // This handles the race condition where the terminal starts outputting
        // (e.g., bash prompt) before the UI has reconciled and bound the node.
        MarkDirty();
        _invalidateCallback?.Invoke();
    }
    
    /// <summary>
    /// Unbinds this node from the current handle.
    /// </summary>
    internal void Unbind()
    {
        if (!_isBound || _handle == null) return;
        
        if (_outputReceivedHandler != null)
        {
            _handle.OutputReceived -= _outputReceivedHandler;
            _outputReceivedHandler = null;
        }
        _isBound = false;
    }
    
    private void OnOutputReceived()
    {
        // Increment output version to track that new content arrived
        // This is checked in NeedsRender to ensure we don't miss content
        // that arrived during a render frame
        System.Threading.Interlocked.Increment(ref _outputVersion);
        
        MarkDirty();
        _invalidateCallback?.Invoke();
    }
    
    /// <summary>
    /// Gets whether there is pending output that hasn't been rendered yet.
    /// </summary>
    /// <remarks>
    /// This handles the race condition where output arrives during a render frame
    /// after the buffer snapshot is taken but before the dirty flag is cleared.
    /// </remarks>
    public bool HasPendingOutput => _outputVersion != _lastRenderedVersion;
    
    /// <summary>
    /// Overrides dirty flag clearing to preserve the flag if pending output exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Terminal output arrives asynchronously from the child process. If output
    /// arrives during a render frame (after snapshot taken but before this method
    /// is called), we must NOT clear the dirty flag or that content will be lost.
    /// </para>
    /// <para>
    /// The version tracking ensures we detect this scenario: <see cref="_outputVersion"/>
    /// is incremented on each <see cref="OnOutputReceived"/>, and <see cref="_lastRenderedVersion"/>
    /// is set in <see cref="Render"/> after taking the snapshot. If they don't match,
    /// more output arrived and we need another render.
    /// </para>
    /// </remarks>
    internal override void ClearDirty()
    {
        // Only clear the dirty flag if we've rendered all pending output
        // If output arrived during the render frame, keep the flag set
        if (!HasPendingOutput)
        {
            base.ClearDirty();
        }
    }
    
    /// <inheritdoc />
    protected override Size MeasureCore(Constraints constraints)
    {
        // If terminal is not running and we have a fallback child, measure the fallback
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            return FallbackChild.Measure(constraints);
        }
        
        // The terminal should ideally fill the available space, but we need to handle
        // unbounded constraints safely. Use the handle's current dimensions as a hint.
        var preferredWidth = _handle?.Width ?? 80;
        var preferredHeight = _handle?.Height ?? 24;
        
        // Use handle dimensions as preferred size, but respect bounded constraints.
        // Consider values over 10000 as "unbounded" since no terminal is that big.
        const int UnboundedThreshold = 10000;
        var width = constraints.MaxWidth < UnboundedThreshold ? constraints.MaxWidth : preferredWidth;
        var height = constraints.MaxHeight < UnboundedThreshold ? constraints.MaxHeight : preferredHeight;
        
        return constraints.Constrain(new Size(width, height));
    }
    
    /// <inheritdoc />
    protected override void ArrangeCore(Rect bounds)
    {
        var previousBounds = Bounds;
        base.ArrangeCore(bounds);
        
        // If terminal is not running and we have a fallback child, arrange the fallback
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            FallbackChild.Arrange(bounds);
            // Don't return — fall through to resize the handle below.
            // This ensures the handle (and underlying PTY) gets resized to match the
            // layout bounds BEFORE the terminal starts running. Without this, the PTY
            // starts at the initial WithDimensions() size (e.g., 80x24) and the shell's
            // first output is laid out at the wrong width.
        }
        
        // Safety: clamp unreasonable bounds to handle dimensions
        // This prevents OOM when parent passes huge values
        const int MaxReasonableSize = 10000;
        var safeWidth = bounds.Width > MaxReasonableSize ? (_handle?.Width ?? 80) : bounds.Width;
        var safeHeight = bounds.Height > MaxReasonableSize ? (_handle?.Height ?? 24) : bounds.Height;
        
        // Resize the handle if:
        // 1. The bounds changed (normal resize scenario), OR
        // 2. The handle was just changed (new handle may have different dimensions than node bounds)
        // This ensures that when switching between terminals, the new handle gets resized
        // to match the node's current layout bounds, even if those bounds didn't change.
        var needsResize = safeWidth != previousBounds.Width || safeHeight != previousBounds.Height || _handleChanged;
        
        if (_handle != null && needsResize)
        {
            _handle.Resize(safeWidth, safeHeight);
            _handleChanged = false;
        }
    }
    
    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (_handle == null) return;
        
        // If terminal is not running and we have a fallback child, render the fallback instead
        if (_handle.State != TerminalState.Running && FallbackChild != null)
        {
            // Use RenderChild for automatic caching support
            context.RenderChild(FallbackChild);
            return;
        }
        
        // Capture the current output version BEFORE taking the snapshot
        // This ensures we detect any output that arrives during render
        var currentVersion = _outputVersion;
        
        // Get the current screen buffer with dimensions atomically
        // This prevents race conditions where dimensions change between getting buffer and reading Width/Height
        var (buffer, handleWidth, handleHeight) = _handle.GetScreenBufferSnapshot();
        
        // Mark this version as rendered (we'll check if more output arrived after this)
        _lastRenderedVersion = currentVersion;
        
        // Clear the entire terminal region before rendering rows.
        // This is necessary because empty terminal cells (TerminalCell.Empty) have null
        // foreground/background, which produces transparent SurfaceCells. Without clearing,
        // empty rows below the cursor won't overwrite content from a previous frame
        // (e.g., after scrolling back from scrollback mode to live mode).
        context.ClearRegion(Bounds);
        
        if (_scrollbackOffset > 0)
        {
            RenderWithScrollback(context, buffer, handleWidth, handleHeight);
        }
        else
        {
            RenderLive(context, buffer, handleWidth, handleHeight);
        }
    }
    
    /// <summary>
    /// Renders the live terminal buffer (no scrollback offset).
    /// </summary>
    private void RenderLive(Hex1bRenderContext context, TerminalCell[,] buffer, int handleWidth, int handleHeight)
    {
        int scrollbackCount = _handle!.ScrollbackCount;
        var selection = _handle.IsInCopyMode ? _handle.Selection : null;
        
        for (int y = 0; y < Math.Min(Bounds.Height, handleHeight); y++)
        {
            int virtualRow = scrollbackCount + y;
            RenderRow(context, y, (x) => buffer[y, x], handleWidth, virtualRow, selection);
        }
        
        // Render copy mode cursor
        if (selection != null)
        {
            RenderCopyModeCursor(context, scrollbackCount, scrollbackCount);
        }
    }
    
    /// <summary>
    /// Renders a composite view of scrollback rows above the active buffer,
    /// shifted up by _scrollbackOffset lines.
    /// </summary>
    private void RenderWithScrollback(Hex1bRenderContext context, TerminalCell[,] activeBuffer, int handleWidth, int handleHeight)
    {
        // Get scrollback rows
        var scrollbackRows = _handle!.GetScrollbackSnapshot(_handle.ScrollbackCount);
        int scrollbackCount = scrollbackRows.Length;
        var selection = _handle.IsInCopyMode ? _handle.Selection : null;
        
        // Clamp offset to available scrollback
        var effectiveOffset = Math.Min(_scrollbackOffset, scrollbackCount);
        
        // The virtual buffer is: [scrollback rows (oldest to newest)] + [active buffer rows]
        // Total virtual rows = scrollbackCount + handleHeight
        // The viewport shows handleHeight rows starting from:
        //   virtualStart = scrollbackCount + handleHeight - handleHeight - effectiveOffset
        //                = scrollbackCount - effectiveOffset
        int virtualStart = scrollbackCount - effectiveOffset;
        
        for (int viewY = 0; viewY < Math.Min(Bounds.Height, handleHeight); viewY++)
        {
            int virtualRow = virtualStart + viewY;
            
            if (virtualRow < scrollbackCount)
            {
                // This row comes from scrollback
                var sbRow = scrollbackRows[virtualRow];
                RenderRow(context, viewY, (x) =>
                {
                    if (x < sbRow.Cells.Length) return sbRow.Cells[x];
                    return TerminalCell.Empty;
                }, handleWidth, virtualRow, selection);
            }
            else
            {
                // This row comes from the active buffer
                int activeRow = virtualRow - scrollbackCount;
                if (activeRow < handleHeight)
                {
                    RenderRow(context, viewY, (x) => activeBuffer[activeRow, x], handleWidth, virtualRow, selection);
                }
            }
        }
        
        // Render copy mode cursor
        if (selection != null)
        {
            RenderCopyModeCursor(context, virtualStart, scrollbackCount);
        }
    }
    
    private void RenderCopyModeCursor(Hex1bRenderContext context, int virtualStart, int scrollbackCount)
    {
        if (_handle?.Selection == null) return;
        var cursorPos = _handle.Selection.Cursor;
        int viewY = cursorPos.Row - virtualStart;
        if (viewY >= 0 && viewY < Bounds.Height && cursorPos.Column < Bounds.Width)
        {
            // Render a block cursor at the copy mode cursor position with inverted colors
            var cell = _handle.GetVirtualCell(cursorPos.Row, cursorPos.Column);
            var ch = cell?.Character ?? " ";
            if (string.IsNullOrEmpty(ch)) ch = " ";
            context.WriteClipped(Bounds.X + cursorPos.Column, Bounds.Y + viewY, $"\x1b[7m{ch}\x1b[0m");
        }
    }
    
    /// <summary>
    /// Renders a single row of terminal cells at the specified view Y position.
    /// </summary>
    private void RenderRow(Hex1bRenderContext context, int viewY, Func<int, TerminalCell> getCell, int handleWidth,
        int virtualRow = -1, TerminalSelection? selection = null)
    {
        var lineBuilder = new System.Text.StringBuilder();
        Hex1b.Theming.Hex1bColor? lastFg = null;
        Hex1b.Theming.Hex1bColor? lastBg = null;
        Hex1b.Theming.Hex1bColor? lastUc = null;
        CellAttributes lastAttrs = CellAttributes.None;
        bool lastSelected = false;
        
        for (int x = 0; x < Math.Min(Bounds.Width, handleWidth); x++)
        {
            var cell = getCell(x);
            bool isSelected = selection != null && virtualRow >= 0 && selection.IsCellSelected(virtualRow, x);
            
            // Build ANSI codes for color changes
            bool needsReset = false;
            
            // Check if selection state changed
            if (isSelected != lastSelected)
            {
                needsReset = true;
            }
            
            // Check if attributes changed
            if (cell.Attributes != lastAttrs)
            {
                needsReset = true;
            }
            
            // Check if colors changed (nullable struct comparison)
            if (!Nullable.Equals(cell.Foreground, lastFg) || !Nullable.Equals(cell.Background, lastBg) || !Nullable.Equals(cell.UnderlineColor, lastUc))
            {
                needsReset = true;
            }
            
            if (needsReset)
            {
                // Reset and apply new attributes
                lineBuilder.Append("\x1b[0m");
                
                if (cell.Attributes.HasFlag(CellAttributes.Bold))
                    lineBuilder.Append("\x1b[1m");
                if (cell.Attributes.HasFlag(CellAttributes.Dim))
                    lineBuilder.Append("\x1b[2m");
                if (cell.Attributes.HasFlag(CellAttributes.Italic))
                    lineBuilder.Append("\x1b[3m");
                if (cell.Attributes.HasFlag(CellAttributes.Underline))
                {
                    if (cell.UnderlineStyle != UnderlineStyle.None && cell.UnderlineStyle != UnderlineStyle.Single)
                        lineBuilder.Append($"\x1b[4:{(int)cell.UnderlineStyle}m");
                    else
                        lineBuilder.Append("\x1b[4m");
                }
                if (cell.Attributes.HasFlag(CellAttributes.Reverse) || isSelected)
                    lineBuilder.Append("\x1b[7m");
                if (cell.Attributes.HasFlag(CellAttributes.Strikethrough))
                    lineBuilder.Append("\x1b[9m");
                
                if (cell.Foreground != null)
                    lineBuilder.Append(cell.Foreground.Value.ToForegroundAnsi());
                
                if (cell.Background != null)
                    lineBuilder.Append(cell.Background.Value.ToBackgroundAnsi());
                
                if (cell.UnderlineColor != null)
                    lineBuilder.Append(cell.UnderlineColor.Value.ToUnderlineColorAnsi());
                
                lastFg = cell.Foreground;
                lastBg = cell.Background;
                lastUc = cell.UnderlineColor;
                lastAttrs = cell.Attributes;
                lastSelected = isSelected;
            }
            
            lineBuilder.Append(cell.Character);
        }
        
        // Reset at end of line
        lineBuilder.Append("\x1b[0m");
        
        // Write the line at the correct position
        context.WriteClipped(Bounds.X, Bounds.Y + viewY, lineBuilder.ToString());
    }
}
