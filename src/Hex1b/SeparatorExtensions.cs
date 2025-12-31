namespace Hex1b;

using Hex1b.Nodes;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="SeparatorWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable concise separator widget creation within widget builder callbacks.
/// Separators automatically adapt to their parent container: horizontal in VStack, vertical in HStack.
/// </para>
/// <para>
/// To customize separator characters, use a <see cref="ThemePanelWidget"/> with 
/// <see cref="Theming.SeparatorTheme.HorizontalChar"/> and <see cref="Theming.SeparatorTheme.VerticalChar"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Using Separator within a VStack:</para>
/// <code>
/// ctx.VStack(v =&gt; [
///     v.Text("Section 1"),
///     v.Separator(),
///     v.Text("Section 2")
/// ])
/// </code>
/// </example>
/// <seealso cref="SeparatorWidget"/>
public static class SeparatorExtensions
{
    /// <summary>
    /// Creates a <see cref="SeparatorWidget"/> that draws a line.
    /// The orientation is inferred from the parent container (horizontal in VStack, vertical in HStack).
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new <see cref="SeparatorWidget"/>.</returns>
    /// <example>
    /// <code>
    /// ctx.VStack(v =&gt; [
    ///     v.Text("Above"),
    ///     v.Separator(),
    ///     v.Text("Below")
    /// ])
    /// </code>
    /// </example>
    public static SeparatorWidget Separator<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a horizontal <see cref="SeparatorWidget"/> regardless of parent container.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new horizontal <see cref="SeparatorWidget"/>.</returns>
    public static SeparatorWidget HSeparator<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new() { ExplicitAxis = LayoutAxis.Vertical }; // Horizontal line in vertical layout

    /// <summary>
    /// Creates a vertical <see cref="SeparatorWidget"/> regardless of parent container.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new vertical <see cref="SeparatorWidget"/>.</returns>
    public static SeparatorWidget VSeparator<TParent>(this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new() { ExplicitAxis = LayoutAxis.Horizontal }; // Vertical line in horizontal layout
}
