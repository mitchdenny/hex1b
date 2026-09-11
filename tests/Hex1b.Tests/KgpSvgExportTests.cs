using System.Xml.Linq;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for SVG export of KGP (Kitty Graphics Protocol) images.
/// Verifies that KGP images are correctly captured in snapshots and rendered
/// in the SVG output with proper positioning, z-order, and data encoding.
/// </summary>
[TestClass]
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

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-basic-image.svg", svg);

        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
        Assert.Contains("preserveAspectRatio=\"none\"", svg);
        Assert.Contains("style=\"image-rendering: pixelated;\"", svg);
    }

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-positioned-image.svg", svg);

        // Cell dimensions from KgpCapabilities default: 10px wide, 20px tall
        var expectedX = 5 * 10; // col 5 * cellWidth
        var expectedY = 2 * 20; // row 2 * cellHeight
        Assert.Contains($"x=\"{expectedX}\"", svg);
        Assert.Contains($"y=\"{expectedY}\"", svg);
    }

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-three-pass.svg", svg);

        // Verify all three passes exist
        Assert.Contains("class=\"terminal-bg\"", svg);
        Assert.Contains("class=\"terminal-images\"", svg);
        Assert.Contains("class=\"terminal-text\"", svg);

        // Verify correct ordering: bg < images < text
        var bgIndex = svg.IndexOf("class=\"terminal-bg\"");
        var imagesIndex = svg.IndexOf("class=\"terminal-images\"");
        var textIndex = svg.IndexOf("class=\"terminal-text\"");

        Assert.IsTrue(bgIndex < imagesIndex, "terminal-bg should appear before terminal-images in SVG");
        Assert.IsTrue(imagesIndex < textIndex, "terminal-images should appear before terminal-text in SVG");
    }

    [TestMethod]
    public void SvgExport_WithoutKgpImages_NoImageElements()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        // Only text, no KGP images
        Send(terminal, "Just plain text, no images here");

        var snapshot = terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("kgp-no-images.svg", svg);

        // No <image elements should be present
        Assert.DoesNotContain("<image", svg);

        // Three-pass structure should still exist
        Assert.Contains("class=\"terminal-bg\"", svg);
        Assert.Contains("class=\"terminal-images\"", svg);
        Assert.Contains("class=\"terminal-text\"", svg);
    }

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-multiple-images.svg", svg);

        // Count <image occurrences
        var imageCount = CountOccurrences(svg, "<image ");
        Assert.AreEqual(2, imageCount);
    }

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-z-order.svg", svg);

        // Image element should appear after bg rects but before text elements
        var bgGroupEnd = svg.IndexOf("</g>", svg.IndexOf("class=\"terminal-bg\""));
        var imageElementIndex = svg.IndexOf("<image ");
        var textGroupStart = svg.IndexOf("class=\"terminal-text\"");

        Assert.IsTrue(imageElementIndex > bgGroupEnd, "Image element should appear after the background group ends");
        Assert.IsTrue(imageElementIndex < textGroupStart, "Image element should appear before the text group starts");
    }

    [TestMethod]
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
        TestCaptureHelper.AttachSvg("kgp-red-pixel-bmp.svg", svg);

        // Extract the data URI from the SVG
        Assert.Contains("data:image/bmp;base64,", svg);

        var dataUriStart = svg.IndexOf("data:image/bmp;base64,");
        var hrefEnd = svg.IndexOf('"', dataUriStart);
        var dataUri = svg[dataUriStart..hrefEnd];
        var base64Part = dataUri["data:image/bmp;base64,".Length..];

        // Decode and validate BMP structure
        var bmpBytes = Convert.FromBase64String(base64Part);
        Assert.IsTrue(bmpBytes.Length >= 54, "BMP should have at least a 54-byte header");
        Assert.AreEqual((byte)'B', bmpBytes[0]);
        Assert.AreEqual((byte)'M', bmpBytes[1]);

        // Validate dimensions in BMP header (offset 18=width, 22=height)
        var bmpWidth = BitConverter.ToInt32(bmpBytes, 18);
        var bmpHeight = BitConverter.ToInt32(bmpBytes, 22);
        Assert.AreEqual(1, bmpWidth);
        Assert.AreEqual(1, bmpHeight);
    }

    [TestMethod]
    public void SvgSnapshot_CapturesKgpState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);

        var cmd = KgpTestHelper.BuildTransmitAndDisplayCommand(
            imageId: 42, width: 3, height: 3, format: KgpFormat.Rgba32,
            displayColumns: 3, displayRows: 2, quiet: 2);
        Send(terminal, cmd);

        var snapshot = terminal.CreateSnapshot();
        TestCaptureHelper.CaptureSvg(snapshot, "kgp-snapshot-state");

        Assert.IsTrue(snapshot.KgpPlacements.Count > 0, "Snapshot should capture KGP placements");
        Assert.IsTrue(snapshot.KgpImages.Count > 0, "Snapshot should capture KGP image data");
        Assert.IsTrue(snapshot.KgpImages.ContainsKey(42), "Snapshot should contain the transmitted image ID");
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void SvgExport_NativeSprite_UsesPixelBounds(bool png, bool crop, bool customMetrics)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload, 12, 8);
        var pixels = png
            ? Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAYAAABWKLW/AAAAEUlEQVR4nGP4z8DwH4YZcHIAXdcR79xPMRAAAAAASUVORK5CYII=")
            : KgpTestHelper.CreatePixelData(3, 3);
        var cropControls = crop ? ",x=1,y=1,w=2,h=2" : "";
        Send(terminal, "\x1b[3;5H" + KgpTestHelper.BuildCommand(
            $"a=T,f={(png ? 100 : 32)},s=3,v=3,i=7,X=9,Y=19,C=1,q=2{cropControls}",
            pixels));
        using var snapshot = terminal.CreateSnapshot();
        var options = customMetrics
            ? new TerminalSvgOptions { CellWidth = 15, CellHeight = 30 }
            : null;

        var svg = XDocument.Parse(snapshot.ToSvg(options));

        XNamespace ns = "http://www.w3.org/2000/svg";
        var image = TestSeq.Single(svg.Descendants()
            .Where(element => (string?)element.Attribute("data-image-id") == "7"));
        var scale = customMetrics ? 1.5 : 1;
        var destinationX = 49 * scale;
        var destinationY = 59 * scale;
        var destinationSize = (crop ? 2 : 3) * scale;
        Assert.AreEqual(png ? "use" : "image", image.Name.LocalName);
        Assert.AreEqual(png && crop ? destinationX - scale : destinationX, (double)image.Attribute("x")!);
        Assert.AreEqual(png && crop ? destinationY - scale : destinationY, (double)image.Attribute("y")!);
        Assert.AreEqual(png ? 3 * scale : destinationSize, (double)image.Attribute("width")!);
        Assert.AreEqual(png ? 3 * scale : destinationSize, (double)image.Attribute("height")!);
        if (png && crop)
        {
            var clip = TestSeq.Single(svg.Descendants(ns + "clipPath")
                .Where(element => ((string?)element.Attribute("id"))?.StartsWith("kgp-png-clip-") == true));
            var bounds = TestSeq.Single(clip.Elements(ns + "rect"));
            Assert.AreEqual(destinationX, (double)bounds.Attribute("x")!);
            Assert.AreEqual(destinationY, (double)bounds.Attribute("y")!);
            Assert.AreEqual(destinationSize, (double)bounds.Attribute("width")!);
            Assert.AreEqual(destinationSize, (double)bounds.Attribute("height")!);
        }
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
