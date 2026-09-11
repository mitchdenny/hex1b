using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1PresentationAdapterTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task ReadFrameAsync_SynchronizedRedraw_DoesNotPublishErasedOrPartialGraphics(bool impacts, bool kgp)
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("old"));
        await presentation.ReadFrameAsync();
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var erase = AnsiTokenizer.Tokenize("\x1b[?2026h\x1b[H\x1b[2J");
        if (impacts)
            terminal.ApplyTokensWithImpacts(erase);
        else
            terminal.ApplyTokens(erase);

        // An already-queued invalidation must not bypass the synchronized block.
        var next = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(next.IsCompleted, "The erased screen must never be published.");
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            kgp
                ? "\x1b[2;2H\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,C=1,q=2;/wAA/w==\x1b\\"
                : "\x1b[2;2H\x1bP7;1q\"1;1;2;6#1;2;100;0;0#1BB\x1b\\"));
        await presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray());
        Assert.IsFalse(next.IsCompleted, "The first particle is still only a partial frame.");
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            (kgp
                ? "\x1b[4;4H\x1b_Ga=T,f=32,s=1,v=1,i=2,p=2,C=1,q=2;AP8A/w==\x1b\\"
                : "\x1b[4;4H\x1bP7;1q\"1;1;2;6#1;2;0;100;0#1BB\x1b\\") +
            "\x1b[Hnew\x1b[?2026l"));

        var frame = await next.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var metadata = ReadMetadata(frame);
        Assert.AreEqual(2, metadata.RootElement.GetProperty("placements").GetArrayLength());
        Assert.IsTrue(metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual("n", ReadFirstCellText(frame));
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnterminatedSynchronizedRedraw_HasBoundedWait()
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026hA"));
        var pending = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(pending.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(900));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026h"));
        Assert.IsFalse(pending.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(100));

        var frame = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.AreEqual("A", ReadFirstCellText(frame));
    }

    [TestMethod]
    public async Task ReadFrameAsync_CancelledSynchronizedWait_CanRetryWithoutLosingBaseline()
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026h"));
        using var cancellation = new CancellationTokenSource();
        var cancelled = presentation.ReadFrameAsync(cancellation.Token).AsTask();
        Assert.IsFalse(cancelled.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        var retry = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(retry.IsCompleted);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("complete\x1b[?2026l"));

        var frame = await retry.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var metadata = ReadMetadata(frame);
        Assert.AreEqual(1u, metadata.RootElement.GetProperty("revision").GetUInt32());
        Assert.AreEqual("c", ReadFirstCellText(frame));
    }

    [TestMethod]
    public async Task ReadFrameAsync_NewSynchronizedBlockBeforeCapture_WaitsForItsEnd()
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026hA"));
        var pending = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(pending.IsCompleted);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026l\x1b[?2026h\rB"));
        Assert.IsFalse(pending.IsCompleted);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\rC\x1b[?2026l"));

        Assert.AreEqual("C", ReadFirstCellText(
            await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReadFrameAsync_ResetDuringSynchronizedOutput_ReleasesWait(bool softReset)
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026hA"));
        var pending = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(pending.IsCompleted);

        terminal.ApplyTokens([softReset ? SoftResetToken.Instance : RisToken.Instance]);

        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public async Task DisposeAsync_SynchronizedFrameWait_CancelsRead()
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload(), timeProvider: clock);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026h"));
        var pending = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(pending.IsCompleted);

        await presentation.DisposeAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        await terminal.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnattachedAdapter_RequiresTerminal()
    {
        await using var presentation = new Hwt1PresentationAdapter();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await presentation.ReadFrameAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray()));
    }

    [TestMethod]
    public async Task ReadFrameAsync_AlreadyCancelled_PreservesInitialBaseline()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await presentation.ReadFrameAsync(new CancellationToken(canceled: true)));
        using var metadata = ReadMetadata(await presentation.ReadFrameAsync());
        Assert.AreEqual(1u, metadata.RootElement.GetProperty("revision").GetUInt32());
        Assert.IsTrue(metadata.RootElement.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    public async Task ReadFrameAsync_InitialState_ContainsCompleteHwt1Baseline()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\u754c"));

        var bytes = await presentation.ReadFrameAsync();
        using var metadata = ReadMetadata(bytes);
        var root = metadata.RootElement;
        Assert.AreEqual(1, root.GetProperty("version").GetInt32());
        Assert.AreEqual(1u, root.GetProperty("revision").GetUInt32());
        Assert.AreEqual(0u, root.GetProperty("baseRevision").GetUInt32());
        Assert.IsTrue(root.GetProperty("full").GetBoolean());
        Assert.AreEqual(20, root.GetProperty("columns").GetInt32());
        Assert.AreEqual(10, root.GetProperty("rows").GetInt32());
        Assert.AreEqual(10, root.GetProperty("cellWidth").GetInt32());
        Assert.AreEqual(20, root.GetProperty("cellHeight").GetInt32());
        Assert.AreEqual(200, ReadCellCount(bytes));
        Assert.AreEqual("A", ReadFirstCellText(bytes));
        Assert.AreEqual(0L, root.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
        var peer = root.GetProperty("peer");
        Assert.IsNull(peer.GetProperty("id").GetString());
        Assert.IsNull(peer.GetProperty("primaryId").GetString());
        Assert.IsTrue(peer.GetProperty("isPrimary").GetBoolean());
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnacknowledgedFrame_CoalescesChangesUntilMatchingAck()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        var initial = await presentation.ReadFrameAsync();
        var initialCopy = initial.ToArray();
        var next = presentation.ReadFrameAsync().AsTask();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("old"));
        presentation.InvalidatePresentation();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\rnew"));
        presentation.InvalidatePresentation();
        await presentation.HandleMessageAsync("""{"type":"ack","revision":0}"""u8.ToArray());
        Assert.IsFalse(next.IsCompleted);

        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var bytes = await next.WaitAsync(TimeSpan.FromSeconds(5));
        using var metadata = ReadMetadata(bytes);
        Assert.AreEqual(2u, metadata.RootElement.GetProperty("revision").GetUInt32());
        Assert.AreEqual(1u, metadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.IsFalse(metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual("n", ReadFirstCellText(bytes));
        TestSeq.AreEqual(initialCopy, initial.ToArray());

        await presentation.HandleMessageAsync("""{"type":"ack","revision":2}"""u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        var noChange = presentation.ReadFrameAsync(cancellation.Token).AsTask();
        Assert.IsFalse(noChange.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => noChange);
    }

    [TestMethod]
    public async Task HandleMessageAsync_Resync_DoesNotReleaseAckAndResendsResources()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,c=3,r=2,C=1,q=2;/wAA/w==\x1b\\"));
        using var initial = ReadMetadata(await presentation.ReadFrameAsync());
        var imageKey = initial.RootElement.GetProperty("images")[0].GetProperty("key").GetString();
        var next = presentation.ReadFrameAsync().AsTask();

        await presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray());
        await presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray());
        Assert.IsFalse(next.IsCompleted);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            presentation.HandleMessageAsync("""{"type":"ack","revision":2}"""u8.ToArray()));
        Assert.IsFalse(next.IsCompleted);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());

        var bytes = await next.WaitAsync(TimeSpan.FromSeconds(5));
        using var metadata = ReadMetadata(bytes);
        var root = metadata.RootElement;
        Assert.IsTrue(root.GetProperty("full").GetBoolean());
        Assert.AreEqual(0u, root.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(200, ReadCellCount(bytes));
        Assert.AreEqual(1, root.GetProperty("images").GetArrayLength());
        Assert.AreEqual(1, root.GetProperty("retainedImages").GetArrayLength());
        Assert.AreEqual(imageKey, root.GetProperty("images")[0].GetProperty("key").GetString());
        Assert.AreEqual(imageKey, root.GetProperty("placements")[0].GetProperty("key").GetString());
        TestSeq.AreEqual(new byte[] { 255, 0, 0, 255 }, bytes.Span[^4..].ToArray());
    }

    [TestMethod]
    public async Task HandleMessageAsync_Resize_WaitsForAckAndEstablishesNewBaseline()
    {
        var workload = new RecordingWorkload();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, workload);
        await presentation.ReadFrameAsync();
        var next = presentation.ReadFrameAsync().AsTask();

        await presentation.HandleMessageAsync("""{"type":"resize","columns":40,"rows":12}"""u8.ToArray());
        Assert.AreEqual(40, terminal.Width);
        Assert.AreEqual(12, terminal.Height);
        Assert.AreEqual((40, 12), workload.LastSize);
        Assert.IsFalse(next.IsCompleted);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());

        var bytes = await next.WaitAsync(TimeSpan.FromSeconds(5));
        using var metadata = ReadMetadata(bytes);
        Assert.IsTrue(metadata.RootElement.GetProperty("full").GetBoolean());
        Assert.AreEqual(0u, metadata.RootElement.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(40, metadata.RootElement.GetProperty("columns").GetInt32());
        Assert.AreEqual(12, metadata.RootElement.GetProperty("rows").GetInt32());
        Assert.AreEqual(480, ReadCellCount(bytes));
    }

    [TestMethod]
    public async Task ReadFrameAsync_ExternalResize_InvalidatesAndPreservesAuthoritativeGeometry()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        await presentation.ReadFrameAsync();
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var pending = presentation.ReadFrameAsync().AsTask();

        terminal.Resize(360, 120);

        using var metadata = ReadMetadata(await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(360, metadata.RootElement.GetProperty("columns").GetInt32());
        Assert.AreEqual(120, metadata.RootElement.GetProperty("rows").GetInt32());
        Assert.AreEqual(360, presentation.Width);
        Assert.AreEqual(120, presentation.Height);
        Assert.IsTrue(metadata.RootElement.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
    }

    [TestMethod]
    [DataRow(1025, 10)]
    [DataRow(20, 513)]
    [DataRow(1024, 257)]
    public async Task ReadFrameAsync_GridBeyondReceiverLimits_RejectsWithoutClamping(int columns, int rows)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        terminal.Resize(columns, rows);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await presentation.ReadFrameAsync());
        Assert.AreEqual(columns, terminal.Width);
        Assert.AreEqual(rows, terminal.Height);
        Assert.AreEqual(columns, presentation.Width);
        Assert.AreEqual(rows, presentation.Height);
    }

    [TestMethod]
    public async Task ReadFrameAsync_CancelledAckWait_PreservesRevisionAndPendingResync()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        await presentation.ReadFrameAsync();
        await presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        var cancelled = presentation.ReadFrameAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);

        var retried = presentation.ReadFrameAsync().AsTask();
        Assert.IsFalse(retried.IsCompleted);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        using var metadata = ReadMetadata(await retried.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2u, metadata.RootElement.GetProperty("revision").GetUInt32());
        Assert.IsTrue(metadata.RootElement.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_PendingWaits_CancelsAndDisconnectsOnce(bool acknowledged)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        var disconnections = 0;
        presentation.Disconnected += () => disconnections++;
        await presentation.ReadFrameAsync();
        if (acknowledged)
            await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var frame = presentation.ReadFrameAsync().AsTask();
        var input = presentation.ReadInputAsync().AsTask();
        Assert.IsFalse(frame.IsCompleted);

        await presentation.DisposeAsync();
        await presentation.DisposeAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => frame);
        await Assert.ThrowsAsync<OperationCanceledException>(() => input);
        Assert.AreEqual(1, disconnections);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await presentation.ReadFrameAsync());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray()));
    }

    [TestMethod]
    public async Task ReadFrameAsync_ConcurrentReaders_RejectsAdditionalReader()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        await presentation.ReadFrameAsync();
        using var cancellation = new CancellationTokenSource();
        var waiting = presentation.ReadFrameAsync(cancellation.Token).AsTask();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await presentation.ReadFrameAsync());
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting);
    }

    [TestMethod]
    public async Task ReadFrameAsync_MissingAcknowledgement_TimesOut()
    {
        var clock = new FakeTimeProvider();
        await using var presentation = new Hwt1PresentationAdapter(20, 10, clock);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());
        await presentation.ReadFrameAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = presentation.ReadFrameAsync(cancellation.Token).AsTask();

        clock.Advance(TimeSpan.FromMinutes(2));

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => waiting);
    }

    [TestMethod]
    [DataRow("""{"type":"pause","paused":true}""")]
    [DataRow("""{"type":"rate","rate":60,"batch":100}""")]
    [DataRow("""{"type":"resize","columns":301,"rows":10}""")]
    [DataRow("""{"type":"resize","columns":20,"rows":101}""")]
    [DataRow("""{"type":"requestPrimary","columns":19,"rows":10}""")]
    [DataRow("""{"type":"requestPrimary","columns":20,"rows":101}""")]
    [DataRow("""{"type":"ack","revision":1}""")]
    public async Task HandleMessageAsync_InvalidCommand_RejectsWithoutChangingDimensions(string json)
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            presentation.HandleMessageAsync(Encoding.UTF8.GetBytes(json)));
        Assert.AreEqual(20, terminal.Width);
        Assert.AreEqual(10, terminal.Height);
    }

    [TestMethod]
    public async Task HandleMessageAsync_OversizedMessage_RejectsBeforeParsing()
    {
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, new RecordingWorkload());

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            presentation.HandleMessageAsync(new byte[64 * 1024 + 1]));
    }

    [TestMethod]
    [DataRow("""{"type":"input","text":"A\u754c"}""", "A\u754c")]
    [DataRow("""{"type":"paste","text":"A\u754c"}""", "\x1b[200~A\u754c\x1b[201~")]
    [DataRow("""{"type":"key","key":"ArrowUp"}""", "\x1bOA")]
    [DataRow("""{"type":"mouse","action":"down","button":"left","x":1,"y":2}""", "\x1b[<0;2;3M")]
    public async Task HandleMessageAsync_SemanticInput_WritesModeAwareBytes(string json, string expected)
    {
        var workload = new RecordingWorkload();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1;2004;1003;1006h"));

        await presentation.HandleMessageAsync(Encoding.UTF8.GetBytes(json));

        TestSeq.AreEqual(Encoding.UTF8.GetBytes(expected), TestSeq.Single(workload.Input));
    }

    [TestMethod]
    public async Task ReadFrameAsync_Backpressure_DoesNotStopOutputProcessingOrByteCounting()
    {
        var workload = new RecordingWorkload();
        await using var presentation = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = CreateTerminal(presentation, workload, startPumps: true);
        await presentation.ReadFrameAsync();
        var next = presentation.ReadFrameAsync().AsTask();

        await workload.Output.Writer.WriteAsync("A"u8.ToArray());
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.GetCell(0, 0).Character == "A", TimeSpan.FromSeconds(5), "first output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await workload.Output.Writer.WriteAsync("\rB"u8.ToArray());
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.GetCell(0, 0).Character == "B", TimeSpan.FromSeconds(5), "second output applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.IsFalse(next.IsCompleted);

        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var bytes = await next.WaitAsync(TimeSpan.FromSeconds(5));
        using var metadata = ReadMetadata(bytes);
        Assert.AreEqual("B", ReadFirstCellText(bytes));
        Assert.AreEqual(3L, metadata.RootElement.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
    }

    private static Hex1bTerminal CreateTerminal(
        Hwt1PresentationAdapter presentation, RecordingWorkload workload, bool startPumps = false,
        TimeProvider? timeProvider = null)
        => new(new Hex1bTerminalOptions
        {
            Width = presentation.Width, Height = presentation.Height,
            PresentationAdapter = presentation, WorkloadAdapter = workload,
            TimeProvider = timeProvider ?? TimeProvider.System,
            RunCallback = startPumps ? null : _ => Task.FromResult(0)
        });

    private static JsonDocument ReadMetadata(ReadOnlyMemory<byte> frame)
    {
        Assert.IsTrue(frame.Span[..4].SequenceEqual("HWT1"u8));
        var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        return JsonDocument.Parse(frame.Slice(8, length));
    }

    private static int ReadCellCount(ReadOnlyMemory<byte> frame)
        => BinaryPrimitives.ReadInt32LittleEndian(frame.Span[(8 + BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]))..]);

    private static string ReadFirstCellText(ReadOnlyMemory<byte> frame)
    {
        var offset = 12 + BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
        Assert.AreEqual(0, BinaryPrimitives.ReadInt32LittleEndian(frame.Span[offset..]));
        var length = BinaryPrimitives.ReadUInt16LittleEndian(frame.Span[(offset + 20)..]);
        return Encoding.UTF8.GetString(frame.Span.Slice(offset + 22, length));
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        public Channel<byte[]> Output { get; } = Channel.CreateUnbounded<byte[]>();
        public List<byte[]> Input { get; } = [];
        public (int Width, int Height) LastSize { get; private set; }
        public event Action? Disconnected;

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => await Output.Reader.ReadAsync(ct);

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Input.Add(data.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            LastSize = (width, height);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Output.Writer.TryComplete();
            Disconnected?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
