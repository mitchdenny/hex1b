using SkiaSharp;
using Svg.Skia;

namespace Hex1b.Website;

/// <summary>
/// Encodes images to Sixel format for terminal display.
/// Sixel is a bitmap graphics format used by some terminals like xterm (with -ti option),
/// mlterm, and others.
/// </summary>
public static class SixelEncoder
{
    /// <summary>
    /// Default cell width in pixels (typical terminal character cell).
    /// </summary>
    private const int DefaultCellWidth = 10;

    /// <summary>
    /// Default cell height in pixels (typical terminal character cell).
    /// </summary>
    private const int DefaultCellHeight = 20;

    /// <summary>
    /// Maximum number of colors in the Sixel palette.
    /// Most terminals support 256 colors.
    /// </summary>
    private const int MaxColors = 256;

    /// <summary>
    /// Loads an image from a file path and encodes it to Sixel format.
    /// Supports common raster formats (PNG, JPEG, GIF, BMP, WebP) and SVG.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="widthCells">Target width in terminal character cells.</param>
    /// <param name="heightCells">Target height in terminal character cells.</param>
    /// <param name="cellWidth">Width of a terminal character cell in pixels.</param>
    /// <param name="cellHeight">Height of a terminal character cell in pixels.</param>
    /// <returns>The Sixel-encoded string ready for terminal output.</returns>
    public static string EncodeFromFile(
        string imagePath,
        int widthCells,
        int heightCells,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        
        // Handle SVG files specially
        if (extension == ".svg")
        {
            return EncodeFromSvg(imagePath, widthCells, heightCells, cellWidth, cellHeight);
        }
        
        // Handle raster images
        using var stream = File.OpenRead(imagePath);
        using var bitmap = SKBitmap.Decode(stream);
        
        if (bitmap == null)
            throw new InvalidOperationException($"Failed to decode image: {imagePath}");

        return EncodeFromBitmap(bitmap, widthCells, heightCells, cellWidth, cellHeight);
    }

    /// <summary>
    /// Loads an SVG file and encodes it to Sixel format.
    /// The SVG is rasterized at the target resolution for best quality.
    /// </summary>
    /// <param name="svgPath">Path to the SVG file.</param>
    /// <param name="widthCells">Target width in terminal character cells.</param>
    /// <param name="heightCells">Target height in terminal character cells.</param>
    /// <param name="cellWidth">Width of a terminal character cell in pixels.</param>
    /// <param name="cellHeight">Height of a terminal character cell in pixels.</param>
    /// <returns>The Sixel-encoded string ready for terminal output.</returns>
    public static string EncodeFromSvg(
        string svgPath,
        int widthCells,
        int heightCells,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight)
    {
        // Calculate target pixel dimensions
        var targetWidth = widthCells * cellWidth;
        var targetHeight = heightCells * cellHeight;

        using var svg = new SKSvg();
        svg.Load(svgPath);
        
        if (svg.Picture == null)
            throw new InvalidOperationException($"Failed to load SVG: {svgPath}");

        // Get the SVG's natural bounds
        var bounds = svg.Picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidOperationException($"SVG has invalid dimensions: {svgPath}");

        // Calculate scale to fit within target while maintaining aspect ratio
        var scaleX = targetWidth / bounds.Width;
        var scaleY = targetHeight / bounds.Height;
        var scale = Math.Min(scaleX, scaleY);

        var renderWidth = (int)Math.Max(1, bounds.Width * scale);
        var renderHeight = (int)Math.Max(1, bounds.Height * scale);

        // Create bitmap at the target size
        using var bitmap = new SKBitmap(renderWidth, renderHeight);
        using var canvas = new SKCanvas(bitmap);
        
        // Clear with transparent background
        canvas.Clear(SKColors.Transparent);
        
        // Scale and draw the SVG
        canvas.Scale((float)scale);
        canvas.DrawPicture(svg.Picture);

        // Build the Sixel output (no further resizing needed)
        return BuildSixelString(bitmap);
    }

