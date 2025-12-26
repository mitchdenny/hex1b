namespace Hex1b;

using Hex1b.Events;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating HyperlinkWidget.
/// Returns the widget directly - covariance allows use in collection expressions.
/// </summary>
public static class HyperlinkExtensions
{
    /// <summary>
    /// Creates a HyperlinkWidget with the specified text and URI.
    /// </summary>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        string uri,
        TextOverflow overflow = TextOverflow.Overflow)
        where TParent : Hex1bWidget
        => new(text, uri, overflow);

    /// <summary>
    /// Creates a HyperlinkWidget with the specified text, URI, and click handler.
    /// </summary>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        string uri,
        Action<HyperlinkClickedEventArgs> onClick)
        where TParent : Hex1bWidget
        => new HyperlinkWidget(text, uri).OnClick(onClick);

    /// <summary>
    /// Creates a HyperlinkWidget with the specified text, URI, and async click handler.
    /// </summary>
    public static HyperlinkWidget Hyperlink<TParent>(
        this WidgetContext<TParent> ctx,
        string text,
        string uri,
        Func<HyperlinkClickedEventArgs, Task> onClick)
        where TParent : Hex1bWidget
        => new HyperlinkWidget(text, uri).OnClick(onClick);
}
