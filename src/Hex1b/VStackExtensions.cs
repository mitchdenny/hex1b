namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for building VStack widgets.
/// The callback returns Hex1bWidget[] using collection expressions.
/// Covariance on Hex1bWidget allows mixing different widget types.
/// </summary>
public static class VStackExtensions
{
    /// <summary>
    /// Creates a VStack where the callback returns an array of children.
    /// Use collection expression syntax: v => [v.Text("a"), v.Button("b", e => {})]
    /// </summary>
    public static VStackWidget VStack<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<VStackWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<VStackWidget>();
        var children = builder(childCtx);
        return new VStackWidget(children);
    }
}
