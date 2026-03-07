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
        var data = new byte[grayData.Length];
        for (int i = 0; i < grayData.Length; i += 3)
        {
            byte gray = grayData[i];
            // Darken R/G channels, boost B channel
            data[i] = (byte)(gray * 0.3);     // R
            data[i + 1] = (byte)(gray * 0.4); // G
            data[i + 2] = (byte)Math.Min(255, gray * 0.6 + 100); // B
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
