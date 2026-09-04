// Copyright (c) Hex1b contributors. Licensed under the MIT license.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hex1b.Automation;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Integration coverage for issue #456's HTML export requirements: the
/// per-cell Sixel metadata payload embedded in HTML export must reflect the
/// exact <see cref="Hex1b.Sixel.SixelPlacement"/> geometry the snapshot
/// reports -- including for non-square placements where a row/column
/// parameter-order mistake would only surface off the diagonal -- and
/// repeated export must be deterministic. HTML export embeds Sixel raster
/// pixels by delegating to the SVG exporter, so pixel-level crop/fidelity
/// coverage lives in <see cref="SixelSvgExportTests"/>; these tests focus on
/// the HTML-specific per-cell JSON metadata contract.
/// </summary>
[TestClass]
public class SixelHtmlExportTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One-band cursor and lifecycle probe.");

    [TestMethod]
    public async Task HtmlExport_WithSixelPlacement_EmbedsSvgViaToSvg()
    {
        await using var terminal = SixelTestTerminal.Create(width: 20, height: 10);

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            SingleBand.Name,
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var svg = snapshot.ToSvg();
        var html = snapshot.ToHtml();
        TestCaptureHelper.AttachFile("sixel-html-embeds-svg.html", html);

        // HTML export must reuse the authoritative SVG exporter rather than
        // re-deriving/re-decoding Sixel raster pixels independently.
        Assert.Contains("data:image/bmp;base64,", html);
        Assert.Contains("<image", html);
    }

    [TestMethod]
    public async Task HtmlExport_WithAsymmetricPlacement_ReportsCorrectPerCellMetadataAtEachCoordinate()
    {
        // Regression coverage for the CoversCell(row, column) parameter-order
        // fix: use a placement whose painted footprint is wider than it is
        // tall (3 columns x 1 row) so that transposing row/column would only
        // manifest off the [0,0] origin -- exactly the kind of bug a square
        // fixture cannot catch.
        // Explicit 1:1 aspect + 3x6 declared extent (mirrors
        // SixelScrollHistoryReflowTests.OneRowThreeCol) so this occupies
        // exactly one row, three columns -- the default 2:1 aspect used by
        // the shared SingleBand fixture would occupy two rows instead.
        var wide = new SixelFixture(
            "html-asymmetric-probe",
            "Three-column, one-row band to catch row/column transposition bugs.",
            "q\"1;1;3;6#1;2;100;0;0!3~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create(width: 20, height: 10);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[4;2H").Concat(wide.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            wide.Name,
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(3, placement.WidthInCells);
        Assert.AreEqual(1, placement.HeightInCells);

        var html = snapshot.ToHtml();
        TestCaptureHelper.AttachFile("sixel-html-asymmetric.html", html);
        var cellData = ExtractCellData(html);

        // Row index (placement.Row == y == 3), three columns starting at
        // placement.Column == x == 1.
        var originCell = cellData.RootElement[placement.Row][placement.Column];
        var sixelAtOrigin = originCell.GetProperty("sixel");
        Assert.AreNotEqual(JsonValueKind.Null, sixelAtOrigin.ValueKind);
        Assert.IsTrue(sixelAtOrigin.GetProperty("origin").GetBoolean());
        Assert.AreEqual(3, sixelAtOrigin.GetProperty("w").GetInt32());
        Assert.AreEqual(1, sixelAtOrigin.GetProperty("h").GetInt32());

        // The two subsequent columns on the SAME row must also report the
        // placement (non-origin), while the row above/below must not.
        for (var dx = 1; dx <= 2; dx++)
        {
            var cell = cellData.RootElement[placement.Row][placement.Column + dx];
            var sixel = cell.GetProperty("sixel");
            Assert.AreNotEqual(JsonValueKind.Null, sixel.ValueKind);
            Assert.IsFalse(sixel.GetProperty("origin").GetBoolean());
        }

        var aboveRow = cellData.RootElement[placement.Row - 1][placement.Column];
        Assert.AreEqual(JsonValueKind.Null, aboveRow.GetProperty("sixel").ValueKind);

        var belowRow = cellData.RootElement[placement.Row + 1][placement.Column];
        Assert.AreEqual(JsonValueKind.Null, belowRow.GetProperty("sixel").ValueKind);
    }

    [TestMethod]
    public async Task HtmlExport_WithGeometryOnlyPlacement_ReportsGeometryOnlyMetadata()
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

        var html = snapshot.ToHtml();
        TestCaptureHelper.AttachFile("sixel-html-geometry-only.html", html);
        var cellData = ExtractCellData(html);

        var originCell = cellData.RootElement[placement.Row][placement.Column].GetProperty("sixel");
        Assert.IsTrue(originCell.GetProperty("geometryOnly").GetBoolean());
        Assert.AreEqual(
            placement.Image.RasterStatus.ToString(),
            originCell.GetProperty("outcome").GetString());
    }

    [TestMethod]
    public async Task HtmlExport_RepeatedExportOfSameSnapshot_IsByteIdentical()
    {
        await using var terminal = SixelTestTerminal.Create(width: 20, height: 10);

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            SingleBand.Name,
            TestContext.Current.CancellationToken);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var first = snapshot.ToHtml();
        var second = snapshot.ToHtml();

        Assert.AreEqual(first, second);
    }

    private static JsonDocument ExtractCellData(string html)
    {
        var match = Regex.Match(html, @"const cellData = (\[.*?\]);", RegexOptions.Singleline);
        Assert.IsTrue(match.Success, "Expected an embedded 'const cellData = [...]' payload in the exported HTML.");
        return JsonDocument.Parse(match.Groups[1].Value);
    }
}
