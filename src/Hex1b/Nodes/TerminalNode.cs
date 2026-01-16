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
    
    /// <inheritdoc />
    public override bool IsFocusable => true;
    
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
        // For now, forward ALL input to the terminal (including Tab)
        // In a future phase, we'll intercept Tab for focus navigation
        if (_handle == null) return InputResult.NotHandled;
        
        // Fire and forget - we don't want to block the input loop
        _ = _handle.SendKeyEventAsync(keyEvent);
        return InputResult.Handled;
    }
    
    private static void Log(string msg) =>
        System.IO.File.AppendAllText("/tmp/hex1b-crash.log", $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
    
    /// <summary>
    /// Binds this node to the current handle, subscribing to output events.
    /// </summary>
    internal void Bind()
    {
        if (_isBound || _handle == null) return;
        
        Log($"TerminalNode.Bind: Subscribing to OutputReceived, hasCallback={_invalidateCallback != null}");
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
        
        Log("TerminalNode.Unbind: Unsubscribing from OutputReceived");
        if (_outputReceivedHandler != null)
        {
            _handle.OutputReceived -= _outputReceivedHandler;
            _outputReceivedHandler = null;
        }
        _isBound = false;
    }
    
    private void OnOutputReceived()
    {
        Log($"TerminalNode.OnOutputReceived: hasCallback={_invalidateCallback != null}");
        MarkDirty();
        _invalidateCallback?.Invoke();
    }
    
    /// <inheritdoc />
    public override Size Measure(Constraints constraints)
    {
        // The terminal should fill the available space
        // We use constraints as the preferred size, not the handle's current dimensions
        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }
    
    /// <inheritdoc />
    public override void Arrange(Rect bounds)
    {
        var previousBounds = Bounds;
        base.Arrange(bounds);
        
        // If size changed, resize the handle (which propagates to the child terminal's PTY)
        if (_handle != null && (bounds.Width != previousBounds.Width || bounds.Height != previousBounds.Height))
        {
            _handle.Resize(bounds.Width, bounds.Height);
        }
    }
    
    /// <inheritdoc />
    public override void Render(Hex1bRenderContext context)
    {
        if (_handle == null) return;
        
        // Get the current screen buffer with dimensions atomically
        // This prevents race conditions where dimensions change between getting buffer and reading Width/Height
        var (buffer, handleWidth, handleHeight) = _handle.GetScreenBufferSnapshot();
        
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
