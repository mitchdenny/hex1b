namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="TextBlockWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable concise text widget creation within widget builder callbacks.
/// The returned <see cref="TextBlockWidget"/> can be further configured with size hints
/// using methods like <see cref="SizeHintExtensions.FillWidth{TWidget}(TWidget)"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Using Text within a VStack:</para>
/// <code>
/// ctx.VStack(v =&gt; [
///     v.Text("Title"),
///     v.Text("Long description that wraps", TextOverflow.Wrap),
///     v.Text("Status: OK").FillWidth()
/// ])
/// </code>
/// </example>
/// <seealso cref="TextBlockWidget"/>
/// <seealso cref="TextOverflow"/>
public static class TextExtensions
{
    /// <summary>
    /// Creates a <see cref="TextBlockWidget"/> with the specified text content.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text content to display.</param>
    /// <returns>A new <see cref="TextBlockWidget"/> with default overflow behavior.</returns>
    /// <example>
    /// <code>
    /// ctx.Text("Hello, World!")
    /// </code>
    /// </example>
    public static TextBlockWidget Text<TParent>(
        this WidgetContext<TParent> ctx,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>
    /// Creates a <see cref="TextBlockWidget"/> with the specified text content and overflow behavior.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="text">The text content to display.</param>
    /// <param name="overflow">
    /// Controls how text handles horizontal overflow. See <see cref="TextOverflow"/> for options.
    /// </param>
    /// <returns>A new <see cref="TextBlockWidget"/> with the specified overflow behavior.</returns>
    /// <example>
    /// <para>Text that wraps to multiple lines:</para>
    /// <code>
    /// ctx.Text("This is a long paragraph that will wrap to fit the available width.", TextOverflow.Wrap)
    /// </code>
    /// <para>Text truncated with ellipsis:</para>
    /// <code>
    /// ctx.Text("Very long title that may be truncated...", TextOverflow.Ellipsis)
    /// </code>
    /// </example>
    public static TextBlockWidget Text<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        TextOverflow overflow)
        where TParent : Hex1bWidget
        => new(text, overflow);
}
