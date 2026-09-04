// Copyright (c) Hex1b contributors. Licensed under the MIT license.

using System.Text;
using Hex1b.Automation;
using Hex1b.Reflow;
using Hex1b.Sixel;
using Hex1b.Testing;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Integration coverage for issue #452: independent Sixel placements moving
/// through terminal scrolling, main-screen scrollback history, viewport
/// clipping, resize, and reflow. These tests exercise raw DCS byte sequences
/// only (never <c>SixelWidget</c>/<c>SixelEncoder</c>) and assert against the
/// production <see cref="Hex1bTerminal"/> Sixel accessors, mirroring the
/// conventions established by <see cref="KgpScrollingTests"/> for the KGP
/// protocol but adapted to Sixel's simpler anonymous-placement model (no IDs,
/// no relative graph, no z-index).
/// </summary>
[TestClass]
public class SixelScrollHistoryReflowTests
{
    // 1x12 px, 1:1 aspect, two full six-pixel bands -> two occupied rows at
    // the harness's default 1x6 cell metrics. Reused from SixelScrollingTests'
    // "scrolling" fixture convention (explicit raster header avoids the
    // default 2:1 vertical aspect doubling that a bare "q...@" payload gets).
    private static readonly SixelFixture TwoRowBar = SixelFixture.Load(
        "scrolling",
        "Two-band bar used as a two-row-tall scroll/history probe.");

    // 1x18 px, 1:1 aspect, three full six-pixel bands -> three occupied rows.
    private static readonly SixelFixture ThreeRowBar = new(
        "three-row-bar",
        "Three-band bar used as a three-row-tall progressive-crop probe.",
        Encoding.ASCII.GetBytes("q\"1;1;1;18#1;2;100;0;0~-~-~"));

    // 3x6 px, 1:1 aspect, one band, three columns -> one row, three columns.
    private static readonly SixelFixture OneRowThreeCol = new(
        "one-row-three-col",
        "Single-row, three-column bar used for horizontal-margin probes.",
        Encoding.ASCII.GetBytes("q\"1;1;3;6#1;2;100;0;0~~~"));

    // 1x6 px, 1:1 aspect, one band -> exactly one occupied row (unlike the
    // "single-band" shared fixture, which relies on the default 2:1 aspect
    // and therefore occupies two rows).
    private static readonly SixelFixture OneRowBar = new(
        "one-row-bar",
        "Single-row bar used for capacity-one pruning probes.",
        Encoding.ASCII.GetBytes("q\"1;1;1;6#1;2;100;0;0@"));

    // 2x6 px, 1:1 aspect, one band, two columns -> one row, two columns. Used
    // for the damage-persistence probe so only one of the two columns is
    // overwritten with text.
    private static readonly SixelFixture TwoColBar = new(
        "two-col-bar",
        "Single-row, two-column bar used for the damage-persistence probe.",
        Encoding.ASCII.GetBytes("q\"1;1;2;6#1;2;100;0;0~~"));

