namespace Hex1b.Markdown;

/// <summary>
/// Emphasis (bold or italic) wrapping inline content.
/// </summary>
public sealed class EmphasisInline : MarkdownInline
{
    /// <summary>
    /// Whether this is strong emphasis (bold) vs. regular emphasis (italic).
    /// </summary>
    public bool IsStrong { get; }

    /// <summary>
    /// The inline content within the emphasis.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Children { get; }

    public EmphasisInline(bool isStrong, IReadOnlyList<MarkdownInline> children)
    {
        IsStrong = isStrong;
        Children = children;
    }
}
