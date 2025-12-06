using Custard.Layout;

namespace Custard;

public sealed class HStackNode : CustardNode
{
    public List<CustardNode> Children { get; set; } = new();
    public List<SizeHint> ChildWidthHints { get; set; } = new();
    public int FocusedIndex { get; set; } = 0;

    public override IEnumerable<CustardNode> GetFocusableNodes()
    {
        foreach (var child in Children)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override Size Measure(Constraints constraints)
    {
        // HStack: sum widths, take max height
        var totalWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            var childSize = child.Measure(Constraints.Unbounded);
            totalWidth += childSize.Width;
            maxHeight = Math.Max(maxHeight, childSize.Height);
        }

        return constraints.Constrain(new Size(totalWidth, maxHeight));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        if (Children.Count == 0) return;

        // Calculate how to distribute width among children
        var availableWidth = bounds.Width;
        var childSizes = new int[Children.Count];
        var totalFixed = 0;
        var totalWeight = 0;

        // First pass: measure content-sized and fixed children
        for (int i = 0; i < Children.Count; i++)
        {
            var hint = i < ChildWidthHints.Count ? ChildWidthHints[i] : SizeHint.Content;

            if (hint.IsFixed)
            {
                childSizes[i] = hint.FixedValue;
                totalFixed += hint.FixedValue;
            }
            else if (hint.IsContent)
            {
                var measured = Children[i].Measure(Constraints.Unbounded);
                childSizes[i] = measured.Width;
                totalFixed += measured.Width;
            }
            else if (hint.IsFill)
            {
                totalWeight += hint.FillWeight;
            }
        }

        // Second pass: distribute remaining space to fill children
        var remaining = Math.Max(0, availableWidth - totalFixed);
        if (totalWeight > 0)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var hint = i < ChildWidthHints.Count ? ChildWidthHints[i] : SizeHint.Content;
                if (hint.IsFill)
                {
                    childSizes[i] = remaining * hint.FillWeight / totalWeight;
                }
            }
        }

        // Arrange children
        var x = bounds.X;
        for (int i = 0; i < Children.Count; i++)
        {
            var childBounds = new Rect(x, bounds.Y, childSizes[i], bounds.Height);
            Children[i].Arrange(childBounds);
            x += childSizes[i];
        }
    }

    public override void Render(CustardRenderContext context)
    {
        // Render children horizontally (no separator, just concatenate)
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Render(context);
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
    {
        // Dispatch to focused child
        if (FocusedIndex >= 0 && FocusedIndex < Children.Count)
        {
            return Children[FocusedIndex].HandleInput(evt);
        }

        return false;
    }
}
