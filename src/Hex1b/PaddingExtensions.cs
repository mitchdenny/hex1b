namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="PaddingWidget"/>.
/// </summary>
public static class PaddingExtensions
{
    /// <summary>
    /// Creates a Padding wrapper with per-side values around a single child widget.
    /// </summary>
    public static PaddingWidget Padding<TParent>(
        this WidgetContext<TParent> ctx,
        int left, int right, int top, int bottom,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(left, right, top, bottom, child);

    /// <summary>
    /// Creates a Padding wrapper with per-side values and a builder for a single child.
    /// </summary>
    public static PaddingWidget Padding<TParent>(
        this WidgetContext<TParent> ctx,
        int left, int right, int top, int bottom,
        Func<WidgetContext<PaddingWidget>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<PaddingWidget>();
        return new PaddingWidget(left, right, top, bottom, builder(childCtx));
    }

    /// <summary>
    /// Creates a Padding wrapper with per-side values and an implicit VStack for multiple children.
    /// </summary>
    public static PaddingWidget Padding<TParent>(
        this WidgetContext<TParent> ctx,
        int left, int right, int top, int bottom,
        Func<WidgetContext<PaddingWidget>, Hex1bWidget[]> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<PaddingWidget>();
        return new PaddingWidget(left, right, top, bottom, new VStackWidget(builder(childCtx)));
    }

    /// <summary>
    /// Creates a Padding wrapper with uniform padding on all sides.
    /// </summary>
    public static PaddingWidget Padding<TParent>(
        this WidgetContext<TParent> ctx,
        int all,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(all, all, all, all, child);

    /// <summary>
    /// Creates a Padding wrapper with uniform horizontal and vertical padding.
    /// </summary>
    public static PaddingWidget Padding<TParent>(
        this WidgetContext<TParent> ctx,
        int horizontal, int vertical,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(horizontal, horizontal, vertical, vertical, child);
}
