using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1ReadOnlyTests
{
    [TestMethod]
    [DataRow("""{"type":"input","text":"input"}""", "input", 20, 10)]
    [DataRow("""{"type":"paste","text":"paste"}""", "\x1b[200~paste\x1b[201~", 20, 10)]
    [DataRow("""{"type":"key","key":"ArrowUp"}""", "\x1bOA", 20, 10)]
    [DataRow("""{"type":"mouse","action":"down","button":"left","x":1,"y":2}""", "\x1b[<0;2;3M", 20, 10)]
    [DataRow("""{"type":"resize","columns":40,"rows":12}""", null, 40, 12)]
    [DataRow("""{"type":"requestPrimary","columns":50,"rows":15}""", null, 50, 15)]
    public async Task IsReadOnly_BeforeAttachmentAndLiveToggles_GatesProducerCommands(
        string json, string? expectedInput, int expectedWidth, int expectedHeight)
    {
        await using var view = new Hwt1PresentationAdapter(20, 10);
        Assert.IsFalse(view.IsReadOnly);
        view.IsReadOnly = true;
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1;2004;1003;1006h"));
        var resized = 0;
        view.Resized += (_, _) => resized++;

        await view.HandleMessageAsync(Encoding.UTF8.GetBytes(json));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.AreEqual(0, resized);
        Assert.AreEqual(20, terminal.Width);
        Assert.AreEqual(10, terminal.Height);
        var baseline = await FrameAsync(view);
        Assert.IsTrue(baseline.GetProperty("full").GetBoolean());

        view.IsReadOnly = false;
        await view.HandleMessageAsync(Encoding.UTF8.GetBytes(json));
        if (expectedInput is not null)
            Assert.AreEqual(expectedInput, await ReadInputAsync(workload, expectedInput.Length));
        else
            Assert.AreEqual(1, resized);
        Assert.AreEqual(expectedWidth, terminal.Width);
        Assert.AreEqual(expectedHeight, terminal.Height);

        view.IsReadOnly = true;
        await view.HandleMessageAsync(Encoding.UTF8.GetBytes(json));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.AreEqual(expectedInput is null ? 1 : 0, resized);
        Assert.AreEqual(expectedWidth, terminal.Width);
        Assert.AreEqual(expectedHeight, terminal.Height);
    }

    [TestMethod]
    [DataRow("""{"type":"unknown"}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"setReadOnly","readOnly":false}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"input","text":null}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"paste","text":1}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":"input"}""", typeof(KeyNotFoundException))]
    [DataRow("""{"type":"key","key":"Unsupported"}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"key","key":"a","ctrl":1}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"mouse","action":"invalid"}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"mouse","action":"down","button":"left","x":-1,"y":0}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"resize","columns":19,"rows":10}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"requestPrimary","columns":20,"rows":101}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"ack","revision":1}""", typeof(InvalidDataException))]
    [DataRow("""{"type":"ack","revision":"1"}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":"selection","action":"invalid","requestId":1}""", typeof(InvalidDataException))]
    [DataRow("""{"type":1}""", typeof(InvalidOperationException))]
    [DataRow("""{"type":""", typeof(JsonException))]
    public async Task HandleMessageAsync_ReadOnly_StillValidatesMessages(string json, Type expectedException)
    {
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            view.HandleMessageAsync(Encoding.UTF8.GetBytes(json)));

        Assert.IsTrue(expectedException.IsInstanceOfType(exception), exception.ToString());
        Assert.IsTrue(view.IsReadOnly);
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.AreEqual(20, terminal.Width);
        Assert.AreEqual(10, terminal.Height);
    }

    [TestMethod]
    public async Task HandleMessageAsync_ReadOnly_OversizeCancellationAndAttachmentChecksStillApply()
    {
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            view.HandleMessageAsync("""{"type":"input","text":"blocked"}"""u8.ToArray()));
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            view.HandleMessageAsync(new byte[64 * 1024 + 1]));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            view.HandleMessageAsync("""{"type":"input","text":"blocked"}"""u8.ToArray(),
                new CancellationToken(canceled: true)));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    public async Task IsReadOnly_HistorySelectionCopyAndResync_ContinueWithoutResettingView()
    {
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).WithScrollback(100).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            string.Concat(Enumerable.Range(0, 30).Select(i => $"row{i:00}\r\n"))));
        await FrameAsync(view);
        await MessageAsync(view, new { type = "viewport", requestId = 1, delta = -100 });
        var history = (await FrameAsync(view)).GetProperty("history");
        Assert.IsFalse(history.GetProperty("following").GetBoolean());
        var rowId = history.GetProperty("rowIds")[0].GetString();
        await MessageAsync(view, new
        {
            type = "selection", action = "start", mode = "line", requestId = 2,
            generation = history.GetProperty("generation").GetString(), rowId, column = 0
        });
        var selected = (await FrameAsync(view)).GetProperty("history").GetProperty("selection");
        Assert.AreEqual("row00", selected.GetProperty("text").GetString());

        foreach (var command in ProducerCommands())
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(command));
        await MessageAsync(view, new { type = "copy", requestId = 3 });
        await MessageAsync(view, new { type = "resync" });
        var resynced = await FrameAsync(view);
        Assert.IsTrue(resynced.GetProperty("full").GetBoolean());
        history = resynced.GetProperty("history");
        Assert.IsFalse(history.GetProperty("following").GetBoolean());
        Assert.AreEqual(rowId, history.GetProperty("rowIds")[0].GetString());
        Assert.AreEqual("valid", history.GetProperty("selection").GetProperty("status").GetString());
        Assert.AreEqual("row00", history.GetProperty("copy").GetProperty("text").GetString());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));

        await MessageAsync(view, new { type = "selection", action = "clear", requestId = 4 });
        await MessageAsync(view, new { type = "viewport", requestId = 5, live = true });
        history = (await FrameAsync(view)).GetProperty("history");
        Assert.IsTrue(history.GetProperty("following").GetBoolean());
        Assert.AreEqual("none", history.GetProperty("selection").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task IsReadOnly_AckBackpressure_DoesNotBlockOutputOrResync()
    {
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        var workload = new RecordingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();
        var first = Metadata(await view.ReadFrameAsync(TestContext.Current.CancellationToken));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var pending = view.ReadFrameAsync(timeout.Token).AsTask();
        workload.Output.Writer.TryWrite("updated"u8.ToArray());
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("updated"), TimeSpan.FromSeconds(5), "read-only output")
            .Build().ApplyAsync(terminal, timeout.Token);
        await MessageAsync(view, new { type = "resync" });
        Assert.IsFalse(pending.IsCompleted);
        await MessageAsync(view, new { type = "ack", revision = first.GetProperty("revision").GetUInt32() });
        var next = Metadata(await pending);
        Assert.IsTrue(next.GetProperty("full").GetBoolean());
        Assert.AreEqual(7L, next.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
        Assert.IsTrue(snapshot.ContainsText("updated"));
    }

    [TestMethod]
    public async Task IsReadOnly_TwoProducerViews_PreservesRolesGeometryAndOtherInput()
    {
        var workload = new RecordingWorkload();
        await using var muxer = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(muxer).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1;2004;1003;1006h"));
        await using var first = await muxer.CreateBrowserViewAsync("first");
        await using var second = await muxer.CreateBrowserViewAsync("second");
        await MessageAsync(first, new { type = "requestPrimary", columns = 20, rows = 10 });
        var primary = (await FrameAsync(first)).GetProperty("peer");
        var firstId = primary.GetProperty("id").GetString();
        Assert.IsTrue(primary.GetProperty("isPrimary").GetBoolean());
        first.IsReadOnly = true;

        foreach (var command in ProducerCommands())
            await first.HandleMessageAsync(Encoding.UTF8.GetBytes(command));
        await MessageAsync(second, new { type = "input", text = "B" });
        Assert.AreEqual("B", await ReadInputAsync(workload, 1));
        await producer.SendInputAsync("direct"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.AreEqual("direct", await ReadInputAsync(workload, 6));
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder().Type("auto").Build()
            .ApplyAsync(producer, TestContext.Current.CancellationToken);
        Assert.AreEqual("auto", await ReadInputAsync(workload, 4));
        Assert.AreEqual(firstId, muxer.PrimaryPeerId);
        Assert.AreEqual(20, producer.Width);
        Assert.AreEqual(10, producer.Height);
        Assert.AreEqual(2, muxer.ClientCount);
        await MessageAsync(first, new { type = "resync" });
        Assert.AreEqual(primary.GetRawText(), (await FrameAsync(first)).GetProperty("peer").GetRawText());

        await MessageAsync(second, new { type = "requestPrimary", columns = 40, rows = 12 });
        var secondPrimary = (await FrameAsync(second)).GetProperty("peer");
        Assert.IsTrue(secondPrimary.GetProperty("isPrimary").GetBoolean());
        var secondId = secondPrimary.GetProperty("id").GetString();
        foreach (var command in ProducerCommands())
            await first.HandleMessageAsync(Encoding.UTF8.GetBytes(command));
        await MessageAsync(second, new { type = "input", text = "C" });
        Assert.AreEqual("C", await ReadInputAsync(workload, 1));
        Assert.AreEqual(secondId, muxer.PrimaryPeerId);
        Assert.AreEqual(40, producer.Width);
        Assert.AreEqual(12, producer.Height);
        var secondary = (await FrameAsync(first)).GetProperty("peer");
        Assert.AreEqual(firstId, secondary.GetProperty("id").GetString());
        Assert.AreEqual(secondId, secondary.GetProperty("primaryId").GetString());
        Assert.IsFalse(secondary.GetProperty("isPrimary").GetBoolean());

        first.IsReadOnly = false;
        await MessageAsync(first, new { type = "input", text = "D" });
        Assert.AreEqual("D", await ReadInputAsync(workload, 1));
        first.IsReadOnly = true;
        await first.DisposeAsync();
        Assert.AreEqual(1, muxer.ClientCount);
        Assert.AreEqual(secondId, muxer.PrimaryPeerId);
        await MessageAsync(second, new { type = "input", text = "E" });
        Assert.AreEqual("E", await ReadInputAsync(workload, 1));
        second.IsReadOnly = true;
        await second.DisposeAsync();
        Assert.AreEqual(0, muxer.ClientCount);
        Assert.IsNull(muxer.PrimaryPeerId);
        Assert.AreEqual(40, producer.Width);
        Assert.AreEqual(12, producer.Height);
        await producer.SendInputAsync("F"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.AreEqual("F", await ReadInputAsync(workload, 1));
    }

    [TestMethod]
    public async Task IsReadOnly_RevokedDuringWrite_AllowsAcceptedWriteButBlocksLaterDispatch()
    {
        var workload = new RecordingWorkload { InputGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();
        var accepted = MessageAsync(view, new { type = "input", text = "accepted" });
        try
        {
            await workload.InputStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Task.Run(() => view.IsReadOnly = true, TestContext.Current.CancellationToken);
            Assert.IsFalse(accepted.IsCompleted);
        }
        finally
        {
            workload.InputGate.TrySetResult();
        }
        await accepted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await MessageAsync(view, new { type = "input", text = "blocked" });
        Assert.AreEqual("accepted", await ReadInputAsync(workload, 8));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        view.IsReadOnly = false;
        await MessageAsync(view, new { type = "input", text = "restored" });
        Assert.AreEqual("restored", await ReadInputAsync(workload, 8));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IsReadOnly_AcceptedWrite_CancellationAndDisposalStillCancel(bool dispose)
    {
        var workload = new RecordingWorkload { InputGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(view).WithDimensions(20, 10).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var accepted = view.HandleMessageAsync("""{"type":"input","text":"accepted"}"""u8.ToArray(), cancellation.Token);
        try
        {
            await workload.InputStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            view.IsReadOnly = true;
            Assert.IsFalse(accepted.IsCompleted);
            if (dispose)
                await view.DisposeAsync();
            else
                cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                accepted.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        }
        finally
        {
            workload.InputGate.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IsReadOnly_PendingFrameAndInput_CancellationAndDisposalStillWork(bool acknowledged)
    {
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new RecordingWorkload())
            .WithPresentation(view).WithDimensions(20, 10).Build();
        var first = Metadata(await view.ReadFrameAsync(TestContext.Current.CancellationToken));
        if (acknowledged)
            await MessageAsync(view, new { type = "ack", revision = first.GetProperty("revision").GetUInt32() });
        using var cancellation = new CancellationTokenSource();
        var cancelled = view.ReadFrameAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelled);
        var frame = view.ReadFrameAsync().AsTask();
        var input = view.ReadInputAsync().AsTask();
        var disconnected = 0;
        view.Disconnected += () => disconnected++;
        view.IsReadOnly = false;
        view.IsReadOnly = true;
        Assert.IsFalse(frame.IsCompleted);
        Assert.IsFalse(input.IsCompleted);

        await view.DisposeAsync();
        await view.DisposeAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => frame);
        await Assert.ThrowsAsync<OperationCanceledException>(() => input);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            MessageAsync(view, new { type = "input", text = "blocked" }));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await view.ReadFrameAsync());
        Assert.AreEqual(1, disconnected);
    }

    private static string[] ProducerCommands() =>
    [
        """{"type":"input","text":"blocked"}""",
        """{"type":"paste","text":"blocked"}""",
        """{"type":"key","key":"ArrowUp"}""",
        """{"type":"mouse","action":"down","button":"left","x":1,"y":2}""",
        """{"type":"resize","columns":60,"rows":15}""",
        """{"type":"requestPrimary","columns":70,"rows":20}"""
    ];

    private static Task MessageAsync(Hwt1PresentationAdapter view, object message)
        => view.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(message), TestContext.Current.CancellationToken);

    private static async Task<JsonElement> FrameAsync(Hwt1PresentationAdapter view)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var metadata = Metadata(await view.ReadFrameAsync(timeout.Token));
        await MessageAsync(view, new { type = "ack", revision = metadata.GetProperty("revision").GetUInt32() });
        return metadata;
    }

    private static JsonElement Metadata(ReadOnlyMemory<byte> frame)
    {
        using var document = JsonDocument.Parse(frame.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..])));
        return document.RootElement.Clone();
    }

    private static async Task<string> ReadInputAsync(RecordingWorkload workload, int length)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var text = new StringBuilder();
        while (text.Length < length)
            text.Append(Encoding.UTF8.GetString(await workload.Input.Reader.ReadAsync(timeout.Token)));
        return text.ToString();
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        public Channel<byte[]> Output { get; } = Channel.CreateUnbounded<byte[]>();
        public Channel<byte[]> Input { get; } = Channel.CreateUnbounded<byte[]>();
        public TaskCompletionSource InputStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? InputGate { get; init; }
        public event Action? Disconnected;

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => await Output.Reader.ReadAsync(ct);

        public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            InputStarted.TrySetResult();
            if (InputGate is not null)
                await InputGate.Task.WaitAsync(ct);
            await Input.Writer.WriteAsync(data.ToArray(), ct);
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Output.Writer.TryComplete();
            Input.Writer.TryComplete();
            Disconnected?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
