namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelParserTests
{
    [TestMethod]
    [DataRow("single-band", 1, 6, "A\n.\n.\n.\n.\n.")]
    [DataRow("multi-band", 1, 12, "A\n.\n.\n.\n.\n.\nA\nA\n.\n.\n.\n.")]
    [DataRow("two-color-overprint", 1, 6, "A\nB\n.\n.\n.\n.")]
    [DataRow("transparent", 2, 6, "A.\n..\n..\n..\n..\n..")]
    public async Task IndependentFixture_GrammarAndPixels_AreInspectable(
        string fixtureName,
        int expectedWidth,
        int expectedHeight,
        string expectedGrid)
    {
        var fixture = SixelFixture.Load(fixtureName, "Manually authored conformance fixture.");
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(expectedWidth, placement.PixelWidth);
        Assert.AreEqual(expectedHeight, placement.PixelHeight);
        Assert.AreEqual(expectedGrid, placement.PixelGrid[..placement.PixelGrid.IndexOf("\n[")]);
    }

    [TestMethod]
    public async Task FractionalCellMetrics_AreCapturedAlongsideCurrentIntegerPlacementMetrics()
    {
        var fixture = SixelFixture.Load("single-band", "Fractional metric probe.");
        await using var terminal = SixelTestTerminal.Create(
            cellPixelWidth: 9,
            cellPixelHeight: 18,
            actualCellPixelWidth: 9.25);

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var observation = terminal.Observe();
        Assert.AreEqual(9, observation.CellPixelWidth);
        Assert.AreEqual(18, observation.CellPixelHeight);
        Assert.AreEqual(9.25, observation.EffectiveCellPixelWidth);
    }

    [TestMethod]
    public async Task RepeatIntroducer_ExpandsIndependentRunLength()
    {
        var fixture = new SixelFixture(
            "repeat",
            "Paints three complete columns using DECGRA and DECGRI.",
            "q\"1;1;3;6#1;2;100;0;0#1!3~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(3, placement.PixelWidth);
        Assert.StartsWith("AAA\nAAA\nAAA\nAAA\nAAA\nAAA", placement.PixelGrid);
    }

    [TestMethod]
    public async Task RgbDefinition_WithExplicitSelection_ProducesExactColor()
    {
        var fixture = new SixelFixture(
            "explicit-rgb-selection",
            "Defines register 1 as red, explicitly selects it, and paints one pixel.",
            "q#1;2;100;0;0#1@"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        Assert.Contains("#FF0000FF", TestSeq.Single(terminal.Observe().Placements).PixelGrid);
    }

    [TestMethod]
    public async Task SvgEvidence_IndependentFixture_ContainsInspectableRaster()
    {
        var fixture = SixelFixture.Load(
            "two-color-overprint",
            "Two-color fixture for readable SVG evidence.");
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var svg = terminal.CreateSvgEvidence();
        Assert.Contains("<svg", svg);
        Assert.Contains("id=\"sixel-pixels\"", svg);
        Assert.Contains("fill=\"#FF0000\"", svg);
        Assert.Contains("fill=\"#00FF00\"", svg);
        TestSvgHelper.AttachFile("sixel-two-color-evidence.svg", svg);
    }

    [TestMethod, Ignore("Owned by #449: HLS conversion currently uses the conventional hue wheel instead of DEC's blue-at-zero hue wheel.")]
    public async Task HlsDefinition_HueZeroProducesBlue()
    {
        var fixture = new SixelFixture(
            "hls-blue",
            "Defines DEC HLS hue zero as blue, explicitly selects it, and paints one pixel.",
            "q#3;1;0;50;100#3@"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        Assert.Contains("#0000FFFF", TestSeq.Single(terminal.Observe().Placements).PixelGrid);
    }

    [TestMethod]
    public async Task AspectMacro_Omitted_UsesDecTwoToOneDefault()
    {
        var defaultAspect = new SixelFixture(
            "default-aspect",
            "Uses the omitted P1 default to paint one complete sixel column.",
            "q~"u8.ToArray());
        var squareAspect = new SixelFixture(
            "square-aspect",
            "Uses P1=7 to select square Sixel pixels.",
            "7q~"u8.ToArray());
        await using var defaultTerminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 1);
        await using var squareTerminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 1);

        await defaultTerminal.FeedAsync(
            defaultAspect.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await squareTerminal.FeedAsync(
            squareAspect.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await defaultTerminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            defaultAspect.Name,
            TestContext.Current.CancellationToken);
        await squareTerminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            squareAspect.Name,
            TestContext.Current.CancellationToken);

        var defaultPlacement = TestSeq.Single(defaultTerminal.Observe().Placements);
        var squarePlacement = TestSeq.Single(squareTerminal.Observe().Placements);
        Assert.AreEqual(squarePlacement.HeightInCells * 2, defaultPlacement.HeightInCells);
    }

    [TestMethod, Ignore("Owned by #449: P2 background policy is not represented by the current decoder.")]
    public async Task OpaqueBackground_UnpaintedPixelsUsePaletteRegisterZero()
    {
        var fixture = new SixelFixture(
            "opaque-background",
            "P2=0 paints register zero into unset pixels.",
            "0;0q#0;2;0;0;100#1;2;100;0;0#1@"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var grid = TestSeq.Single(terminal.Observe().Placements).PixelGrid;
        Assert.StartsWith("A\nB\nB\nB\nB\nB", grid);
        Assert.Contains("A=#FF0000FF", grid);
        Assert.Contains("B=#0000FFFF", grid);
    }

    [TestMethod]
    public async Task Decgra_PhAndPv_DefineHorizontalAndVerticalExtents()
    {
        var fixture = SixelFixture.Load("raster-extent", "DECGRA declares a 4x7 raster.");
        await using var terminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 1);

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(4, placement.WidthInCells);
        Assert.AreEqual(7, placement.HeightInCells);
    }

    [TestMethod]
    public async Task DeclaredHeight_PartialFinalBand_PreservesSevenPixelExtent()
    {
        var fixture = SixelFixture.Load("raster-extent", "DECGRA declares seven rows but paints one band.");
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(7, TestSeq.Single(terminal.Observe().Placements).PixelHeight);
    }

    [TestMethod]
    public async Task PaintedRaster_BeyondDeclaredExtent_ExpandsToPaintedExtent()
    {
        var payload = new SixelFixture(
            "painted-beyond-declared",
            "Declares 1x1 then paints two columns across two bands.",
            "q\"1;1;1;1~~-~~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            payload.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            payload.Name,
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(2, placement.PixelWidth);
        Assert.AreEqual(12, placement.PixelHeight);
    }

    [TestMethod, Ignore("Owned by #449: palette registers are currently reset for each decoded sequence.")]
    public async Task PaletteDefinition_SubsequentSequence_PersistsRegisterValue()
    {
        var defineRed = new SixelFixture("define-red", "Defines register 5 as red.", "q#5;2;100;0;0@"u8.ToArray());
        var selectRed = new SixelFixture("select-red", "Selects persistent register 5.", "q#5@"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        var bytes = defineRed.StandardBytes
            .Concat(selectRed.StandardBytes)
            .Concat("X"u8.ToArray())
            .ToArray();
        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "persistent palette register",
            TestContext.Current.CancellationToken);

        var placements = terminal.Observe().Placements;
        Assert.AreEqual(2, placements.Count);
        Assert.Contains("#FF0000FF", placements[1].PixelGrid);
    }
}
