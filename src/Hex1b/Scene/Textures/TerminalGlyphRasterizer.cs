namespace Hex1b.Scene.Textures;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Computes the per-pixel "ink coverage" of a terminal glyph so a single
/// <see cref="TerminalCell"/> can be expanded into a small block of texture pixels.
/// </summary>
/// <remarks>
/// <para>
/// Terminal cells are not single colours — they carry a glyph drawn in the foreground
/// colour on top of the background colour. To project a terminal faithfully onto a mesh
/// we need to know <em>where</em> inside each cell the foreground ink lands. This class
/// answers that question as a coverage value in <c>[0, 1]</c> for a normalized sub-cell
/// position, where <c>(0, 0)</c> is the top-left of the cell and <c>(1, 1)</c> is the
/// bottom-right.
/// </para>
/// <para>
/// The block-element and braille glyphs are handled exactly, because those are precisely
/// the glyphs the scene's own half-block and braille shaders emit — so sampling a nested
/// scene reconstructs its sub-cell pixel grid rather than smearing it. Ordinary text
/// glyphs fall back to a uniform approximate coverage.
/// </para>
/// </remarks>
[Experimental("HEX1B_SCENE", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/scene.md")]
public static class TerminalGlyphRasterizer
{
    /// <summary>Approximate ink coverage used for ordinary (non-block, non-braille) visible glyphs.</summary>
    public const float DefaultTextCoverage = 0.55f;

    /// <summary>
    /// Returns the foreground ink coverage of <paramref name="glyph"/> at the normalized
    /// sub-cell position (<paramref name="fx"/>, <paramref name="fy"/>), each in <c>[0, 1)</c>.
    /// </summary>
    /// <param name="glyph">The grapheme cluster from a <see cref="TerminalCell"/>.</param>
    /// <param name="fx">Horizontal position within the cell, 0 = left, approaching 1 = right.</param>
    /// <param name="fy">Vertical position within the cell, 0 = top, approaching 1 = bottom.</param>
    /// <returns>Ink coverage in <c>[0, 1]</c>; 0 means pure background, 1 means pure foreground.</returns>
    public static float CoverageAt(string? glyph, float fx, float fy)
    {
        fx = System.Math.Clamp(fx, 0f, 0.999999f);
        fy = System.Math.Clamp(fy, 0f, 0.999999f);

        if (string.IsNullOrEmpty(glyph))
            return 0f;

        var rune = char.ConvertToUtf32(glyph, 0);

        // Space / no-break space => pure background.
        if (rune == ' ' || rune == 0xA0 || rune == 0)
            return 0f;

        // Block Elements (U+2580 - U+259F): handled geometrically.
        if (rune >= 0x2580 && rune <= 0x259F)
            return BlockElementCoverage(rune, fx, fy);

        // Braille Patterns (U+2800 - U+28FF): 2x4 dot grid.
        if (rune >= 0x2800 && rune <= 0x28FF)
            return BrailleCoverage(rune, fx, fy);

        // Anything else visible: uniform approximation.
        return DefaultTextCoverage;
    }

    private static float BlockElementCoverage(int rune, float fx, float fy)
    {
        switch (rune)
        {
            case 0x2580: return fy < 0.5f ? 1f : 0f;            // ▀ upper half
            case 0x2584: return fy >= 0.5f ? 1f : 0f;           // ▄ lower half
            case 0x258C: return fx < 0.5f ? 1f : 0f;            // ▌ left half
            case 0x2590: return fx >= 0.5f ? 1f : 0f;           // ▐ right half
            case 0x2588: return 1f;                             // █ full block

            // Lower one-eighth .. seven-eighths blocks (fill upward from bottom).
            case 0x2581: return fy >= 7f / 8f ? 1f : 0f;        // ▁
            case 0x2582: return fy >= 6f / 8f ? 1f : 0f;        // ▂
            case 0x2583: return fy >= 5f / 8f ? 1f : 0f;        // ▃
            case 0x2585: return fy >= 3f / 8f ? 1f : 0f;        // ▅
            case 0x2586: return fy >= 2f / 8f ? 1f : 0f;        // ▆
            case 0x2587: return fy >= 1f / 8f ? 1f : 0f;        // ▇

            // Left blocks (fill rightward from left).
            case 0x2589: return fx < 7f / 8f ? 1f : 0f;         // ▉
            case 0x258A: return fx < 6f / 8f ? 1f : 0f;         // ▊
            case 0x258B: return fx < 5f / 8f ? 1f : 0f;         // ▋
            case 0x258D: return fx < 3f / 8f ? 1f : 0f;         // ▍
            case 0x258E: return fx < 2f / 8f ? 1f : 0f;         // ▎
            case 0x258F: return fx < 1f / 8f ? 1f : 0f;         // ▏

            case 0x2594: return fy < 1f / 8f ? 1f : 0f;         // ▔ upper one-eighth
            case 0x2595: return fx >= 7f / 8f ? 1f : 0f;        // ▕ right one-eighth

            // Shade blocks: uniform partial coverage.
            case 0x2591: return 0.25f;                          // ░ light
            case 0x2592: return 0.50f;                          // ▒ medium
            case 0x2593: return 0.75f;                          // ▓ dark

            // Quadrant blocks (U+2596 - U+259F).
            default: return QuadrantCoverage(rune, fx, fy);
        }
    }

    private static float QuadrantCoverage(int rune, float fx, float fy)
    {
        var left = fx < 0.5f;
        var top = fy < 0.5f;
        var upperLeft = top && left;
        var upperRight = top && !left;
        var lowerLeft = !top && left;
        var lowerRight = !top && !left;

        bool filled = rune switch
        {
            0x2596 => lowerLeft,                                        // ▖
            0x2597 => lowerRight,                                       // ▗
            0x2598 => upperLeft,                                        // ▘
            0x2599 => upperLeft || lowerLeft || lowerRight,             // ▙
            0x259A => upperLeft || lowerRight,                          // ▚
            0x259B => upperLeft || upperRight || lowerLeft,            // ▛
            0x259C => upperLeft || upperRight || lowerRight,           // ▜
            0x259D => upperRight,                                       // ▝
            0x259E => upperRight || lowerLeft,                          // ▞
            0x259F => upperRight || lowerLeft || lowerRight,           // ▟
            _ => false
        };

        return filled ? 1f : 0f;
    }

    private static float BrailleCoverage(int rune, float fx, float fy)
    {
        // Braille cell is 2 columns x 4 rows of dots.
        var pattern = rune - 0x2800;
        var col = fx < 0.5f ? 0 : 1;
        var row = (int)(fy * 4f);
        if (row > 3) row = 3;

        // Dot bit layout within a braille cell:
        //   col0      col1
        //   (1) 0x01  (4) 0x08   row0
        //   (2) 0x02  (5) 0x10   row1
        //   (3) 0x04  (6) 0x20   row2
        //   (7) 0x40  (8) 0x80   row3
        int bit = (col, row) switch
        {
            (0, 0) => 0x01,
            (0, 1) => 0x02,
            (0, 2) => 0x04,
            (1, 0) => 0x08,
            (1, 1) => 0x10,
            (1, 2) => 0x20,
            (0, 3) => 0x40,
            (1, 3) => 0x80,
            _ => 0
        };

        return (pattern & bit) != 0 ? 1f : 0f;
    }
}