    /// <summary>
    /// Encodes a SkiaSharp bitmap to Sixel format.
    /// </summary>
    /// <param name="bitmap">The source bitmap.</param>
    /// <param name="widthCells">Target width in terminal character cells.</param>
    /// <param name="heightCells">Target height in terminal character cells.</param>
    /// <param name="cellWidth">Width of a terminal character cell in pixels.</param>
    /// <param name="cellHeight">Height of a terminal character cell in pixels.</param>
    /// <returns>The Sixel-encoded string ready for terminal output.</returns>
    public static string EncodeFromBitmap(
        SKBitmap bitmap,
        int widthCells,
        int heightCells,
        int cellWidth = DefaultCellWidth,
        int cellHeight = DefaultCellHeight)
    {
        // Calculate target pixel dimensions
        var targetWidth = widthCells * cellWidth;
        var targetHeight = heightCells * cellHeight;

        // Resize the image to fit the target dimensions while maintaining aspect ratio
        using var resized = ResizeImage(bitmap, targetWidth, targetHeight);

        // Build the Sixel output
        return BuildSixelString(resized);
    }

    /// <summary>
    /// Encodes raw pixel data to Sixel format.
    /// </summary>
    /// <param name="pixels">RGBA pixel data (4 bytes per pixel).</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>The Sixel-encoded string ready for terminal output.</returns>
    public static string EncodeFromPixels(byte[] pixels, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        
        unsafe
        {
            fixed (byte* ptr = pixels)
            {
                bitmap.SetPixels((IntPtr)ptr);
            }
        }

        return BuildSixelString(bitmap);
    }

    /// <summary>
    /// Resizes an image to fit within the target dimensions while maintaining aspect ratio.
    /// </summary>
    private static SKBitmap ResizeImage(SKBitmap source, int maxWidth, int maxHeight)
    {
        // Calculate the scale factor to fit within bounds while maintaining aspect ratio
        var scaleX = (float)maxWidth / source.Width;
        var scaleY = (float)maxHeight / source.Height;
        var scale = Math.Min(scaleX, scaleY);

        var newWidth = (int)(source.Width * scale);
        var newHeight = (int)(source.Height * scale);

        // Ensure minimum size
        newWidth = Math.Max(1, newWidth);
        newHeight = Math.Max(1, newHeight);

        var resized = new SKBitmap(newWidth, newHeight);
        using var canvas = new SKCanvas(resized);
        canvas.Clear(SKColors.Transparent);
        
        // Use high-quality sampling for resize
        using var paint = new SKPaint
        {
            IsAntialias = true
        };
        var destRect = new SKRect(0, 0, newWidth, newHeight);
        canvas.DrawBitmap(source, destRect, paint);
        return resized;
    }

    /// <summary>
    /// Builds the complete Sixel string from a bitmap.
    /// </summary>
    private static string BuildSixelString(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // Extract pixel data and build color palette
        var (palette, indexedPixels) = BuildPalette(bitmap);

        var sb = new System.Text.StringBuilder();

        // DCS (Device Control String) introducer with Sixel parameters
        // Format: DCS P1 ; P2 ; P3 q
        // P1 = pixel aspect ratio (7 = 1:1)
        // P2 = background select (0 = device default, 1 = no change)
        // P3 = horizontal grid size (0 = default)
        sb.Append("\x1bP0;1;0q");

        // Set raster attributes: "width;height
        // This helps the terminal know the image dimensions
        sb.Append($"\"1;1;{width};{height}");

        // Output color definitions
        for (int i = 0; i < palette.Count; i++)
        {
            var color = palette[i];
            // Color definition: #Pc;Pu;Px;Py;Pz
            // Pc = color number
            // Pu = color coordinate system (2 = RGB)
            // Px, Py, Pz = RGB values (0-100)
            var r = (int)(color.Red / 255.0 * 100);
            var g = (int)(color.Green / 255.0 * 100);
            var b = (int)(color.Blue / 255.0 * 100);
            sb.Append($"#{i};2;{r};{g};{b}");
        }

        // Output sixel data
        // Sixels are encoded in bands of 6 vertical pixels
        var numBands = (height + 5) / 6;

        for (int band = 0; band < numBands; band++)
        {
            var bandStart = band * 6;

            // For each color in the palette, output the pixels for this band
            for (int colorIndex = 0; colorIndex < palette.Count; colorIndex++)
            {
                var runData = BuildColorRunForBand(
                    indexedPixels, width, height, bandStart, colorIndex);

                if (runData.Length > 0)
                {
                    sb.Append($"#{colorIndex}");
                    sb.Append(runData);
                    sb.Append('$'); // Carriage return - go back to start of band
                }
            }

            if (band < numBands - 1)
            {
                sb.Append('-'); // Line feed - move to next band
            }
        }

        // ST (String Terminator)
        sb.Append("\x1b\\");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a color palette from the image using color quantization.
    /// </summary>
    private static (List<SKColor> palette, int[,] indexedPixels) BuildPalette(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var indexedPixels = new int[width, height];

        // Collect all unique colors and their frequencies
        var colorCounts = new Dictionary<SKColor, int>();
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                
                // Skip fully transparent pixels
                if (color.Alpha < 128)
                {
                    indexedPixels[x, y] = -1; // Mark as transparent
                    continue;
                }

                // Quantize color to reduce palette size (reduce to 6 bits per channel)
                var quantized = new SKColor(
                    (byte)((color.Red >> 2) << 2),
                    (byte)((color.Green >> 2) << 2),
                    (byte)((color.Blue >> 2) << 2));

                if (!colorCounts.TryAdd(quantized, 1))
                {
                    colorCounts[quantized]++;
                }
            }
        }

