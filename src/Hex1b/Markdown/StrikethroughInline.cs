namespace Hex1b.Markdown;

/// <summary>
/// Strikethrough text (~~text~~).
/// </summary>
public sealed class StrikethroughInline : MarkdownInline
{
    /// <summary>
    /// The inline content within the strikethrough.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Children { get; }

    public StrikethroughInline(IReadOnlyList<MarkdownInline> children)
    {
        Children = children;
    }
}
