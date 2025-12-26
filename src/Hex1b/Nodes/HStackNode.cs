using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class HStackNode : Hex1bNode, ILayoutProvider
{
    public List<Hex1bNode> Children { get; set; } = new();

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

    public bool ShouldRenderAt(int x, int y)
    {
        if (ClipMode == ClipMode.Overflow)
            return true;
            
        return x >= ClipRect.X && 
               x < ClipRect.X + ClipRect.Width &&
               y >= ClipRect.Y && 
               y < ClipRect.Y + ClipRect.Height;
    }

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
    {
        if (ClipMode == ClipMode.Overflow)
            return (x, text);
            
        // If entire line is outside vertical bounds, return empty
        if (y < ClipRect.Y || y >= ClipRect.Y + ClipRect.Height)
            return (x, "");
            
        var clipLeft = ClipRect.X;
        var clipRight = ClipRect.X + ClipRect.Width;

        // Entirely outside horizontal bounds (based on starting X)
        if (x >= clipRight)
            return (x, "");

        // Clip by visible columns (ANSI-aware) so we never cut escape sequences.
        var visibleLength = AnsiString.VisibleLength(text);
        if (visibleLength <= 0)
            return (x, "");

        var startColumn = Math.Max(0, clipLeft - x);
        var endColumnExclusive = Math.Min(visibleLength, clipRight - x);

        // Entirely left of the clip region.
        if (endColumnExclusive <= 0)
            return (x, "");

        if (endColumnExclusive <= startColumn)
            return (x, "");

        var sliceLength = endColumnExclusive - startColumn;
        
        // Use SliceByDisplayWidth to properly handle wide characters and get padding info
        var (slicedText, _, paddingBefore, paddingAfter) = 
            DisplayWidth.SliceByDisplayWidthWithAnsi(text, startColumn, sliceLength);
        
        if (slicedText.Length == 0 && paddingBefore == 0)
            return (x, "");

        // Build the result with padding if needed
        var clippedText = new string(' ', paddingBefore) + slicedText + new string(' ', paddingAfter);

        // If we clipped away printable characters on the right, preserve any trailing
        // escape suffix (typically a reset-to-inherited) to avoid style leaking.
        if (endColumnExclusive < visibleLength)
        {
            var suffix = AnsiString.TrailingEscapeSuffix(text);
            if (!string.IsNullOrEmpty(suffix) && !clippedText.EndsWith(suffix, StringComparison.Ordinal))
                clippedText += suffix;
        }

        var adjustedX = x + startColumn;
        return (adjustedX, clippedText);
    }
    
    #endregion

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
        var previousLayout = context.CurrentLayoutProvider;
        context.CurrentLayoutProvider = this;
        
        // Render children at their positioned bounds
        for (int i = 0; i < Children.Count; i++)
        {
            context.SetCursorPosition(Children[i].Bounds.X, Children[i].Bounds.Y);
            Children[i].Render(context);
        }
        
        context.CurrentLayoutProvider = previousLayout;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Tab navigation is handled by the app-level FocusRing via InputBindingActionContext.
        // HStack just provides Tab/Shift+Tab bindings that delegate to the context.
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Gets the direct children of this container for input routing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren() => Children;
}
