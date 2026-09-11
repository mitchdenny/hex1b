using System.IO.Pipelines;
using System.Text;
using Hex1b.Sixel;
using Hex1b.Tests.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

/// <summary>
/// Integration coverage for <see cref="Hmp1SixelStateReplay"/>: the plain
/// cursor+DCS bytes a late-joining HMP1 peer receives so its Sixel placements
/// match the producer's, without requiring a live upstream terminal.
/// </summary>
[TestClass]
public class Hmp1SixelStateReplayTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public void SixelReplayInfrastructure_RemainsInternal()
    {
        Type[] implementationTypes =
        [
            typeof(Hmp1Protocol),
            typeof(Hmp1FrameType),
            typeof(Hmp1SixelStateReplay),
            typeof(Hmp1SixelRecording),
            typeof(Hmp1SixelRecordingSnapshot),
            typeof(Hmp1SixelRecordedImage),
            typeof(Hmp1SixelRecordedPlacement),
            typeof(Hmp1SixelRecordingException),
            typeof(Hmp1SixelRecordingFailureReason),
        ];

        foreach (var type in implementationTypes)
        {
            Assert.IsFalse(type.IsVisible, $"{type.FullName} must remain internal HMP1 plumbing.");
        }
    }

    [TestMethod]
    public async Task StateSync_WithActiveSixelPlacement_ReplaysImageToLateJoiningPeer()
    {
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithPresentation(server)
            .Build();

        // server.Capabilities (10x20 cell pixels) drives the producer's Sixel
        // raster geometry, so the fixture must span more than one cell pixel
        // width (10) to produce a multi-cell placement: 11 pixels wide, one
        // band (6 pixel rows) tall.
        var fixture = new SixelFixture(
            "hmp1-single-band",
            "One-band red probe for HMP1 replay coverage.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[3;5H" + Encoding.ASCII.GetString(fixture.StandardBytes)));

        using (var producerState = producer.CreateSnapshot())
        {
            Assert.HasCount(1, producerState.SixelPlacements);
            Assert.IsTrue(producerState.ContainsSixelData());
        }

        var (serverStream, clientStream) = CreateFullDuplexPair();
        using var cts = new CancellationTokenSource(TestTimeout);
        var addClientTask = server.AddClient(serverStream, cts.Token);
        await Hmp1Protocol.WriteClientHelloAsync(
            clientStream,
            displayName: "late-viewer",
            defaultRole: null,
            cts.Token);

        var hello = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending Hello.");
        var stateSync = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending StateSync.");
        await Hmp1Protocol.ReadActivityStateAsync(clientStream, cts.Token);
        var sixelReplay = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending Sixel replay.");

        Assert.AreEqual(Hmp1FrameType.Hello, hello.Type);
        Assert.AreEqual(Hmp1FrameType.StateSync, stateSync.Type);
        Assert.AreEqual(Hmp1FrameType.Output, sixelReplay.Type);
        var replayResultStarted = TimeProvider.System.GetTimestamp();
        while (server.LastSixelReplayResult is null &&
               TimeProvider.System.GetElapsedTime(replayResultStarted) < TestTimeout)
        {
            await Task.Delay(10, cts.Token);
        }
        Assert.IsNotNull(server.LastSixelReplayResult);
        Assert.AreEqual(
            Hmp1SixelStateReplay.ReplayOutcome.Complete,
            server.LastSixelReplayResult.Value.Outcome);
        Assert.AreEqual(1, server.LastSixelReplayResult.Value.ReplayedPlacements);

        var handle = await addClientTask;
        await using var handleDispose = handle;
        await using var viewer = Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true })
            .Build();

        // Apply in the exact wire order Hmp1PresentationAdapter emits them:
        // StateSync (with its unconditional CSI-2J) first, then the Sixel
        // placement-creation + damage-patch replay.
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            Encoding.UTF8.GetString(stateSync.Payload.Span)));
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            Encoding.UTF8.GetString(sixelReplay.Payload.Span)));

        using var producerSnapshot = producer.CreateSnapshot();
        using var snapshot = viewer.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(2, placement.Row);
        Assert.AreEqual(4, placement.Column);
        Assert.AreEqual(2, placement.WidthInCells);
        Assert.AreEqual(1, placement.HeightInCells);
        Assert.IsFalse(placement.IsGeometryOnly);

        var producerPlacement = TestSeq.Single(producerSnapshot.SixelPlacements);
        var producerPixels = producerPlacement.Image.GetPixels();
        var replayedPixels = placement.Image.GetPixels();
        Assert.IsNotNull(producerPixels);
        Assert.IsNotNull(replayedPixels);
        Assert.AreEqual(producerPixels.Width, replayedPixels.Width);
        Assert.AreEqual(producerPixels.Height, replayedPixels.Height);
        for (var y = 0; y < producerPixels.Height; y++)
        {
            for (var x = 0; x < producerPixels.Width; x++)
            {
                Assert.AreEqual(producerPixels[x, y], replayedPixels[x, y]);
            }
        }

        Assert.AreEqual(producerSnapshot.CursorX, snapshot.CursorX);
        Assert.AreEqual(producerSnapshot.CursorY, snapshot.CursorY);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task StateSync_WithDamagedSixelCell_PreservesOverwrittenTextAfterReplay(
        bool linkedCell, bool activeHyperlink)
    {
        // Regression coverage for the ordering fix: Sixel placement creation
        // blanks its occupied cells (unlike KGP), so the placement-creation
        // replay must land *after* StateSync's own unconditional CSI-2J, and
        // any cell the placement had damaged must be patched back afterward
        // so its overwritten text survives.
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithPresentation(server)
            .Build();

        var fixture = new SixelFixture(
            "hmp1-damage-band",
            "A two-cell-wide band whose origin cell is later overwritten with text.",
            "q#1;2;100;0;0#1!11~"u8.ToArray());
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;1H" + Encoding.ASCII.GetString(fixture.StandardBytes)));
        // The damaged cell's link can differ from the active link for future output.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H" +
            (linkedCell ? "\x1b]8;id=cell;https://example.com/cell\x1b\\" : "") +
            "X\x1b]8;;\x1b\\" +
            (activeHyperlink ? "\x1b]8;id=active;https://example.com/active\x1b\\" : "")));

        using (var producerState = producer.CreateSnapshot())
        {
            var producerPlacement = TestSeq.Single(producerState.SixelPlacements);
            Assert.IsTrue(producerPlacement.IsCellDamaged(0, 0));
            Assert.AreEqual("X", producerState.GetCell(0, 0).Character);
        }

        var (serverStream, clientStream) = CreateFullDuplexPair();
        using var cts = new CancellationTokenSource(TestTimeout);
        var addClientTask = server.AddClient(serverStream, cts.Token);
        await Hmp1Protocol.WriteClientHelloAsync(
            clientStream,
            displayName: "late-viewer",
            defaultRole: null,
            cts.Token);

        var hello = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending Hello.");
        var stateSync = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending StateSync.");
        await Hmp1Protocol.ReadActivityStateAsync(clientStream, cts.Token);
        var sixelReplay = await Hmp1Protocol.ReadFrameAsync(clientStream, cts.Token)
            ?? throw new AssertFailedException("Server closed the stream before sending Sixel replay.");

        Assert.AreEqual(Hmp1FrameType.Hello, hello.Type);
        Assert.AreEqual(Hmp1FrameType.StateSync, stateSync.Type);
        Assert.AreEqual(Hmp1FrameType.Output, sixelReplay.Type);

        var handle = await addClientTask;
        await using var handleDispose = handle;
        await using var viewer = Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true })
            .Build();

        // Apply in the exact wire order Hmp1PresentationAdapter emits them:
        // StateSync first, then the Sixel placement-creation + damage-patch
        // replay.
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            Encoding.UTF8.GetString(stateSync.Payload.Span)));
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(
            Encoding.UTF8.GetString(sixelReplay.Payload.Span)));

        using var snapshot = viewer.CreateSnapshot();

        // The damaged cell's text must survive: the trailing damage-patch
        // step re-applies it after the placement-creation replay re-blanks
        // it.
        Assert.AreEqual("X", snapshot.GetCell(0, 0).Character);
        Assert.AreEqual(linkedCell ? "https://example.com/cell" : null,
            snapshot.GetCell(0, 0).HyperlinkData?.Uri);
        Assert.AreEqual(linkedCell ? "id=cell" : null,
            snapshot.GetCell(0, 0).HyperlinkData?.Parameters);

        // The placement itself is still present and still occupies its
        // second cell (never damaged).
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(0, placement.Column);

        viewer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1HY"));
        using var continued = viewer.CreateSnapshot();
        Assert.AreEqual(activeHyperlink ? "https://example.com/active" : null,
            continued.GetCell(0, 2).HyperlinkData?.Uri);
        Assert.AreEqual(activeHyperlink ? "id=active" : null,
            continued.GetCell(0, 2).HyperlinkData?.Parameters);
    }

    [TestMethod]
    public void BuildPlacementSequence_GeometryOnlyPayload_RestoresDcsFraming()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1bP0;1q\"1;1;999999999;999999999#1@\x1b\\"));
        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);
        Assert.IsTrue(placement.IsGeometryOnly);

        var replay = Hmp1SixelStateReplay.BuildPlacementSequence(placement);

        StringAssert.Contains(replay, "\x1bP");
        StringAssert.EndsWith(replay, "\x1b\\");
        using var viewer = CreateHeadlessTerminal();
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(replay));
        using var viewerSnapshot = viewer.CreateSnapshot();
        var replayed = TestSeq.Single(viewerSnapshot.SixelPlacements);
        Assert.AreEqual(placement.Image.Extents.Logical, replayed.Image.Extents.Logical);
    }

    [TestMethod]
    public void BuildPlacementSequence_ReplayedIntoZeroBudgetTerminal_IsRejectedCleanly()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1bPq#1;2;100;0;0#1@\x1b\\"));
        using var producerSnapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(producerSnapshot.SixelPlacements);
        var replay = Hmp1SixelStateReplay.BuildPlacementSequence(placement);

        using var viewer = CreateHeadlessTerminal(maximumRetainedBytesPerScreen: 0);
        viewer.ApplyTokens(AnsiTokenizer.Tokenize(replay));

        Assert.AreEqual(0, viewer.SixelPlacementCount);
        Assert.AreEqual(0, viewer.TrackedSixelCount);
        Assert.AreEqual(0L, viewer.SixelRetainedByteCount);
    }

    [TestMethod]
    public async Task WriteAsync_PlacementCountExceedsLimit_ReturnsTypedLimitWithoutWriting()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq@\x1b\\"));
        using var snapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        var placements = Enumerable.Repeat(
            placement,
            Hmp1SixelStateReplay.MaximumPlacementCount + 1).ToArray();
        await using var stream = new MemoryStream();

        var result = await Hmp1SixelStateReplay.WriteAsync(
            stream,
            placements,
            [],
            TestContext.Current.CancellationToken);

        Assert.AreEqual(Hmp1SixelStateReplay.ReplayOutcome.ResourceLimitExceeded, result.Outcome);
        Assert.AreEqual("placement_count", result.Limit);
        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public async Task WriteAsync_PreCancelled_DoesNotBuildOrWriteReplay()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq@\x1b\\"));
        using var snapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        await using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Hmp1SixelStateReplay.WriteAsync(stream, [placement], [], cts.Token));
        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public async Task WriteAsync_CancelledDuringFrameWrite_StopsReplay()
    {
        using var producer = CreateHeadlessTerminal();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq!64~\x1b\\"));
        using var snapshot = producer.CreateSnapshot();
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        using var cts = new CancellationTokenSource();
        await using var stream = new CancellingWriteStream(cts);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Hmp1SixelStateReplay.WriteAsync(stream, [placement], [], cts.Token));

        Assert.AreEqual(1, stream.WriteCount);
    }

    [TestMethod]
    public async Task WriteAsync_RetentionLimitedPlacement_IsExplicitlySkipped()
    {
        var policy = SixelCompatibilityPolicy.Default with
        {
            MaximumRetainedDcsBytes = 16,
            MaximumDiagnostics = 1,
        };
        await using var producer = SixelTestTerminal.Create(policy: policy);
        var bytes = Encoding.ASCII.GetBytes(
            $"\x1bP7q!9999999999~{new string('~', policy.MaximumRetainedDcsBytes + 8)}\x1b\\");
        await producer.FeedAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        await producer.WaitForAsync(
            _ => producer.Terminal.SixelPlacementCount == 1,
            "retention-limited placement",
            TestContext.Current.CancellationToken);
        using var snapshot = producer.Terminal.CreateSnapshot();
        await using var stream = new MemoryStream();

        var result = await Hmp1SixelStateReplay.WriteAsync(
            stream,
            snapshot.SixelPlacements,
            [],
            TestContext.Current.CancellationToken);

        Assert.AreEqual(Hmp1SixelStateReplay.ReplayOutcome.Complete, result.Outcome);
        Assert.AreEqual(0, result.ReplayedPlacements);
        Assert.AreEqual(1, result.SkippedPlacements);
        Assert.AreEqual(0, result.FrameCount);
        Assert.AreEqual(0, stream.Length);
    }

    private static Hex1bTerminal CreateHeadlessTerminal(
        long maximumRetainedBytesPerScreen = 320L * 1024 * 1024) =>
        Hex1bTerminal.CreateBuilder()
            .WithDimensions(20, 10)
            .WithWorkload(new NullWorkloadAdapter())
            .WithHeadless(new TerminalCapabilities { SupportsSixel = true })
            .WithGraphics(options =>
                options.MaximumRetainedBytesPerScreen = maximumRetainedBytesPerScreen)
            .Build();

    private sealed class NullWorkloadAdapter : IHex1bTerminalWorkloadAdapter
    {
        public event Action? Disconnected
        {
            add { }
            remove { }
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (Stream S1, Stream S2) CreateFullDuplexPair()
    {
        var p12 = new Pipe();
        var p21 = new Pipe();
        return (
            new DuplexPipeStream(p21.Reader.AsStream(), p12.Writer.AsStream()),
            new DuplexPipeStream(p12.Reader.AsStream(), p21.Writer.AsStream()));
    }

    private sealed class DuplexPipeStream(Stream readStream, Stream writeStream) : Stream
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => writeStream.Flush();
        public override Task FlushAsync(CancellationToken ct) => writeStream.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => readStream.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => readStream.ReadAsync(buffer, ct);
        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => writeStream.WriteAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { readStream.Dispose(); } catch { }
                try { writeStream.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    private sealed class CancellingWriteStream(CancellationTokenSource cancellation) : Stream
    {
        internal int WriteCount { get; private set; }
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteCount++;
            cancellation.Cancel();
            return ValueTask.FromCanceled(cancellationToken);
        }
    }
}
