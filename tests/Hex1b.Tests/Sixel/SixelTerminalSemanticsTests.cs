using System.Text;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelTerminalSemanticsTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One-band cursor and lifecycle probe.");

    [TestMethod]
    public async Task DecsdmEnabled_SequenceEndsWithCursorBelowGraphic()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes("\x1b[?80h\x1b[3;4H")
            .Concat(SingleBand.StandardBytes)
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "DECSDM cursor movement",
            TestContext.Current.CancellationToken);

        var observation = terminal.Observe();
        var placement = TestSeq.Single(observation.Placements);

        // The fixture omits P1, so the default 2:1 aspect renders one six-pixel
        // band as twelve device pixels — two rows at the harness's six-pixel cells.
        Assert.AreEqual(2, placement.OriginRow);
        Assert.AreEqual(2, placement.HeightInCells);

        // Mode 8452 is reset, so the cursor returns to the column the sequence
        // started in, one row below the last row the graphic occupies.
        Assert.AreEqual(3, observation.CursorX);
        Assert.AreEqual(placement.OriginRow + placement.HeightInCells, observation.CursorY);
    }

    [TestMethod]
    public async Task OriginMode_WithMargins_AnchorsGraphicAtMarginRelativeCursor()
    {
        await using var terminal = SixelTestTerminal.Create(width: 12, height: 8);
        var prefix = Encoding.ASCII.GetBytes("\x1b[?69h\x1b[3;10s\x1b[2;7r\x1b[?6h\x1b[1;1H");
        var bytes = prefix.Concat(SingleBand.StandardBytes).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "margin-relative Sixel origin",
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(2, placement.OriginColumn);
        Assert.AreEqual(1, placement.OriginRow);
    }

    [TestMethod, Ignore("Owned by #453: text overlap must operate on independent graphics placements.")]
    public async Task TextWrittenOverGraphic_DamagesOnlyOverlappedGraphicArea()
    {
        var wide = new SixelFixture(
            "wide-red",
            "Four red columns provide damaged and undamaged regions.",
            "q#1;2;100;0;0#1!4~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 6);
        var bytes = wide.StandardBytes.Concat(Encoding.ASCII.GetBytes("\x1b[1;2HX")).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text overlapping Sixel",
            TestContext.Current.CancellationToken);

        Assert.StartsWith(
            "A.AA\nA.AA\nA.AA\nA.AA\nA.AA\nA.AA",
            terminal.Observe().CompositePixelGrid());
    }

    [TestMethod]
    public async Task EraseDisplay_ClearsOverlappingGraphic()
    {
        await using var terminal = SixelTestTerminal.Create();
        await terminal.FeedAsync(
            SingleBand.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "Sixel before ED",
            TestContext.Current.CancellationToken);
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2JX"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "ED clearing Sixel state",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod, Ignore("Owned by #453: overlapping Sixel sequences retain independent placements (see SixelPlacementLifetimeTests), but pixel-level transparent compositing of overlapping rasters for rendering is deferred.")]
    public async Task TransparentSixelOverExistingSixel_PreservesUnpaintedPixels()
    {
        var baseImage = new SixelFixture(
            "red-pair",
            "Two red top pixels provide an underlying raster.",
            "q#1;2;100;0;0#1@@"u8.ToArray());
        var transparent = new SixelFixture(
            "transparent-green",
            "Paints green at the left while preserving the right pixel.",
            "0;1q#2;2;0;100;0#2A?"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 6);
        var bytes = baseImage.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[1;1H"))
            .Concat(transparent.StandardBytes)
            .Concat("X"u8.ToArray())
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "overlapping transparent Sixel",
            TestContext.Current.CancellationToken);

        var composite = terminal.Observe().CompositePixelGrid();
        Assert.StartsWith("AA\nB.", composite);
    }

    [TestMethod]
    public async Task Ris_ClearsGraphicsAndResetsPalette()
    {
        var defineRegister = new SixelFixture(
            "define-register",
            "Defines register 5 as red before reset.",
            "q#5;2;100;0;0#5@"u8.ToArray());
        var selectAfterReset = new SixelFixture(
            "select-after-reset",
            "Selects register 5 after reset without redefining it.",
            "q#5@"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();
        await using var freshTerminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            defineRegister.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic before RIS",
            TestContext.Current.CancellationToken);

        // The raw byte path does not yet decode ESC c into a RIS token (owned by
        // #453), so drive the reset through the token stream directly, mirroring
        // SixelRasterIntegrationTests.ColorRegisters_AreResetByRis.
        await terminal.FeedPreTokenizedAsync(
            Encoding.ASCII.GetBytes("\x1bc"),
            [Hex1b.Tokens.RisToken.Instance],
            TestContext.Current.CancellationToken);
        await terminal.FeedAsync(
            selectAfterReset.StandardBytes.Concat("X"u8.ToArray()).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "RIS clearing Sixel state",
            TestContext.Current.CancellationToken);

        var freshBytes = selectAfterReset.StandardBytes.Concat("X"u8.ToArray()).ToArray();
        await freshTerminal.FeedAsync(
            freshBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await freshTerminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "fresh default palette register",
            TestContext.Current.CancellationToken);

        var afterReset = TestSeq.Single(terminal.Observe().Placements).PixelGrid;
        var freshDefault = TestSeq.Single(freshTerminal.Observe().Placements).PixelGrid;
        Assert.AreEqual(freshDefault, afterReset);
        Assert.DoesNotContain("#FF0000FF", afterReset);
    }

    [TestMethod]
    public async Task AlternateScreenExit_RestoresMainScreenGraphic()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = SingleBand.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[?1049hALT\x1b[?1049lMAIN"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.InAlternateScreen && snapshot.ContainsText("MAIN"),
            "return to main screen",
            TestContext.Current.CancellationToken);

        Assert.HasCount(1, terminal.Observe().Placements);
    }
}
