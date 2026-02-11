namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="AlignWidget"/>.
/// </summary>
/// <remarks>
/// Provides fluent API methods for aligning child widgets within available space.
/// Use <see cref="Align{TParent}(WidgetContext{TParent}, Alignment, Hex1bWidget)"/> for
/// specific alignments or <see cref="Center{TParent}(WidgetContext{TParent}, Hex1bWidget)"/>
/// as a convenience for centering.
/// </remarks>
/// <seealso cref="AlignWidget"/>
/// <seealso cref="Alignment"/>
public static class AlignExtensions
{
    /// <summary>
    /// Aligns a child widget within the available space.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="alignment">The alignment flags (e.g., Alignment.Center, Alignment.TopRight).</param>
    /// <param name="child">The child widget to align.</param>
    /// <returns>An AlignWidget with the specified alignment.</returns>
    /// <example>
    /// <code>
    /// // Right-align a button
    /// ctx.Align(Alignment.Right, ctx.Button("OK"))
    /// 
    /// // Center a dialog
    /// ctx.Align(Alignment.Center, ctx.Border(b =&gt; [...]).Title("Dialog"))
    /// </code>
    /// </example>
    public static AlignWidget Align<TParent>(
        this WidgetContext<TParent> ctx,
        Alignment alignment,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child, alignment);

    /// <summary>
    /// Aligns a child widget within the available space using a builder.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="alignment">The alignment flags (e.g., Alignment.Center, Alignment.TopRight).</param>
    /// <param name="builder">A function that builds the child widget.</param>
    /// <returns>An AlignWidget with the specified alignment.</returns>
    public static AlignWidget Align<TParent>(
        this WidgetContext<TParent> ctx,
        Alignment alignment,
        Func<WidgetContext<AlignWidget>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<AlignWidget>();
        var child = builder(childCtx);
        return new AlignWidget(child, alignment);
    }

    /// <summary>
    /// Centers a child widget both horizontally and vertically.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to center.</param>
    /// <returns>An AlignWidget centered in both axes.</returns>
    /// <example>
    /// <code>
    /// ctx.Center(ctx.Text("Welcome!"))
    /// </code>
    /// </example>
    public static AlignWidget Center<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child, Alignment.Center);

    /// <summary>
    /// Centers a child widget both horizontally and vertically using a builder.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="builder">A function that builds the child widget.</param>
    /// <returns>An AlignWidget centered in both axes.</returns>
    public static AlignWidget Center<TParent>(
        this WidgetContext<TParent> ctx,
        Func<WidgetContext<AlignWidget>, Hex1bWidget> builder)
        where TParent : Hex1bWidget
    {
        var childCtx = new WidgetContext<AlignWidget>();
        var child = builder(childCtx);
        return new AlignWidget(child, Alignment.Center);
    }
}
