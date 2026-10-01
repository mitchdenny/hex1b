using Hex1b.Widgets;

namespace Hex1b.Events;

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
