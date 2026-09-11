using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1TitleTests
{
    [TestMethod]
    public async Task ReadFrameAsync_TitleOnlyOutput_EmitsDeltaWithRequiredTitleAndNoCells()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var initial = await ReadFrameAsync(view);
        Assert.AreEqual("", initial.Metadata.GetProperty("title").GetString());
        Assert.IsTrue(initial.Metadata.GetProperty("full").GetBoolean());

        workload.Write("\x1b]2;repo;東京 😀\x1b\\");
        var changed = await ReadUntilAsync(view, metadata =>
            metadata.GetProperty("title").GetString() == "repo;東京 😀");
        Assert.IsFalse(changed.Metadata.GetProperty("full").GetBoolean());
        Assert.AreEqual(changed.Metadata.GetProperty("revision").GetUInt32() - 1,
            changed.Metadata.GetProperty("baseRevision").GetUInt32());
        Assert.AreEqual(0, changed.CellCount);

        workload.Write("\x1b]2;\x07");
        var cleared = await ReadUntilAsync(view, metadata => metadata.GetProperty("title").GetString() == "");
        Assert.AreEqual(0, cleared.CellCount);
        Assert.IsFalse(cleared.Metadata.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    public async Task ReadFrameAsync_ResyncAndIconOnlyOutput_RetainsAuthoritativeTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        terminal.ApplyTokens([new OscToken("0", "", "same;title")]);
        var initial = await ReadFrameAsync(view);
        Assert.AreEqual("same;title", initial.Metadata.GetProperty("title").GetString());

        terminal.ApplyTokens([new OscToken("1", "", "icon-only")]);
        await view.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray(), TestContext.Current.CancellationToken);
        var resync = await ReadFrameAsync(view);
        Assert.IsTrue(resync.Metadata.GetProperty("full").GetBoolean());
        Assert.AreEqual("same;title", resync.Metadata.GetProperty("title").GetString());

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("cell-change"));
        var delta = await ReadFrameAsync(view);
        Assert.IsFalse(delta.Metadata.GetProperty("full").GetBoolean());
        Assert.AreEqual("same;title", delta.Metadata.GetProperty("title").GetString());
        Assert.IsTrue(delta.CellCount > 0);
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnacknowledgedTitleChanges_CoalescesToLatestSnapshot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var baseline = await ReadFrameAsync(view, acknowledge: false);
        terminal.ApplyTokens([
            new OscToken("2", "", "intermediate"),
            new OscToken("2", "", "final"),
        ]);
        await AcknowledgeAsync(view, baseline.Metadata);

        var frame = await ReadFrameAsync(view);
        Assert.AreEqual("final", frame.Metadata.GetProperty("title").GetString());
        Assert.AreEqual(0, frame.CellCount);
        Assert.AreEqual("", baseline.Metadata.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task ProducerBackedViews_LateAttachmentAndNewViewReconnect_UseCurrentTitleIncludingClear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens([new OscToken("2", "", "before;attachment")]);
        await using var first = await server.CreateBrowserViewAsync("first", TestContext.Current.CancellationToken);
        var initial = await ReadFrameAsync(first);
        Assert.AreEqual("before;attachment", initial.Metadata.GetProperty("title").GetString());

        await using var late = await server.CreateBrowserViewAsync("late", TestContext.Current.CancellationToken);
        var lateInitial = await ReadFrameAsync(late);
        Assert.AreEqual("before;attachment", lateInitial.Metadata.GetProperty("title").GetString());
        producer.ApplyTokens([new OscToken("2", "", "live;title")]);
        var firstLive = await ReadUntilAsync(first, metadata => metadata.GetProperty("title").GetString() == "live;title");
        var lateLive = await ReadUntilAsync(late, metadata => metadata.GetProperty("title").GetString() == "live;title");
        Assert.AreEqual(0, firstLive.CellCount);
        Assert.AreEqual(0, lateLive.CellCount);

        await first.DisposeAsync();
        producer.ApplyTokens([new OscToken("2", "", "")]);
        await using var reconnected = await server.CreateBrowserViewAsync("reconnected", TestContext.Current.CancellationToken);
        var reconnectInitial = await ReadFrameAsync(reconnected);
        Assert.IsTrue(reconnectInitial.Metadata.GetProperty("full").GetBoolean());
        Assert.AreEqual("", reconnectInitial.Metadata.GetProperty("title").GetString());
        var lateClear = await ReadUntilAsync(late, metadata => metadata.GetProperty("title").GetString() == "");
        Assert.AreEqual(0, lateClear.CellCount);
    }

    [TestMethod]
    public async Task ProducerBackedView_HistoricalViewport_TitleChangesWithoutChangingHistoricalCells()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5)
            .WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]2;old;title\x07" +
            string.Concat(Enumerable.Range(0, 15).Select(row => $"row{row:00}\r\n")) +
            "\x1b]2;current;title\x07"));
        await using var view = await server.CreateBrowserViewAsync("history", TestContext.Current.CancellationToken);
        await ReadFrameAsync(view);
        await view.HandleMessageAsync("""{"type":"viewport","requestId":1,"delta":-100}"""u8.ToArray(),
            TestContext.Current.CancellationToken);
        var historical = await ReadFrameAsync(view);
        var history = historical.Metadata.GetProperty("history");
        Assert.IsFalse(history.GetProperty("following").GetBoolean());
        Assert.AreEqual("current;title", historical.Metadata.GetProperty("title").GetString());

        producer.ApplyTokens([new OscToken("2", "", "new;title")]);
        var changed = await ReadFrameAsync(view);
        Assert.AreEqual("new;title", changed.Metadata.GetProperty("title").GetString());
        Assert.AreEqual(0, changed.CellCount);
        Assert.AreEqual(history.GetProperty("top").GetInt32(),
            changed.Metadata.GetProperty("history").GetProperty("top").GetInt32());
    }

    private static async Task<(JsonElement Metadata, int CellCount)> ReadFrameAsync(
        Hwt1PresentationAdapter view, bool acknowledge = true, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken, cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = await view.ReadFrameAsync(timeout.Token);
        Assert.IsTrue(bytes.Span[..4].SequenceEqual("HWT1"u8));
        var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]);
        using var document = JsonDocument.Parse(bytes.Slice(8, metadataLength));
        var metadata = document.RootElement.Clone();
        Assert.AreEqual(JsonValueKind.String, metadata.GetProperty("title").ValueKind);
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(8 + metadataLength)..]);
        if (acknowledge)
            await AcknowledgeAsync(view, metadata);
        return (metadata, count);
    }

    private static async Task<(JsonElement Metadata, int CellCount)> ReadUntilAsync(
        Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            var frame = await ReadFrameAsync(view, cancellationToken: timeout.Token);
            if (predicate(frame.Metadata))
                return frame;
        }
    }

    private static Task AcknowledgeAsync(Hwt1PresentationAdapter view, JsonElement metadata)
        => view.HandleMessageAsync(Encoding.UTF8.GetBytes(
            $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""),
            TestContext.Current.CancellationToken);
}
