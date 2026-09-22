namespace Hex1b.Markdown;

/// <summary>
/// A heading block (h1-h6).
/// </summary>
public sealed class HeadingBlock : MarkdownBlock
{
    /// <summary>
    /// The heading level (1-6).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// The inline content of the heading.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; }

    /// <summary>
    /// The plain text content of the heading (inlines flattened to text).
    /// </summary>
    public string Text { get; }

    public HeadingBlock(int level, IReadOnlyList<MarkdownInline> inlines, string text)
    {
        Level = level;
        Inlines = inlines;
        Text = text;
    }
}
