namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building FloatPanel widgets.
/// </summary>
public static class FloatPanelExtensions
{
    /// <summary>
    /// Creates a FloatPanel where children are positioned at absolute (x, y) coordinates.
    /// Use <see cref="Place{TParent}"/> within the builder to position children.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.FloatPanel(f => [
    ///     f.Place(10, 5, f.Icon("📍")),
    ///     f.Place(20, 8, f.Text("Hello")),
    /// ])
    /// </code>
    /// </example>
    public static FloatPanelWidget FloatPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<FloatPanelWidget>, FloatChild[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<FloatPanelWidget>();
        var children = builder(childCtx);
        return new FloatPanelWidget(children);
    }

    /// <summary>
    /// Positions a widget at absolute (x, y) character coordinates within a FloatPanel.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="x">The X coordinate within the panel.</param>
    /// <param name="y">The Y coordinate within the panel.</param>
    /// <param name="widget">The widget to position.</param>
    public static FloatChild Place<TParent>(
        this WidgetContext<TParent> ctx,
        int x,
        int y,
        Hex1bWidget widget)
        where TParent : Hex1bWidget
        => new(x, y, widget);
}
