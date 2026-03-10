using Hex1b.Documents;

namespace Hex1b.Widgets;

/// <summary>
/// A popup menu of selectable actions displayed at a document position.
/// Used for code actions, quick fixes, and rename operations.
/// </summary>
/// <param name="Anchor">The document position to anchor the menu near.</param>
/// <param name="Items">The selectable menu items.</param>
public record ActionMenu(
    DocumentPosition Anchor,
    IReadOnlyList<ActionMenuItem> Items)
{
    /// <summary>Optional title displayed at the top of the menu.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// A single item in an <see cref="ActionMenu"/>.
/// </summary>
/// <param name="Label">The display text for this item.</param>
/// <param name="Id">A unique identifier for this item.</param>
public record ActionMenuItem(string Label, string Id)
{
    /// <summary>Optional detail text shown after the label.</summary>
    public string? Detail { get; init; }

    /// <summary>Whether this item is marked as preferred/recommended.</summary>
    public bool IsPreferred { get; init; }
}
