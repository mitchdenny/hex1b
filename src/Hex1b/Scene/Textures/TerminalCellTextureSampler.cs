namespace Hex1b.Scene.Textures;

using Hex1b.Theming;

/// <summary>
/// Converts a terminal screen buffer (<see cref="TerminalCell"/> grid) into RGBA pixel
/// data for a <see cref="SceneTexture2D"/>.
/// </summary>
/// <remarks>
/// Each terminal cell is expanded into a small block of pixels
/// (<c>cellPixelWidth x cellPixelHeight</c>). Within that block the foreground colour is
/// blended over the background colour according to the glyph's ink coverage (see
/// <see cref="TerminalGlyphRasterizer"/>). This is a best-effort translation: block and
/// braille glyphs reconstruct faithfully, while ordinary text becomes a tinted smear.
/// </remarks>
public static class TerminalCellTextureSampler
{
    /// <summary>Default pixel width each terminal cell expands to.</summary>
    public const int DefaultCellPixelWidth = 2;

    /// <summary>Default pixel height each terminal cell expands to.</summary>
    public const int DefaultCellPixelHeight = 2;

    /// <summary>
    /// Creates a new <see cref="SceneTexture2D"/> sized to fit the buffer and fills it
    /// from the supplied cells.
    /// </summary>
    /// <param name="buffer">The cell buffer, indexed <c>[row, column]</c>.</param>
    /// <param name="width">Number of columns in the buffer.</param>
    /// <param name="height">Number of rows in the buffer.</param>
    /// <param name="cellPixelWidth">Pixels per cell horizontally.</param>
    /// <param name="cellPixelHeight">Pixels per cell vertically.</param>
    /// <param name="defaultForeground">Colour used when a cell's foreground is the terminal default.</param>
    /// <param name="defaultBackground">Colour used when a cell's background is the terminal default.</param>
    public static SceneTexture2D CreateTexture(
        TerminalCell[,] buffer,
        int width,
        int height,
        int cellPixelWidth = DefaultCellPixelWidth,
        int cellPixelHeight = DefaultCellPixelHeight,
        Hex1bColor? defaultForeground = null,
        Hex1bColor? defaultBackground = null)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Buffer dimensions must be positive.");
        if (cellPixelWidth <= 0 || cellPixelHeight <= 0)
            throw new ArgumentException("Cell pixel dimensions must be positive.");

        var texture = new SceneTexture2D(width * cellPixelWidth, height * cellPixelHeight);
        SampleInto(texture, buffer, width, height, cellPixelWidth, cellPixelHeight, defaultForeground, defaultBackground);
        return texture;
    }

    /// <summary>
    /// Fills an existing texture from the supplied cells. The texture must already be sized
    /// <c>(width * cellPixelWidth) x (height * cellPixelHeight)</c>.
    /// </summary>
    public static void SampleInto(
        SceneTexture2D texture,
        TerminalCell[,] buffer,
        int width,
        int height,
        int cellPixelWidth = DefaultCellPixelWidth,
        int cellPixelHeight = DefaultCellPixelHeight,
        Hex1bColor? defaultForeground = null,
        Hex1bColor? defaultBackground = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(buffer);

        var fgDefault = defaultForeground ?? Hex1bColor.White;
        var bgDefault = defaultBackground ?? Hex1bColor.Black;

        var bufRows = buffer.GetLength(0);
        var bufCols = buffer.GetLength(1);

        for (int cy = 0; cy < height; cy++)
        {
            for (int cx = 0; cx < width; cx++)
            {
                TerminalCell cell = (cy < bufRows && cx < bufCols)
                    ? buffer[cy, cx]
                    : TerminalCell.Empty;

                ResolveColors(cell, fgDefault, bgDefault, out var fg, out var bg);
                var glyph = cell.IsHidden ? " " : cell.Character;

                CellGlyphBlitter.Blit(texture, cx, cy, cellPixelWidth, cellPixelHeight, glyph, fg, bg);
            }
        }
    }

    private static void ResolveColors(
        TerminalCell cell,
        Hex1bColor fgDefault,
        Hex1bColor bgDefault,
        out Hex1bColor foreground,
        out Hex1bColor background)
    {
        var fg = cell.Foreground is { IsDefault: false } f ? f : fgDefault;
        var bg = cell.Background is { IsDefault: false } b ? b : bgDefault;

        if (cell.IsReverse)
        {
            (fg, bg) = (bg, fg);
        }

        foreground = fg;
        background = bg;
    }
}
