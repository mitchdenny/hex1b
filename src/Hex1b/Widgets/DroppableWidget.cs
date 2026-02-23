using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that defines a drop target region. When a draggable item is dragged over this region,
/// the builder lambda receives a <see cref="DroppableContext"/> with the current drag-hover state
/// so the content can render differently to indicate drop availability.
/// </summary>
/// <example>
/// <code>
/// ctx.Droppable(dc => [
///     dc.Text(dc.IsHoveredByDrag ? "Drop here!" : "Target area"),
/// ])
/// .Accept(data => data is string s &amp;&amp; s.EndsWith(".txt"))
/// .OnDrop(e => HandleDrop(e.DragData))
/// </code>
/// </example>
public sealed record DroppableWidget(Func<DroppableContext, Hex1bWidget> Builder) : Hex1bWidget
{
    /// <summary>
    /// Predicate that determines whether this target accepts the dragged data.
    /// If null, all drag data is accepted.
    /// </summary>
    internal Func<object, bool>? AcceptPredicate { get; init; }

    /// <summary>
    /// The async handler called when an accepted item is dropped on this target.
    /// </summary>
    internal Func<DropEventArgs, Task>? DropHandler { get; init; }

    /// <summary>
    /// Sets a predicate that determines whether this target accepts the given drag data.
    /// When a drag hovers over this target, the predicate is evaluated and the result
    /// is available via <see cref="DroppableContext.CanAcceptDrag"/>.
    /// </summary>
    public DroppableWidget Accept(Func<object, bool> predicate)
        => this with { AcceptPredicate = predicate };

    /// <summary>
    /// Sets a synchronous drop handler.
    /// </summary>
    public DroppableWidget OnDrop(Action<DropEventArgs> handler)
        => this with { DropHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous drop handler.
    /// </summary>
    public DroppableWidget OnDrop(Func<DropEventArgs, Task> handler)
        => this with { DropHandler = handler };

    /// <summary>
    /// The async handler called when an item is dropped on a specific <see cref="DropTargetWidget"/> within this droppable.
    /// </summary>
    internal Func<DropTargetEventArgs, Task>? DropTargetHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler for drops on specific <see cref="DropTargetWidget"/> insertion points.
    /// </summary>
    public DroppableWidget OnDropTarget(Action<DropTargetEventArgs> handler)
        => this with { DropTargetHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler for drops on specific <see cref="DropTargetWidget"/> insertion points.
    /// </summary>
    public DroppableWidget OnDropTarget(Func<DropTargetEventArgs, Task> handler)
        => this with { DropTargetHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DroppableNode ?? new DroppableNode();
        node.SourceWidget = this;
        node.AcceptPredicate = AcceptPredicate;

        // Wire up drop handler
        if (DropHandler != null)
        {
            node.DropAction = async (ctx, dragData, source, localX, localY) =>
            {
                var args = new DropEventArgs(this, node, ctx, dragData, source, localX, localY);
                await DropHandler(args);
            };
        }
        else
        {
            node.DropAction = null;
        }

        // Wire up drop target handler
        if (DropTargetHandler != null)
        {
            node.DropTargetAction = async (ctx, targetId, dragData, source) =>
            {
                var args = new DropTargetEventArgs(this, node, ctx, targetId, dragData, source);
                await DropTargetHandler(args);
            };
        }
        else
        {
            node.DropTargetAction = null;
        }

        var dc = new DroppableContext(node);
        var childWidget = Builder(dc);
        node.Child = await context.ReconcileChildAsync(node.Child, childWidget, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(DroppableNode);
}
