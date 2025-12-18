namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building HStack widgets.
/// </summary>
public static class HStackExtensions
{
    /// <summary>
    /// Creates an HStack where the callback returns an array of children.
    /// </summary>
    public static HStackWidget HStack<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<HStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<HStackWidget>();
        var children = builder(childCtx);
        return new HStackWidget(children);
    }
}
