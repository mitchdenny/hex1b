using Hex1b.Sixel;

namespace Hex1b.Automation;

/// <summary>
/// Decodes Sixel graphics data to raw pixel arrays for SVG rendering.
/// </summary>
/// <remarks>
/// This compatibility decoder consumes the authoritative incremental Sixel
/// parser result. It does not independently scan raw payload text.
/// </remarks>
public static class SixelDecoder
{
    private const int MaximumDecodedPixels = 16 * 1024 * 1024;
    private const long MaximumRasterOperations = MaximumDecodedPixels * 4L;

    /// <summary>
    /// Decodes a Sixel DCS payload to raw RGBA pixel data.
    /// </summary>
    /// <param name="payload">The Sixel payload (including or excluding DCS wrapper).</param>
    /// <param name="cellWidth">The width of a terminal cell in pixels.</param>
    /// <param name="cellHeight">The height of a terminal cell in pixels.</param>
    /// <returns>Decoded image with RGBA pixel data, or null if decoding cannot be retained safely.</returns>
    public static SixelImage? Decode(string payload, int cellWidth = 9, int cellHeight = 18)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        _ = cellWidth;
        _ = cellHeight;
        return Decode(SixelParser.ParsePayload(payload));
    }

    internal static SixelImage? Decode(SixelParseResult result)
    {
        if (!result.CommandsComplete ||
            result.Outcome is SixelParseOutcome.Cancelled or
                SixelParseOutcome.Malformed or
                SixelParseOutcome.Rejected)
        {
            return null;
        }

        var width = result.DeclaredExtent.Width;
        var height = result.DeclaredExtent.Height;
        var hasData = false;
        long rasterOperations = 0;
        foreach (var command in result.Commands)
        {
            if (command.Kind != SixelCommandKind.Data)
            {
                continue;
            }

            hasData = true;
            width = Math.Max(width, SaturatingAdd(command.X, command.RepeatCount));
            height = Math.Max(height, SaturatingMultiply(SaturatingAdd(command.Band, 1), 6));
            rasterOperations += (long)command.RepeatCount *
                System.Numerics.BitOperations.PopCount((uint)command.Value);
            if (rasterOperations > MaximumRasterOperations)
            {
                return null;
            }
        }

        if (!hasData || width <= 0 || height <= 0)
        {
            return null;
        }

        var pixelCount = (long)width * height;
        if (pixelCount > MaximumDecodedPixels)
        {
            return null;
        }

        var pixels = new byte[checked((int)pixelCount * 4)];
        var palette = new Dictionary<int, (byte R, byte G, byte B)>();
        InitializeDefaultPalette(palette);
        var selectedColor = 0;

        foreach (var command in result.Commands)
        {
            if (command.Kind == SixelCommandKind.Palette &&
                command.Palette is { } paletteCommand)
            {
                selectedColor = paletteCommand.Register;
                if (paletteCommand.IsDefinition)
                {
                    palette[paletteCommand.Register] = ConvertColor(paletteCommand);
                }
                continue;
            }

            if (command.Kind != SixelCommandKind.Data ||
                command.Value == 0 ||
                !palette.TryGetValue(selectedColor, out var color))
            {
                continue;
            }

            var endX = Math.Min(width, SaturatingAdd(command.X, command.RepeatCount));
            for (var x = command.X; x < endX; x++)
            {
                for (var bit = 0; bit < 6; bit++)
                {
                    if ((command.Value & (1 << bit)) == 0)
                    {
                        continue;
                    }

                    var y = SaturatingAdd(SaturatingMultiply(command.Band, 6), bit);
                    if (y >= height)
                    {
                        continue;
                    }

                    var pixelIndex = checked(((y * width) + x) * 4);
                    pixels[pixelIndex] = color.R;
                    pixels[pixelIndex + 1] = color.G;
                    pixels[pixelIndex + 2] = color.B;
                    pixels[pixelIndex + 3] = 255;
                }
            }
        }

        return new SixelImage(width, height, pixels);
    }

    private static (byte R, byte G, byte B) ConvertColor(SixelPaletteCommand command)
    {
        var x = command.X ?? 0;
        var y = command.Y ?? 0;
        var z = command.Z ?? 0;
        return command.ColorSpace switch
        {
            SixelColorSpace.Rgb => (
                PercentageToByte(x),
                PercentageToByte(y),
                PercentageToByte(z)),
            SixelColorSpace.Hls => HlsToRgb(x, y, z),
            _ => (0, 0, 0),
        };
    }

    private static byte PercentageToByte(int value) =>
        (byte)(Math.Clamp(value, 0, 100) * 255 / 100);

    private static void InitializeDefaultPalette(Dictionary<int, (byte R, byte G, byte B)> palette)
    {
        palette[0] = (0, 0, 0);
        palette[1] = (51, 51, 255);
        palette[2] = (255, 51, 51);
        palette[3] = (51, 255, 51);
        palette[4] = (255, 51, 255);
        palette[5] = (51, 255, 255);
        palette[6] = (255, 255, 51);
        palette[7] = (250, 250, 250);
        palette[8] = (128, 128, 128);
        palette[9] = (102, 102, 255);
        palette[10] = (255, 102, 102);
        palette[11] = (102, 255, 102);
        palette[12] = (255, 102, 255);
        palette[13] = (102, 255, 255);
        palette[14] = (255, 255, 102);
        palette[15] = (255, 255, 255);
    }

    private static (byte R, byte G, byte B) HlsToRgb(int h, int l, int s)
    {
        var hue = Math.Clamp(h, 0, 360) / 360.0;
        var lightness = Math.Clamp(l, 0, 100) / 100.0;
        var saturation = Math.Clamp(s, 0, 100) / 100.0;

        if (saturation == 0)
        {
            var component = (byte)(lightness * 255);
            return (component, component, component);
        }

        var q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;
        var p = 2 * lightness - q;
        return (
            (byte)(HueToRgb(p, q, hue + (1.0 / 3)) * 255),
            (byte)(HueToRgb(p, q, hue) * 255),
            (byte)(HueToRgb(p, q, hue - (1.0 / 3)) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }
        if (t > 1)
        {
            t -= 1;
        }
        if (t < 1.0 / 6)
        {
            return p + (q - p) * 6 * t;
        }
        if (t < 1.0 / 2)
        {
            return q;
        }
        if (t < 2.0 / 3)
        {
            return p + (q - p) * (2.0 / 3 - t) * 6;
        }
        return p;
    }

    private static int SaturatingAdd(int left, int right) =>
        right <= int.MaxValue - left ? left + right : int.MaxValue;

    private static int SaturatingMultiply(int left, int right) =>
        left <= int.MaxValue / right ? left * right : int.MaxValue;
}

/// <summary>
/// Represents a decoded Sixel image as RGBA pixel data.
/// </summary>
public sealed class SixelImage
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the raw RGBA pixel data (4 bytes per pixel: R, G, B, A).
    /// </summary>
    public byte[] Pixels { get; }

    /// <summary>
    /// Creates a new Sixel image.
    /// </summary>
    public SixelImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }
}
