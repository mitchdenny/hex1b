namespace Hex1b.Markdown;

/// <summary>
/// A list block (ordered or unordered).
/// </summary>
public sealed class ListBlock : MarkdownBlock
{
    /// <summary>
    /// Whether this is an ordered list (1., 2., etc.) or unordered (-, *, +).
    /// </summary>
    public bool IsOrdered { get; }

    /// <summary>
    /// The starting number for ordered lists (usually 1).
    /// </summary>
    public int StartNumber { get; }

    /// <summary>
    /// The items in the list.
    /// </summary>
    public IReadOnlyList<ListItemBlock> Items { get; }

    public ListBlock(bool isOrdered, int startNumber, IReadOnlyList<ListItemBlock> items)
    {
        IsOrdered = isOrdered;
        StartNumber = startNumber;
        Items = items;
    }
}
