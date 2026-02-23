using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Input;

/// <summary>
/// Centralized manager for drag-and-drop operations.
/// Tracks the active drag state including the data being dragged,
/// the source node, the currently hovered drop target, and cursor position.
/// Owned by <see cref="Hex1bApp"/>.
/// </summary>
internal sealed class DragDropManager
{
    /// <summary>
    /// The reference data supplied by the draggable source.
    /// </summary>
    public object? ActiveDragData { get; private set; }

    /// <summary>
    /// The node that initiated the drag.
    /// </summary>
    public DraggableNode? ActiveSource { get; private set; }

    /// <summary>
    /// The source widget that initiated the drag. Stored to access DragOverlayBuilder.
    /// </summary>
    public DraggableWidget? ActiveSourceWidget { get; private set; }

    /// <summary>
    /// The droppable node currently under the cursor (if any).
    /// </summary>
    public DroppableNode? HoveredTarget { get; private set; }

    /// <summary>
    /// Whether a drag operation is currently active.
    /// </summary>
    public bool IsDragging => ActiveSource != null;

    /// <summary>
    /// Current drag position (screen coordinates).
    /// </summary>
    public int DragX { get; private set; }

    /// <summary>
    /// Current drag position (screen coordinates).
    /// </summary>
    public int DragY { get; private set; }

    /// <summary>
    /// Begins a drag operation.
    /// </summary>
    public void StartDrag(DraggableNode source, object dragData, int x, int y)
    {
        ActiveSource = source;
        ActiveSourceWidget = source.SourceWidget;
        ActiveDragData = dragData;
        DragX = x;
        DragY = y;
        HoveredTarget = null;
    }

    /// <summary>
    /// Updates the drag position and hovered target.
    /// </summary>
    public void UpdatePosition(int x, int y, DroppableNode? hoveredTarget)
    {
        DragX = x;
        DragY = y;
        HoveredTarget = hoveredTarget;
    }

    /// <summary>
    /// Ends the drag operation and clears all state.
    /// </summary>
    public void EndDrag()
    {
        ActiveSource = null;
        ActiveSourceWidget = null;
        ActiveDragData = null;
        HoveredTarget = null;
        DragX = 0;
        DragY = 0;
    }

    /// <summary>
    /// Builds the drag overlay widget if a drag is active and the source has a DragOverlayBuilder.
    /// Returns null if no overlay should be shown.
    /// </summary>
    public Hex1bWidget? BuildOverlayWidget()
    {
        if (!IsDragging || ActiveSource == null || ActiveSourceWidget?.DragOverlayBuilder == null)
            return null;

        var context = new DraggableContext(ActiveSource);
        var overlayContent = ActiveSourceWidget.DragOverlayBuilder(context);
        return new DragOverlayWidget(overlayContent, DragX, DragY);
    }
}
