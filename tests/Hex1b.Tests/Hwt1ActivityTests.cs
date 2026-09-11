using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1ActivityTests
{
    [TestMethod]
    public async Task ReadFrameAsync_ActivityOnlyOutput_CarriesDefaultsChangesAndClearsWithoutCells()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var initial = await ReadFrameAsync(view);
        AssertActivity(initial.Metadata, "none", null, "unknown", null);
        foreach (var (sequence, state, percentage, phase, exitCode) in new (string, string, int?, string, int?)[]
        {
            ("\x1b]9;4;3\a\x1b]133;C\a", "indeterminate", null, "executing", null),
            ("\x1b]9;4;1;50\a", "normal", 50, "executing", null),
            ("\x1b]9;4;4;50\a", "warning", 50, "executing", null),
            ("\x1b]9;4;2;50\a\x1b]133;D;-1\a", "error", 50, "finished", -1),
            ("\x1b]133;A\a\x1b]133;B\a", "error", 50, "commandLine", -1),
            ("\x1b]9;4;0\a", "none", null, "commandLine", -1),
            ("\x1b" + "c", "none", null, "unknown", null),
        })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));
            var frame = await ReadFrameAsync(view);
            AssertActivity(frame.Metadata, state, percentage, phase, exitCode);
            Assert.AreEqual(0, frame.CellCount);
            Assert.IsFalse(frame.Metadata.GetProperty("full").GetBoolean());
        }
        AssertActivity(initial.Metadata, "none", null, "unknown", null);
    }

    [TestMethod]
    public async Task ReadFrameAsync_UnacknowledgedActivity_CoalescesAndResyncRetainsFinalState()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var initial = await ReadFrameAsync(view, acknowledge: false);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]133;A\a\x1b]133;B\a\x1b]133;C\a\x1b]9;4;3\a" +
            "\x1b]9;4;1;100\a\x1b]133;D;0\a\x1b]9;4;0\a"));
        await AcknowledgeAsync(view, initial.Metadata);
        var frame = await ReadFrameAsync(view);
        AssertActivity(frame.Metadata, "none", null, "finished", 0);
        Assert.AreEqual(0, frame.CellCount);
        await view.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray(), TestContext.Current.CancellationToken);
        var resync = await ReadFrameAsync(view);
        Assert.IsTrue(resync.Metadata.GetProperty("full").GetBoolean());
        AssertActivity(resync.Metadata, "none", null, "finished", 0);
    }

    [TestMethod]
    public async Task ProducerBackedViews_LateAttachmentHistoryAndReconnect_KeepCurrentActivity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 10).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            string.Concat(Enumerable.Range(0, 20).Select(row => $"row{row:00}\r\n")) +
            "\x1b]9;4;1;20\a\x1b]133;C\a"));
        await using var first = await server.CreateBrowserViewAsync("first", TestContext.Current.CancellationToken);
        AssertActivity((await ReadFrameAsync(first)).Metadata, "normal", 20, "executing", null);
        await first.HandleMessageAsync("""{"type":"viewport","requestId":1,"delta":-100}"""u8.ToArray(),
            TestContext.Current.CancellationToken);
        var historical = await ReadFrameAsync(first);
        Assert.IsFalse(historical.Metadata.GetProperty("history").GetProperty("following").GetBoolean());
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;2;20\a\x1b]133;D;1\a"));
        var changed = await ReadFrameAsync(first);
        AssertActivity(changed.Metadata, "error", 20, "finished", 1);
        Assert.AreEqual(0, changed.CellCount);
        await using var late = await server.CreateBrowserViewAsync("late", TestContext.Current.CancellationToken);
        AssertActivity((await ReadFrameAsync(late)).Metadata, "error", 20, "finished", 1);
        await late.DisposeAsync();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;0\a\x1b]133;D\a"));
        await using var reconnected = await server.CreateBrowserViewAsync("reconnected", TestContext.Current.CancellationToken);
        AssertActivity((await ReadFrameAsync(reconnected)).Metadata, "none", null, "finished", null);
    }

    [TestMethod]
    public async Task ReadFrameAsync_SynchronizedActivity_WaitsForCompletedSnapshot()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        await ReadFrameAsync(view);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?2026h\x1b]9;4;3\a\x1b]133;C\a"));
        var pending = ReadFrameAsync(view);
        Assert.IsFalse(pending.IsCompleted);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;1;50\a\x1b[?2026l"));
        AssertActivity((await pending).Metadata, "normal", 50, "executing", null);
    }

    private static void AssertActivity(JsonElement metadata, string state, int? percentage, string phase, int? exitCode)
    {
        var progress = metadata.GetProperty("progress");
        var shell = metadata.GetProperty("shellIntegration");
        Assert.AreEqual(state, progress.GetProperty("state").GetString());
        Assert.AreEqual(percentage, progress.GetProperty("percentage").Deserialize<int?>());
        Assert.AreEqual(phase, shell.GetProperty("phase").GetString());
        Assert.AreEqual(exitCode, shell.GetProperty("lastExitCode").Deserialize<int?>());
    }

    private static async Task<(JsonElement Metadata, int CellCount)> ReadFrameAsync(
        Hwt1PresentationAdapter view, bool acknowledge = true)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = await view.ReadFrameAsync(timeout.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]);
        using var document = JsonDocument.Parse(bytes.Slice(8, length));
        var metadata = document.RootElement.Clone();
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(8 + length)..]);
        if (acknowledge)
            await AcknowledgeAsync(view, metadata);
        return (metadata, count);
    }

    private static Task AcknowledgeAsync(Hwt1PresentationAdapter view, JsonElement metadata)
        => view.HandleMessageAsync(Encoding.UTF8.GetBytes(
            $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""),
            TestContext.Current.CancellationToken);
}
