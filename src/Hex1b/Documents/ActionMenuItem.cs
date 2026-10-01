using Hex1b.Documents;

namespace Hex1b.Widgets;

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
