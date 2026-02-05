namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ScrollPanelWidget.
/// </summary>
public static class ScrollPanelExtensions
{
    /// <summary>
    /// Creates a vertical scroll panel with the specified child.
    /// </summary>
    public static ScrollPanelWidget VScrollPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, ScrollOrientation.Vertical, showScrollbar);

    /// <summary>
    /// Creates a vertical scroll panel with a VStack child built from a callback.
    /// </summary>
    public static ScrollPanelWidget VScrollPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> childBuilder,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        return new ScrollPanelWidget(
            new VStackWidget(childBuilder(childCtx)),
            ScrollOrientation.Vertical,
            showScrollbar);
    }

    /// <summary>
    /// Creates a horizontal scroll panel with the specified child.
    /// </summary>
    public static ScrollPanelWidget HScrollPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, ScrollOrientation.Horizontal, showScrollbar);

    /// <summary>
    /// Creates a horizontal scroll panel with an HStack child built from a callback.
    /// </summary>
    public static ScrollPanelWidget HScrollPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<HStackWidget>, Hex1bWidget[]> childBuilder,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget>();
        return new ScrollPanelWidget(
            new HStackWidget(childBuilder(childCtx)),
            ScrollOrientation.Horizontal,
            showScrollbar);
    }

    /// <summary>
    /// Creates a scroll panel with the specified orientation.
    /// </summary>
    public static ScrollPanelWidget ScrollPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        ScrollOrientation orientation,
        bool showScrollbar = true)
        where TParent : Hex1bWidget
        => new(child, orientation, showScrollbar);
}
