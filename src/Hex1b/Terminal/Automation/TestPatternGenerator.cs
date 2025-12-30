namespace Hex1b.Terminal.Automation;

/// <summary>
/// Generates well-known test pattern images for verifying graphics rendering.
/// These patterns have predictable, easily verifiable outputs.
/// </summary>
public static class TestPatternGenerator
{
    /// <summary>
    /// Generates SMPTE-style color bars (simplified 7-bar version).
    /// Colors from left to right: White, Yellow, Cyan, Green, Magenta, Red, Blue.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateSmpteColorBars(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        
        // SMPTE colors (in order): White, Yellow, Cyan, Green, Magenta, Red, Blue
        var colors = new (byte R, byte G, byte B)[]
        {
            (255, 255, 255), // White
            (255, 255, 0),   // Yellow
            (0, 255, 255),   // Cyan
            (0, 255, 0),     // Green
            (255, 0, 255),   // Magenta
            (255, 0, 0),     // Red
            (0, 0, 255),     // Blue
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var barIndex = x * colors.Length / width;
                barIndex = Math.Clamp(barIndex, 0, colors.Length - 1);
                var color = colors[barIndex];
                
                var i = (y * width + x) * 4;
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Generates a 3x3 grid of primary and secondary colors.
    /// Layout:
    ///   Red    Green  Blue
    ///   Yellow Cyan   Magenta
    ///   Black  Gray   White
    /// </summary>
    /// <param name="width">Image width in pixels (should be divisible by 3).</param>
    /// <param name="height">Image height in pixels (should be divisible by 3).</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateColorGrid(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        
        // 3x3 grid of colors
        var colors = new (byte R, byte G, byte B)[,]
        {
            { (255, 0, 0),   (0, 255, 0),   (0, 0, 255) },     // Red, Green, Blue
            { (255, 255, 0), (0, 255, 255), (255, 0, 255) },   // Yellow, Cyan, Magenta
            { (0, 0, 0),     (128, 128, 128), (255, 255, 255) } // Black, Gray, White
        };

        for (int y = 0; y < height; y++)
        {
            var row = y * 3 / height;
            row = Math.Clamp(row, 0, 2);
            
            for (int x = 0; x < width; x++)
            {
                var col = x * 3 / width;
                col = Math.Clamp(col, 0, 2);
                
                var color = colors[row, col];
                var i = (y * width + x) * 4;
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Generates a horizontal grayscale gradient from black (left) to white (right).
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateGrayscaleGradient(int width, int height)
    {
        var pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var gray = (byte)(x * 255 / Math.Max(1, width - 1));
                var i = (y * width + x) * 4;
                pixels[i] = gray;
                pixels[i + 1] = gray;
                pixels[i + 2] = gray;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Generates RGB gradient bars (3 horizontal bands: R gradient, G gradient, B gradient).
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels (should be divisible by 3).</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateRgbGradients(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var bandHeight = height / 3;

        for (int y = 0; y < height; y++)
        {
            var band = y / bandHeight;
            band = Math.Clamp(band, 0, 2);
            
            for (int x = 0; x < width; x++)
            {
                var intensity = (byte)(x * 255 / Math.Max(1, width - 1));
                var i = (y * width + x) * 4;
                
                pixels[i] = band == 0 ? intensity : (byte)0;     // R
                pixels[i + 1] = band == 1 ? intensity : (byte)0; // G
                pixels[i + 2] = band == 2 ? intensity : (byte)0; // B
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Generates a checkerboard pattern with specified colors.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="squareSize">Size of each square in pixels.</param>
    /// <param name="color1">First color (default: black).</param>
    /// <param name="color2">Second color (default: white).</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateCheckerboard(
        int width, 
        int height, 
        int squareSize = 8,
        (byte R, byte G, byte B)? color1 = null,
        (byte R, byte G, byte B)? color2 = null)
    {
        var pixels = new byte[width * height * 4];
        var c1 = color1 ?? (0, 0, 0);
        var c2 = color2 ?? (255, 255, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var xSquare = x / squareSize;
                var ySquare = y / squareSize;
                var isEven = (xSquare + ySquare) % 2 == 0;
                var color = isEven ? c1 : c2;
                
                var i = (y * width + x) * 4;
                pixels[i] = color.R;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.B;
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// Generates a crosshair/registration mark pattern useful for alignment testing.
    /// White background with black crosshairs and corner markers.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>RGBA pixel data.</returns>
    public static byte[] GenerateRegistrationMarks(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        
        // Fill with white background
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 255;
            pixels[i + 2] = 255;
            pixels[i + 3] = 255;
        }

        var centerX = width / 2;
        var centerY = height / 2;
        var markerSize = Math.Min(width, height) / 8;

        // Draw center crosshair
        for (int x = 0; x < width; x++)
        {
            SetPixel(pixels, width, x, centerY, 0, 0, 0);
            if (centerY > 0) SetPixel(pixels, width, x, centerY - 1, 0, 0, 0);
        }
        for (int y = 0; y < height; y++)
        {
            SetPixel(pixels, width, centerX, y, 0, 0, 0);
            if (centerX > 0) SetPixel(pixels, width, centerX - 1, y, 0, 0, 0);
        }

        // Draw corner markers (red)
        DrawCornerMarker(pixels, width, height, 0, 0, markerSize, (255, 0, 0));
        DrawCornerMarker(pixels, width, height, width - markerSize, 0, markerSize, (0, 255, 0));
        DrawCornerMarker(pixels, width, height, 0, height - markerSize, markerSize, (0, 0, 255));
        DrawCornerMarker(pixels, width, height, width - markerSize, height - markerSize, markerSize, (255, 255, 0));

        return pixels;
    }

    /// <summary>
    /// Converts raw RGBA pixel data to a Sixel payload string.
    /// </summary>
    /// <param name="pixels">RGBA pixel data (4 bytes per pixel).</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>Complete Sixel DCS sequence.</returns>
    public static string ConvertToSixel(byte[] pixels, int width, int height)
    {
        var sb = new System.Text.StringBuilder();
        
        // DCS introducer with raster attributes
        sb.Append("\x1bPq");
        sb.Append($"\"1;1;{height};{width}"); // Raster attributes: Pan;Pad;Ph;Pv

        // Build color palette - quantize to 256 colors max
        var palette = BuildPalette(pixels, width, height, out var indexedPixels);
        
        // Output palette definitions
        for (int i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            var r = c.R * 100 / 255;
            var g = c.G * 100 / 255;
            var b = c.B * 100 / 255;
            sb.Append($"#{i};2;{r};{g};{b}");
        }

        // Output sixel data in bands of 6 rows
        var numBands = (height + 5) / 6;
        
        for (int band = 0; band < numBands; band++)
        {
            var bandStart = band * 6;
            
            for (int colorIndex = 0; colorIndex < palette.Count; colorIndex++)
            {
                var runData = BuildColorRun(indexedPixels, width, height, bandStart, colorIndex);
                if (runData.Length > 0)
                {
                    sb.Append($"#{colorIndex}");
                    sb.Append(runData);
                    sb.Append('$');
                }
            }
            
            if (band < numBands - 1)
            {
                sb.Append('-');
            }
        }

        // String terminator
        sb.Append("\x1b\\");
        
        return sb.ToString();
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, byte r, byte g, byte b)
    {
        var i = (y * width + x) * 4;
        if (i >= 0 && i + 3 < pixels.Length)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }
    }

    private static void DrawCornerMarker(byte[] pixels, int width, int height, int startX, int startY, int size, (byte R, byte G, byte B) color)
    {
        for (int dy = 0; dy < size && startY + dy < height; dy++)
        {
            for (int dx = 0; dx < size && startX + dx < width; dx++)
            {
                SetPixel(pixels, width, startX + dx, startY + dy, color.R, color.G, color.B);
            }
        }
    }

    private static List<(byte R, byte G, byte B)> BuildPalette(byte[] pixels, int width, int height, out int[,] indexedPixels)
    {
        indexedPixels = new int[width, height];
        var colorCounts = new Dictionary<(byte R, byte G, byte B), int>();
        
        // Count colors (quantize to 6-bit per channel for smaller palette)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var r = (byte)((pixels[i] >> 2) << 2);
                var g = (byte)((pixels[i + 1] >> 2) << 2);
                var b = (byte)((pixels[i + 2] >> 2) << 2);
                var color = (r, g, b);
                
                if (!colorCounts.TryAdd(color, 1))
                    colorCounts[color]++;
            }
        }

        // Take top 256 colors
        var palette = colorCounts
            .OrderByDescending(kv => kv.Value)
            .Take(256)
            .Select(kv => kv.Key)
            .ToList();

        // Build lookup
        var colorToIndex = new Dictionary<(byte R, byte G, byte B), int>();
        for (int i = 0; i < palette.Count; i++)
            colorToIndex[palette[i]] = i;

        // Map pixels to palette
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var r = (byte)((pixels[i] >> 2) << 2);
                var g = (byte)((pixels[i + 1] >> 2) << 2);
                var b = (byte)((pixels[i + 2] >> 2) << 2);
                var color = (r, g, b);
                
                if (colorToIndex.TryGetValue(color, out var idx))
                    indexedPixels[x, y] = idx;
                else
                    indexedPixels[x, y] = FindClosestColor(color, palette);
            }
        }

        return palette;
    }

    private static int FindClosestColor((byte R, byte G, byte B) target, List<(byte R, byte G, byte B)> palette)
    {
        var minDist = int.MaxValue;
        var closest = 0;
        
        for (int i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            var dr = target.R - c.R;
            var dg = target.G - c.G;
            var db = target.B - c.B;
            var dist = dr * dr + dg * dg + db * db;
            
            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }
        
        return closest;
    }

    private static string BuildColorRun(int[,] indexedPixels, int width, int height, int bandStart, int colorIndex)
    {
        var sb = new System.Text.StringBuilder();
        var hasPixels = false;
        var runLength = 0;
        var lastChar = '\0';

        for (int x = 0; x < width; x++)
        {
            var sixelValue = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                var y = bandStart + bit;
                if (y < height && indexedPixels[x, y] == colorIndex)
                {
                    sixelValue |= (1 << bit);
                }
            }

            if (sixelValue != 0)
                hasPixels = true;

            var sixelChar = (char)(63 + sixelValue);

            if (sixelChar == lastChar)
            {
                runLength++;
            }
            else
            {
                if (runLength > 0)
                    AppendRun(sb, lastChar, runLength);
                lastChar = sixelChar;
                runLength = 1;
            }
        }

        if (runLength > 0)
            AppendRun(sb, lastChar, runLength);

        return hasPixels ? sb.ToString() : "";
    }

    private static void AppendRun(System.Text.StringBuilder sb, char c, int count)
    {
        if (count <= 3)
            sb.Append(new string(c, count));
        else
            sb.Append($"!{count}{c}");
    }
}
