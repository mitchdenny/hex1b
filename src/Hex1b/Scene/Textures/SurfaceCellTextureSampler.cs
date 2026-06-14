namespace Hex1b.Scene.Textures;

using Hex1b.Surfaces;
using Hex1b.Theming;

/// <summary>
/// Converts a rendered <see cref="Surface"/> (grid of <see cref="SurfaceCell"/>) into RGBA
/// pixel data for a <see cref="SceneTexture2D"/>, so any widget rendered offscreen can be
/// projected onto mesh geometry.
/// </summary>
/// <remarks>
/// This is the surface-buffer counterpart of <see cref="TerminalCellTextureSampler"/>. It
/// shares the same cell-to-pixel core (<see cref="CellGlyphBlitter"/>), so block and braille
/// glyphs reconstruct identically. <see cref="SurfaceCell"/> foreground/background are
/// nullable (transparent); transparent channels fall back to the supplied defaults.
/// Continuation cells of wide glyphs are rendered blank — a documented POC simplification.
/// </remarks>
public static class SurfaceCellTextureSampler
{
    /// <summary>Default pixel width each cell expands to.</summary>
    public const int DefaultCellPixelWidth = 2;

    /// <summary>Default pixel height each cell expands to.</summary>
    public const int DefaultCellPixelHeight = 2;

    /// <summary>
    /// Creates a new <see cref="SceneTexture2D"/> sized to fit the surface and fills it.
    /// </summary>
    public static SceneTexture2D CreateTexture(
        Surface surface,
        int cellPixelWidth = DefaultCellPixelWidth,
        int cellPixelHeight = DefaultCellPixelHeight,
        Hex1bColor? defaultForeground = null,
        Hex1bColor? defaultBackground = null)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (cellPixelWidth <= 0 || cellPixelHeight <= 0)
            throw new ArgumentException("Cell pixel dimensions must be positive.");
        if (surface.Width <= 0 || surface.Height <= 0)
            throw new ArgumentException("Surface dimensions must be positive.");

        var texture = new SceneTexture2D(surface.Width * cellPixelWidth, surface.Height * cellPixelHeight);
        SampleInto(texture, surface, cellPixelWidth, cellPixelHeight, defaultForeground, defaultBackground);
        return texture;
    }

    /// <summary>
    /// Fills an existing texture from the supplied surface. The texture must already be sized
    /// <c>(surface.Width * cellPixelWidth) x (surface.Height * cellPixelHeight)</c>.
    /// </summary>
    public static void SampleInto(
        SceneTexture2D texture,
        Surface surface,
        int cellPixelWidth = DefaultCellPixelWidth,
        int cellPixelHeight = DefaultCellPixelHeight,
        Hex1bColor? defaultForeground = null,
        Hex1bColor? defaultBackground = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(surface);

        var fgDefault = defaultForeground ?? Hex1bColor.White;
        var bgDefault = defaultBackground ?? Hex1bColor.Black;

        for (int cy = 0; cy < surface.Height; cy++)
        {
            for (int cx = 0; cx < surface.Width; cx++)
            {
                var cell = surface.GetCell(cx, cy);

                ResolveColors(cell, fgDefault, bgDefault, out var fg, out var bg);

                // Continuation cells of wide glyphs carry no glyph of their own; render blank.
                var glyph = cell.IsContinuation ? " " : cell.Character;

                CellGlyphBlitter.Blit(texture, cx, cy, cellPixelWidth, cellPixelHeight, glyph, fg, bg);
            }
        }
    }

    private static void ResolveColors(
        SurfaceCell cell,
        Hex1bColor fgDefault,
        Hex1bColor bgDefault,
        out Hex1bColor foreground,
        out Hex1bColor background)
    {
        var fg = cell.Foreground is { } f ? f : fgDefault;
        var bg = cell.Background is { } b ? b : bgDefault;

        if ((cell.Attributes & CellAttributes.Reverse) != 0)
        {
            (fg, bg) = (bg, fg);
        }

        foreground = fg;
        background = bg;
    }
}
