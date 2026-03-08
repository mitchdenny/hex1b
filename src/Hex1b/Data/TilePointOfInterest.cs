namespace Hex1b.Data;

/// <summary>
/// Represents a point of interest on a tile map, displayed as an icon at specific tile coordinates.
/// </summary>
/// <param name="X">The X coordinate in tile space.</param>
/// <param name="Y">The Y coordinate in tile space.</param>
/// <param name="Icon">The icon character or string to display (e.g., "📍", "🏠", "▶").</param>
/// <param name="Label">An optional text label displayed near the icon.</param>
/// <param name="Tag">Optional user data associated with this point of interest.</param>
public record TilePointOfInterest(
    double X,
    double Y,
    string Icon,
    string? Label = null,
    object? Tag = null);
