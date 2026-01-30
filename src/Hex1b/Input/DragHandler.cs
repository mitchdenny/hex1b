namespace Hex1b.Input;

/// <summary>
/// A handler for drag operations, returned by a drag binding's factory.
/// Receives move and end events for the duration of the drag.
/// </summary>
public sealed class DragHandler
{
    /// <summary>
    /// Called when the mouse moves during the drag.
    /// Parameters are (deltaX, deltaY) from the drag start position.
    /// </summary>
    public Action<int, int>? OnMove { get; }
    
    /// <summary>
    /// Called when the drag ends (mouse button released).
    /// </summary>
    public Action? OnEnd { get; }
    
    /// <summary>
    /// Gets whether this handler is empty (no callbacks set).
    /// An empty handler indicates the drag was rejected and should not be captured.
    /// </summary>
    public bool IsEmpty => OnMove == null && OnEnd == null;

    public DragHandler(Action<int, int>? onMove = null, Action? onEnd = null)
    {
        OnMove = onMove;
        OnEnd = onEnd;
    }
}
