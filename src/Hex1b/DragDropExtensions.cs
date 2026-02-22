using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating <see cref="DraggableWidget"/> and <see cref="DroppableWidget"/>.
/// </summary>
public static class DragDropExtensions
{
    /// <summary>
    /// Creates a draggable wrapper around a single child widget.
    /// The builder lambda receives a <see cref="DraggableContext"/> with
    /// <see cref="DraggableContext.IsDragging"/> to reflect the current drag state.
    /// </summary>
    /// <param name="ctx">The parent widget context.</param>
    /// <param name="dragData">The reference data that will be passed to the drop target.</param>
    /// <param name="builder">A builder that creates the child widget using drag state.</param>
    /// <example>
    /// <code>
    /// ctx.Draggable("task-1", dc =>
    ///     dc.Border(dc.Text(dc.IsDragging ? "Dragging..." : "Task 1"))
    /// )
    /// </code>
    /// </example>
    public static DraggableWidget Draggable<TParent>(
        this WidgetContext<TParent> ctx,
        object dragData,
        Func<DraggableContext, Hex1bWidget> builder)
        where TParent : Hex1bWidget
        => new(dragData, builder);

    /// <summary>
    /// Creates a draggable wrapper with an implicit VStack for multiple children.
    /// </summary>
    public static DraggableWidget Draggable<TParent>(
        this WidgetContext<TParent> ctx,
        object dragData,
        Func<DraggableContext, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
        => new(dragData, dc => new VStackWidget(builder(dc)));

    /// <summary>
    /// Creates a droppable target that can receive dragged items.
    /// The builder lambda receives a <see cref="DroppableContext"/> with
    /// <see cref="DroppableContext.IsHoveredByDrag"/> and <see cref="DroppableContext.CanAcceptDrag"/>
    /// to reflect the current drag-hover state.
    /// </summary>
    /// <param name="ctx">The parent widget context.</param>
    /// <param name="builder">A builder that creates the child widget using drop state.</param>
    /// <example>
    /// <code>
    /// ctx.Droppable(dc =>
    ///     dc.Text(dc.IsHoveredByDrag ? "Drop here!" : "Target")
    /// )
    /// .Accept(data => data is string)
    /// .OnDrop(e => HandleDrop(e.DragData))
    /// </code>
    /// </example>
    public static DroppableWidget Droppable<TParent>(
        this WidgetContext<TParent> ctx,
        Func<DroppableContext, Hex1bWidget> builder)
        where TParent : Hex1bWidget
        => new(builder);

    /// <summary>
    /// Creates a droppable target with an implicit VStack for multiple children.
    /// </summary>
    public static DroppableWidget Droppable<TParent>(
        this WidgetContext<TParent> ctx,
        Func<DroppableContext, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
        => new(dc => new VStackWidget(builder(dc)));
}
