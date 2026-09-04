using System.IO.Pipelines;
using System.Text;
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
    public async Task StateSync_WithDamagedSixelCell_PreservesOverwrittenTextAfterReplay()
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
        // Overwrite only the origin cell with plain text, damaging it.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1HX"));

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

        // The placement itself is still present and still occupies its
        // second cell (never damaged).
        var placement = TestSeq.Single(snapshot.SixelPlacements);
        Assert.AreEqual(0, placement.Row);
        Assert.AreEqual(0, placement.Column);
    }

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
}
