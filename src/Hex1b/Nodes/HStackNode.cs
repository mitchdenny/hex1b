using Hex1b.Input;
using Hex1b.Layout;

namespace Hex1b;

public sealed class HStackNode : Hex1bNode
{
    public List<Hex1bNode> Children { get; set; } = new();

    /// <summary>
    /// HStack manages focus for its descendants, so nested containers don't independently set focus.
    /// </summary>
    public override bool ManagesChildFocus => true;

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
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
        // Pass height constraint to children so they can size appropriately
        var totalWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            // Children get the parent's height constraint but unbounded width
            var childConstraints = new Constraints(0, int.MaxValue, 0, constraints.MaxHeight);
            var childSize = child.Measure(childConstraints);
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
            var hint = Children[i].WidthHint ?? SizeHint.Content;

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
                var hint = Children[i].WidthHint ?? SizeHint.Content;
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

    public override void Render(Hex1bRenderContext context)
    {
        // Render children at their positioned bounds
        for (int i = 0; i < Children.Count; i++)
        {
            context.SetCursorPosition(Children[i].Bounds.X, Children[i].Bounds.Y);
            Children[i].Render(context);
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation is handled by the app-level FocusRing via ActionContext.
        // HStack just provides Tab/Shift+Tab bindings that delegate to the context.
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren() => Children;
}
