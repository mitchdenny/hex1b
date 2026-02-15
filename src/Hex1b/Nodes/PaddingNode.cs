using Hex1b.Layout;

namespace Hex1b.Nodes;

/// <summary>
/// A node that adds invisible padding around its child content.
/// Subtracts padding from constraints during measure and offsets the child during arrange.
/// </summary>
public sealed class PaddingNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }

    public int Left { get; set; }
    public int Right { get; set; }
    public int Top { get; set; }
    public int Bottom { get; set; }

    private int HorizontalPadding => Left + Right;
    private int VerticalPadding => Top + Bottom;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Child == null)
        {
            return constraints.Constrain(new Size(HorizontalPadding, VerticalPadding));
        }

        // Subtract padding from constraints for the child
        var childConstraints = new Constraints(
            Math.Max(0, constraints.MinWidth - HorizontalPadding),
            Math.Max(0, constraints.MaxWidth - HorizontalPadding),
            Math.Max(0, constraints.MinHeight - VerticalPadding),
            Math.Max(0, constraints.MaxHeight - VerticalPadding)
        );

        var childSize = Child.Measure(childConstraints);

        // Add padding back to child's measured size
        return constraints.Constrain(new Size(
            childSize.Width + HorizontalPadding,
            childSize.Height + VerticalPadding));
    }

    protected override void ArrangeCore(Rect rect)
    {
        base.ArrangeCore(rect);

        if (Child != null)
        {
            var innerBounds = new Rect(
                rect.X + Left,
                rect.Y + Top,
                Math.Max(0, rect.Width - HorizontalPadding),
                Math.Max(0, rect.Height - VerticalPadding));
            Child.Arrange(innerBounds);
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (Child != null)
        {
            context.RenderChild(Child);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child != null)
        {
            foreach (var focusable in Child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IReadOnlyList<Hex1bNode> GetChildren()
    {
        return Child != null ? [Child] : [];
    }
}
