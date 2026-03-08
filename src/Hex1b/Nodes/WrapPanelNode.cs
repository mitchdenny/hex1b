using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for <see cref="WrapPanelWidget"/>.
/// Lays out children sequentially, wrapping to the next row or column
/// when the available extent is exceeded.
/// </summary>
public sealed class WrapPanelNode : Hex1bNode, ILayoutProvider
{
    public List<Hex1bNode> Children { get; set; } = new();

    /// <summary>
    /// The clip mode for the WrapPanel's content. Defaults to Clip.
    /// </summary>
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    /// <summary>
    /// The primary layout direction.
    /// </summary>
    public WrapOrientation Orientation { get; set; } = WrapOrientation.Horizontal;

    public override bool ManagesChildFocus => true;

    #region ILayoutProvider

    public Rect ClipRect => Bounds;
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

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

    public override IEnumerable<Hex1bNode> GetChildren() => Children;

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Children.Count == 0)
            return constraints.Constrain(Size.Zero);

        if (Orientation == WrapOrientation.Horizontal)
            return MeasureHorizontal(constraints);
        else
            return MeasureVertical(constraints);
    }

    private Size MeasureHorizontal(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        var x = 0;
        var rowHeight = 0;
        var totalHeight = 0;
        var totalWidth = 0;

        foreach (var child in Children)
        {
            var childSize = child.Measure(new Constraints(0, maxWidth, 0, int.MaxValue));

            // Wrap to next row if this child exceeds the row width (unless it's the first in the row)
            if (x > 0 && x + childSize.Width > maxWidth)
            {
                totalHeight += rowHeight;
                totalWidth = Math.Max(totalWidth, x);
                x = 0;
                rowHeight = 0;
            }

            x += childSize.Width;
            rowHeight = Math.Max(rowHeight, childSize.Height);
        }

        // Account for the last row
        totalHeight += rowHeight;
        totalWidth = Math.Max(totalWidth, x);

        return constraints.Constrain(new Size(totalWidth, totalHeight));
    }

    private Size MeasureVertical(Constraints constraints)
    {
        var maxHeight = constraints.MaxHeight;
        var y = 0;
        var colWidth = 0;
        var totalWidth = 0;
        var totalHeight = 0;

        foreach (var child in Children)
        {
            var childSize = child.Measure(new Constraints(0, int.MaxValue, 0, maxHeight));

            if (y > 0 && y + childSize.Height > maxHeight)
            {
                totalWidth += colWidth;
                totalHeight = Math.Max(totalHeight, y);
                y = 0;
                colWidth = 0;
            }

            y += childSize.Height;
            colWidth = Math.Max(colWidth, childSize.Width);
        }

        totalWidth += colWidth;
        totalHeight = Math.Max(totalHeight, y);

        return constraints.Constrain(new Size(totalWidth, totalHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        if (Children.Count == 0)
            return;

        if (Orientation == WrapOrientation.Horizontal)
            ArrangeHorizontal(bounds);
        else
            ArrangeVertical(bounds);
    }

    private void ArrangeHorizontal(Rect bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var rowHeight = 0;

        foreach (var child in Children)
        {
            // Re-measure to get the child's desired size
            var childSize = child.Measure(new Constraints(0, bounds.Width, 0, int.MaxValue));

            if (x > bounds.X && x + childSize.Width > bounds.Right)
            {
                // Wrap to next row
                y += rowHeight;
                x = bounds.X;
                rowHeight = 0;
            }

            child.Arrange(new Rect(x, y, childSize.Width, childSize.Height));
            x += childSize.Width;
            rowHeight = Math.Max(rowHeight, childSize.Height);
        }
    }

    private void ArrangeVertical(Rect bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var colWidth = 0;

        foreach (var child in Children)
        {
            var childSize = child.Measure(new Constraints(0, int.MaxValue, 0, bounds.Height));

            if (y > bounds.Y && y + childSize.Height > bounds.Bottom)
            {
                // Wrap to next column
                x += colWidth;
                y = bounds.Y;
                colWidth = 0;
            }

            child.Arrange(new Rect(x, y, childSize.Width, childSize.Height));
            y += childSize.Height;
            colWidth = Math.Max(colWidth, childSize.Width);
        }
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

        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.Tab).Triggers(WrapPanelWidget.FocusNextAction, ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Triggers(WrapPanelWidget.FocusPreviousAction, ctx => ctx.FocusPrevious(), "Previous focusable");
    }
}
