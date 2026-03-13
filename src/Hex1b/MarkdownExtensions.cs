namespace Hex1b;

using Hex1b.Documents;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="MarkdownWidget"/>.
/// </summary>
public static class MarkdownExtensions
{
    /// <summary>
    /// Creates a markdown widget that parses and renders the given markdown source.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="source">The markdown source text.</param>
    public static MarkdownWidget Markdown<TParent>(
        this WidgetContext<TParent> ctx,
        string source)
        where TParent : Hex1bWidget
        => new(source);

    /// <summary>
    /// Creates a markdown widget from a <see cref="ReadOnlyMemory{T}"/> source.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="source">The markdown source memory.</param>
    public static MarkdownWidget Markdown<TParent>(
        this WidgetContext<TParent> ctx,
        ReadOnlyMemory<char> source)
        where TParent : Hex1bWidget
        => new(source.Span.ToString());

    /// <summary>
    /// Creates a markdown widget backed by an <see cref="IHex1bDocument"/>.
    /// The document's <see cref="IHex1bDocument.Version"/> is used for efficient
    /// change detection; re-parsing only occurs when the document version advances.
    /// </summary>
    /// <param name="ctx">The widget context.</param>
    /// <param name="document">The document to render as markdown.</param>
    public static MarkdownWidget Markdown<TParent>(
        this WidgetContext<TParent> ctx,
        IHex1bDocument document)
        where TParent : Hex1bWidget
        => new(document);
}
