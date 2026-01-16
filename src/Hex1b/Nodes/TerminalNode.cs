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
/// </remarks>
public sealed class TerminalNode : Hex1bNode
{
    private TerminalWidgetHandle? _handle;
    private bool _isBound;
    private Action? _outputReceivedHandler;
    private Action? _invalidateCallback;
    private bool _isFocused;
    
    // Tracks output version to detect if output arrived during render
    // This prevents the race condition where output arrives after snapshot
    // but before ClearDirtyFlags, causing the new content to be lost
    private volatile int _outputVersion;
    private int _lastRenderedVersion;
    
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
    /// The terminal captures all input when focused and running, bypassing
    /// normal input binding processing so that all keys (including Ctrl+C)
    /// are forwarded to the child process.
    /// </remarks>
    public override bool CapturesAllInput => _handle != null && _handle.State == TerminalState.Running && IsFocused;
    
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
            }
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
    
    /// <inheritdoc />
    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        // If terminal is not running and we have a fallback, don't forward to terminal
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            // Let the input router handle it - it will route to focused nodes in the fallback tree
            return InputResult.NotHandled;
        }
        
        // Forward input to the terminal
        if (_handle == null) return InputResult.NotHandled;
        
        // Fire and forget - we don't want to block the input loop
        _ = _handle.SendEventAsync(keyEvent);
        return InputResult.Handled;
    }
    
    /// <summary>
    /// Handles mouse events by forwarding them to the child terminal with translated coordinates.
    /// </summary>
    /// <param name="localX">X coordinate relative to this node's bounds.</param>
    /// <param name="localY">Y coordinate relative to this node's bounds.</param>
    /// <param name="mouseEvent">The mouse event.</param>
    /// <returns>Handled if the event was forwarded, NotHandled otherwise.</returns>
    public InputResult HandleMouseEvent(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        if (_handle == null) return InputResult.NotHandled;
        
        // Create a translated event with local coordinates for the child terminal
        var translatedEvent = mouseEvent with { X = localX, Y = localY };
        
        // Fire and forget - we don't want to block the input loop
        _ = _handle.SendEventAsync(translatedEvent);
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
        
        // Forward click events to the child terminal (same as other mouse events)
        return HandleMouseEvent(localX, localY, mouseEvent);
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
    internal void Bind()
    {
        if (_isBound || _handle == null) return;
        
        _outputReceivedHandler = OnOutputReceived;
        _handle.OutputReceived += _outputReceivedHandler;
        _isBound = true;
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
        
        System.IO.File.AppendAllText("/tmp/hex1b-render.log", $"[{DateTime.Now:HH:mm:ss.fff}] TerminalNode.OnOutputReceived: hasCallback={_invalidateCallback != null} version={_outputVersion}\n");
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
    public override Size Measure(Constraints constraints)
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
    public override void Arrange(Rect bounds)
    {
        var previousBounds = Bounds;
        base.Arrange(bounds);
        
        // If terminal is not running and we have a fallback child, arrange the fallback
        if (_handle != null && _handle.State != TerminalState.Running && FallbackChild != null)
        {
            FallbackChild.Arrange(bounds);
            return;
        }
        
        // Safety: clamp unreasonable bounds to handle dimensions
        // This prevents OOM when parent passes huge values
        const int MaxReasonableSize = 10000;
        var safeWidth = bounds.Width > MaxReasonableSize ? (_handle?.Width ?? 80) : bounds.Width;
        var safeHeight = bounds.Height > MaxReasonableSize ? (_handle?.Height ?? 24) : bounds.Height;
        
        // If size changed, resize the handle (which propagates to the child terminal's PTY)
        if (_handle != null && (safeWidth != previousBounds.Width || safeHeight != previousBounds.Height))
        {
            _handle.Resize(safeWidth, safeHeight);
        }
    }
    
    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (_handle == null) return;
        
        // If terminal is not running and we have a fallback child, render the fallback instead
        if (_handle.State != TerminalState.Running && FallbackChild != null)
        {
            FallbackChild.Render(context);
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
        
        System.IO.File.AppendAllText("/tmp/hex1b-render.log", $"[{DateTime.Now:HH:mm:ss.fff}] TerminalNode.Render: Bounds={Bounds.X},{Bounds.Y},{Bounds.Width}x{Bounds.Height} Buffer={handleWidth}x{handleHeight} version={currentVersion}\\n");
        
        // Render each row that fits within our bounds
        for (int y = 0; y < Math.Min(Bounds.Height, handleHeight); y++)
        {
            var lineBuilder = new System.Text.StringBuilder();
            Hex1b.Theming.Hex1bColor? lastFg = null;
            Hex1b.Theming.Hex1bColor? lastBg = null;
            CellAttributes lastAttrs = CellAttributes.None;
            
            for (int x = 0; x < Math.Min(Bounds.Width, handleWidth); x++)
            {
                var cell = buffer[y, x];
                
                // Build ANSI codes for color changes
                bool needsReset = false;
                
                // Check if attributes changed
                if (cell.Attributes != lastAttrs)
                {
                    needsReset = true;
                }
                
                // Check if colors changed (nullable struct comparison)
                if (!Nullable.Equals(cell.Foreground, lastFg) || !Nullable.Equals(cell.Background, lastBg))
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
                        lineBuilder.Append("\x1b[4m");
                    if (cell.Attributes.HasFlag(CellAttributes.Reverse))
                        lineBuilder.Append("\x1b[7m");
                    if (cell.Attributes.HasFlag(CellAttributes.Strikethrough))
                        lineBuilder.Append("\x1b[9m");
                    
                    if (cell.Foreground != null)
                        lineBuilder.Append(cell.Foreground.Value.ToForegroundAnsi());
                    
                    if (cell.Background != null)
                        lineBuilder.Append(cell.Background.Value.ToBackgroundAnsi());
                    
                    lastFg = cell.Foreground;
                    lastBg = cell.Background;
                    lastAttrs = cell.Attributes;
                }
                
                lineBuilder.Append(cell.Character);
            }
            
            // Reset at end of line
            lineBuilder.Append("\x1b[0m");
            
            // Write the line at the correct position
            context.WriteClipped(Bounds.X, Bounds.Y + y, lineBuilder.ToString());
        }
    }
}
