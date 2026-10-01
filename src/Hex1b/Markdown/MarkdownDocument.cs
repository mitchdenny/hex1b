namespace Hex1b.Markdown;

/// <summary>
/// Represents a parsed markdown document as a list of block-level elements.
/// </summary>
public sealed class MarkdownDocument
{
    /// <summary>
    /// The top-level blocks in the document.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Blocks { get; }

    /// <summary>
    /// Reference link definitions collected from the document (case-insensitive keys).
    /// </summary>
    public IReadOnlyDictionary<string, LinkDefinition> LinkDefinitions { get; }

    public MarkdownDocument(
        IReadOnlyList<MarkdownBlock> blocks,
        IReadOnlyDictionary<string, LinkDefinition>? linkDefinitions = null)
    {
        Blocks = blocks;
        LinkDefinitions = linkDefinitions
            ?? new Dictionary<string, LinkDefinition>(StringComparer.OrdinalIgnoreCase);
    }
}
