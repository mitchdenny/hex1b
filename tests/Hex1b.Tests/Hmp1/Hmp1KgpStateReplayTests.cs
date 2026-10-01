using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Hex1b.Automation;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1KgpStateReplayTests
{
    [TestMethod]
    public async Task WriteAsync_CompressedRoot_ChunksEncodedBytesWithMatchingCompressionControl()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(workload, TimeProvider.System);
        var pixels = new byte[1024 * 512 * 4];
        var encoded = Compress(pixels);
        Assert.IsGreaterThan(3072, encoded.Length);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1024,v=512,o=z,i=7,c=1,r=1,C=1,q=2", encoded)));
        using var snapshot = producer.CreateSnapshot(includeAllKgpImages: true);
        Assert.AreEqual(1, snapshot.KgpImages.Count);
        using var stream = new MemoryStream();
        await Hmp1KgpStateReplay.WriteAsync(stream, snapshot.KgpPlacements, snapshot.KgpImages,
            snapshot.CursorX, snapshot.CursorY, TestContext.Current.CancellationToken);
        Assert.IsLessThan((long)pixels.Length / 8, stream.Length,
            "Replay must transmit the retained encoded root, not materialized pixels.");
        stream.Position = 0;
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, TimeProvider.System);
        var wire = new StringBuilder();
        while (stream.Position < stream.Length)
        {
            var frame = (await Hmp1Protocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken))!.Value;
            Assert.AreEqual(Hmp1FrameType.Output, frame.Type);
            var ansi = Encoding.UTF8.GetString(frame.Payload.Span);
            wire.Append(ansi);
            viewer.ApplyTokens(AnsiTokenizer.Tokenize(ansi));
        }
        Assert.Contains(",o=z,m=1;", wire.ToString());
        var replay = viewer.KgpImageStore.GetImageById(7)!;
        Assert.IsTrue(replay.IsZlibCompressed);
        TestSeq.AreEqual(encoded, replay.EncodedData.ToArray());
        TestSeq.AreEqual(pixels, replay.Data);
    }

    [TestMethod]
    [DataRow(KgpFormat.Rgb24, false)]
    [DataRow(KgpFormat.Rgba32, false)]
    [DataRow(KgpFormat.Png, false)]
    [DataRow(KgpFormat.Rgb24, true)]
    [DataRow(KgpFormat.Rgba32, true)]
    [DataRow(KgpFormat.Png, true)]
    public async Task WriteAsync_UnplacedResidentImage_PreservesFormatAndLaterPlacement(KgpFormat format, bool compressed)
    {
        var time = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, time);
        var pixels = format == KgpFormat.Png
            ? Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAYAAABWKLW/AAAAEUlEQVR4nGP4z8DwH4YZcHIAXdcR79xPMRAAAAAASUVORK5CYII=")
            : Enumerable.Range(0, 3 * 3 * (format == KgpFormat.Rgb24 ? 3 : 4)).Select(i => (byte)i).ToArray();
        var encoded = compressed ? Compress(pixels) : pixels;
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand($"a=t,f={(int)format},s=3,v=3,i=7300,q=2" +
                (compressed ? ",o=z" : ""), encoded)));
        using var source = producer.CreateSnapshot(includeAllKgpImages: true);
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, time);

        await ReplayAsync(source, viewer);

        using (var beforePlacement = viewer.CreateSnapshot())
        {
            Assert.IsEmpty(beforePlacement.KgpImages);
            Assert.IsEmpty(beforePlacement.KgpPlacements);
        }
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;8H" + KgpTestHelper.BuildCommand("a=p,i=7300,C=1,q=2")));
        using var replay = viewer.CreateSnapshot();
        Assert.AreEqual(format, replay.KgpImages[7300].Format);
        TestSeq.AreEqual(pixels, replay.KgpImages[7300].Data);
        Assert.AreEqual(compressed, replay.KgpImages[7300].IsZlibCompressed);
        if (compressed)
            TestSeq.AreEqual(encoded, replay.KgpImages[7300].EncodedData.ToArray());
        Assert.AreEqual(7, TestSeq.Single(replay.KgpPlacements).Column);
    }

    [TestMethod]
    public async Task CreateSnapshot_AllResidentImages_RespectsDeletionEvictionAndSnapshotLifetime()
    {
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(producerWorkload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true })
            .WithDimensions(20, 10).WithGraphics(options => options.MaximumRetainedBytesPerScreen = 12).Build();
        foreach (var id in new[] { 6, 7, 8, 9 })
            producer.ApplyTokens(AnsiTokenizer.Tokenize(
                KgpTestHelper.BuildCommand($"a=t,f=32,s=1,v=1,i={id},q=2", [(byte)id, 0, 0, 255])));
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=d,d=I,i=8,q=2") +
            KgpTestHelper.BuildCommand("a=d,d=i,i=7,q=2")));
        using var source = producer.CreateSnapshot(includeAllKgpImages: true);
        TestSeq.AreEqual(new uint[] { 7, 9 }, source.KgpImages.Keys.Order());
        Assert.IsEmpty(source.KgpPlacements);
        using (var screen = producer.CreateSnapshot())
            Assert.IsEmpty(screen.KgpImages, "Display snapshots must not acquire unused resident pixels.");
        Assert.AreSame(producer.KgpImageStore.GetImageById(7), source.KgpImages[7]);

        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,i=7,q=2", [70, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=d,d=I,i=9,q=2")));
        using var current = producer.CreateSnapshot(includeAllKgpImages: true);
        Assert.AreEqual(7u, TestSeq.Single(current.KgpImages.Keys));
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, TimeProvider.System);

        await ReplayAsync(source, viewer);

        Assert.AreEqual(2, viewer.KgpImageStore.ImageCount);
        Assert.IsNull(viewer.KgpImageStore.GetImageById(6));
        Assert.IsNull(viewer.KgpImageStore.GetImageById(8));
        TestSeq.AreEqual(new byte[] { 7, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(7)!.Data);
        TestSeq.AreEqual(new byte[] { 9, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(9)!.Data);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task WriteAsync_UnplacedNewerImageNumber_PreservesOlderPlacementAndFutureNumberLookup(bool wrappedIds, bool compressed)
    {
        var time = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, time);
        if (wrappedIds)
        {
            producer.KgpImageStore.StoreImage(new KgpImageData(uint.MaxValue, 42, [1, 0, 0, 255],
                1, 1, KgpFormat.Rgba32));
            producer.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand("a=p,I=42,C=1,q=2")));
        }
        else
            producer.ApplyTokens(AnsiTokenizer.Tokenize(
                KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,I=42,C=1,q=2" + (compressed ? ",o=z" : ""),
                    compressed ? Compress([1, 0, 0, 255]) : [1, 0, 0, 255])));
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,I=42,q=2" + (compressed ? ",o=z" : ""),
                compressed ? Compress([2, 0, 0, 255]) : [2, 0, 0, 255])));
        using var source = producer.CreateSnapshot(includeAllKgpImages: true);
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, time);

        await ReplayAsync(source, viewer);

        using (var replay = viewer.CreateSnapshot())
            TestSeq.AreEqual(new byte[] { 1, 0, 0, 255 },
                replay.KgpImages[TestSeq.Single(replay.KgpPlacements).ImageId].Data);
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[2;1H" + KgpTestHelper.BuildCommand("a=p,I=42,C=1,q=2")));
        using var continued = viewer.CreateSnapshot();
        var latest = continued.KgpPlacements.Single(placement => placement.Row == 1);
        TestSeq.AreEqual(new byte[] { 2, 0, 0, 255 }, continued.KgpImages[latest.ImageId].Data);
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=d,d=N,I=42,q=2") +
            "\x1b[3;1H" + KgpTestHelper.BuildCommand("a=p,I=42,C=1,q=2")));
        using var fallback = viewer.CreateSnapshot();
        var older = fallback.KgpPlacements.Single(placement => placement.Row == 2);
        Assert.AreEqual(2, fallback.KgpPlacements.Count);
        TestSeq.AreEqual(new byte[] { 1, 0, 0, 255 }, fallback.KgpImages[older.ImageId].Data);
    }

    [TestMethod]
    public async Task WriteAsync_NumberedAnimationGenerations_PreservesIndependentPlaybackCheckpoints()
    {
        var producerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,I=42,p=11,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,I=42,z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,I=42,r=1,z=20,s=3,v=3,q=2")));
        for (var elapsed = 0; elapsed < 55; elapsed++)
            producerTime.Advance(TimeSpan.FromMilliseconds(1));
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,I=42,q=2", [3, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,I=42,z=80,q=2", [4, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,I=42,r=1,z=60,c=2,s=1,v=2,q=2")));
        using var source = producer.CreateSnapshot(includeAllKgpImages: true);
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, new FakeTimeProvider());

        await ReplayAsync(source, viewer);

        using var replay = viewer.CreateSnapshot(includeAllKgpImages: true);
        Assert.AreEqual(2, replay.KgpImages.Count);
        foreach (var expected in source.KgpImages.Values)
        {
            var actual = replay.KgpImages.Values.Single(image => image.Data[0] == expected.Data[0]);
            Assert.AreEqual(expected.CurrentFrameNumber, actual.CurrentFrameNumber);
            Assert.AreEqual(expected.AnimationState!.PlaybackState, actual.AnimationState!.PlaybackState);
            Assert.AreEqual(expected.AnimationState.MaximumLoops, actual.AnimationState.MaximumLoops);
            Assert.AreEqual(expected.AnimationState.CompletedLoops, actual.AnimationState.CompletedLoops);
            TestSeq.AreEqual(expected.AnimationFrames!.Select(frame => frame.GapMilliseconds),
                actual.AnimationFrames!.Select(frame => frame.GapMilliseconds));
            TestSeq.AreEqual(expected.CurrentFrameData, actual.CurrentFrameData);
        }
        Assert.AreEqual((byte)1, replay.KgpImages[TestSeq.Single(replay.KgpPlacements).ImageId].Data[0]);
        Assert.AreEqual((byte)3, viewer.KgpImageStore.GetImageByNumber(42)!.Data[0]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteAsync_AnimationWithoutPlacements_PreservesPixelsForLaterCommands(bool numbered)
    {
        var time = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, time);
        var identity = numbered ? "I=42" : "i=7";
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand($"a=T,f=24,s=1,v=1,{identity},C=1,q=2", [1, 2, 3]) +
            KgpTestHelper.BuildCommand($"a=f,f=32,s=1,v=1,{identity},z=30,q=2", [4, 5, 6, 255]) +
            KgpTestHelper.BuildCommand($"a=a,{identity},r=1,z=20,c=2,s=1,v=3,q=2")));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, time);

        await ReplayAsync(source, viewer, omitPlacements: true);

        Assert.AreEqual(1, viewer.KgpImageStore.ImageCount);
        using (var invisible = viewer.CreateSnapshot())
            Assert.IsEmpty(invisible.KgpPlacements);
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand($"a=f,f=32,s=1,v=1,{identity},z=50,q=2", [7, 8, 9, 255]) +
            KgpTestHelper.BuildCommand($"a=a,{identity},c=3,q=2") +
            "\x1b[3;5H" + KgpTestHelper.BuildCommand($"a=p,{identity},C=1,q=2")));
        using var replay = viewer.CreateSnapshot();
        var image = TestSeq.Single(replay.KgpImages.Values);
        Assert.AreEqual(3, image.FrameCount);
        Assert.AreEqual(3, image.CurrentFrameNumber);
        Assert.AreEqual(3u, image.AnimationState!.MaximumLoops);
        TestSeq.AreEqual(new[] { 20, 30, 50 }, image.AnimationFrames!.Select(frame => frame.GapMilliseconds));
        TestSeq.AreEqual(new byte[] { 7, 8, 9, 255 }, image.CurrentFrameData);
        Assert.AreEqual(2, TestSeq.Single(replay.KgpPlacements).Row);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteAsync_ComposedAnimation_PreservesAllFramesGapsAndStoppedCurrentFrame(bool compressed)
    {
        var time = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, time);
        var root = Enumerable.Range(0, 40 * 30).SelectMany(_ => new byte[] { 80, 40, 20 }).ToArray();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" +
            KgpTestHelper.BuildCommand("a=T,f=24,s=40,v=30,i=7,p=11,X=9,Y=19,C=1,q=2" +
                (compressed ? ",o=z" : ""), compressed ? Compress(root) : root) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,c=1,x=2,y=3,z=30,q=2", [200, 100, 50, 128]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,x=1,y=1,X=1,z=-1,q=2", [17, 34, 51, 0]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=40,v=30,i=7,X=1,z=45,q=2", KgpTestHelper.CreatePixelData(40, 30)) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=25,c=3,s=1,v=3,q=2")));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, time);

        await ReplayAsync(source, viewer);

        using var replay = viewer.CreateSnapshot();
        var expected = source.KgpImages[7];
        var actual = replay.KgpImages[7];
        Assert.AreEqual(4, actual.FrameCount);
        Assert.AreEqual(KgpFormat.Rgba32, actual.Format);
        Assert.AreEqual(3, actual.CurrentFrameNumber);
        Assert.AreEqual(KgpParsedCommand.AnimationPlaybackState.Stopped, actual.AnimationState!.PlaybackState);
        Assert.AreEqual(3u, actual.AnimationState.MaximumLoops);
        for (var frame = 0; frame < expected.FrameCount; frame++)
        {
            TestSeq.AreEqual(expected.AnimationFrames![frame].Data, actual.AnimationFrames![frame].Data);
            Assert.AreEqual(expected.AnimationFrames[frame].GapMilliseconds, actual.AnimationFrames[frame].GapMilliseconds);
        }
        TestSeq.AreEqual(new byte[] { 17, 34, 51, 0 }, actual.CurrentFrameData.AsSpan((40 + 1) * 4, 4).ToArray());
        Assert.IsTrue(TestSeq.Single(replay.KgpPlacements).UsesNativeSize);
        Assert.AreEqual(source.CursorX, replay.CursorX);
        Assert.AreEqual(source.CursorY, replay.CursorY);
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
    }

    [TestMethod]
    [DataRow(55, false)]
    [DataRow(70, false)]
    [DataRow(500, false)]
    [DataRow(55, true)]
    public async Task WriteAsync_FiniteAnimation_PreservesLoopProgressAndRemainingGap(int elapsedMilliseconds, bool numbered)
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        // Force numbered images to receive a different allocated ID on replay.
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,i=123,q=2", [0, 0, 0, 0])));
        var identity = numbered ? "I=42" : "i=7";
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand($"a=T,f=32,s=1,v=1,{identity},C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand($"a=f,f=32,s=1,v=1,{identity},z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand($"a=a,{identity},r=1,z=20,s=3,v=2,q=2")));
        for (var elapsed = 0; elapsed < elapsedMilliseconds; elapsed++)
            producerTime.Advance(TimeSpan.FromMilliseconds(1));
        using var source = producer.CreateSnapshot();
        Assert.AreEqual(1u, TestSeq.Single(source.KgpImages.Values).AnimationState!.CompletedLoops);
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer);

        foreach (var advance in new[] { 0, 14, 1, 30, 500 })
        {
            producerTime.Advance(TimeSpan.FromMilliseconds(advance));
            viewerTime.Advance(TimeSpan.FromMilliseconds(advance));
            using var expectedSnapshot = producer.CreateSnapshot();
            using var actualSnapshot = viewer.CreateSnapshot();
            var expected = TestSeq.Single(expectedSnapshot.KgpImages.Values);
            var actual = TestSeq.Single(actualSnapshot.KgpImages.Values);
            Assert.AreEqual(2, actual.FrameCount);
            Assert.AreEqual(expected.CurrentFrameNumber, actual.CurrentFrameNumber,
                $"Advance {advance}; source shown {expected.AnimationState!.CurrentFrameShownAt:O}, now {producerTime.GetUtcNow():O}; " +
                $"replay shown {actual.AnimationState!.CurrentFrameShownAt:O}, now {viewerTime.GetUtcNow():O}, " +
                $"gaps {string.Join(',', actual.AnimationFrames!.Select(frame => frame.GapMilliseconds))}");
            TestSeq.AreEqual(expected.CurrentFrameData, actual.CurrentFrameData);
            Assert.AreEqual(expected.AnimationState!.PlaybackState, actual.AnimationState!.PlaybackState);
            Assert.AreEqual(expected.AnimationState.MaximumLoops, actual.AnimationState.MaximumLoops);
            Assert.AreEqual(expected.AnimationState.CompletedLoops, actual.AnimationState.CompletedLoops);
        }
        using var parked = viewer.CreateSnapshot();
        Assert.AreEqual(2, TestSeq.Single(parked.KgpImages.Values).CurrentFrameNumber);
    }

    [TestMethod]
    public async Task WriteAsync_LoadingTail_PreservesElapsedTimeAndResumesWhenFrameAppended()
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=20,s=2,q=2")));
        producerTime.Advance(TimeSpan.FromMilliseconds(20));
        producerTime.Advance(TimeSpan.FromMilliseconds(80));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer);

        Assert.AreEqual(2, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        var append = AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=50,q=2", [3, 0, 0, 255]));
        producer.ApplyTokens(append);
        viewer.ApplyTokens(append);
        Assert.AreEqual(3, producer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        TestSeq.AreEqual(new byte[] { 3, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameData);
        viewerTime.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(3, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
    }

    [TestMethod]
    public async Task WriteAsync_PlainAnsiConsumer_FiniteFinalPassDoesNotRestart()
    {
        var producerTime = new FakeTimeProvider();
        var viewerTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = CreateTerminal(producerWorkload, producerTime);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=30,q=2", [2, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=20,s=3,v=2,q=2")));
        for (var elapsed = 0; elapsed < 500; elapsed++)
            producerTime.Advance(TimeSpan.FromMilliseconds(1));
        using var source = producer.CreateSnapshot();
        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = CreateTerminal(viewerWorkload, viewerTime);

        await ReplayAsync(source, viewer, applyCheckpoint: false);

        viewerTime.Advance(TimeSpan.FromSeconds(10));
        Assert.AreEqual(2, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        TestSeq.AreEqual(new byte[] { 2, 0, 0, 255 }, viewer.KgpImageStore.GetImageById(7)!.CurrentFrameData);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(data);
        return output.ToArray();
    }

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload, TimeProvider time)
        => Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = true, CellPixelWidth = 10, CellPixelHeight = 20 })
            .WithDimensions(20, 10).WithTimeProvider(time).Build();

    private static async Task ReplayAsync(Hex1bTerminalSnapshot source, Hex1bTerminal viewer,
        bool applyCheckpoint = true, bool omitPlacements = false)
    {
        using var stream = new MemoryStream();
        await Hmp1KgpStateReplay.WriteAsync(stream, omitPlacements ? [] : source.KgpPlacements, source.KgpImages,
            source.CursorX, source.CursorY, TestContext.Current.CancellationToken, source.KgpAnimationTimestamp);
        stream.Position = 0;
        while (stream.Position < stream.Length)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(stream, TestContext.Current.CancellationToken)
                ?? throw new AssertFailedException("Expected a KGP replay frame.");
            if (frame.Type == Hmp1FrameType.Output)
                viewer.ApplyTokens(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(frame.Payload.Span)));
            else
            {
                Assert.AreEqual(Hmp1FrameType.KgpAnimationState, frame.Type);
                if (applyCheckpoint)
                    viewer.ApplyHmp1KgpAnimationState(JsonSerializer.Deserialize(
                        frame.Payload.Span, Hmp1JsonContext.Default.Hmp1KgpAnimationState)!);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task WriteAsync_NativeSprite_PreservesPixelSizeOffsetsAndCrop(bool crop)
    {
        var capabilities = new TerminalCapabilities
        {
            SupportsKgp = true,
            CellPixelWidth = 10,
            CellPixelHeight = 20
        };
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(producerWorkload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 10)
            .Build();
        var pixels = KgpTestHelper.CreatePixelData(3, 3);
        var cropControls = crop ? ",x=1,y=1,w=2,h=2" : "";
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" + KgpTestHelper.BuildCommand(
                $"a=T,f=32,s=3,v=3,i=7,p=11,X=9,Y=19,C=1,q=2{cropControls}",
                pixels)));
        using var source = producer.CreateSnapshot();
        using var stream = new MemoryStream();

        await Hmp1KgpStateReplay.WriteAsync(
            stream, source.KgpPlacements, source.KgpImages,
            source.CursorX, source.CursorY, TestContext.Current.CancellationToken);

        using var viewerWorkload = new Hex1bAppWorkloadAdapter();
        using var viewer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(viewerWorkload)
            .WithHeadless(capabilities)
            .WithDimensions(20, 10)
            .Build();
        stream.Position = 0;
        while (stream.Position < stream.Length)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(
                stream, TestContext.Current.CancellationToken)
                ?? throw new AssertFailedException("Expected a KGP replay frame.");
            Assert.AreEqual(Hmp1FrameType.Output, frame.Type);
            var payload = Encoding.UTF8.GetString(frame.Payload.Span);
            Assert.DoesNotContain(",c=", payload);
            Assert.DoesNotContain(",r=", payload);
            viewer.ApplyTokens(AnsiTokenizer.Tokenize(payload));
        }

        using var replay = viewer.CreateSnapshot();
        var placement = TestSeq.Single(replay.KgpPlacements);
        Assert.IsTrue(placement.UsesNativeSize);
        Assert.AreEqual(7u, placement.ImageId);
        Assert.AreEqual(11u, placement.PlacementId);
        Assert.AreEqual(2, placement.Row);
        Assert.AreEqual(4, placement.Column);
        Assert.AreEqual(2u, placement.DisplayColumns);
        Assert.AreEqual(2u, placement.DisplayRows);
        Assert.AreEqual(9u, placement.CellOffsetX);
        Assert.AreEqual(19u, placement.CellOffsetY);
        Assert.AreEqual(crop ? 1u : 0u, placement.SourceX);
        Assert.AreEqual(crop ? 1u : 0u, placement.SourceY);
        Assert.AreEqual(crop ? 2u : 3u, placement.SourceWidth);
        Assert.AreEqual(crop ? 2u : 3u, placement.SourceHeight);
        TestSeq.AreEqual(pixels, replay.KgpImages[7].Data);
        Assert.AreEqual(source.CursorX, replay.CursorX);
        Assert.AreEqual(source.CursorY, replay.CursorY);
    }
}
