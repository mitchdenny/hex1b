using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that makes its child tree draggable.
/// Registers a drag binding for left mouse button. When dragged,
/// <see cref="Hex1bApp"/> manages the drag-drop operation via <see cref="DragDropManager"/>.
/// </summary>
public sealed class DraggableNode : Hex1bNode
{
    /// <summary>
    /// The single child node.
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// The reference data that will be passed to the drop target.
    /// </summary>
    public object DragData { get; set; } = null!;

    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public DraggableWidget? SourceWidget { get; set; }

    private bool _isDragging;

    /// <summary>
    /// Whether this node is currently being dragged. Set by <see cref="Hex1bApp"/>.
    /// </summary>
    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging != value)
            {
                _isDragging = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Register a drag binding so the existing drag capture mechanism in Hex1bApp kicks in.
        // The actual drag-drop logic is handled by Hex1bApp + DragDropManager, not by this handler.
        bindings.Drag(MouseButton.Left).Action(
            (localX, localY) => new DragHandler(
                onMove: (ctx, dx, dy) => { },
                onEnd: ctx => { }),
            "Drag");
    }

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

    public override Rect HitTestBounds => Bounds;

    public override IReadOnlyList<Hex1bNode> GetChildren()
        => Child != null ? [Child] : [];
}
