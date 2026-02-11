namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building BorderWidget.
/// </summary>
public static class BorderExtensions
{
    /// <summary>
    /// Creates a Border wrapping a single child widget.
    /// </summary>
    public static BorderWidget Border<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates a Border with a VStack child.
    /// </summary>
    public static BorderWidget Border<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new BorderWidget(new VStackWidget(children));
    }
}
