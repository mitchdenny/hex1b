namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="WrapPanelWidget"/>.
/// </summary>
public static class WrapPanelExtensions
{
    /// <summary>
    /// Creates a horizontal WrapPanel where children flow left-to-right
    /// and wrap to the next row when the available width is exceeded.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.WrapPanel(w => templates.Select(t => w.Border(b => [
    ///     b.Text(t.Name),
    ///     b.Text(t.Language),
    /// ]).FixedWidth(30)).ToArray())
    /// </code>
    /// </example>
    public static WrapPanelWidget WrapPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<WrapPanelWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<WrapPanelWidget>();
        var children = builder(childCtx);
        return new WrapPanelWidget(children);
    }

    /// <summary>
    /// Creates a vertical WrapPanel where children flow top-to-bottom
    /// and wrap to the next column when the available height is exceeded.
    /// </summary>
    public static WrapPanelWidget VWrapPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<WrapPanelWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<WrapPanelWidget>();
        var children = builder(childCtx);
        return new WrapPanelWidget(children) { Orientation = WrapOrientation.Vertical };
    }
}
