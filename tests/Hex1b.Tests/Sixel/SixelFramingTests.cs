using System.Text;

namespace Hex1b.Tests.Sixel;

[TestClass]
public class SixelFramingTests
{
    private static readonly SixelFixture SingleBand = SixelFixture.Load(
        "single-band",
        "One red pixel at the top of a six-pixel band.");

    [TestMethod]
    public async Task StandardFraming_EverySplitBoundary_ProducesIdenticalModelAndPresentationBytes()
    {
        var runs = await SixelTestTerminal.ObserveEverySplitAsync(
            SingleBand,
            TestContext.Current.CancellationToken);
        var baseline = runs[0].Observation.ModelFingerprint();

        Assert.AreEqual(SingleBand.StandardBytes.Length, runs.Count);
        foreach (var run in runs)
        {
            TestSeq.AreEqual(
                SingleBand.StandardBytes,
                run.PresentationBytes,
                $"Presentation bytes changed at split boundary {run.SplitBoundary}.");
            Assert.AreEqual(
                baseline,
                run.Observation.ModelFingerprint(),
                $"Terminal model changed at split boundary {run.SplitBoundary}.");
        }
    }

    [TestMethod]
    public async Task NativePassthrough_OneByteChunks_ForwardsBytesExactly()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = SingleBand.StandardBytes;

        await terminal.FeedAsync(
            bytes,
            Enumerable.Repeat(1, bytes.Length).ToArray(),
            TestContext.Current.CancellationToken);

        TestSeq.AreEqual(bytes, terminal.PresentationBytes);
    }

    [TestMethod, Ignore("Owned by #446: C1 bytes are currently decoded as invalid UTF-8 before DCS framing.")]
    public async Task C1Framing_CompatibilityInput_ProducesSameModelAsStandardFraming()
    {
        await using var standard = SixelTestTerminal.Create();
        await standard.FeedAsync(
            SingleBand.StandardBytes,
            cancellationToken: TestContext.Current.CancellationToken);
        await standard.WaitForAsync(
            snapshot => snapshot.ContainsSixelData(),
            "standard-framed Sixel",
            TestContext.Current.CancellationToken);

        var expectedModel = standard.Observe().ModelFingerprint();
        var runs = await SixelTestTerminal.ObserveEverySplitAsync(
            SingleBand,
            TestContext.Current.CancellationToken,
            useC1Framing: true);

        Assert.AreEqual(SingleBand.C1Bytes.Length, runs.Count);
        foreach (var run in runs)
        {
            TestSeq.AreEqual(
                SingleBand.C1Bytes,
                run.PresentationBytes,
                $"C1 presentation bytes changed at split boundary {run.SplitBoundary}.");
            Assert.AreEqual(expectedModel, run.Observation.ModelFingerprint());
        }
    }

    [TestMethod, Ignore("Owned by #446: every complete DCS token is currently dispatched as Sixel.")]
    public async Task NonSixelDcs_CompleteSequence_IsNotTrackedAsSixel()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes("\x1bP1+r544e\x1b\\X");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following a non-Sixel DCS",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }

    [TestMethod, Ignore("Owned by #446: DCS cancellation is not represented in the current string-based tokenizer.")]
    public async Task Cancel_IncompleteSixel_DiscardsGraphicAndResumesText()
    {
        await using var terminal = SixelTestTerminal.Create();
        var bytes = Encoding.ASCII.GetBytes("\x1bPq#1;2;100;0;0@\x18X");

        await terminal.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await terminal.WaitForAsync(
            snapshot => snapshot.ContainsText("X"),
            "text following a cancelled Sixel DCS",
            TestContext.Current.CancellationToken);

        Assert.IsEmpty(terminal.Observe().Placements);
    }
}
