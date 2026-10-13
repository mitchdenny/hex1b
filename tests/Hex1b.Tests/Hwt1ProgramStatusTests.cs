using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1ProgramStatusTests
{
    [TestMethod]
    public async Task ReadFrameAsync_StatusOnlyChanges_ProjectsFullInventoryWithoutCells()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(view).WithDimensions(20, 10).Build();
        var initial = await ReadFrameAsync(view);
        Assert.AreEqual(0, initial.Metadata.GetProperty("programStatus").GetProperty("records").GetArrayLength());

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;state=working:app=deploy\a" +
            "\x1b]7501;state=blocked:id=east:kind=permission:progress=40:msg=QXBwcm92ZT8=\a"));
        var changed = await ReadFrameAsync(view);
        Assert.AreEqual(0, changed.Cells);
        var records = changed.Metadata.GetProperty("programStatus").GetProperty("records");
        Assert.AreEqual(2, records.GetArrayLength());
        Assert.AreEqual("blocked", records[1].GetProperty("state").GetString());
        Assert.AreEqual("permission", records[1].GetProperty("kind").GetString());
        Assert.AreEqual("deploy", records[1].GetProperty("effectiveApp").GetString());
        Assert.AreEqual("Approve?", records[1].GetProperty("message").GetString());
        Assert.AreEqual(40, records[1].GetProperty("progress").GetInt32());

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7501;state=clear\a"));
        var cleared = await ReadFrameAsync(view);
        Assert.AreEqual(0, cleared.Cells);
        Assert.AreEqual(0, cleared.Metadata.GetProperty("programStatus").GetProperty("records").GetArrayLength());
        Assert.AreEqual(2, records.GetArrayLength());
    }

    [TestMethod]
    public async Task ProducerBackedViews_LateAttachAndCoalescing_UseLatestAuthoritativeInventory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 10).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;state=working:app=demo\a\x1b]7501;state=error:id=retained\a"));
        await using var view = await server.CreateBrowserViewAsync("late", TestContext.Current.CancellationToken);
        var initial = await ReadFrameAsync(view, acknowledge: false);
        Assert.AreEqual(2, initial.Metadata.GetProperty("programStatus").GetProperty("records").GetArrayLength());
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;state=blocked:kind=auth:app=demo\a" +
            "\x1b]7501;state=done:app=demo\a\x1b]133;A\a"));
        await AcknowledgeAsync(view, initial.Metadata);
        var frame = await ReadFrameAsync(view);
        var records = frame.Metadata.GetProperty("programStatus").GetProperty("records");
        Assert.AreEqual(2, records.GetArrayLength());
        Assert.AreEqual("error", records[0].GetProperty("state").GetString());
        Assert.AreEqual("done", records[1].GetProperty("state").GetString());
        await view.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray(), TestContext.Current.CancellationToken);
        var resynced = await ReadFrameAsync(view);
        Assert.IsTrue(resynced.Metadata.GetProperty("full").GetBoolean());
        Assert.AreEqual(records.GetRawText(),
            resynced.Metadata.GetProperty("programStatus").GetProperty("records").GetRawText());
    }

    private static async Task<(JsonElement Metadata, int Cells)> ReadFrameAsync(
        Hwt1PresentationAdapter view, bool acknowledge = true)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var bytes = await view.ReadFrameAsync(timeout.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..]);
        using var document = JsonDocument.Parse(bytes.Slice(8, length));
        var metadata = document.RootElement.Clone();
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[(8 + length)..]);
        if (acknowledge) await AcknowledgeAsync(view, metadata);
        return (metadata, count);
    }

    private static Task AcknowledgeAsync(Hwt1PresentationAdapter view, JsonElement metadata)
        => view.HandleMessageAsync(Encoding.UTF8.GetBytes(
            $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""),
            TestContext.Current.CancellationToken);
}
