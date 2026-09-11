using System.Text;

namespace Hex1b.Tests.Sixel;

/// <summary>
/// Regression coverage for stage #456's snapshot raster-retention contract:
/// a decoded (or geometry-only) <see cref="Hex1b.SixelData"/> instance is
/// retained once per referenced image, never once per covered cell, and
/// multiple independently captured <see cref="Hex1bTerminalSnapshot"/>
/// instances taken from the same live terminal state safely share that same
/// instance rather than each cloning their own copy. Disposing one snapshot
/// must not corrupt, invalidate, or double-release data still reachable
/// through another live snapshot.
/// </summary>
/// <remarks>
/// <see cref="Hex1bTerminalSnapshot"/>'s <c>Dispose()</c> only releases
/// tracked hyperlink references; <see cref="Hex1b.SixelData"/> itself is a
/// plain garbage-collected value with no reference-counted lifetime of its
/// own, so "safe sharing" here means reference identity is preserved across
/// snapshots and disposal of one snapshot has zero observable effect on
/// another snapshot's placements, images, or decoded pixels. See
/// <see cref="SixelPlacementLifetimeTests.IdenticalPayloads_AtDifferentPositions_ShareOneImageAcrossTwoPlacements"/>
/// for the analogous same-snapshot (two placements, one live screen) sharing
/// contract this test extends across independently captured snapshots.
/// </remarks>
[TestClass]
public class SixelSnapshotSharingTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One-band cursor and lifecycle probe.");

    [TestMethod]
    public async Task TwoSnapshots_CapturedFromSameLiveState_ShareTheSameImageInstance()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic present",
            TestContext.Current.CancellationToken);

        using var first = terminal.Terminal.CreateSnapshot();
        using var second = terminal.Terminal.CreateSnapshot();

        var firstPlacement = TestSeq.Single(first.SixelPlacements);
        var secondPlacement = TestSeq.Single(second.SixelPlacements);

        // Independently captured snapshots must not clone the underlying
        // raster: the same content hash resolves to the exact same SixelData
        // instance whether reached through the first or second snapshot.
        Assert.AreSame(firstPlacement.Image, secondPlacement.Image);

        var firstImageEntry = TestSeq.Single(first.SixelImages.Values);
        var secondImageEntry = TestSeq.Single(second.SixelImages.Values);
        Assert.AreSame(firstImageEntry, secondImageEntry);
        Assert.AreSame(firstPlacement.Image, firstImageEntry);
    }

    [TestMethod]
    public async Task DisposingOneSnapshot_LeavesAnotherSnapshotsSixelDataFullyIntact()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic present",
            TestContext.Current.CancellationToken);

        var first = terminal.Terminal.CreateSnapshot();
        using var second = terminal.Terminal.CreateSnapshot();

        var expectedPixels = TestSeq.Single(second.SixelPlacements).Image.GetPixels()!.AsSpan().ToArray();

        // Dispose the first snapshot; the second must remain fully usable —
        // same placement count, same image reference, same pixel content —
        // proving Sixel resources aren't shared-then-released underneath a
        // still-live snapshot.
        first.Dispose();

        Assert.IsTrue(second.ContainsSixelData());
        var survivingPlacement = TestSeq.Single(second.SixelPlacements);
        Assert.IsNotNull(survivingPlacement.Image);
        TestSeq.AreEqual(expectedPixels, survivingPlacement.Image.GetPixels()!.AsSpan().ToArray());
    }

    [TestMethod]
    public async Task DisposingSnapshot_MultipleTimes_IsIdempotentAndDoesNotThrow()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic present",
            TestContext.Current.CancellationToken);

        var snapshot = terminal.Terminal.CreateSnapshot();

        snapshot.Dispose();
        snapshot.Dispose();
        snapshot.Dispose();
    }

    [TestMethod]
    public async Task SnapshotTakenBeforeErase_KeepsItsOwnImageInstance_IndependentOfALaterEmptySnapshot()
    {
        await using var terminal = SixelTestTerminal.Create();

        await terminal.FeedAsync(SingleBand.StandardBytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "graphic before erase",
            TestContext.Current.CancellationToken);

        using var beforeErase = terminal.Terminal.CreateSnapshot();
        var originalImage = TestSeq.Single(beforeErase.SixelPlacements).Image;

        // Erase-in-display (all) removes the placement from the live screen.
        await terminal.FeedAsync(Encoding.ASCII.GetBytes("\x1b[2J"), cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            _ => terminal.Terminal.SixelPlacementCount == 0,
            "graphic cleared from live screen",
            TestContext.Current.CancellationToken);

        Assert.AreEqual(0, terminal.Terminal.TrackedSixelCount);
        Assert.AreEqual(0, terminal.Terminal.SixelPlacementCount);

        // A snapshot captured *after* the erase correctly reflects the now
        // empty live state...
        using var afterErase = terminal.Terminal.CreateSnapshot();
        Assert.IsFalse(afterErase.ContainsSixelData());
        Assert.IsEmpty(afterErase.SixelPlacements);

        // ...while the earlier snapshot's placement/image reference is wholly
        // unaffected by both the live erase and the newer empty snapshot's
        // existence: it keeps observing its own graphic.
        Assert.IsTrue(beforeErase.ContainsSixelData());
        Assert.AreSame(originalImage, TestSeq.Single(beforeErase.SixelPlacements).Image);
    }
}
