using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that makes its child tree draggable via mouse interaction.
/// The builder lambda receives a <see cref="DraggableContext"/> with the current drag state
/// so the content can render differently while being dragged.
/// </summary>
/// <example>
/// <code>
/// ctx.Draggable("task-1", dc =>
///     dc.Border(dc.Text(dc.IsDragging ? "Dragging..." : "Task 1"))
/// )
/// .DragOverlay(dc => dc.Text("📋 Task 1"))
/// </code>
/// </example>
public sealed record DraggableWidget(object DragData, Func<DraggableContext, Hex1bWidget> Builder) : Hex1bWidget
{
    /// <summary>
    /// Optional builder for a visual overlay shown at the cursor position during drag.
    /// </summary>
    internal Func<DraggableContext, Hex1bWidget>? DragOverlayBuilder { get; init; }

    /// <summary>
    /// Sets a builder for the drag overlay — a visual representation that follows the cursor during drag.
    /// </summary>
    public DraggableWidget DragOverlay(Func<DraggableContext, Hex1bWidget> builder)
        => this with { DragOverlayBuilder = builder };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DraggableNode ?? new DraggableNode();
        node.SourceWidget = this;
        node.DragData = DragData;

        var dc = new DraggableContext(node);
        var childWidget = Builder(dc);
        node.Child = await context.ReconcileChildAsync(node.Child, childWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DraggableNode);
}
