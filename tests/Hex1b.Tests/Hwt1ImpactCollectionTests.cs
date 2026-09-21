using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1ImpactCollectionTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task PumpOutput_WithOptionalObservers_PreservesCellsGraphicsAndCapture(bool filtered, bool captured)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        var observer = new ImpactObserver();
        var options = new Hex1bTerminalOptions
        {
            Width = 20,
            Height = 10,
            WorkloadAdapter = workload,
            PresentationAdapter = view
        };
        if (filtered)
            options.PresentationFilters.Add(observer);
        await using var terminal = new Hex1bTerminal(options);
        var output = new ConcurrentQueue<string>();
        await using var capture = captured
            ? await terminal.BeginCaptureAsync((item, _) =>
            {
                if (item.Kind == TerminalCaptureEventKind.Output)
                    output.Enqueue(item.Output!);
                return ValueTask.CompletedTask;
            }, timeout.Token)
            : null;
        await ReadUntilAsync(view, _ => true, timeout.Token);

        const string data = "\x1b[?2026h\x1b[H\x1b[31mpayload\x1b[0m" +
            "\x1b[2;2H\x1b_Ga=T,f=32,s=1,v=1,i=1,p=1,C=1,q=2;/wAA/w==\x1b\\" +
            "\x1b[4;4H\x1bP7;1q\"1;1;2;6#1;2;0;100;0#1BB\x1b\\" +
            "\x1b]2;impact-done\x07\x1b[?2026l";
        workload.Write(data);
        var frame = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "impact-done" &&
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 1, timeout.Token);

        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("payload"));
        Assert.AreEqual(2, frame.GetProperty("placements").GetArrayLength());
        Assert.AreEqual(2, frame.GetProperty("retainedImages").GetArrayLength());
        if (filtered)
        {
            Assert.IsGreaterThan(0, observer.CellImpacts);
            Assert.IsGreaterThan(0, observer.GraphicsImpacts);
        }
        if (capture is not null)
        {
            using var barrier = await capture.BarrierAsync(timeout.Token);
            Assert.IsTrue(barrier.Snapshot.ContainsText("payload"));
            Assert.Contains("payload", string.Concat(output));
            Assert.Contains("impact-done", string.Concat(output));
        }

        workload.Write("\x1b_Ga=d,d=I,i=1,q=2;\x1b\\\x1b]2;deleted\x07");
        var deleted = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "deleted" &&
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 2, timeout.Token);
        Assert.AreEqual(1, deleted.GetProperty("placements").GetArrayLength());

        workload.Write("\x1b[2J\x1b]2;cleared\x07");
        var cleared = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "cleared" &&
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 3, timeout.Token);
        Assert.AreEqual(0, cleared.GetProperty("placements").GetArrayLength());
        if (capture is not null)
        {
            using var barrier = await capture.BarrierAsync(timeout.Token);
            Assert.IsFalse(barrier.Snapshot.ContainsText("payload"));
            Assert.Contains("deleted", string.Concat(output));
            Assert.Contains("cleared", string.Concat(output));
        }
    }

    [TestMethod]
    public async Task PumpOutput_TitleOnlyAfterAck_PreservesBatchAccountingAndInvalidation()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        await ReadUntilAsync(view, _ => true, timeout.Token);
        workload.Write("\x1b]2;first\x07");
        var first = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "first" &&
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 1, timeout.Token);
        workload.Write("\x1b]2;second\x07");
        var second = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "second" &&
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 2, timeout.Token);
        Assert.IsGreaterThan(first.GetProperty("revision").GetUInt32(), second.GetProperty("revision").GetUInt32());
        Assert.IsFalse(second.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PumpOutput_UnfilteredHwt_DoesNotAllocatePerTokenImpacts(bool preTokenized)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        var probe = new ApplicationAllocationProbe();
        var options = new Hex1bTerminalOptions
        {
            Width = 20,
            Height = 10,
            WorkloadAdapter = workload,
            PresentationAdapter = view
        };
        options.WorkloadFilters.Add(probe);
        await using var terminal = new Hex1bTerminal(options);
        terminal.PresentationInvalidated += probe.Complete;
        await ReadUntilAsync(view, _ => true, timeout.Token);

        // Parsing happens before the probe. SGR itself needs no per-token allocation;
        // constructing even the two empty impact lists would exceed this budget.
        const int tokenCount = 4096;
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\x1b[0m", tokenCount)));
        if (preTokenized)
            await workload.WriteTokensWithBytesAsync(AnsiTokenizer.Tokenize(Encoding.UTF8.GetString(bytes)), bytes,
                cancellationToken: timeout.Token);
        else
            workload.Write(bytes.AsMemory());
        var (allocated, sameThread, count) = await probe.Result.Task.WaitAsync(timeout.Token);
        Assert.IsTrue(sameThread, "The allocation region must not cross an asynchronous continuation.");
        Assert.AreEqual(tokenCount, count);
        Assert.IsLessThan(tokenCount * 32L, allocated,
            $"Token application allocated {allocated} bytes for {tokenCount} SGR tokens.");
        await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 1, timeout.Token);
    }

    [TestMethod]
    public async Task StateSync_UnfilteredHwt_DoesNotAllocatePerTokenImpacts()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var streams = WebTerminalDemo.DuplexPipeStream.CreatePair();
        await using var wire = streams.Server;
        await using var workload = Hmp1TestHelpers.NewClient(streams.Client);
        await using var view = new Hwt1PresentationAdapter(20, 10);
        var probe = new ApplicationAllocationProbe();
        var options = new Hex1bTerminalOptions
        {
            Width = 20,
            Height = 10,
            WorkloadAdapter = workload,
            PresentationAdapter = view
        };
        options.WorkloadFilters.Add(probe);
        await using var terminal = new Hex1bTerminal(options);
        terminal.PresentationInvalidated += probe.Complete;

        const int tokenCount = 4096;
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\x1b[0m", tokenCount)));
        var connecting = workload.ConnectAsync(timeout.Token);
        var hello = await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        Assert.AreEqual(Hmp1FrameType.ClientHello, hello!.Value.Type);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 10, "impact-replay", null, [], timeout.Token);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, bytes, timeout.Token);
        await Hmp1Protocol.WriteActivityStateAsync(wire, Hmp1ActivityState.Default, timeout.Token);
        await connecting.WaitAsync(timeout.Token);
        await terminal.WaitForHmp1InitialReplayAsync(timeout.Token);

        var (allocated, sameThread, count) = await probe.Result.Task.WaitAsync(timeout.Token);
        Assert.IsTrue(sameThread, "The allocation region must not cross an asynchronous continuation.");
        Assert.AreEqual(tokenCount, count);
        Assert.IsLessThan(tokenCount * 32L, allocated,
            $"Replay application allocated {allocated} bytes for {tokenCount} SGR tokens.");
        await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("stats").GetProperty("outputBatches").GetInt64() == 1, timeout.Token);
    }

    [TestMethod]
    public async Task ApplyTokensWithImpacts_CaptureLifecycle_OverridesSuppressionOnlyWhileAttached()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var tokens = AnsiTokenizer.Tokenize("\x1b[Htext");

        Assert.IsEmpty(terminal.ApplyTokensWithImpacts(tokens, collectImpacts: false));
        var defaults = terminal.ApplyTokensWithImpacts(tokens);
        Assert.AreEqual(tokens.Count, defaults.Count);
        Assert.IsTrue(defaults.Any(token => token.CellImpacts.Count > 0));

        var output = new ConcurrentQueue<string>();
        await using (var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            if (item.Kind == TerminalCaptureEventKind.Output)
                output.Enqueue(item.Output!);
            return ValueTask.CompletedTask;
        }, timeout.Token))
        {
            var captured = terminal.ApplyTokensWithImpacts(tokens, collectImpacts: false);
            Assert.AreEqual(tokens.Count, captured.Count);
            Assert.IsTrue(captured.Any(token => token.CellImpacts.Count > 0));
            using var barrier = await capture.BarrierAsync(timeout.Token);
            Assert.IsTrue(barrier.Snapshot.ContainsText("text"));
            Assert.Contains("text", string.Concat(output));
        }

        Assert.IsEmpty(terminal.ApplyTokensWithImpacts(tokens, collectImpacts: false));
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("text"));
    }

    [TestMethod]
    [DataRow("\x1b[2S\x1b[?1049h")]
    [DataRow("123456789Z\x1b[?1049h")]
    [DataRow("\x1b[2;4HR\x1b[3b\x1b[?1049h")]
    public void ApplyTokensWithImpacts_CollectionDisabledAndReentrantDisposal_AbortsApplication(string output)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        Hex1bTerminal? terminal = null;
        var callbackCount = 0;
        terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(4, 2)
            .WithScrollback(10, _ =>
            {
                callbackCount++;
                terminal!.Dispose();
            }).Build();
        using (terminal)
        {
            var applied = terminal.ApplyTokensWithImpacts(
                AnsiTokenizer.Tokenize(output), collectImpacts: false);
            Assert.IsEmpty(applied);
            Assert.AreEqual(1, callbackCount);
            using var snapshot = terminal.CreateSnapshot();
            Assert.IsFalse(snapshot.InAlternateScreen);
            Assert.IsFalse(snapshot.ContainsText("Z"));
            Assert.IsTrue(snapshot.CursorX >= 0 && snapshot.CursorX < snapshot.Width);
            Assert.IsTrue(snapshot.CursorY >= 0 && snapshot.CursorY < snapshot.Height);
        }
    }

    private static async Task<JsonElement> ReadUntilAsync(
        Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate, CancellationToken ct)
    {
        while (true)
        {
            var frame = await view.ReadFrameAsync(ct);
            Assert.IsTrue(frame.Span[..4].SequenceEqual("HWT1"u8));
            var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
            using var document = JsonDocument.Parse(frame.Slice(8, length));
            var metadata = document.RootElement;
            var revision = metadata.GetProperty("revision").GetUInt32();
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes($$"""{"type":"ack","revision":{{revision}}}"""), ct);
            if (predicate(metadata))
                return metadata.Clone();
        }
    }

    private sealed class ImpactObserver : IHex1bTerminalPresentationFilter
    {
        public int CellImpacts { get; private set; }
        public int GraphicsImpacts { get; private set; }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            CellImpacts += tokens.Sum(token => token.CellImpacts.Count);
            GraphicsImpacts += tokens.Sum(token => token.GraphicsImpacts.Count);
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens.Select(token => token.Token).ToArray());
        }

        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private sealed class ApplicationAllocationProbe : IHex1bTerminalWorkloadFilter
    {
        private long _before;
        private int _thread;
        private int _tokenCount;
        private bool _measuring;
        public TaskCompletionSource<(long Allocated, bool SameThread, int Count)> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            _thread = Environment.CurrentManagedThreadId;
            _tokenCount = tokens.Count;
            _measuring = true;
            _before = GC.GetAllocatedBytesForCurrentThread();
            return ValueTask.CompletedTask;
        }

        public void Complete()
        {
            if (!_measuring)
                return;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - _before;
            _measuring = false;
            Result.TrySetResult((allocated, _thread == Environment.CurrentManagedThreadId, _tokenCount));
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
