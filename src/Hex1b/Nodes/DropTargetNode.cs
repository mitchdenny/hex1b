using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// A node that marks an insertion point for drag-and-drop within a <see cref="DroppableNode"/>.
/// Visibility is controlled by the builder callback: when inactive, the builder typically returns
/// a zero-height widget; when active (nearest to cursor), it returns a visual indicator.
/// </summary>
public sealed class DropTargetNode : Hex1bNode
{
    /// <summary>
    /// The single child node (the visual indicator).
    /// </summary>
    public Hex1bNode? Child { get; set; }

    /// <summary>
    /// Unique identifier for this drop target within its parent droppable.
    /// </summary>
    public string TargetId { get; set; } = "";

    private bool _isActive;

    /// <summary>
    /// Whether this is the currently active drop target (nearest to cursor).
    /// Set by <see cref="Hex1bApp"/> during drag operations.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The drag data of the item currently being dragged. Null if no drag is active.
    /// </summary>
    public object? DragData { get; set; }

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child == null)
            return new Size(constraints.MaxWidth, 0);

        // Respect child's HeightHint (e.g., Fixed(0) for invisible drop targets)
        if (Child.HeightHint is { IsFixed: true } hint)
        {
            var constrained = constraints.WithMaxHeight(hint.FixedValue);
            var size = Child.Measure(constrained);
            return new Size(size.Width, hint.FixedValue);
        }

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
        yield break; // Drop targets never capture focus
    }
}
