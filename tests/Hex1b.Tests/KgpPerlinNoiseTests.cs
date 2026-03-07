using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Complex KGP rendering tests using procedural Perlin noise images with
/// cropped placements in a checkerboard tiling pattern.
/// </summary>
public class KgpPerlinNoiseTests
{
    private const int TermWidth = 80;
    private const int TermHeight = 24;
    private const int CellW = 10;
    private const int CellH = 20;
    private const int TileCols = 4;
    private const int TileRows = 2;

    private static readonly TerminalCapabilities Caps = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        CellPixelWidth = CellW,
        CellPixelHeight = CellH,
    };

    /// <summary>
    /// Fills the entire terminal with a checkerboard of 4x2-cell tiles.
    /// Even tiles show grayscale Perlin noise, odd tiles show blue-tinted Perlin noise.
    /// Each tile is a cropped region of the full-screen image, so the noise pattern
    /// is spatially continuous across tile boundaries.
    /// </summary>
    [Fact]
    public void PerlinCheckerboard_FullScreen_GrayscaleAndBlueTiles()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(Caps)
            .WithDimensions(TermWidth, TermHeight)
            .Build();

        int imgW = TermWidth * CellW;   // 800
        int imgH = TermHeight * CellH;  // 480

        // Generate Perlin noise pixel data
        var grayPixels = GeneratePerlinNoiseRgb(imgW, imgH, scale: 60.0, seed: 42);
        var bluePixels = TintBlue(grayPixels, imgW, imgH);

        // Transmit grayscale image (id=1) using chunked transfer for large payload
        TransmitImage(terminal, imageId: 1, (uint)imgW, (uint)imgH, grayPixels);

        // Transmit blue-tinted image (id=2)
        TransmitImage(terminal, imageId: 2, (uint)imgW, (uint)imgH, bluePixels);

        // Place cropped tiles in checkerboard pattern
        int tilesX = TermWidth / TileCols;   // 20
        int tilesY = TermHeight / TileRows;  // 12

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                int col = tx * TileCols;
                int row = ty * TileRows;
                bool isEven = (tx + ty) % 2 == 0;
                uint imageId = isEven ? 1u : 2u;

                // Pixel crop region for this tile
                uint srcX = (uint)(col * CellW);
                uint srcY = (uint)(row * CellH);
                uint srcW = (uint)(TileCols * CellW);
                uint srcH = (uint)(TileRows * CellH);

                // Move cursor to tile position
                Send(terminal, $"\x1b[{row + 1};{col + 1}H");

                // Place cropped region of the image
                var ctrl = $"a=p,i={imageId},c={TileCols},r={TileRows},x={srcX},y={srcY},w={srcW},h={srcH},q=2";
                Send(terminal, KgpTestHelper.BuildCommand(ctrl));
            }
        }

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("kgp-perlin-checkerboard.svg", svg);

        // Verify all tiles were placed
        int expectedPlacements = tilesX * tilesY; // 240
        Assert.Equal(expectedPlacements, snapshot.KgpPlacements.Count);

        // Verify both images exist
        Assert.True(snapshot.KgpImages.ContainsKey(1), "Grayscale image should be stored");
        Assert.True(snapshot.KgpImages.ContainsKey(2), "Blue-tinted image should be stored");

        // Verify checkerboard pattern: alternating image IDs
        var topLeft = snapshot.KgpPlacements.First(p => p.Row == 0 && p.Column == 0);
        var topSecond = snapshot.KgpPlacements.First(p => p.Row == 0 && p.Column == TileCols);
        Assert.Equal(1u, topLeft.ImageId);
        Assert.Equal(2u, topSecond.ImageId);

        // Verify each tile has correct crop parameters
        foreach (var placement in snapshot.KgpPlacements)
        {
            Assert.Equal((uint)TileCols, placement.DisplayColumns);
            Assert.Equal((uint)TileRows, placement.DisplayRows);
            Assert.Equal((uint)(placement.Column * CellW), placement.SourceX);
            Assert.Equal((uint)(placement.Row * CellH), placement.SourceY);
            Assert.Equal((uint)(TileCols * CellW), placement.SourceWidth);
            Assert.Equal((uint)(TileRows * CellH), placement.SourceHeight);
        }

        // Verify SVG has the expected number of <image> elements
        int imageCount = CountOccurrences(svg, "<image ");
        Assert.Equal(expectedPlacements, imageCount);
    }

    /// <summary>
    /// Similar to the full checkerboard but with larger 8x4-cell tiles for a
    /// coarser pattern that's easier to visually verify.
    /// </summary>
    [Fact]
    public void PerlinCheckerboard_LargeTiles_8x4()
    {
        const int largeTileCols = 8;
        const int largeTileRows = 4;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(Caps)
            .WithDimensions(TermWidth, TermHeight)
            .Build();

        int imgW = TermWidth * CellW;
        int imgH = TermHeight * CellH;

        var grayPixels = GeneratePerlinNoiseRgb(imgW, imgH, scale: 80.0, seed: 7);
        var bluePixels = TintBlue(grayPixels, imgW, imgH);

        TransmitImage(terminal, imageId: 1, (uint)imgW, (uint)imgH, grayPixels);
        TransmitImage(terminal, imageId: 2, (uint)imgW, (uint)imgH, bluePixels);

        int tilesX = TermWidth / largeTileCols;   // 10
        int tilesY = TermHeight / largeTileRows;  // 6

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                int col = tx * largeTileCols;
                int row = ty * largeTileRows;
                uint imageId = (tx + ty) % 2 == 0 ? 1u : 2u;

                uint srcX = (uint)(col * CellW);
                uint srcY = (uint)(row * CellH);
                uint srcW = (uint)(largeTileCols * CellW);
                uint srcH = (uint)(largeTileRows * CellH);

                Send(terminal, $"\x1b[{row + 1};{col + 1}H");
                Send(terminal, KgpTestHelper.BuildCommand(
                    $"a=p,i={imageId},c={largeTileCols},r={largeTileRows},x={srcX},y={srcY},w={srcW},h={srcH},q=2"));
            }
        }

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("kgp-perlin-checkerboard-large.svg", svg);

        Assert.Equal(tilesX * tilesY, snapshot.KgpPlacements.Count);
        Assert.Equal(tilesX * tilesY, CountOccurrences(svg, "<image "));
    }

    /// <summary>
    /// Verifies that cropped placements of the same image produce different BMP
    /// content in the SVG, confirming that cropping actually extracts different regions.
    /// </summary>
    [Fact]
    public void PerlinCrop_DifferentRegions_ProduceDifferentBmpData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(Caps)
            .WithDimensions(TermWidth, TermHeight)
            .Build();

        int imgW = TermWidth * CellW;
        int imgH = TermHeight * CellH;
        var pixels = GeneratePerlinNoiseRgb(imgW, imgH, scale: 30.0, seed: 99);

        TransmitImage(terminal, imageId: 1, (uint)imgW, (uint)imgH, pixels);

        // Place two non-overlapping crops of the same image
        Send(terminal, "\x1b[1;1H");
        Send(terminal, KgpTestHelper.BuildCommand(
            $"a=p,i=1,c=4,r=2,x=0,y=0,w={4 * CellW},h={2 * CellH},q=2"));

        Send(terminal, $"\x1b[1;{TileCols + 1}H");
        Send(terminal, KgpTestHelper.BuildCommand(
            $"a=p,i=1,c=4,r=2,x={4 * CellW},y=0,w={4 * CellW},h={2 * CellH},q=2"));

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("kgp-perlin-crop-regions.svg", svg);

        // Extract both base64 data URIs and verify they differ
        var dataUris = ExtractDataUris(svg);
        Assert.Equal(2, dataUris.Count);
        Assert.NotEqual(dataUris[0], dataUris[1]);
    }

    /// <summary>
    /// Fills the screen with white 'A' characters with green underlines, then overlays
    /// a staircase of overlapping KGP images descending from the top-left. Each image
    /// uses a different color tint on Perlin noise and a progressively lower z-index,
    /// so the top-left image is closest under the text and subsequent images stack behind.
    /// </summary>
    [Fact]
    public void PerlinStaircase_OverlappingZOrder_WithTextOverlay()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(Caps)
            .WithDimensions(TermWidth, TermHeight)
            .Build();

        // Fill entire screen with white 'A' characters with green underlines
        // SGR 37 = white fg, SGR 4 = underline, SGR 58;2;0;180;0 = green underline color
        Send(terminal, "\x1b[37;4;58;2;0;180;0m");
        for (int row = 0; row < TermHeight; row++)
        {
            Send(terminal, $"\x1b[{row + 1};1H");
            Send(terminal, new string('A', TermWidth));
        }

        // Image dimensions: 16 cols x 8 rows per image
        const int imgCols = 16;
        const int imgRows = 8;
        int imgPixelW = imgCols * CellW;  // 160
        int imgPixelH = imgRows * CellH;  // 160

        // Generate base Perlin noise at image size
        var baseNoise = GeneratePerlinNoiseRgb(imgPixelW, imgPixelH, scale: 25.0, seed: 13);

        // Create 6 tinted variants: red, orange, yellow, cyan, magenta, green
        var tints = new (string Name, double R, double G, double B, int RB, int GB, int BB)[]
        {
            ("red",     0.8, 0.2, 0.1, 80, 0,  0),
            ("orange",  0.8, 0.5, 0.1, 80, 40, 0),
            ("yellow",  0.8, 0.8, 0.2, 80, 80, 0),
            ("cyan",    0.2, 0.7, 0.8, 0,  40, 80),
            ("magenta", 0.7, 0.2, 0.7, 60, 0,  60),
            ("green",   0.2, 0.8, 0.3, 0,  80, 0),
        };

        // Transmit each tinted image
        for (int i = 0; i < tints.Length; i++)
        {
            var t = tints[i];
            var tinted = TintColor(baseNoise, t.R, t.G, t.B, t.BB, t.RB, t.GB);
            TransmitImage(terminal, imageId: (uint)(i + 1), (uint)imgPixelW, (uint)imgPixelH, tinted);
        }

        // Place images in a staircase: each offset by (6 cols, 2 rows)
        // Z-index descends: -1 (closest under text) to -6 (furthest back)
        const int stepCols = 6;
        const int stepRows = 2;

        for (int i = 0; i < tints.Length; i++)
        {
            int col = i * stepCols;
            int row = i * stepRows;
            int zIndex = -(i + 1); // -1, -2, -3, -4, -5, -6

            Send(terminal, $"\x1b[{row + 1};{col + 1}H");
            Send(terminal, KgpTestHelper.BuildCommand(
                $"a=p,i={i + 1},c={imgCols},r={imgRows},z={zIndex},q=2"));
        }

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("kgp-perlin-staircase-zorder.svg", svg);

        // Verify all 6 images were placed
        Assert.Equal(tints.Length, snapshot.KgpPlacements.Count);
        Assert.Equal(tints.Length, snapshot.KgpImages.Count);

        // Verify staircase positioning
        for (int i = 0; i < tints.Length; i++)
        {
            var p = snapshot.KgpPlacements[i];
            Assert.Equal((uint)(i + 1), p.ImageId);
            Assert.Equal(i * stepRows, p.Row);
            Assert.Equal(i * stepCols, p.Column);
            Assert.Equal(-(i + 1), p.ZIndex);
        }

        // Verify z-order in SVG: images should be sorted by ZIndex ascending
        // (lowest z-index = furthest back = rendered first in SVG)
        var dataUris = ExtractDataUris(svg);
        Assert.Equal(tints.Length, dataUris.Count);
        // All data URIs should be distinct (different tint colors)
        Assert.Equal(tints.Length, dataUris.Distinct().Count());

        // Verify text layer exists above images
        var imagesGroupEnd = svg.LastIndexOf("</g>", svg.IndexOf("class=\"terminal-text\""));
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");
        Assert.True(textGroupStart > imagesGroupEnd,
            "Text group should appear after images group in SVG");

        // Verify the 'A' characters are in the text layer
        Assert.Contains(">A<", svg);
    }

    #region Perlin noise generation

    /// <summary>
    /// Generates RGB24 pixel data filled with 2D Perlin noise.
    /// </summary>
    private static byte[] GeneratePerlinNoiseRgb(int width, int height, double scale, int seed)
    {
        var data = new byte[width * height * 3];
        var perm = BuildPermutationTable(seed);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double nx = x / scale;
                double ny = y / scale;
                // Layer two octaves for richer detail
                double val = PerlinNoise(nx, ny, perm) * 0.7
                           + PerlinNoise(nx * 2.0, ny * 2.0, perm) * 0.3;
                // Map from [-1,1] to [0,255]
                byte gray = (byte)Math.Clamp((int)((val + 1.0) * 0.5 * 255), 0, 255);

                int i = (y * width + x) * 3;
                data[i] = gray;
                data[i + 1] = gray;
                data[i + 2] = gray;
            }
        }

        return data;
    }

    /// <summary>
    /// Creates a blue-tinted copy of RGB24 pixel data.
    /// Keeps luminance but shifts hue toward blue.
    /// </summary>
    private static byte[] TintBlue(byte[] grayData, int width, int height)
    {
        return TintColor(grayData, 0.3, 0.4, 0.6, 100);
    }

    /// <summary>
    /// Creates a color-tinted copy of grayscale RGB24 pixel data.
    /// Each channel is scaled by a factor and optionally boosted by a constant.
    /// </summary>
    private static byte[] TintColor(byte[] grayData, double rScale, double gScale, double bScale, int bBoost = 0, int rBoost = 0, int gBoost = 0)
    {
        var data = new byte[grayData.Length];
        for (int i = 0; i < grayData.Length; i += 3)
        {
            byte gray = grayData[i];
            data[i] = (byte)Math.Clamp((int)(gray * rScale) + rBoost, 0, 255);
            data[i + 1] = (byte)Math.Clamp((int)(gray * gScale) + gBoost, 0, 255);
            data[i + 2] = (byte)Math.Clamp((int)(gray * bScale) + bBoost, 0, 255);
        }

        return data;
    }

    private static double PerlinNoise(double x, double y, int[] perm)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        double u = Fade(xf);
        double v = Fade(yf);

        int aa = perm[perm[xi] + yi];
        int ab = perm[perm[xi] + yi + 1];
        int ba = perm[perm[xi + 1] + yi];
        int bb = perm[perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        double x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad(int hash, double x, double y)
    {
        int h = hash & 3;
        double u = h < 2 ? x : y;
        double v = h < 2 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static int[] BuildPermutationTable(int seed)
    {
        var rng = new Random(seed);
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;

        // Fisher-Yates shuffle
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // Double the table to avoid modular indexing
        var perm = new int[512];
        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        return perm;
    }

    #endregion

    #region Helpers

    private static void Send(Hex1bTerminal terminal, string escapeSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));
    }

    /// <summary>
    /// Transmits a large image using chunked KGP transfer to stay within
    /// base64 payload limits per escape sequence.
    /// </summary>
    private static void TransmitImage(Hex1bTerminal terminal, uint imageId, uint width, uint height, byte[] rgbData)
    {
        const int chunkSize = 4096;
        int offset = 0;

        while (offset < rgbData.Length)
        {
            int remaining = rgbData.Length - offset;
            int thisChunk = Math.Min(chunkSize, remaining);
            var chunk = new byte[thisChunk];
            Array.Copy(rgbData, offset, chunk, 0, thisChunk);
            bool isLast = offset + thisChunk >= rgbData.Length;

            string ctrl;
            if (offset == 0)
                ctrl = $"a=t,f=24,s={width},v={height},i={imageId},m={(isLast ? 0 : 1)},q=2";
            else
                ctrl = $"m={(isLast ? 0 : 1)},q=2";

            Send(terminal, KgpTestHelper.BuildCommand(ctrl, chunk));
            offset += thisChunk;
        }
    }

    private static List<string> ExtractDataUris(string svg)
    {
        var uris = new List<string>();
        const string marker = "data:image/bmp;base64,";
        int idx = 0;
        while ((idx = svg.IndexOf(marker, idx, StringComparison.Ordinal)) != -1)
        {
            int end = svg.IndexOf('"', idx);
            uris.Add(svg[idx..end]);
            idx = end;
        }

        return uris;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    #endregion
}
