using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments raised when a link in a markdown widget is activated.
/// Set <see cref="Handled"/> to <c>true</c> to suppress default behavior
/// (e.g., opening a browser or scrolling to a heading).
/// </summary>
public sealed class MarkdownLinkActivatedEventArgs
{
    internal MarkdownLinkActivatedEventArgs(
        string url,
        string text,
        MarkdownLinkKind kind,
        MarkdownWidget widget)
    {
        Url = url;
        Text = text;
        Kind = kind;
        Widget = widget;
    }

    /// <summary>
    /// The URL of the activated link.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The visible text of the activated link.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The kind of link (external, intra-document, or custom).
    /// </summary>
    public MarkdownLinkKind Kind { get; }

    /// <summary>
    /// The source markdown widget that contains the activated link.
    /// </summary>
    public MarkdownWidget Widget { get; }

    /// <summary>
    /// Set to <c>true</c> to indicate the event has been handled and
    /// default behavior should be suppressed.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Classify a URL into a <see cref="MarkdownLinkKind"/>.
    /// </summary>
    internal static MarkdownLinkKind ClassifyUrl(string url)
    {
        if (url.StartsWith('#'))
            return MarkdownLinkKind.IntraDocument;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return MarkdownLinkKind.External;

        return MarkdownLinkKind.Custom;
    }
}

/// <summary>
/// Describes the kind of link activated in a markdown widget.
/// </summary>
public enum MarkdownLinkKind
{
    /// <summary>
    /// An external link (http:// or https://).
    /// Default behavior: open in the system browser.
    /// </summary>
    External,

    /// <summary>
    /// An intra-document link (#heading-slug).
    /// Default behavior: scroll to the referenced heading.
    /// </summary>
    IntraDocument,

    /// <summary>
    /// A link with a custom scheme (e.g., command:, mailto:).
    /// No default behavior — only the handler fires.
    /// </summary>
    Custom
}
