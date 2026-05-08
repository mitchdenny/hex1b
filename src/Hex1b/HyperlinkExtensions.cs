namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating and configuring <see cref="HyperlinkWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable concise hyperlink widget creation within widget builder callbacks.
/// The returned <see cref="HyperlinkWidget"/> can be further configured with overflow
/// methods like <see cref="Wrap"/> and <see cref="Ellipsis"/>, click handlers, or size hints.
/// </para>
/// </remarks>
/// <example>
/// <para>Using Hyperlink within a VStack:</para>
/// <code>
/// context.VStack(v =&gt; [
///     v.Hyperlink("Click here", "https://example.com"),
///     v.Hyperlink("Long link text that wraps", "https://example.com").Wrap()
/// ])
/// </code>
/// </example>
/// <seealso cref="HyperlinkWidget"/>
/// <seealso cref="TextOverflow"/>
public static class HyperlinkExtensions
{
    /// <summary>
    /// Creates a <see cref="HyperlinkWidget"/> with the specified text and URI.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="text">The visible text to display.</param>
    /// <param name="uri">The URI to link to when clicked.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> with default overflow behavior (Truncate).</returns>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> context,
        string text,
        string uri)
        where TParent : Hex1bWidget
        => new(text, uri);

    /// <summary>
    /// Creates a <see cref="HyperlinkWidget"/> with the specified text, URI, and click handler.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="text">The visible text to display.</param>
    /// <param name="uri">The URI to link to when clicked.</param>
    /// <param name="onClick">The click handler to invoke when the hyperlink is activated.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> configured with the click handler.</returns>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> context,
        string text,
        string uri,
        Action<HyperlinkClickedEventArgs> onClick)
        where TParent : Hex1bWidget
        => new HyperlinkWidget(text, uri).OnClick(onClick);

    /// <summary>
    /// Creates a <see cref="HyperlinkWidget"/> with the specified text, URI, and async click handler.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="text">The visible text to display.</param>
    /// <param name="uri">The URI to link to when clicked.</param>
    /// <param name="onClick">The async click handler to invoke when the hyperlink is activated.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> configured with the async click handler.</returns>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> context,
        string text,
        string uri,
        Func<HyperlinkClickedEventArgs, Task> onClick)
        where TParent : Hex1bWidget
        => new HyperlinkWidget(text, uri).OnClick(onClick);

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Truncate"/>.
    /// Text is clipped by parent containers with no visual indicator.
    /// </summary>
    /// <param name="widget">The hyperlink widget to configure.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> with Truncate overflow behavior.</returns>
    /// <remarks>This is the default behavior for hyperlink widgets.</remarks>
    public static HyperlinkWidget Truncate(this HyperlinkWidget widget)
        => widget with { Overflow = TextOverflow.Truncate };

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Wrap"/>.
    /// Text wraps to multiple lines at word boundaries.
    /// </summary>
    /// <param name="widget">The hyperlink widget to configure.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> with Wrap overflow behavior.</returns>
    public static HyperlinkWidget Wrap(this HyperlinkWidget widget)
        => widget with { Overflow = TextOverflow.Wrap };

    /// <summary>
    /// Sets the text overflow behavior to <see cref="TextOverflow.Ellipsis"/>.
    /// Text is truncated with "..." when it exceeds the available width.
    /// </summary>
    /// <param name="widget">The hyperlink widget to configure.</param>
    /// <returns>A new <see cref="HyperlinkWidget"/> with Ellipsis overflow behavior.</returns>
    public static HyperlinkWidget Ellipsis(this HyperlinkWidget widget)
        => widget with { Overflow = TextOverflow.Ellipsis };
}
