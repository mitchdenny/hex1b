using Hex1b.Kgp;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SVG export of KGP (Kitty Graphics Protocol) images.
/// Verifies that KGP images are correctly captured in snapshots and rendered
/// in the SVG output with proper positioning, z-order, and data encoding.
/// </summary>
public class KgpSvgExportTests
{
    private static readonly TerminalCapabilities KgpCapabilities = new()
    {
        SupportsKgp = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
    };

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload, int width = 80, int height = 24)
    {
        return Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless(KgpCapabilities)
            .WithDimensions(width, height)
            .Build();
    }

    private static void Send(Hex1bTerminal terminal, string escapeSequence)
    {
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(escapeSequence));
    }

    [Fact]
    public void SvgExport_WithKgpImage_ContainsImageElement()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Transmit and display a 2x2 RGB24 image
        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 1, width: 2, height: 2, format: KgpFormat.Rgb24,
            displayColumns: 2, displayRows: 1, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
        Assert.Contains("preserveAspectRatio=\"none\"", svg);
        Assert.Contains("style=\"image-rendering: pixelated;\"", svg);
    }

    [Fact]
    public void SvgExport_WithKgpImage_CorrectPositioning()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Write some text then move cursor to row 2, col 5 (1-based CSI H)
        Send(terminal, "Hello");
        Send(terminal, "\x1b[3;6H"); // row 3, col 6 (0-based: row 2, col 5)

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 1, width: 2, height: 2, format: KgpFormat.Rgb24,
            displayColumns: 4, displayRows: 2, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Cell dimensions from KgpCapabilities default: 10px wide, 20px tall
        var expectedX = 5 * 10; // col 5 * cellWidth
        var expectedY = 2 * 20; // row 2 * cellHeight
        Assert.Contains($"x=\"{expectedX}\"", svg);
        Assert.Contains($"y=\"{expectedY}\"", svg);
    }

    [Fact]
    public void SvgExport_WithKgpImage_ThreePassStructure()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Write some text
        Send(terminal, "Hello World");

        // Transmit and display a KGP image
        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 1, width: 2, height: 2, format: KgpFormat.Rgb24,
            displayColumns: 2, displayRows: 1, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Verify all three passes exist
        Assert.Contains("class=\"terminal-bg\"", svg);
        Assert.Contains("class=\"terminal-images\"", svg);
        Assert.Contains("class=\"terminal-text\"", svg);

        // Verify correct ordering: bg < images < text
        var bgIndex = svg.IndexOf("class=\"terminal-bg\"");
        var imagesIndex = svg.IndexOf("class=\"terminal-images\"");
        var textIndex = svg.IndexOf("class=\"terminal-text\"");

        Assert.True(bgIndex < imagesIndex,
            "terminal-bg should appear before terminal-images in SVG");
        Assert.True(imagesIndex < textIndex,
            "terminal-images should appear before terminal-text in SVG");
    }

    [Fact]
    public void SvgExport_WithoutKgpImages_NoImageElements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Only text, no KGP images
        Send(terminal, "Just plain text, no images here");

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // No <image elements should be present
        Assert.DoesNotContain("<image", svg);

        // Three-pass structure should still exist
        Assert.Contains("class=\"terminal-bg\"", svg);
        Assert.Contains("class=\"terminal-images\"", svg);
        Assert.Contains("class=\"terminal-text\"", svg);
    }

    [Fact]
    public void SvgExport_MultipleKgpImages_AllRendered()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Place first image at position (0,0)
        var cmd1 = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 1, width: 2, height: 2, format: KgpFormat.Rgb24,
            displayColumns: 3, displayRows: 1, quiet: 2);
        Send(terminal, cmd1);

        // Move cursor and place second image at different position
        Send(terminal, "\x1b[5;10H"); // row 5, col 10
        var cmd2 = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 2, width: 4, height: 4, format: KgpFormat.Rgba32,
            displayColumns: 4, displayRows: 2, quiet: 2);
        Send(terminal, cmd2);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Count <image occurrences
        var imageCount = CountOccurrences(svg, "<image ");
        Assert.Equal(2, imageCount);
    }

    [Fact]
    public void SvgExport_KgpImage_ZOrderBetweenBgAndText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Write text first
        Send(terminal, "Overlapping text");

        // Move back and place image overlapping the text
        Send(terminal, "\x1b[1;1H"); // move to top-left
        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 1, width: 2, height: 2, format: KgpFormat.Rgb24,
            displayColumns: 4, displayRows: 1, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Image element should appear after bg rects but before text elements
        var bgGroupEnd = svg.IndexOf("</g>", svg.IndexOf("class=\"terminal-bg\""));
        var imageElementIndex = svg.IndexOf("<image ");
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");

        Assert.True(imageElementIndex > bgGroupEnd,
            "Image element should appear after the background group ends");
        Assert.True(imageElementIndex < textGroupStart,
            "Image element should appear before the text group starts");
    }

    [Fact]
    public void SvgExport_KgpImage_Rgb24_ProducesValidBmp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Create a 1x1 red pixel image (RGB24: R=255, G=0, B=0)
        var redPixel = new byte[] { 255, 0, 0 };
        var cmd = KgpTestHelper.BuildCommand(
            $"a=T,f=24,s=1,v=1,i=1,c=1,r=1,q=2", redPixel);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();

        // Extract the data URI from the SVG
        Assert.Contains("data:image/bmp;base64,", svg);

        var dataUriStart = svg.IndexOf("data:image/bmp;base64,");
        var hrefEnd = svg.IndexOf('"', dataUriStart);
        var dataUri = svg[dataUriStart..hrefEnd];
        var base64Part = dataUri["data:image/bmp;base64,".Length..];

        // Decode and validate BMP structure
        var bmpBytes = Convert.FromBase64String(base64Part);
        Assert.True(bmpBytes.Length >= 54, "BMP should have at least a 54-byte header");
        Assert.Equal((byte)'B', bmpBytes[0]);
        Assert.Equal((byte)'M', bmpBytes[1]);

        // Validate dimensions in BMP header (offset 18=width, 22=height)
        var bmpWidth = BitConverter.ToInt32(bmpBytes, 18);
        var bmpHeight = BitConverter.ToInt32(bmpBytes, 22);
        Assert.Equal(1, bmpWidth);
        Assert.Equal(1, bmpHeight);
    }

    [Fact]
    public void SvgSnapshot_CapturesKgpState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 42, width: 3, height: 3, format: KgpFormat.Rgba32,
            displayColumns: 3, displayRows: 2, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();

        Assert.True(snapshot.KgpPlacements.Count > 0,
            "Snapshot should capture KGP placements");
        Assert.True(snapshot.KgpImages.Count > 0,
            "Snapshot should capture KGP image data");
        Assert.True(snapshot.KgpImages.ContainsKey(42),
            "Snapshot should contain the transmitted image ID");
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
}
