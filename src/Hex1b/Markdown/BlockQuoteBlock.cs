namespace Hex1b.Markdown;

/// <summary>
/// A block quote (lines prefixed with &gt;).
/// </summary>
public sealed class BlockQuoteBlock : MarkdownBlock
{
    /// <summary>
    /// The nested blocks inside the block quote.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Children { get; }

    public BlockQuoteBlock(IReadOnlyList<MarkdownBlock> children)
    {
        Children = children;
    }
}
