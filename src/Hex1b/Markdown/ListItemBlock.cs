namespace Hex1b.Markdown;

/// <summary>
/// A single item within a list.
/// </summary>
public sealed class ListItemBlock : MarkdownBlock
{
    /// <summary>
    /// The nested blocks inside the list item.
    /// </summary>
    public IReadOnlyList<MarkdownBlock> Children { get; }

    /// <summary>
    /// Task list checkbox state. <c>null</c> for normal list items,
    /// <c>true</c> for checked (<c>[x]</c>), <c>false</c> for unchecked (<c>[ ]</c>).
    /// </summary>
    public bool? IsChecked { get; }

    public ListItemBlock(IReadOnlyList<MarkdownBlock> children, bool? isChecked = null)
    {
        Children = children;
        IsChecked = isChecked;
    }
}
