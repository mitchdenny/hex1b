namespace Hex1b.Scene.Textures;
using System.Diagnostics.CodeAnalysis;

using Hex1b.Theming;

/// <summary>
/// Shared low-level routine that expands a single resolved terminal/surface cell into a
/// block of texture pixels, blending the foreground colour over the background according
/// to the glyph's ink coverage (see <see cref="TerminalGlyphRasterizer"/>).
/// </summary>
/// <remarks>
/// Both <see cref="TerminalCellTextureSampler"/> (driven by <see cref="TerminalCell"/>) and
/// <see cref="SurfaceCellTextureSampler"/> (driven by
/// <see cref="Hex1b.Surfaces.SurfaceCell"/>) share this core so block and braille glyphs
/// reconstruct identically regardless of the cell source.
/// </remarks>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
internal static class CellGlyphBlitter
{
    /// <summary>
    /// Writes the pixels for one cell into <paramref name="texture"/>.
    /// </summary>
    /// <param name="texture">Destination texture.</param>
    /// <param name="cellX">Cell column index.</param>
    /// <param name="cellY">Cell row index.</param>
    /// <param name="cellPixelWidth">Pixels per cell horizontally.</param>
    /// <param name="cellPixelHeight">Pixels per cell vertically.</param>
    /// <param name="glyph">The grapheme cluster to rasterize (already resolved for hidden cells).</param>
    /// <param name="foreground">Resolved foreground colour.</param>
    /// <param name="background">Resolved background colour.</param>
    public static void Blit(
        SceneTexture2D texture,
        int cellX,
        int cellY,
        int cellPixelWidth,
        int cellPixelHeight,
        string glyph,
        Hex1bColor foreground,
        Hex1bColor background)
    {
        for (int sy = 0; sy < cellPixelHeight; sy++)
        {
            float fyN = (sy + 0.5f) / cellPixelHeight;
            int py = cellY * cellPixelHeight + sy;

            for (int sx = 0; sx < cellPixelWidth; sx++)
            {
                float fxN = (sx + 0.5f) / cellPixelWidth;
                var coverage = TerminalGlyphRasterizer.CoverageAt(glyph, fxN, fyN);

                byte r = Lerp(background.R, foreground.R, coverage);
                byte g = Lerp(background.G, foreground.G, coverage);
                byte b = Lerp(background.B, foreground.B, coverage);

                texture.SetPixel(cellX * cellPixelWidth + sx, py, r, g, b, 255);
            }
        }
    }

    private static byte Lerp(byte a, byte b, float t)
    {
        var value = a + (b - a) * t;
        return (byte)System.Math.Clamp((int)System.Math.Round(value), 0, 255);
    }
}
