using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Regression/integration coverage for stage #451's independent Sixel raster
/// storage and placement state: <see cref="Hex1b.SixelGraphicsState"/> and its
/// per-screen <see cref="Hex1b.SixelImageStore"/>/placement lists.
/// </summary>
/// <remarks>
/// These tests exercise the terminal end-to-end through hand-authored raw DCS
/// Sixel byte sequences (via <see cref="SixelFixture"/>/<see cref="SixelTestTerminal"/>),
/// asserting on the placement/image bookkeeping the old per-cell
/// <c>CellAttributes.Sixel</c> ownership model used to own. They intentionally
/// avoid destructive overlap/erase semantics (#453) and scrolling/reflow
/// projection across the scrollback boundary (#452); see
/// <see cref="SixelScrollingTests"/> and <see cref="SixelTerminalSemanticsTests"/>
/// for the deferred placeholders covering those stages.
/// </remarks>
[TestClass]
public class SixelPlacementLifetimeTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One-band cursor and lifecycle probe.");

    [TestMethod]
    public async Task Placement_SpanningMultipleCells_OccupiesItsEntireDeclaredSpan()
    {
        var wide = new SixelFixture(
            "wide-multirow",
            "A four-column, two-band-tall image occupying multiple cells.",
            "q#1;2;100;0;0#1!4~-!4~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create(cellPixelWidth: 1, cellPixelHeight: 6);

        await terminal.FeedAsync(wide.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            wide.Name,
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.IsGreaterThan(1, placement.WidthInCells);
        Assert.IsGreaterThan(1, placement.HeightInCells);

        var observation = terminal.Observe();
        Assert.HasCount(
            placement.WidthInCells * placement.HeightInCells,
            observation.OccupiedCells);
    }

    [TestMethod]
    public async Task IdenticalPayloads_AtDifferentPositions_ShareOneImageAcrossTwoPlacements()
    {
        await using var terminal = SixelTestTerminal.Create();

        var bytes = SingleBand.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[5;1H"))
            .Concat(SingleBand.StandardBytes)
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => terminal.Terminal.SixelPlacementCount == 2,
            "two placements from identical content",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(2, terminal.Terminal.SixelPlacementCount);

        var placements = terminal.Terminal.SixelPlacements;
        Assert.AreSame(placements[0].Image, placements[1].Image);
        Assert.AreNotEqual(placements[0].Row, placements[1].Row);
    }

    [TestMethod]
    public async Task OverlappingPlacements_AreBothRetained_AndQueryReturnsTheTopmost()
    {
        var red = new SixelFixture(
            "overlap-red",
            "A red band occupying the origin cell.",
            "q#1;2;100;0;0#1~"u8.ToArray());
        var green = new SixelFixture(
            "overlap-green",
            "A green band painted at the same origin afterwards.",
            "q#2;2;0;100;0#2~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        var bytes = red.StandardBytes.Concat(Encoding.ASCII.GetBytes("\x1b[1;1H")).Concat(green.StandardBytes).ToArray();
        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => terminal.Terminal.SixelPlacementCount == 2,
            "two overlapping placements",
            TestContext.Current.CancellationToken);

        // Both rasters remain independently retained even though they occupy
        // the exact same cell span (the old cell-owned model could only ever
        // retain one raster per cell).
        Assert.AreEqual(2, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(2, terminal.Terminal.SixelPlacementCount);

        using var snapshot = terminal.Terminal.CreateSnapshot();
        var topmost = snapshot.GetSixelDataAt(0, 0);
        Assert.IsNotNull(topmost);
        // The green placement was written last, so it is the topmost by
        // write sequence even though the red placement below it still exists.
        Assert.Contains("0;100;0", topmost.Payload);
    }

    [TestMethod]
    public async Task GeometryOnlyPlacement_IsRetainedAsAPlacementNotSilentlyDropped()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => terminal.Terminal.SixelPlacementCount == 1,
            "geometry-only placement retained",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);
        Assert.IsTrue(placement.WidthInCells > 0);
    }

    [TestMethod]
    public async Task OriginCellOverwrittenWithText_DamagesOnlyTheOverwrittenCell()
    {
        var wide = new SixelFixture(
            "origin-overwrite",
            "A two-column band so the origin cell can be overwritten while the second cell still paints.",
            "q#1;2;100;0;0#1!2~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(wide.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            wide.Name,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(1, terminal.Terminal.SixelPlacementCount);

        // Overwrite only the origin cell (column 0) with plain text.
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[1;1HX"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "origin cell overwritten",
            TestContext.Current.CancellationToken);

        // The placement remains reachable only because the second cell still
        // paints; the overwritten origin cell is destructively damaged.
        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(1, terminal.Terminal.SixelPlacementCount);
        Assert.IsNull(terminal.Terminal.GetSixelDataAt(0, 0));
        Assert.IsNotNull(terminal.Terminal.GetSixelDataAt(1, 0));
    }

    [TestMethod]
    public async Task Placement_RemovedFromActiveScreen_StillReachableThroughAnExistingSnapshot()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic before clear",
            TestContext.Current.CancellationToken);

        using var snapshotBeforeClear = terminal.Terminal.CreateSnapshot();
        Assert.IsTrue(snapshotBeforeClear.ContainsSixelData());

        // Erase-in-display (all) removes the placement from the live screen.
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[2J"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 0,
            "graphic cleared from live screen",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);

        // The earlier snapshot copied its own placements/images, so it still
        // observes the graphic even though the live screen released it.
        Assert.IsTrue(snapshotBeforeClear.ContainsSixelData());
        Assert.IsNotNull(snapshotBeforeClear.GetSixelDataAt(0, 0));
    }

    [TestMethod]
    public async Task AlternateScreen_OwnsIndependentPlacementsFromTheMainScreen()
    {
        var alternateFixture = new SixelFixture(
            "alt-only",
            "A green band written only to the alternate screen.",
            "q#2;2;0;100;0#2~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic on main screen",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, terminal.Terminal.SixelPlacementCount);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h").Concat(alternateFixture.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.InAlternateScreen && terminal.Terminal.SixelPlacementCount == 1,
            "graphic on alternate screen",
            TestContext.Current.CancellationToken);

        // The alternate screen's single placement is the *alternate* graphic,
        // not the main screen's (they don't merge or interfere).
        var altPlacement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.Contains("0;100;0", altPlacement.Image.Payload);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049l"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.InAlternateScreen,
            "return to main screen",
            TestContext.Current.CancellationToken);

        // The main screen's original graphic is exactly as it was; the
        // alternate screen's graphic is gone with the alternate state.
        var mainPlacement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.Contains("2;100;0;0", mainPlacement.Image.Payload);
    }

    [TestMethod]
    public async Task RepeatedAlternateScreenEntry_ResetsOnlyTheAlternateState()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic on main screen",
            TestContext.Current.CancellationToken);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h").Concat(SingleBand.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.InAlternateScreen && terminal.Terminal.SixelPlacementCount == 1,
            "first alternate graphic",
            TestContext.Current.CancellationToken);

        // Entering the alternate screen again (already active) resets only
        // the alternate graphics state: the prior alternate placement is gone,
        // and nothing here touches the main screen's placement.
        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 0,
            "re-entering the alternate screen clears the alternate placement",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049l"),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.InAlternateScreen,
            "return to main screen",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.SixelPlacementCount);
    }

    [TestMethod]
    public async Task Ris_ClearsBothMainAndAlternateGraphicsState()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic on main screen",
            TestContext.Current.CancellationToken);

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[?1049h").Concat(SingleBand.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.InAlternateScreen && terminal.Terminal.SixelPlacementCount == 1,
            "graphic on alternate screen",
            TestContext.Current.CancellationToken);

        // The raw byte path does not yet decode ESC c into a RIS token (owned
        // by #453), so drive the reset through the token stream directly,
        // mirroring SixelRasterIntegrationTests.ColorRegisters_AreResetByRis.
        await terminal.FeedPreTokenizedAsync(
            Encoding.ASCII.GetBytes("\x1b" + "c"),
            [RisToken.Instance],
            TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.InAlternateScreen,
            "RIS restores the main screen",
            TestContext.Current.CancellationToken);

        // RIS is a full reset: both the main and the (now-exited) alternate
        // graphics state are cleared, unlike a plain alternate-screen exit
        // which only discards the alternate state.
        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);
    }
}
