using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Composite node for the trace timeline widget. Delegates layout and rendering
/// to an internally composed child (SplitterNode with tree + timeline panels).
/// </summary>
internal sealed class TraceTimelineNode<T> : Hex1bNode
{
    /// <summary>
    /// The composed child node (Splitter with tree on left, timeline on right).
    /// </summary>
    public Hex1bNode? ComposedChild { get; set; }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (ComposedChild == null)
            return constraints.Constrain(new Size(0, 0));

        return ComposedChild.Measure(constraints);
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);
        ComposedChild?.Arrange(bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        ComposedChild?.Render(context);
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (ComposedChild != null)
            yield return ComposedChild;
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (ComposedChild != null)
        {
            foreach (var focusable in ComposedChild.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
}
