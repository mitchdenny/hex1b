namespace Hex1b;

using Hex1b.Layout;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for building SplitterWidget.
/// </summary>
public static class SplitterExtensions
{
    /// <summary>
    /// Creates a horizontal Splitter with left and right child widgets.
    /// </summary>
    public static SplitterWidget Splitter<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget left,
        Hex1bWidget right,
        int leftWidth = 30)
        where TParent : Hex1bWidget
        => new(left, right, leftWidth, SplitterOrientation.Horizontal);

    /// <summary>
    /// Creates a Splitter with first and second child widgets.
    /// </summary>
    public static SplitterWidget Splitter<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget first,
        Hex1bWidget second,
        int firstSize,
        SplitterOrientation orientation)
        where TParent : Hex1bWidget
        => new(first, second, firstSize, orientation);

    /// <summary>
    /// Creates a vertical Splitter with top and bottom child widgets.
    /// </summary>
    public static SplitterWidget VSplitter<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget top,
        Hex1bWidget bottom,
        int topHeight = 10)
        where TParent : Hex1bWidget
        => new(top, bottom, topHeight, SplitterOrientation.Vertical);

    /// <summary>
    /// Creates a horizontal Splitter where both panes are VStacks built from callbacks.
    /// </summary>
    public static SplitterWidget Splitter<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> leftBuilder,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> rightBuilder,
        int leftWidth = 30)
        where TParent : Hex1bWidget
    {
        var leftCtx = new WidgetContext<VStackWidget>();
        var rightCtx = new WidgetContext<VStackWidget>();
        return new SplitterWidget(
            new VStackWidget(leftBuilder(leftCtx)),
            new VStackWidget(rightBuilder(rightCtx)),
            leftWidth,
            SplitterOrientation.Horizontal);
    }

    /// <summary>
    /// Creates a vertical Splitter where both panes are VStacks built from callbacks.
    /// </summary>
    public static SplitterWidget VSplitter<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> topBuilder,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> bottomBuilder,
        int topHeight = 10)
        where TParent : Hex1bWidget
    {
        var topCtx = new WidgetContext<VStackWidget>();
        var bottomCtx = new WidgetContext<VStackWidget>();
        return new SplitterWidget(
            new VStackWidget(topBuilder(topCtx)),
            new VStackWidget(bottomBuilder(bottomCtx)),
            topHeight,
            SplitterOrientation.Vertical);
    }
}
