using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for setting size hints on widgets.
/// These enable a fluent API: ctx.Text("Hello").Height(SizeHint.Fill)
/// </summary>
public static class SizeHintExtensions
{
    /// <summary>
    /// Sets the width hint for this widget.
    /// </summary>
    public static TWidget Width<TWidget>(this TWidget widget, SizeHint hint)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = hint };

    /// <summary>
    /// Sets the height hint for this widget.
    /// </summary>
    public static TWidget Height<TWidget>(this TWidget widget, SizeHint hint)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { HeightHint = hint };

    /// <summary>
    /// Sets this widget to fill available width.
    /// </summary>
    public static TWidget FillWidth<TWidget>(this TWidget widget)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = SizeHint.Fill };

    /// <summary>
    /// Sets this widget to fill available height.
    /// </summary>
    public static TWidget FillHeight<TWidget>(this TWidget widget)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { HeightHint = SizeHint.Fill };

    /// <summary>
    /// Sets this widget to fill available space in both dimensions.
    /// </summary>
    public static TWidget Fill<TWidget>(this TWidget widget)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = SizeHint.Fill, HeightHint = SizeHint.Fill };

    /// <summary>
    /// Sets this widget to fill available width with a weight.
    /// </summary>
    public static TWidget FillWidth<TWidget>(this TWidget widget, int weight)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = SizeHint.Weighted(weight) };

    /// <summary>
    /// Sets this widget to fill available height with a weight.
    /// </summary>
    public static TWidget FillHeight<TWidget>(this TWidget widget, int weight)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { HeightHint = SizeHint.Weighted(weight) };

    /// <summary>
    /// Sets this widget to a fixed width.
    /// </summary>
    public static TWidget FixedWidth<TWidget>(this TWidget widget, int width)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = SizeHint.Fixed(width) };

    /// <summary>
    /// Sets this widget to a fixed height.
    /// </summary>
    public static TWidget FixedHeight<TWidget>(this TWidget widget, int height)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { HeightHint = SizeHint.Fixed(height) };

    /// <summary>
    /// Sets this widget to size to its content width.
    /// </summary>
    public static TWidget ContentWidth<TWidget>(this TWidget widget)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { WidthHint = SizeHint.Content };

    /// <summary>
    /// Sets this widget to size to its content height.
    /// </summary>
    public static TWidget ContentHeight<TWidget>(this TWidget widget)
        where TWidget : Hex1bWidget
        => (TWidget)widget with { HeightHint = SizeHint.Content };
}
