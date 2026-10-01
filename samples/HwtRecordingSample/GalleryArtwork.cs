using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace HwtRecordingSample;

internal static class GalleryArtwork
{
    public const int Width = 320;
    public const int Height = 180;

    public static byte[] Landscape(bool night)
    {
        var pixels = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var horizon = y / (double)Height;
                var color = night
                    ? Rgba32.FromRgb((byte)(12 + 25 * horizon), (byte)(20 + 40 * horizon), (byte)(55 + 65 * horizon))
                    : Rgba32.FromRgb((byte)(245 - 50 * horizon), (byte)(110 + 65 * horizon), (byte)(85 + 75 * horizon));

                if (Math.Pow(x - 235, 2) + Math.Pow(y - 46, 2) < 25 * 25)
                    color = night ? Rgba32.FromRgb(230, 240, 255) : Rgba32.FromRgb(255, 230, 155);
                if (night && (x * 73 + y * 137) % 997 == 0 && y < 95)
                    color = Rgba32.FromRgb(195, 225, 255);
                if (y > 115 - 38 * Math.Sin(x * 0.017) - 18 * Math.Cos(x * 0.047))
                    color = night ? Rgba32.FromRgb(39, 55, 105) : Rgba32.FromRgb(120, 80, 130);
                if (y > 146 - 22 * Math.Cos(x * 0.024) + 14 * Math.Sin(x * 0.039))
                    color = night ? Rgba32.FromRgb(17, 32, 62) : Rgba32.FromRgb(52, 58, 93);

                var offset = (y * Width + x) * 4;
                pixels[offset] = color.R;
                pixels[offset + 1] = color.G;
                pixels[offset + 2] = color.B;
                pixels[offset + 3] = 255;
            }
        }
        return pixels;
    }

    public static SixelPixelBuffer Waves(bool night)
    {
        var pixels = new SixelPixelBuffer(Width, Height);
        Rgba32[] palette = night
            ? [Rgba32.FromRgb(15, 24, 65), Rgba32.FromRgb(38, 48, 100),
               Rgba32.FromRgb(62, 75, 145), Rgba32.FromRgb(100, 113, 185),
               Rgba32.FromRgb(150, 171, 220), Rgba32.FromRgb(209, 230, 255)]
            : [Rgba32.FromRgb(8, 60, 93), Rgba32.FromRgb(8, 95, 130),
               Rgba32.FromRgb(12, 133, 160), Rgba32.FromRgb(37, 174, 181),
               Rgba32.FromRgb(99, 213, 199), Rgba32.FromRgb(210, 248, 219)];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var wave = y + 14 * Math.Sin(x * 0.027 + (night ? 2 : 0))
                    + 9 * Math.Cos(x * 0.061 + y * 0.015);
                var band = ((int)Math.Floor(wave / 9) % palette.Length + palette.Length) % palette.Length;
                pixels[x, y] = palette[band];
            }
        }
        return pixels;
    }
}
