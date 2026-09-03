using System.Text;
using Hex1b.Reflow;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelScrollingTests
{
    private static readonly SixelFixture Scrolling = SixelFixture.Load(
        "scrolling",
        "A two-band image used to inspect viewport and history behavior.");

    [TestMethod, Ignore("Owned by #452: a placement that spans the scrollback boundary is not yet split into independent screen/history projections (full-fidelity scrolling/reflow).")]
    public async Task FullScreenScroll_PreservesGraphicAcrossVisibleAndHistoryRows()
    {
        await using var terminal = SixelTestTerminal.Create(
            height: 3,
            scrollbackCapacity: 3);
        var bytes = Scrolling.StandardBytes
            .Concat(Encoding.ASCII.GetBytes("\x1b[3;1H\n"))
            .ToArray();

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ScrollbackLineCount == 1,
            "Sixel projected into scrollback",
            TestContext.Current.CancellationToken);

        var withHistory = terminal.Observe();
        var visibleOnly = terminal.Observe(includeScrollback: false);
        Assert.IsTrue(withHistory.OccupiedRows.Any(row => row.InScrollback));
        Assert.IsTrue(withHistory.OccupiedRows.Any(row => !row.InScrollback));
        Assert.IsNotEmpty(visibleOnly.OccupiedRows);
    }

    [TestMethod]
    public async Task ResizeNarrower_ClipsGraphicWithoutDestroyingSourceState()
    {
        var fixture = new SixelFixture(
            "wide",
            "A four-column full-height band.",
            "q#1;2;100;0;0~~~~"u8.ToArray());
        await using var terminal = SixelTestTerminal.Create(
            width: 6,
            height: 4,
            cellPixelWidth: 1,
            cellPixelHeight: 6);
        await terminal.FeedAsync(
            fixture.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            fixture.Name,
            TestContext.Current.CancellationToken);

        terminal.Terminal.Resize(2, 4);
        var narrow = terminal.Observe(includeScrollback: false);
        Assert.AreEqual(4, TestSeq.Single(narrow.Placements).PixelWidth);
        Assert.IsTrue(narrow.OccupiedCells.All(cell => cell.Column < 2));

        terminal.Terminal.Resize(6, 4);
        var restored = terminal.Observe(includeScrollback: false);
        Assert.AreEqual(4, TestSeq.Single(restored.Placements).PixelWidth);
        Assert.IsTrue(restored.OccupiedCells.Any(cell => cell.Column == 3));
    }

    [TestMethod, Ignore("Owned by #452: Sixel placements do not yet participate in row-lineage reflow.")]
    public async Task Reflow_WiderThenNarrower_PreservesGraphicAnchor()
    {
        await using var terminal = SixelTestTerminal.Create(
            width: 5,
            height: 5,
            reflow: KittyReflowStrategy.Instance);
        var bytes = Encoding.ASCII.GetBytes("ABCDEF")
            .Concat(Scrolling.StandardBytes)
            .ToArray();
        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "Sixel anchored after wrapped text",
            TestContext.Current.CancellationToken);

        var before = TestSeq.Single(terminal.Observe().Placements);
        terminal.Terminal.Resize(10, 5);
        var wider = TestSeq.Single(terminal.Observe().Placements);
        Assert.AreEqual(6, wider.OriginColumn);
        Assert.AreEqual(0, wider.OriginRow);

        terminal.Terminal.Resize(5, 5);
        var after = TestSeq.Single(terminal.Observe().Placements);

        Assert.AreNotEqual(before.OriginColumn, wider.OriginColumn);
        Assert.AreNotEqual(before.OriginRow, wider.OriginRow);
        Assert.AreEqual(before.OriginColumn, after.OriginColumn);
        Assert.AreEqual(before.OriginRow, after.OriginRow);
    }
}
