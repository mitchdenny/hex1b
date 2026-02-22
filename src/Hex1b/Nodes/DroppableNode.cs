using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that acts as a drop target for drag-and-drop operations.
/// Not focusable itself — children can be focusable (including <see cref="DraggableNode"/>).
/// Drop target state (hover, acceptance) is managed by <see cref="DragDropManager"/> via <see cref="Hex1bApp"/>.
/// </summary>
public sealed class DroppableNode : Hex1bNode
{
    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public DroppableWidget? SourceWidget { get; set; }

    /// <summary>
    /// Predicate that determines whether this target accepts drag data.
    /// Null means accept all.
    /// </summary>
    public Func<object, bool>? AcceptPredicate { get; set; }

    /// <summary>
    /// The async action to execute when a valid item is dropped on this target.
    /// Parameters: context, dragData, sourceNode, localX, localY.
    /// </summary>
    public Func<InputBindingActionContext, object, DraggableNode, int, int, Task>? DropAction { get; set; }

    private bool _isHoveredByDrag;

    /// <summary>
    /// Whether a dragged item is currently hovering over this drop target.
    /// Set by <see cref="Hex1bApp"/>.
    /// </summary>
    public bool IsHoveredByDrag
    {
        get => _isHoveredByDrag;
        set
        {
            if (_isHoveredByDrag != value)
            {
                _isHoveredByDrag = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Whether the currently hovering drag data passes the Accept predicate.
    /// </summary>
    public bool CanAcceptDrag { get; set; }

    /// <summary>
    /// The drag data of the item currently hovering. Null if not hovered.
    /// </summary>
    public object? HoveredDragData { get; set; }

    /// <summary>
    /// Evaluates whether this target accepts the given drag data.
    /// </summary>
    public bool Accepts(object dragData)
        => AcceptPredicate == null || AcceptPredicate(dragData);

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child == null)
            return constraints.Constrain(Size.Zero);
        return Child.Measure(constraints);
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);
        Child?.Arrange(rect);
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
            context.RenderChild(Child);
    }

    public override IReadOnlyList<Hex1bNode> GetChildren()
        => Child != null ? [Child] : [];

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // Not focusable itself, but children (e.g., DraggableNodes) may be
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
                yield return focusable;
        }
    }
}
