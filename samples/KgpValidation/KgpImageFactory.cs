namespace KgpValidation;

/// <summary>
/// Generates small deterministic images without native image dependencies.
/// </summary>
internal static class KgpImageFactory
{
    public static byte[] CreateRgbaGradient(int width, int height)
    {
        var data = Allocate(width, height, 4);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                data[offset] = Scale(x, width);
                data[offset + 1] = Scale(y, height);
                data[offset + 2] = (byte)(255 - data[offset]);
                data[offset + 3] = 255;
            }
        }
        return data;
    }

    public static byte[] CreateRgbBars(int width, int height)
    {
        (byte R, byte G, byte B)[] colors =
        [
            (255, 64, 64),
            (255, 192, 32),
            (64, 220, 96),
            (64, 180, 255),
            (96, 96, 255),
            (220, 80, 255),
        ];
        var data = Allocate(width, height, 3);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = colors[Math.Min(
                    colors.Length - 1,
                    x * colors.Length / width)];
                var offset = (y * width + x) * 3;
                data[offset] = color.R;
                data[offset + 1] = color.G;
                data[offset + 2] = color.B;
            }
        }
        return data;
    }

    public static byte[] CreateRgbaChecker(int width, int height)
    {
        var data = Allocate(width, height, 4);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var first = ((x / 8) + (y / 8)) % 2 == 0;
                var offset = (y * width + x) * 4;
                data[offset] = first ? (byte)30 : (byte)240;
                data[offset + 1] = first ? (byte)190 : (byte)70;
                data[offset + 2] = first ? (byte)240 : (byte)40;
                data[offset + 3] = 255;
            }
        }
        return data;
    }

    public static byte[] CreateRgbaBullseye(int width, int height)
    {
        var data = Allocate(width, height, 4);
        var centerX = (width - 1) / 2.0;
        var centerY = (height - 1) / 2.0;
        var radius = Math.Min(width, height) / 2.0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var distance = Math.Sqrt(
                    Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                var offset = (y * width + x) * 4;
                if (distance > radius)
                {
                    data[offset + 3] = 0;
                    continue;
                }

                var ring = (int)(distance / Math.Max(1, radius / 4));
                var bright = ring % 2 == 0;
                data[offset] = bright ? (byte)255 : (byte)25;
                data[offset + 1] = bright ? (byte)70 : (byte)180;
                data[offset + 2] = bright ? (byte)60 : (byte)255;
                data[offset + 3] = 255;
            }
        }
        return data;
    }

    public static byte[] CreateRgbaQuadrants(int width, int height)
    {
        var data = Allocate(width, height, 4);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var top = y < height / 2;
                var left = x < width / 2;
                var color = (top, left) switch
                {
                    (true, true) => (R: (byte)240, G: (byte)48, B: (byte)48),
                    (true, false) => (R: (byte)48, G: (byte)220, B: (byte)80),
                    (false, true) => (R: (byte)48, G: (byte)96, B: (byte)240),
                    _ => (R: (byte)250, G: (byte)210, B: (byte)40),
                };
                var offset = (y * width + x) * 4;
                data[offset] = color.R;
                data[offset + 1] = color.G;
                data[offset + 2] = color.B;
                data[offset + 3] = 255;
            }
        }
        return data;
    }

    public static byte[] CreateRgbaSolid(
        int width,
        int height,
        byte red,
        byte green,
        byte blue,
        byte alpha = 255)
    {
        var data = Allocate(width, height, 4);
        for (var offset = 0; offset < data.Length; offset += 4)
        {
            data[offset] = red;
            data[offset + 1] = green;
            data[offset + 2] = blue;
            data[offset + 3] = alpha;
        }
        return data;
    }

    private static byte[] Allocate(int width, int height, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        return new byte[checked(width * height * channels)];
    }

    private static byte Scale(int value, int extent)
        => extent <= 1
            ? (byte)0
            : (byte)(value * 255 / (extent - 1));
}