    [TestMethod]
    [DataRow("\n", DisplayName = "LF")]
    [DataRow("\u001bD", DisplayName = "IND")]
    public async Task ScrollUpEquivalents_LineFeedAndIndex_BothMoveDepartingRowIntoHistory(string terminator)
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 3);
        var bytes = TwoRowBar.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H" + terminator))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "scroll into history",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);
        Assert.HasCount(1, terminal.Terminal.SixelPlacements);
        Assert.AreEqual(1, terminal.Terminal.SixelPlacements[0].PaintedRowCount);
    }

    [TestMethod]
    [DataRow("\u001bM", DisplayName = "RI")]
    [DataRow("\u001b[T", DisplayName = "SD")]
    public async Task ScrollDownEquivalents_ReverseIndexAndScrollDown_ShiftPlacementDownWithinMargin(string sequence)
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5);

        // Cursor is parked one row above the region bottom so painting the
        // two-row graphic needs a row for the cursor to land on below it;
        // ResolveSixelPlacement pre-scrolls the region up once to make room,
        // landing the placement at Row=1 (0-based) instead of Row=3.
        var prefix = Encoding.ASCII.GetBytes("\x1b[2;4r\x1b[4;1H");
        var bytes = prefix.Concat(TwoRowBar.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[2;1H" + sequence))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "shift within vertical margin",
            TestContext.Current.CancellationToken);

        // Placement lands at Row=1 (Top=1,Bottom=2) after the pre-scroll, then
        // RI/SD (issued with the cursor at the region's top margin) shifts it
        // down by one row within the [1,3] region: Top=2, Bottom=3.
        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(2, placement.PaintedTop);
    }

    [TestMethod]
    public async Task HorizontalMargins_ScrollUp_LeavesPlacementsNotWhollyContainedUntouched()
    {
        await using var terminal = SixelTestTerminal.Create(width: 12, height: 3);

        // DECLRMM clips painting unconditionally to the horizontal margin
        // (cols 4-8, 1-based), so a placement anchored entirely outside it
        // paints nothing (HasPaintedExtent is false). Containment then falls
        // back to the declared footprint, which is outside the margin on
        // both sides here (cols 1-3 and cols 9-11, 1-based) - so SU must
        // leave both untouched (Row stays 0) rather than shifting them.
        var prefix = Encoding.ASCII.GetBytes("\x1b[?69h\x1b[4;8s\x1b[1;1H");
        var second = Encoding.ASCII.GetBytes("\x1b[1;9H");
        var bytes = prefix
            .Concat(OneRowThreeCol.StandardBytes)
            .Concat(second)
            .Concat(OneRowThreeCol.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[S"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements.Count == 2,
            "both placements created (neither paints visibly, so ContainsSixelData never becomes true)",
            TestContext.Current.CancellationToken);

        Assert.HasCount(2, terminal.Terminal.SixelPlacements);
        foreach (var placement in terminal.Terminal.SixelPlacements)
        {
            Assert.AreEqual(0, placement.Row);
        }
    }

    [TestMethod]
    public async Task HorizontalMargins_ScrollUp_ShiftsWhollyContainedPlacementUpByOne()
    {
        await using var terminal = SixelTestTerminal.Create(width: 12, height: 3);

        // A one-row placement leaves a row of headroom above it in the
        // full-height region, so the shift is clean (no cropping needed) -
        // unlike a two-row placement here, which would fill the region
        // exactly and trigger ResolveSixelPlacement's pre-scroll-for-cursor
        // room, or get cropped by the very shift being tested.
        var prefix = Encoding.ASCII.GetBytes("\x1b[?69h\x1b[4;8s\x1b[2;5H");
        var bytes = prefix
            .Concat(OneRowThreeCol.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[S"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "horizontal-margin shift",
            TestContext.Current.CancellationToken);

        var placement = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(1, placement.PaintedRowCount);
    }

    [TestMethod]
    public async Task PartialVerticalMargins_ScrollUp_CropsThenRemovesContainedPlacement()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5);
        var prefix = Encoding.ASCII.GetBytes("\x1b[2;4r\x1b[2;1H");
        var scrollOnce = Encoding.ASCII.GetBytes("\x1b[S");
        var bytes = prefix.Concat(ThreeRowBar.StandardBytes).Concat(scrollOnce).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "first partial-margin scroll",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(2, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements is [{ } p] && p.PaintedRowCount == 1,
            "second partial-margin scroll crops to a single row",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements.Count == 0,
            "third partial-margin scroll removes remainder",
            TestContext.Current.CancellationToken);
        Assert.IsEmpty(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(0, terminal.Terminal.ScrollbackCount);
    }

    [TestMethod]
    public async Task PartialVerticalMargins_ScrollDown_CropsBottomThenRemovesContainedPlacement()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5);
        var prefix = Encoding.ASCII.GetBytes("\x1b[2;5r\x1b[4;1H");
        var scrollOnce = Encoding.ASCII.GetBytes("\x1b[T");
        var bytes = prefix.Concat(TwoRowBar.StandardBytes).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created inside vertical margin",
            TestContext.Current.CancellationToken);
        // The cursor (row 3, 0-based) plus the graphic's height overflows the
        // region bottom (row 4) by exactly the one row the cursor itself needs
        // to land on below the graphic, so ResolveSixelPlacement's proactive
        // pre-scroll-for-cursor-room mechanism fires once before the placement
        // is even created, landing it at row 2 (rows 2-3) instead of row 3.
        Assert.AreEqual(2, TestSeq.Single(terminal.Terminal.SixelPlacements).Row);
        Assert.AreEqual(2, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        // First reverse-margin scroll shifts rows 2-3 down to rows 3-4: still
        // fully inside the region (bottom row 4 == region bottom), so nothing
        // is cropped yet.
        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements is [{ } p] && p.Row == 3,
            "first reverse-margin scroll shifts the placement flush to the region bottom",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(2, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        // Second reverse-margin scroll shifts rows 3-4 down to rows 4-5: row 5
        // falls outside the region bottom (row 4) and is cropped away.
        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements is [{ } p] && p.PaintedRowCount == 1,
            "second reverse-margin scroll crops the bottom row",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        // Third reverse-margin scroll pushes the remaining row entirely past
        // the region bottom, removing the placement.
        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements.Count == 0,
            "third reverse-margin scroll removes remainder",
            TestContext.Current.CancellationToken);
        Assert.IsEmpty(terminal.Terminal.SixelPlacements);
    }

    [TestMethod]
    public async Task PartialVerticalMargins_FullyDepartingSingleRowPlacement_MovesIntoHistory()
    {
        // The physical terminal (5 rows) is taller than the DECSTBM region
        // (rows 1-3, i.e. 0-based rows 0-2): unlike every other history test
        // in this suite, the scroll region here does not span the whole
        // screen. A scroll within that partial region still departs row 0
        // into the terminal's own text scrollback, and a placement that
        // fully occupies the departing row (this fixture is exactly one row
        // tall, anchored at the region's top) must transfer into Sixel
        // history the same way it would under a full-height region -- it
        // must not be silently dropped just because the region is partial.
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5, scrollbackCapacity: 3);
        var prefix = Encoding.ASCII.GetBytes("\x1b[1;3r\x1b[1;1H");
        var bytes = prefix.Concat(OneRowBar.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "partial-margin scroll captures the departing row",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);
        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.IsEmpty(terminal.Terminal.SixelPlacements);
    }

    [TestMethod]
    public async Task ScrollDown_AfterScrollUpIntoHistory_DoesNotResurrectTheDepartedRow()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 3);
        var bytes = TwoRowBar.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "scroll into history",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);
        Assert.AreEqual(1, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\x1b[T"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "reverse scroll after history capture",
            TestContext.Current.CancellationToken);

        // The departed row lives only in history now; scrolling back down must
        // not resurrect it into the live remainder (it must stay a single row).
        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);
        Assert.AreEqual(1, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);
    }

    [TestMethod]
    public async Task DecsdmEnabled_ExplicitLineFeed_StillMovesPlacementIntoHistory()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 3);
        var bytes = Encoding.ASCII.GetBytes("\x1b[?80h")
            .Concat(TwoRowBar.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "DECSDM does not gate ordinary scrolling",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);
    }

    [TestMethod]
    public async Task CapacityOnePruning_OrdinaryScrollEntry_DropsHistoryPlacementOnNextEviction()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 1);
        var bytes = OneRowBar.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "first scroll moves placement to the sole history slot",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);
        Assert.IsEmpty(terminal.Terminal.SixelPlacements);

        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\n"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.TrackedSixelCount == 0,
            "second scroll evicts the capacity-one buffer and drops the image",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
    }

    [TestMethod]
    public async Task CapacityTwoPruning_ReflowCreatedMultiRowEntry_TransfersRemainderAcrossEvictions()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 4,
            height: 3,
            scrollbackCapacity: 2,
            reflow: KittyReflowStrategy.Instance);

        await terminal.FeedAsync(TwoRowBar.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created at row 0",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0, TestSeq.Single(terminal.Terminal.SixelPlacements).Row);

        // Shrinking height alone is sufficient to trigger reflow. With no
        // history yet and the placement anchored at row 0, its single reflow
        // anchor maps to unified row 0 -- the oldest eventual history slot --
        // so the whole two-row placement is attributed to one history entry.
        terminal.Terminal.Resize(4, 1);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount == 2,
            "reflow moves the whole placement into one history entry",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);
        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);

        // First ordinary scroll evicts the oldest history slot: the reflow
        // entry has RetainedRows > 1, so it transfers its remainder to the
        // successor slot instead of being dropped.
        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\n"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.ScrollbackCount == 2,
            "first scroll transfers the remainder to the successor slot",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, terminal.Terminal.TrackedSixelCount);

        // Second ordinary scroll evicts the (now single-row) transferred
        // entry outright; there is no remainder left to transfer.
        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\n"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.TrackedSixelCount == 0,
            "second scroll drops the transferred remainder",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
    }

    [TestMethod]
    public async Task ProgressiveCrop_EveryScrollStepShrinksPaintedExtentByOneRow()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 5);
        var prefix = Encoding.ASCII.GetBytes("\x1b[2;4r\x1b[2;1H");
        var scrollOnce = Encoding.ASCII.GetBytes("\x1b[S");
        var bytes = prefix.Concat(ThreeRowBar.StandardBytes).ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created wholly inside margins",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(3, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        for (var expected = 2; expected >= 1; expected--)
        {
            await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
            var target = expected;
            await terminal.WaitForAsync(
                _ => terminal.Terminal.SixelPlacements is [{ } p] && p.PaintedRowCount == target,
                $"crop step to {expected} painted rows",
                TestContext.Current.CancellationToken);
        }

        await terminal.FeedAsync(scrollOnce, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => !snapshot.ContainsSixelData(),
            "final scroll removes the fully cropped placement",
            TestContext.Current.CancellationToken);
        Assert.IsEmpty(terminal.Terminal.SixelPlacements);
    }

    [TestMethod]
    public async Task ResizeTallerThenShorter_ClipsGraphicWithoutDestroyingSourceState()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 6);
        await terminal.FeedAsync(ThreeRowBar.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(3, TestSeq.Single(terminal.Terminal.SixelPlacements).HeightInCells);

        terminal.Terminal.Resize(8, 8);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "resize taller keeps the declared footprint",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(3, TestSeq.Single(terminal.Terminal.SixelPlacements).HeightInCells);
        Assert.AreEqual(3, TestSeq.Single(terminal.Terminal.SixelPlacements).PaintedRowCount);

        // Resize/clip-without-reflow never mutates the placement's own painted
        // window (only scroll-margin cropping and history/snapshot slicing do
        // that) - only viewport observation narrows, and only for rows the new,
        // shorter viewport can no longer show. The declared footprint and the
        // placement's own PaintedRowCount both stay at 3.
        terminal.Terminal.Resize(8, 2);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "resize shorter clips the viewport without destroying source state",
            TestContext.Current.CancellationToken);
        var clipped = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(3, clipped.HeightInCells, "declared footprint is never mutated by resize/clip");
        Assert.AreEqual(3, clipped.PaintedRowCount, "the placement's own painted window is never mutated by a viewport-only resize");

        var observedShort = terminal.Observe(includeScrollback: false);
        Assert.IsTrue(observedShort.OccupiedCells.All(cell => cell.Row < 2), "the shorter viewport can only observe rows within its own bounds");

        terminal.Terminal.Resize(8, 8);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "resize back taller reveals the previously off-screen rows",
            TestContext.Current.CancellationToken);
        var observedRestored = terminal.Observe(includeScrollback: false);
        Assert.IsTrue(observedRestored.OccupiedCells.Any(cell => cell.Row == 2), "resizing back taller must reveal rows never actually destroyed by the shorter viewport");
    }

    [TestMethod]
    public async Task ResizeWider_RevealsColumnsPreviouslyClippedByANarrowerViewport()
    {
        // Paint fully unclipped at the initial wide terminal (all three columns
        // painted) so that the later narrowing is a viewport-only clip - not
        // the separate, permanent paint-time clip against the active width
        // that ResolveSixelPlacement applies (which a later resize can never
        // retroactively undo, unlike a plain viewport resize).
        await using var terminal = SixelTestTerminal.Create(width: 6, height: 3);
        await terminal.FeedAsync(OneRowThreeCol.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created unclipped at the wide terminal",
            TestContext.Current.CancellationToken);
        var full = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(3, full.WidthInCells);
        Assert.AreEqual(3, full.PaintedColumnCount, "nothing to clip at paint time when the viewport is already wide enough");

        terminal.Terminal.Resize(2, 3);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "resize narrower clips the observed viewport without destroying source state",
            TestContext.Current.CancellationToken);
        var narrowed = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(3, narrowed.WidthInCells, "declared footprint is never mutated by a viewport-only resize");
        Assert.AreEqual(3, narrowed.PaintedColumnCount, "the placement's own painted window is never mutated by a viewport-only resize");
        var observedNarrow = terminal.Observe(includeScrollback: false);
        Assert.IsTrue(observedNarrow.OccupiedCells.All(cell => cell.Column < 2), "the narrower viewport can only observe columns within its own bounds");

        terminal.Terminal.Resize(6, 3);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "resize wider reveals the previously clipped columns",
            TestContext.Current.CancellationToken);

        // The placement's own painted window was never mutated by the narrower
        // viewport (only the observed viewport was narrower) - it's the
        // Observe() projection, bounded by the *current* viewport, that
        // reveals the full three columns without any reflow.
        var wide = TestSeq.Single(terminal.Terminal.SixelPlacements);
        Assert.AreEqual(3, wide.WidthInCells, "declared footprint is unaffected by viewport resize");
        var observedWide = terminal.Observe(includeScrollback: false);
        Assert.IsTrue(observedWide.OccupiedCells.Any(cell => cell.Column == 2), "resizing wider must reveal columns never actually destroyed by the narrower viewport");
    }

    [TestMethod]
    public async Task FractionalPixelWidth_CeilsToAWholeOccupiedCellIncludingItsPartialRemainder()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3);
        var fractional = new SixelFixture(
            "fractional-width",
            "Five-pixel-wide raster over one-pixel-wide cells.",
            Encoding.ASCII.GetBytes("q\"1;1;5;6#1;2;100;0;0!5@"));

        await terminal.FeedAsync(fractional.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "fractional-width placement created",
            TestContext.Current.CancellationToken);

        var observation = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(5, observation.PixelWidth);
        Assert.AreEqual(5, observation.WidthInCells, "1px-wide cells: no partial-remainder rounding needed here");
    }

    [TestMethod]
    public async Task ProtocolMetricChange_WithoutResize_LeavesExistingPlacementUnaffected()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 8);
        await terminal.FeedAsync(TwoRowBar.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created under the original cell metrics",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(2, TestSeq.Single(terminal.Terminal.SixelPlacements).HeightInCells);

        terminal.Terminal.SetSixelCellMetrics(new SixelCellMetrics(
            1,
            3,
            SixelCellMetricsSource.Direct,
            SixelCellMetricsReliability.Authoritative));

        await terminal.FeedAsync(
            Encoding.ASCII.GetBytes("\x1b[4;1H").Concat(TwoRowBar.StandardBytes).ToArray(),
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements.Count == 2,
            "second placement created under the changed cell metrics",
            TestContext.Current.CancellationToken);

        var placements = terminal.Terminal.SixelPlacements;
        Assert.AreEqual(2, placements[0].HeightInCells, "the metric change does not retroactively affect an existing placement");
        Assert.AreEqual(4, placements[1].HeightInCells, "the same 12px payload now spans four 3px-tall cells");
    }

    [TestMethod]
    public async Task AlternateScreenScrolling_NeverCreatesOrAffectsMainScreenHistory()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 4);
        var mainBytes = TwoRowBar.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();
        await terminal.FeedAsync(mainBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "main-screen history captured",
            TestContext.Current.CancellationToken);
        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount);

        var altBytes = Encoding.ASCII.GetBytes("\x1b[?1049h")
            .Concat(Encoding.ASCII.GetBytes("\n\n\n\n"))
            .Concat(Encoding.ASCII.GetBytes("\x1b[?1049l"))
            .ToArray();
        await terminal.FeedAsync(altBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "alternate screen scrolling settles back on the main screen",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(1, terminal.Terminal.ScrollbackCount, "alternate-screen scrolling must never touch main history");
    }

    [TestMethod]
    public async Task ScrollbackWidthProjection_OriginalPreservesContentClippedUnderCurrentWidth()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 2);
        var bytes = Encoding.ASCII.GetBytes("\x1b[1;5H")
            .Concat(OneRowThreeCol.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "placement captured into history at the original width",
            TestContext.Current.CancellationToken);

        terminal.Terminal.Resize(6, 3);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "narrower terminal",
            TestContext.Current.CancellationToken);

        var current = terminal.Observe(includeScrollback: true, scrollbackWidth: ScrollbackWidth.CurrentTerminal);
        var original = terminal.Observe(includeScrollback: true, scrollbackWidth: ScrollbackWidth.Original);

        var currentOccupied = current.OccupiedCells.Count(c => c.Row == 0);
        var originalOccupied = original.OccupiedCells.Count(c => c.Row == 0);
        Assert.IsGreaterThan(currentOccupied, originalOccupied, "the original-width projection preserves columns clipped away under the current width");
    }

    [TestMethod]
    public async Task DamageAppliedBeforeScroll_PersistsAcrossScrollAndSnapshot()
    {
        // Painting a 1-row-tall placement at row 0 (0-based) leaves no overflow
        // against the bottom margin (row0 + height1 <= clipBottom2), so it does
        // NOT trigger ResolveSixelPlacement's proactive pre-scroll-for-cursor-room
        // mechanism - unlike painting at the screen's last row, which would move
        // the placement one row higher than expected before we ever damage it.
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 3, scrollbackCapacity: 3);
        var bytes = Encoding.ASCII.GetBytes("\x1b[1;1H")
            .Concat(TwoColBar.StandardBytes)
            .Concat(Encoding.ASCII.GetBytes("\x1b[1;1HX"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacements is [{ } p] && p.CoversCell(p.PaintedTop, 0) == false,
            "damage applied to the placement's origin cell",
            TestContext.Current.CancellationToken);

        // Now scroll the damaged placement (still at row 0) into history: the
        // cursor is on the bottom row, so a plain LF scrolls the whole screen
        // up by one, pushing row 0 - the placement's row - into scrollback.
        var scroll = Encoding.ASCII.GetBytes("\x1b[3;1H\n");
        await terminal.FeedAsync(scroll, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount > 0,
            "damaged placement scrolled into history",
            TestContext.Current.CancellationToken);

        var snapshot = terminal.Terminal.CreateSnapshot(scrollbackLines: 1);
        var historyPlacement = TestSeq.Single(snapshot.SixelPlacements);
        var row = historyPlacement.PaintedTop;

        Assert.IsFalse(historyPlacement.CoversCell(row, 0), "the overwritten origin cell must remain damaged after the scroll+snapshot round trip");
        Assert.IsTrue(historyPlacement.CoversCell(row, 1), "the sibling cell was never overwritten and must remain covered");
    }

    [TestMethod]
    public async Task FinalHistoryReferenceRelease_ReleasesTrackedImageOnceAllReferencesAreGone()
    {
        await using var terminal = SixelTestTerminal.Create(width: 8, height: 1, scrollbackCapacity: 1);
        await terminal.FeedAsync(OneRowBar.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "placement created",
            TestContext.Current.CancellationToken);

        var snapshotWhileLive = terminal.Terminal.CreateSnapshot();
        Assert.IsTrue(snapshotWhileLive.ContainsSixelData());

        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\n"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.TrackedSixelCount == 1,
            "the single-row placement moves entirely into the sole history slot",
            TestContext.Current.CancellationToken);

        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\n"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.TrackedSixelCount == 0,
            "the next eviction drops the last live reference",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
        Assert.IsTrue(snapshotWhileLive.ContainsSixelData(), "a previously captured snapshot keeps its own independent reference");
    }
}
