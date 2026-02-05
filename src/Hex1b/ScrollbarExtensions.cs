namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building ScrollbarWidget.
/// </summary>
public static class ScrollbarExtensions
{
    /// <summary>
    /// Creates a vertical scrollbar.
    /// </summary>
    public static ScrollbarWidget VScrollbar<TParent>(
        this WidgetContext<TParent> ctx,
        int contentSize,
        int viewportSize,
        int offset)
        where TParent : Hex1bWidget
        => new(ScrollOrientation.Vertical, contentSize, viewportSize, offset);

    /// <summary>
    /// Creates a horizontal scrollbar.
    /// </summary>
    public static ScrollbarWidget HScrollbar<TParent>(
        this WidgetContext<TParent> ctx,
        int contentSize,
        int viewportSize,
        int offset)
        where TParent : Hex1bWidget
        => new(ScrollOrientation.Horizontal, contentSize, viewportSize, offset);

    /// <summary>
    /// Creates a scrollbar with the specified orientation.
    /// </summary>
    public static ScrollbarWidget Scrollbar<TParent>(
        this WidgetContext<TParent> ctx,
        ScrollOrientation orientation,
        int contentSize,
        int viewportSize,
        int offset)
        where TParent : Hex1bWidget
        => new(orientation, contentSize, viewportSize, offset);
}
