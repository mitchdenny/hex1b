using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Composite node for the trace timeline widget. Delegates layout and rendering
/// to an internally composed <see cref="SplitterNode"/> child.
/// </summary>
internal sealed class TraceTimelineNode<T> : Hex1bNode
{
    /// <summary>
    /// The composed splitter child (tree on left, timeline on right).
    /// </summary>
    public Hex1bNode? SplitterChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (SplitterChild == null)
            return constraints.Constrain(new Size(0, 0));

        return SplitterChild.Measure(constraints);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        SplitterChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        SplitterChild?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (SplitterChild != null)
            yield return SplitterChild;
    }
}
