using System.Text.RegularExpressions;

namespace Hex1b.Terminal.Testing;

/// <summary>
/// Decodes Sixel graphics data to raw pixel arrays for SVG rendering.
/// </summary>
/// <remarks>
/// <para>
/// Sixel is a graphics format where each "sixel" represents a column of 6 vertical pixels.
/// The data includes a color palette definition followed by sixel character data.
/// </para>
/// <para>
/// This decoder is used to convert sixel data embedded in terminal snapshots
/// into images that can be embedded in SVG output for visual testing.
/// </para>
/// </remarks>
public static class SixelDecoder
{
    /// <summary>
    /// Decodes a Sixel DCS payload to raw RGBA pixel data.
    /// </summary>
    /// <param name="payload">The Sixel payload (including or excluding DCS wrapper).</param>
    /// <param name="cellWidth">The width of a terminal cell in pixels.</param>
    /// <param name="cellHeight">The height of a terminal cell in pixels.</param>
    /// <returns>Decoded image with RGBA pixel data, or null if decoding fails.</returns>
    public static SixelImage? Decode(string payload, int cellWidth = 9, int cellHeight = 18)
    {
        if (string.IsNullOrEmpty(payload))
            return null;

        try
        {
            // Strip DCS wrapper if present: ESC P ... q <data> ESC \
            var sixelData = StripDcsWrapper(payload);
            if (string.IsNullOrEmpty(sixelData))
                return null;

            // Parse the sixel data
            return ParseSixelData(sixelData, cellWidth, cellHeight);
        }
        catch
        {
            // If parsing fails, return null (graceful degradation)
            return null;
        }
    }

    private static string StripDcsWrapper(string payload)
    {
        // DCS starts with ESC P or 0x90, ends with ESC \ or 0x9C
        var start = payload.IndexOf('q');
        if (start < 0)
        {
            // No 'q' found - might be raw sixel data without header
            return payload;
        }

        // Find the end - ESC \ (0x1B 0x5C) or ST (0x9C)
        var end = payload.IndexOf("\x1b\\", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = payload.IndexOf('\x9C', start);
        }
        if (end < 0)
        {
            end = payload.Length;
        }

        return payload.Substring(start + 1, end - start - 1);
    }

    private static SixelImage? ParseSixelData(string data, int cellWidth, int cellHeight)
    {
        // Parse color palette and sixel data
        var palette = new Dictionary<int, (byte R, byte G, byte B)>();
        
        // Default palette (standard 16 colors)
        InitializeDefaultPalette(palette);

        // Temporary storage for sixel rows
        // Each sixel represents 6 vertical pixels
        var rows = new List<List<int>>(); // List of rows, each row is list of color indices
        var currentRow = new List<int>();
        var currentX = 0;
        var maxWidth = 0;
        var currentColorIndex = 0;

        var i = 0;
        while (i < data.Length)
        {
            var ch = data[i];

            if (ch == '#')
            {
                // Color definition or selection: #<colorIndex>[;<type>;<p1>;<p2>;<p3>]
                i++;
                var colorData = ParseColorCommand(data, ref i);
                if (colorData.HasValue)
                {
                    var (colorIndex, r, g, b, isDefinition) = colorData.Value;
                    if (isDefinition)
                    {
                        palette[colorIndex] = (r, g, b);
                    }
                    currentColorIndex = colorIndex;
                }
            }
            else if (ch == '!')
            {
                // Repeat: !<count><sixel>
                i++;
                var (count, sixelChar) = ParseRepeat(data, ref i);
                for (int j = 0; j < count; j++)
                {
                    while (rows.Count < 6)
                        rows.Add([]);
                    
                    var sixelValue = sixelChar - '?';
                    for (int bit = 0; bit < 6; bit++)
                    {
                        var row = rows[bit];
                        while (row.Count <= currentX)
                            row.Add(-1); // Transparent
                        
                        if ((sixelValue & (1 << bit)) != 0)
                        {
                            row[currentX] = currentColorIndex;
                        }
                    }
                    currentX++;
                    maxWidth = Math.Max(maxWidth, currentX);
                }
            }
            else if (ch == '$')
            {
                // Carriage return - go back to start of current band
                currentX = 0;
                i++;
            }
            else if (ch == '-')
            {
                // Graphics new line - move to next band of 6 rows
                currentX = 0;
                // Pad existing rows and start new band
                for (int j = 0; j < 6; j++)
                {
                    rows.Add([]);
                }
                i++;
            }
            else if (ch == '"')
            {
                // Raster attributes: "Pan;Pad;Ph;Pv - skip for now
                i++;
                while (i < data.Length && (char.IsDigit(data[i]) || data[i] == ';'))
                    i++;
            }
            else if (ch >= '?' && ch <= '~')
            {
                // Sixel character (value = ch - 63)
                while (rows.Count < 6)
                    rows.Add([]);
                
                var sixelValue = ch - '?';
                for (int bit = 0; bit < 6; bit++)
                {
                    var rowIndex = rows.Count - 6 + bit;
                    if (rowIndex < 0) continue;
                    
                    var row = rows[rowIndex];
                    while (row.Count <= currentX)
                        row.Add(-1); // Transparent
                    
                    if ((sixelValue & (1 << bit)) != 0)
                    {
                        row[currentX] = currentColorIndex;
                    }
                }
                currentX++;
                maxWidth = Math.Max(maxWidth, currentX);
                i++;
            }
            else
            {
                // Unknown character, skip
                i++;
            }
        }

        if (maxWidth == 0 || rows.Count == 0)
            return null;

        // Convert to RGBA pixels
        var height = rows.Count;
        var width = maxWidth;
        var pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            var row = y < rows.Count ? rows[y] : null;
            for (int x = 0; x < width; x++)
            {
                var pixelIndex = (y * width + x) * 4;
                var colorIndex = (row != null && x < row.Count) ? row[x] : -1;

                if (colorIndex >= 0 && palette.TryGetValue(colorIndex, out var color))
                {
                    pixels[pixelIndex] = color.R;
                    pixels[pixelIndex + 1] = color.G;
                    pixels[pixelIndex + 2] = color.B;
                    pixels[pixelIndex + 3] = 255; // Fully opaque
                }
                else
                {
                    // Transparent
                    pixels[pixelIndex] = 0;
                    pixels[pixelIndex + 1] = 0;
                    pixels[pixelIndex + 2] = 0;
                    pixels[pixelIndex + 3] = 0;
                }
            }
        }

