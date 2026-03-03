using Hex1b.Theming;
using SkiaSharp;

namespace TileDemo;

/// <summary>
/// Converts a raster PNG tile image into a grid of <see cref="Hex1b.Data.TileData"/> cells
/// using Unicode half-block characters (▀▄█) for 1×2 sub-pixel resolution.
/// </summary>
/// <remarks>
/// Each terminal cell represents two vertically stacked pixels:
/// - Foreground color = top pixel
/// - Background color = bottom pixel
/// - Character chosen to display the correct visual blend
/// </remarks>
internal static class HalfBlockRenderer
{
    private const char UpperHalf = '▀';
    private const char LowerHalf = '▄';
    private const char FullBlock = '█';

    /// <summary>
    /// Decodes a PNG byte array and converts it to a character grid.
    /// Returns (characters, fgColors, bgColors) arrays of size (width, height/2).
    /// </summary>
    public static (string[,] Chars, Hex1bColor[,] Fg, Hex1bColor[,] Bg) Render(byte[] pngBytes)
    {
        using var bitmap = SKBitmap.Decode(pngBytes);
        if (bitmap is null)
            return CreatePlaceholder(256, 128, "decode error");

        var w = bitmap.Width;
        var h = bitmap.Height;
        var cellH = h / 2;

        var chars = new string[w, cellH];
        var fg = new Hex1bColor[w, cellH];
        var bg = new Hex1bColor[w, cellH];

        for (var cy = 0; cy < cellH; cy++)
        {
            var topRow = cy * 2;
            var botRow = topRow + 1;

            for (var cx = 0; cx < w; cx++)
            {
                var topPixel = bitmap.GetPixel(cx, topRow);
                var botPixel = botRow < h ? bitmap.GetPixel(cx, botRow) : topPixel;

                var topColor = ToHex1bColor(topPixel);
                var botColor = ToHex1bColor(botPixel);

                if (ColorsEqual(topPixel, botPixel))
                {
                    // Both pixels same color — full block with that color as bg
                    chars[cx, cy] = " ";
                    fg[cx, cy] = topColor;
                    bg[cx, cy] = topColor;
                }
                else
                {
                    // Different colors — upper half shows top pixel as fg, bottom as bg
                    chars[cx, cy] = UpperHalf.ToString();
                    fg[cx, cy] = topColor;
                    bg[cx, cy] = botColor;
                }
            }
        }

        return (chars, fg, bg);
    }

    /// <summary>
    /// Creates a placeholder grid for failed tile loads.
    /// </summary>
    public static (string[,] Chars, Hex1bColor[,] Fg, Hex1bColor[,] Bg) CreatePlaceholder(
        int width, int height, string message)
    {
        var chars = new string[width, height];
        var fg = new Hex1bColor[width, height];
        var bg = new Hex1bColor[width, height];
        var dimColor = Hex1bColor.FromRgb(40, 40, 40);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                chars[x, y] = " ";
                fg[x, y] = dimColor;
                bg[x, y] = dimColor;
            }
        }

        // Write message centered
        var msgY = height / 2;
        var msgX = (width - message.Length) / 2;
        for (var i = 0; i < message.Length && msgX + i < width; i++)
        {
            if (msgX + i >= 0)
            {
                chars[msgX + i, msgY] = message[i].ToString();
                fg[msgX + i, msgY] = Hex1bColor.FromRgb(100, 100, 100);
            }
        }

        return (chars, fg, bg);
    }

    private static Hex1bColor ToHex1bColor(SKColor c) =>
        Hex1bColor.FromRgb(c.Red, c.Green, c.Blue);

    private static bool ColorsEqual(SKColor a, SKColor b) =>
        a.Red == b.Red && a.Green == b.Green && a.Blue == b.Blue;
}
