namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="TextBlockWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable concise text widget creation within widget builder callbacks.
/// The returned <see cref="TextBlockWidget"/> can be further configured with overflow
/// methods like <see cref="Wrap"/> and <see cref="Ellipsis"/>, or size hints like
/// <see cref="SizeHintExtensions.FillWidth{TWidget}(TWidget)"/>.
/// </para>
/// </remarks>
/// <example>
/// <para>Using Text within a VStack:</para>
/// <code>
/// context.VStack(v =&gt; [
///     v.Text("Title"),
///     v.Text("Long description that wraps").Wrap(),
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
    /// <param name="context">The widget context.</param>
    /// <param name="text">The text content to display.</param>
    /// <returns>A new <see cref="TextBlockWidget"/> with default overflow behavior (Truncate).</returns>
    /// <example>
    /// <code>
    /// context.Text("Hello, World!")
    /// </code>
    /// </example>
    public static TextBlockWidget Text<TParent>(
        this WidgetContext<TParent> context,
        string text)
        where TParent : Hex1bWidget
        => new(text);

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Truncate"/>.
    /// Text is clipped by parent containers with no visual indicator.
    /// </summary>
    /// <param name="widget">The text widget to configure.</param>
    /// <returns>A new <see cref="TextBlockWidget"/> with Truncate overflow behavior.</returns>
    /// <remarks>This is the default behavior for text widgets.</remarks>
    /// <example>
    /// <code>
    /// v.Text("Long text that will be clipped").Truncate()
    /// </code>
    /// </example>
    public static TextBlockWidget Truncate(this TextBlockWidget widget)
        => widget with { Overflow = TextOverflow.Truncate };

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Wrap"/>.
    /// Text wraps to multiple lines at word boundaries.
    /// </summary>
    /// <param name="widget">The text widget to configure.</param>
    /// <returns>A new <see cref="TextBlockWidget"/> with Wrap overflow behavior.</returns>
    /// <example>
    /// <code>
    /// v.Text("This long paragraph will wrap to fit the available width").Wrap()
    /// </code>
    /// </example>
    public static TextBlockWidget Wrap(this TextBlockWidget widget)
        => widget with { Overflow = TextOverflow.Wrap };

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Ellipsis"/>.
    /// Text is truncated with "..." when it exceeds the available width.
    /// </summary>
    /// <param name="widget">The text widget to configure.</param>
    /// <returns>A new <see cref="TextBlockWidget"/> with Ellipsis overflow behavior.</returns>
    /// <example>
    /// <code>
    /// v.Text("Very long title that may be truncated...").Ellipsis()
    /// </code>
    /// </example>
    public static TextBlockWidget Ellipsis(this TextBlockWidget widget)
        => widget with { Overflow = TextOverflow.Ellipsis };
}