        return new SixelImage(width, height, pixels);
    }

    private static (int colorIndex, byte r, byte g, byte b, bool isDefinition)? ParseColorCommand(string data, ref int i)
    {
        // Parse color index
        var colorIndexStr = "";
        while (i < data.Length && char.IsDigit(data[i]))
        {
            colorIndexStr += data[i++];
        }

        if (!int.TryParse(colorIndexStr, out var colorIndex))
            return null;

        // Check if this is a definition (has semicolons)
        if (i < data.Length && data[i] == ';')
        {
            i++; // Skip first semicolon
            
            // Parse color type (1 = HLS, 2 = RGB)
            var typeStr = "";
            while (i < data.Length && char.IsDigit(data[i]))
            {
                typeStr += data[i++];
            }
            
            if (!int.TryParse(typeStr, out var colorType))
                return (colorIndex, 0, 0, 0, false); // Just selection
            
            if (i < data.Length && data[i] == ';') i++;
            
            // Parse color values
            var values = new List<int>();
            for (int v = 0; v < 3; v++)
            {
                var valStr = "";
                while (i < data.Length && char.IsDigit(data[i]))
                {
                    valStr += data[i++];
                }
                if (int.TryParse(valStr, out var val))
                    values.Add(val);
                else
                    values.Add(0);
                
                if (i < data.Length && data[i] == ';') i++;
            }

            if (colorType == 2 && values.Count >= 3)
            {
                // RGB: values are 0-100 percentages
                var r = (byte)(values[0] * 255 / 100);
                var g = (byte)(values[1] * 255 / 100);
                var b = (byte)(values[2] * 255 / 100);
                return (colorIndex, r, g, b, true);
            }
            else if (colorType == 1 && values.Count >= 3)
            {
                // HLS: convert to RGB
                var (r, g, b) = HlsToRgb(values[0], values[1], values[2]);
                return (colorIndex, r, g, b, true);
            }
            
            return (colorIndex, 0, 0, 0, false);
        }

        return (colorIndex, 0, 0, 0, false); // Just selection, no definition
    }

    private static (int count, char sixelChar) ParseRepeat(string data, ref int i)
    {
        var countStr = "";
        while (i < data.Length && char.IsDigit(data[i]))
        {
            countStr += data[i++];
        }

        var count = 1;
        if (!string.IsNullOrEmpty(countStr))
        {
            int.TryParse(countStr, out count);
        }

        char sixelChar = '?'; // Default to empty sixel
        if (i < data.Length && data[i] >= '?' && data[i] <= '~')
        {
            sixelChar = data[i++];
        }

        return (count, sixelChar);
    }

    private static void InitializeDefaultPalette(Dictionary<int, (byte R, byte G, byte B)> palette)
    {
        // VT340 default 16-color palette (approximate)
        palette[0] = (0, 0, 0);       // Black
        palette[1] = (51, 51, 255);   // Blue
        palette[2] = (255, 51, 51);   // Red
        palette[3] = (51, 255, 51);   // Green
        palette[4] = (255, 51, 255);  // Magenta
        palette[5] = (51, 255, 255);  // Cyan
        palette[6] = (255, 255, 51);  // Yellow
        palette[7] = (250, 250, 250); // White
        palette[8] = (128, 128, 128); // Gray
        palette[9] = (102, 102, 255); // Light blue
        palette[10] = (255, 102, 102); // Light red
        palette[11] = (102, 255, 102); // Light green
        palette[12] = (255, 102, 255); // Light magenta
        palette[13] = (102, 255, 255); // Light cyan
        palette[14] = (255, 255, 102); // Light yellow
        palette[15] = (255, 255, 255); // Bright white
    }

    private static (byte R, byte G, byte B) HlsToRgb(int h, int l, int s)
    {
        // HLS values are 0-360 for H, 0-100 for L and S
        var hue = h / 360.0;
        var lightness = l / 100.0;
        var saturation = s / 100.0;

        double r, g, b;

        if (saturation == 0)
        {
            r = g = b = lightness;
        }
        else
        {
            var q = lightness < 0.5
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;

            r = HueToRgb(p, q, hue + 1.0 / 3);
            g = HueToRgb(p, q, hue);
            b = HueToRgb(p, q, hue - 1.0 / 3);
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
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
