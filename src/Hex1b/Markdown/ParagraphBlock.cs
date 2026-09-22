namespace Hex1b.Markdown;

/// <summary>
/// A paragraph block containing inline content.
/// </summary>
public sealed class ParagraphBlock : MarkdownBlock
{
    /// <summary>
    /// The inline content of the paragraph.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; }

    /// <summary>
    /// The plain text content (inlines flattened to text).
    /// </summary>
    public string Text { get; }

    public ParagraphBlock(IReadOnlyList<MarkdownInline> inlines, string text)
    {
        Inlines = inlines;
        Text = text;
    }
}
