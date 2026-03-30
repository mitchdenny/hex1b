using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class HStackNode : Hex1bNode, ILayoutProvider
{
    public List<Hex1bNode> Children { get; set; } = new();
    public List<FloatEntry> Floats { get; set; } = new();
    public List<Hex1bNode> AllChildrenInOrder { get; set; } = new();

    /// <summary>
    /// The clip mode for the HStack's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    /// <summary>
    /// HStack manages focus for its descendants, so nested containers don't independently set focus.
    /// </summary>
    public override bool ManagesChildFocus => true;

    #region ILayoutProvider Implementation
    
    /// <summary>
    /// The clip rectangle for child content.
    /// </summary>
    public Rect ClipRect => Bounds;
    
    /// <inheritdoc />
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        var source = AllChildrenInOrder.Count > 0 ? AllChildrenInOrder : Children;
        foreach (var child in source)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // HStack: sum widths, take max height
        // Pass height constraint to children so they can size appropriately
        // Use long to prevent int overflow when children return int.MaxValue from
        // unbounded measurement.
        long totalWidth = 0;
        var maxHeight = 0;

        foreach (var child in Children)
        {
            // Children get the parent's height constraint but unbounded width
            var childConstraints = new Constraints(0, int.MaxValue, 0, constraints.MaxHeight);
            var childSize = child.Measure(childConstraints);
            totalWidth += childSize.Width;
            maxHeight = Math.Max(maxHeight, childSize.Height);
        }

        var clampedWidth = (int)Math.Min(totalWidth, int.MaxValue);
        return constraints.Constrain(new Size(clampedWidth, maxHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        if (Children.Count == 0)
        {
            FloatLayoutHelper.ArrangeFloats(Floats, bounds);
            return;
        }

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
                // Cap to availableWidth to prevent widgets that fill all space
                // from returning int.MaxValue and causing unbounded arrangement/render loops.
                var measured = Children[i].Measure(Constraints.Unbounded);
                childSizes[i] = Math.Min(measured.Width, availableWidth);
                totalFixed += childSizes[i];
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

        // Arrange floats after flow layout completes
        FloatLayoutHelper.ArrangeFloats(Floats, bounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        for (int i = 0; i < Children.Count; i++)
        {
            context.RenderChild(Children[i]);
        }

        // Render floats on top
        FloatLayoutHelper.RenderFloats(Floats, context);
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation is handled by the app-level FocusRing via InputBindingActionContext.
        // HStack just provides Tab/Shift+Tab bindings that delegate to the context.
        bindings.Key(Hex1bKey.Tab).Triggers(HStackWidget.FocusNextAction, ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Triggers(HStackWidget.FocusPreviousAction, ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
        => AllChildrenInOrder.Count > 0 ? AllChildrenInOrder : Children;
}
