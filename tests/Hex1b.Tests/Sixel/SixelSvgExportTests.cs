// Copyright (c) Hex1b contributors. Licensed under the MIT license.

using System.Text;
using System.Text.RegularExpressions;
using Hex1b.Automation;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Integration coverage for issue #456's SVG export requirements: Sixel
/// placements must render using the exact snapshot pixels/cell geometry and
/// source crop the authoritative <see cref="Hex1b.Sixel.SixelPlacement"/>
/// model reports — not a re-derived or re-decoded approximation — and must
/// produce an explicit diagnostic placeholder for geometry-only placements
/// rather than silently omitting them. These tests exercise raw DCS byte
/// sequences only (never <c>SixelWidget</c>/<c>SixelEncoder</c>), mirroring
/// the conventions established by <see cref="SixelScrollHistoryReflowTests"/>
/// and <see cref="SixelPlacementLifetimeTests"/>.
/// </summary>
[TestClass]
public class SixelSvgExportTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One-band cursor and lifecycle probe.");

    // 1x18 px, 1:1 aspect, three full six-pixel bands -> three occupied rows
    // at the default 1x6 cell metrics harness. Reused verbatim from
    // SixelScrollHistoryReflowTests' progressive-crop probe so this test
    // exercises the exact same crop scenario against the SVG exporter.
    private static readonly SixelFixture ThreeRowBar = new(
        "three-row-bar",
        "Three-band bar used as a three-row-tall progressive-crop probe.",
        Encoding.ASCII.GetBytes("q\"1;1;1;18#1;2;100;0;0~-~-~"));

    [TestMethod]
    public async Task SvgExport_WithSixelPlacement_ContainsImageElementAtCorrectCellPosition()
    {
        await using var terminal = SixelTestTerminal.Create(width: 20, height: 10);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[3;5H").Concat(SingleBand.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            SingleBand.Name,
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("sixel-basic-position.svg", svg);

        Assert.Contains("<image", svg);
        Assert.Contains("data:image/bmp;base64,", svg);
        Assert.Contains("preserveAspectRatio=\"none\"", svg);

        // Default SixelTestTerminal cell metrics: 1x6 pixels per cell.
        var expectedX = placement.PaintedLeft * 1;
        var expectedY = placement.PaintedTop * 6;
        Assert.Contains($"x=\"{expectedX}\"", svg);
        Assert.Contains($"y=\"{expectedY}\"", svg);
    }

    [TestMethod]
    public async Task SvgExport_WithPartiallyCroppedPlacement_EmbedsExactCroppedPixelsNotSquishedFullImage()
    {
        // Regression coverage: the SVG exporter must size *and* fill its
        // embedded raster from the placement's exact painted/visible crop
        // (Hex1b.Sixel.SixelPlacement.GetPaintedPixels), not the full
        // originally-declared image scaled down to the cropped cell box.
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5);
        var prefix = Encoding.ASCII.GetBytes("\x1b[2;4r\x1b[2;1H");
        var scrollOnce = Encoding.ASCII.GetBytes("\x1b[S");
        var bytes = prefix.Concat(ThreeRowBar.StandardBytes).Concat(scrollOnce).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "first partial-margin scroll",
            TestContext.Current.CancellationToken);

        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements is [{ } p] && p.PaintedRowCount == 1,
            "second partial-margin scroll crops to a single row",
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(1, placement.PaintedRowCount);
        Assert.AreEqual(1, placement.WidthInCells);

        // Sanity: the full declared image is still 3 rows tall (18px) even
        // though only 1 row (6px) survives the crop — this is exactly the
        // gap between "full declared image" and "painted/visible crop" that
        // the exporter must respect.
        var fullPixels = placement.Image.GetPixels();
        Assert.IsNotNull(fullPixels);
        Assert.AreEqual(18, fullPixels.Height);

        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("sixel-partial-crop.svg", svg);

        var (bmpWidth, bmpHeight) = ExtractEmbeddedBmpDimensions(svg);

        // The embedded raster must match the cropped cell footprint (1 col x
        // 1 row -> 1x6 px), never the full declared image (1x18 px).
        Assert.AreEqual(1, bmpWidth);
        Assert.AreEqual(6, bmpHeight);
    }

    [TestMethod]
    public async Task SvgExport_WithDamagedCell_RendersTransparentBackgroundUnderText()
    {
        var wide = new SixelFixture(
            "svg-damage-probe",
            "A two-column band so the origin cell can be overwritten while the second cell still paints.",
            "q#1;2;100;0;0#1!2~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(wide.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            wide.Name,
            TestContext.Current.CancellationToken);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[1;1HX"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "origin cell overwritten",
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("sixel-damage-layering.svg", svg);

        var placement = TestSeq.Single(snapshot.SixelPlacements);
        var (bmpWidth, _) = ExtractEmbeddedBmpDimensions(svg);

        // Both cells are still within the painted rectangle (the placement
        // survives because its second cell still paints), so the embedded
        // raster still spans both columns...
        Assert.AreEqual(2, bmpWidth);

        // ...but the damaged (overwritten) origin column must decode as the
        // BMP encoder's transparent background fill (0x1e,0x1e,0x1e), not the
        // placement's opaque red, and the "X" text must be drawn on top in
        // the separate terminal-text layer.
        var pixel = ExtractEmbeddedBmpPixel(svg, x: 0, y: 0);
        Assert.AreEqual((0x1e, 0x1e, 0x1e), pixel);
        Assert.Contains(">X<", svg);
        Assert.IsTrue(placement.IsCellDamaged(placement.Row, placement.Column));
    }

    [TestMethod]
    public async Task SvgExport_WithGeometryOnlyPlacement_RendersExplicitDiagnosticPlaceholder()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => terminal.Terminal.SixelPlacementCount == 1,
            "geometry-only placement retained",
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);

        var svg = snapshot.ToSvg();
        TestCaptureHelper.AttachSvg("sixel-geometry-only-placeholder.svg", svg);

        // A deterministic, discoverable placeholder must be present -- never
        // a silent omission of the placement from the export.
        Assert.Contains("sixel-geometry-only", svg);
        Assert.Contains($"data-sixel-outcome=\"{placement.Image.RasterStatus}\"", svg);
        Assert.Contains("<title>", svg);
        Assert.DoesNotContain("data:image/bmp;base64,", svg);
    }

    [TestMethod]
    public async Task SvgExport_RepeatedExportOfSameSnapshot_IsByteIdentical()
    {
        await using var terminal = SixelTestTerminal.Create(width: 20, height: 10);

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            SingleBand.Name,
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var first = snapshot.ToSvg();
        var second = snapshot.ToSvg();

        Assert.AreEqual(first, second);
    }

    private static (int Width, int Height) ExtractEmbeddedBmpDimensions(string svg)
    {
        var bmp = DecodeFirstBmp(svg);
        var width = BitConverter.ToInt32(bmp, 18);
        var height = BitConverter.ToInt32(bmp, 22);
        return (width, height);
    }

    private static (byte R, byte G, byte B) ExtractEmbeddedBmpPixel(string svg, int x, int y)
    {
        var bmp = DecodeFirstBmp(svg);
        var width = BitConverter.ToInt32(bmp, 18);
        var height = BitConverter.ToInt32(bmp, 22);
        var rowStride = (width * 3 + 3) & ~3;

        // BMP rows are stored bottom-up.
        var rowOffset = 54 + (height - 1 - y) * rowStride;
        var pixelOffset = rowOffset + x * 3;
        var b = bmp[pixelOffset];
        var g = bmp[pixelOffset + 1];
        var r = bmp[pixelOffset + 2];
        return (r, g, b);
    }

    private static byte[] DecodeFirstBmp(string svg)
    {
        var match = Regex.Match(svg, "data:image/bmp;base64,([A-Za-z0-9+/=]+)");
        Assert.IsTrue(match.Success, "Expected at least one embedded BMP data URI in the SVG output.");
        return Convert.FromBase64String(match.Groups[1].Value);
    }
}
