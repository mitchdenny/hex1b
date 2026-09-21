using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1UnusedImpactTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task PumpOutput_HwtWithOptionalObservers_PreservesCellsGraphicsAndCapture(bool filtered, bool captured)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        var observer = new ImpactObserver();
        var options = new Hex1bTerminalOptions
        {
            Width = 20, Height = 10, WorkloadAdapter = workload, PresentationAdapter = view
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
            Assert.IsTrue(observer.CellImpacts > 0);
            Assert.IsTrue(observer.GraphicsImpacts > 0);
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
        Assert.IsTrue(second.GetProperty("revision").GetUInt32() > first.GetProperty("revision").GetUInt32());
        Assert.IsFalse(second.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    public async Task ApplyTokensWithImpacts_CollectionDisabled_AppliesTokensWithoutResults()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var tokens = AnsiTokenizer.Tokenize("\x1b[2;3H\x1b[31mtext\x1b]2;title\x07");

        var uncollected = terminal.ApplyTokensWithImpacts(tokens, collectImpacts: false);
        Assert.IsEmpty(uncollected);
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("text"));
        Assert.AreEqual(6, snapshot.CursorX);
        Assert.AreEqual(1, snapshot.CursorY);
        Assert.AreEqual("title", terminal.WindowTitle);

        var collected = terminal.ApplyTokensWithImpacts(tokens);
        TestSeq.AreEqual(tokens, collected.Select(item => item.Token));
        Assert.IsTrue(collected.Any(item => item.HasCellImpacts));
        Assert.IsTrue(collected.Any(item => item.CursorMoved));
    }

    [TestMethod]
    public async Task ApplyTokensWithImpacts_CaptureAttachedThenDetached_OnlyForcesCollectionWhileAttached()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();
        var output = new ConcurrentQueue<string>();
        await using var capture = await terminal.BeginCaptureAsync((item, _) =>
        {
            if (item.Kind == TerminalCaptureEventKind.Output)
                output.Enqueue(item.Output!);
            return ValueTask.CompletedTask;
        }, timeout.Token);
        var tokens = AnsiTokenizer.Tokenize("captured");
        var collected = terminal.ApplyTokensWithImpacts(tokens, collectImpacts: false);
        TestSeq.AreEqual(tokens, collected.Select(item => item.Token));
        Assert.IsTrue(collected.Any(item => item.HasCellImpacts));
        using var boundary = await capture.DetachAsync(timeout.Token);
        Assert.IsTrue(boundary.Snapshot.ContainsText("captured"));
        Assert.AreEqual("captured", string.Concat(output));

        var uncollected = terminal.ApplyTokensWithImpacts(
            AnsiTokenizer.Tokenize("\r\nafter"), collectImpacts: false);
        Assert.IsEmpty(uncollected);
        using var snapshot = terminal.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("after"));
        Assert.AreEqual("captured", string.Concat(output));
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
}
