using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Data;

/// <summary>
/// Represents a single tile's visual data — the content and colors to render.
/// </summary>
/// <param name="Content">The text content to render (may be multi-character for larger tiles).</param>
/// <param name="Foreground">The foreground color for the tile.</param>
/// <param name="Background">The background color for the tile.</param>
public readonly record struct TileData(
    string Content,
    Hex1bColor Foreground,
    Hex1bColor Background);
