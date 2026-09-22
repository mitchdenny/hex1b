using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for adding definitions to grid definition collections.
/// </summary>
public static class GridDefinitionCollectionExtensions
{
    /// <summary>
    /// Adds a column definition with the specified width hint.
    /// </summary>
    public static void Add(this GridDefinitionCollection<GridColumnDefinition> collection, SizeHint width)
        => collection.Add(new GridColumnDefinition(width));

    /// <summary>
    /// Adds a row definition with the specified height hint.
    /// </summary>
    public static void Add(this GridDefinitionCollection<GridRowDefinition> collection, SizeHint height)
        => collection.Add(new GridRowDefinition(height));
}
