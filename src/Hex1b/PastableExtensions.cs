using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for building PastableWidget.
/// </summary>
public static class PastableExtensions
{
    /// <summary>
    /// Creates a Pastable container wrapping a single child widget.
    /// </summary>
    public static PastableWidget Pastable<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates a Pastable container with a VStack child.
    /// </summary>
    public static PastableWidget Pastable<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new PastableWidget(new VStackWidget(children));
    }
}