        // Sort by frequency and take top MaxColors
        var palette = colorCounts
            .OrderByDescending(kv => kv.Value)
            .Take(MaxColors)
            .Select(kv => kv.Key)
            .ToList();

        // Build color index lookup
        var colorToIndex = new Dictionary<SKColor, int>();
        for (int i = 0; i < palette.Count; i++)
        {
            colorToIndex[palette[i]] = i;
        }

        // Map pixels to palette indices
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (indexedPixels[x, y] == -1)
                    continue; // Already marked as transparent

                var color = bitmap.GetPixel(x, y);
                var quantized = new SKColor(
                    (byte)((color.Red >> 2) << 2),
                    (byte)((color.Green >> 2) << 2),
                    (byte)((color.Blue >> 2) << 2));

                if (colorToIndex.TryGetValue(quantized, out var index))
                {
                    indexedPixels[x, y] = index;
                }
                else
                {
                    // Find closest color in palette
                    indexedPixels[x, y] = FindClosestColor(quantized, palette);
                }
            }
        }

        return (palette, indexedPixels);
    }

    /// <summary>
    /// Finds the closest color in the palette using Euclidean distance.
    /// </summary>
    private static int FindClosestColor(SKColor target, List<SKColor> palette)
    {
        var minDistance = double.MaxValue;
        var closestIndex = 0;

        for (int i = 0; i < palette.Count; i++)
        {
            var c = palette[i];
            var dr = target.Red - c.Red;
            var dg = target.Green - c.Green;
            var db = target.Blue - c.Blue;
            var distance = dr * dr + dg * dg + db * db;

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    /// <summary>
    /// Builds the sixel run-length encoded data for a single color in a band.
    /// </summary>
    private static string BuildColorRunForBand(
        int[,] indexedPixels,
        int width,
        int height,
        int bandStart,
        int colorIndex)
    {
        var sb = new System.Text.StringBuilder();
        var hasAnyPixels = false;

        int runLength = 0;
        char lastChar = '\0';

        for (int x = 0; x < width; x++)
        {
            // Build the sixel byte for this column (6 vertical pixels)
            int sixelValue = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                var y = bandStart + bit;
                if (y < height)
                {
                    var pixelColor = indexedPixels[x, y];
                    if (pixelColor == colorIndex)
                    {
                        sixelValue |= (1 << bit);
                    }
                }
            }

            if (sixelValue != 0)
            {
                hasAnyPixels = true;
            }

            // Sixel character is base 63 ('?') plus the 6-bit value
            var sixelChar = (char)(63 + sixelValue);

            // Run-length encoding
            if (sixelChar == lastChar)
            {
                runLength++;
            }
            else
            {
                if (runLength > 0)
                {
                    AppendRun(sb, lastChar, runLength);
                }
                lastChar = sixelChar;
                runLength = 1;
            }
        }

        // Append final run
        if (runLength > 0)
        {
            AppendRun(sb, lastChar, runLength);
        }

        return hasAnyPixels ? sb.ToString() : "";
    }

    /// <summary>
    /// Appends a run-length encoded sixel sequence.
    /// </summary>
    private static void AppendRun(System.Text.StringBuilder sb, char c, int count)
    {
        if (count <= 3)
        {
            // Short runs: just repeat the character
            sb.Append(new string(c, count));
        }
        else
        {
            // Longer runs: use repeat introducer !<count><char>
            sb.Append($"!{count}{c}");
        }
    }
}
