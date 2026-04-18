using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Composite node for the trace timeline widget. Delegates layout and rendering
/// to an internally composed <see cref="TreeNode"/> child.
/// </summary>
internal sealed class TraceTimelineNode<T> : Hex1bNode
{
    /// <summary>
    /// The composed tree child (with custom row content).
    /// </summary>
    public Hex1bNode? TreeChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (TreeChild == null)
            return constraints.Constrain(new Size(0, 0));

        return TreeChild.Measure(constraints);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        TreeChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        TreeChild?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (TreeChild != null)
            yield return TreeChild;
    }
}
