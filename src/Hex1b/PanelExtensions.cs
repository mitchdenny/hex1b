namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building PanelWidget.
/// </summary>
public static class PanelExtensions
{
    /// <summary>
    /// Creates a Panel wrapping a single child widget.
    /// </summary>
    public static PanelWidget Panel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates a Panel with a VStack child.
    /// </summary>
    public static PanelWidget Panel<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new PanelWidget(new VStackWidget(children));
    }
}
