namespace Hex1b.Input;

/// <summary>
/// A handler for drag operations, returned by a drag binding's factory.
/// Receives move and end events for the duration of the drag.
/// </summary>
public sealed class DragHandler
{
    /// <summary>
    /// Called when the mouse moves during the drag.
    /// Parameters are (context, deltaX, deltaY) where delta is from the drag start position.
    /// </summary>
    public Action<InputBindingActionContext, int, int>? OnMove { get; }
    
    /// <summary>
    /// Called when the drag ends (mouse button released).
    /// </summary>
    public Action<InputBindingActionContext>? OnEnd { get; }
    
    /// <summary>
    /// Gets whether this handler is empty (no callbacks set).
    /// An empty handler indicates the drag was rejected and should not be captured.
    /// </summary>
    public bool IsEmpty => OnMove == null && OnEnd == null;

    /// <summary>
    /// Creates a new drag handler with context-aware callbacks.
    /// </summary>
    /// <param name="onMove">Called during drag with (context, deltaX, deltaY).</param>
    /// <param name="onEnd">Called when drag ends with context.</param>
    public DragHandler(Action<InputBindingActionContext, int, int>? onMove = null, Action<InputBindingActionContext>? onEnd = null)
    {
        OnMove = onMove;
        OnEnd = onEnd;
    }

    /// <summary>
    /// Creates a new drag handler with simple callbacks (no context).
    /// For backwards compatibility with existing code.
    /// </summary>
    /// <param name="onMove">Called during drag with (deltaX, deltaY).</param>
    /// <param name="onEnd">Called when drag ends.</param>
    public static DragHandler Simple(Action<int, int>? onMove = null, Action? onEnd = null)
    {
        return new DragHandler(
            onMove != null ? (ctx, dx, dy) => onMove(dx, dy) : null,
            onEnd != null ? ctx => onEnd() : null
        );
    }
}
