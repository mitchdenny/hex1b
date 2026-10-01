using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Input;
using Hex1b.Reflow;
using Microsoft.Extensions.Time.Testing;
using WebTerminalDemo;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalDemoRelayTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public async Task Relay_AttachAfterCrop_DoesNotReplaySplitWideGlyph(bool explicitCrop, bool alternate)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(80, 10);
        if (explicitCrop)
            server.WithReflow(NoReflowStrategy.Instance);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(80, 10).WithScrollback(100).Build();
        var ct = TestContext.Current.CancellationToken;
        var text = "UNI_" + string.Concat(Enumerable.Repeat("\u754ce\u0301", 36)) + "_END!";
        workload.Write(text + "\r\n$ ");
        using var ready = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(snapshot => snapshot.GetLineTrimmed(2) == "$", TimeSpan.FromSeconds(5), "wide output consumed")
            .Build().ApplyAsync(producer, ct);
        if (alternate)
            producer.EnterAlternateScreen();
        producer.Resize(20, 10);
        if (alternate)
            producer.ExitAlternateScreen();
        using (var cropped = producer.CreateSnapshot())
            Assert.AreEqual(" ", cropped.GetCell(19, 0).Character, "A split wide glyph must be erased, not left without its tail.");
        producer.Resize(40, 10);
        producer.Resize(80, 10);

        await using var relay = await Hmp1BrowserView.CreateAsync(server, "cropped-relay", ct);
        Assert.AreEqual("UNI_" + string.Concat(Enumerable.Repeat("\u754ce\u0301", 5)),
            await ReadFirstLogicalLineAsync(relay.Presentation));
        Assert.AreEqual(0, producer.ScrollbackCount);
        await relay.Presentation.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray(), ct);
        var frame = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("full").GetBoolean());
        Assert.AreEqual(10, frame.GetProperty("history").GetProperty("totalRows").GetInt32(),
            "Replay must not introduce a new physical row.");
    }

    [TestMethod]
    [DataRow(false, "none")]
    [DataRow(true, "none")]
    [DataRow(false, "kgp")]
    [DataRow(true, "kgp")]
    [DataRow(false, "sixel")]
    [DataRow(true, "sixel")]
    public async Task Relay_AttachAfterSoftWrap_PreservesLogicalLine(bool wideText, string graphics)
    {
        var text = new string('A', 79) + (wideText ? "\u754ce\u0301" : "BC") + new string('Z', 35);
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(80, 10).WithReflow(GhosttyReflowStrategy.Instance);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(80, 10).WithScrollback(100).Build();
        var ct = TestContext.Current.CancellationToken;
        workload.Write(text[..30] +
            (graphics == "kgp" ? KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=529,C=1,q=2", [255, 0, 0, 255]) : "") +
            text[30..] + "\r\n$ " +
            (graphics == "sixel" ? "\x1b" + "7\x1b[1d\x1b[31G\x1bPq#0;2;100;0;0~\x1b\\\x1b" + "8" : ""));
        using var ready = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(snapshot => snapshot.GetLineTrimmed(2) == "$" &&
                (graphics != "sixel" || snapshot.SixelPlacements.Count == 1),
                TimeSpan.FromSeconds(5), "wrapped output consumed")
            .Build().ApplyAsync(producer, ct);
        if (graphics == "sixel")
        {
            Assert.AreEqual(" ", ready.GetCell(30, 0).Character);
            text = text[..30] + " " + text[31..];
        }
        await using var relay = await Hmp1BrowserView.CreateAsync(server, "late-reflow", ct);
        await relay.Presentation.HandleMessageAsync("""{"type":"requestPrimary","columns":80,"rows":10}"""u8.ToArray(), ct);
        await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.AreEqual(text, await ReadFirstLogicalLineAsync(relay.Presentation));
        var requestId = 1;
        foreach (var width in new[] { 20, 40, 80 })
        {
            await relay.Presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(
                new { type = "resize", columns = width, rows = 10 }), ct);
            await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("columns").GetInt32() == width);
            requestId += 2;
            Assert.AreEqual(text, await ReadFirstLogicalLineAsync(relay.Presentation, requestId));
        }
    }

    [TestMethod]
    [DataRow("Ghostty", true)]
    [DataRow("Ghostty", false)]
    [DataRow("Vte", true)]
    [DataRow("None", true)]
    public async Task Relay_Resize_UsesProducerReflowPolicy(string strategy, bool relayPrimary)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(80, 10);
        if (strategy != "None")
            server.WithReflow(strategy == "Vte" ? VteReflowStrategy.Instance : GhosttyReflowStrategy.Instance);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(80, 10).WithScrollback(100).Build();
        var ct = TestContext.Current.CancellationToken;
        await using var direct = await server.CreateBrowserViewAsync("direct", ct);
        await using var relay = await Hmp1BrowserView.CreateAsync(server, "relay", ct);
        var primary = relayPrimary ? relay.Presentation : direct;
        await primary.HandleMessageAsync("""{"type":"requestPrimary","columns":80,"rows":10}"""u8.ToArray(), ct);
        await ReadUntilAsync(primary, frame => frame.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        var text = new string('a', 30) + "LINKED-" + new string('b', 30) + "-END";
        workload.Write(text[..30] +
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=529,C=1,q=2", [255, 0, 0, 255]) +
            text[30..] + "\r\n$ ");
        using var ready = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(snapshot => snapshot.GetLineTrimmed(1) == "$", TimeSpan.FromSeconds(5), "shell output consumed")
            .Build().ApplyAsync(producer, ct);
        Assert.AreEqual(text, await ReadFirstLogicalLineAsync(relay.Presentation));
        var requestId = 1;
        foreach (var width in new[] { 20, 40, 80, 20, 80 })
        {
            await primary.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(
                new { type = "resize", columns = width, rows = 10 }), ct);
            var relayedFrame = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("columns").GetInt32() == width);
            var directFrame = await ReadUntilAsync(direct, frame => frame.GetProperty("columns").GetInt32() == width);
            if (strategy != "None")
            {
                var expectedImage = directFrame.GetProperty("placements")[0];
                var relayedImage = relayedFrame.GetProperty("placements")[0];
                Assert.AreEqual(expectedImage.GetProperty("x").GetDouble(), relayedImage.GetProperty("x").GetDouble());
                Assert.AreEqual(expectedImage.GetProperty("y").GetDouble(), relayedImage.GetProperty("y").GetDouble());
            }
            var expected = strategy == "None" ? text[..20] : text;
            requestId += 2;
            Assert.AreEqual(expected, await ReadFirstLogicalLineAsync(direct, requestId), $"Producer policy at {width}");
            Assert.AreEqual(expected, await ReadFirstLogicalLineAsync(relay.Presentation, requestId), $"Relay policy at {width}");
        }
    }

    private static async Task<string?> ReadFirstLogicalLineAsync(Hwt1PresentationAdapter view, int requestId = 1)
    {
        var ct = TestContext.Current.CancellationToken;
        await view.HandleMessageAsync("""{"type":"resync"}"""u8.ToArray(), ct);
        var history = (await ReadUntilAsync(view, frame => frame.GetProperty("full").GetBoolean()))
            .GetProperty("history");
        await view.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "selection", action = "start", mode = "line", requestId, column = 0,
            generation = history.GetProperty("generation").GetString(),
            rowId = history.GetProperty("rowIds")[0].GetString()
        }), ct);
        var selected = await ReadUntilAsync(view, frame =>
            frame.GetProperty("history").GetProperty("selection").GetProperty("status").GetString() == "valid");
        var text = selected.GetProperty("history").GetProperty("selection").GetProperty("text").GetString();
        await view.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(
            new { type = "selection", action = "clear", requestId = requestId + 1 }), ct);
        await ReadUntilAsync(view, frame => frame.GetProperty("history").GetProperty("selection").GetProperty("status").GetString() != "valid");
        return text;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Relay_AttachingDuringPlacementReplacement_ReplaysPixelsOnEveryConnection(bool alternate)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).WithTimeProvider(new FakeTimeProvider()).Build();
        var ct = TestContext.Current.CancellationToken;
        await using var direct = await server.CreateBrowserViewAsync("direct", ct);
        workload.Write((alternate ? "\x1b[?1049h" : "") +
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,i=7300,q=2", [255, 0, 0, 255]));
        string? previousPeer = null;

        for (var connection = 0; connection < 2; connection++)
        {
            workload.Write("\x1b[?2026h" + KgpTestHelper.BuildCommand("a=d,d=a,q=2") +
                $"\x1b[Hcleared-{connection}");
            using var cleared = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.ContainsText($"cleared-{connection}") &&
                    snapshot.KgpPlacements.Count == 0, TimeSpan.FromSeconds(5), "placement-free attachment cut")
                .Build().ApplyAsync(producer, ct);
            await using var relay = await Hmp1BrowserView.CreateAsync(server, "relay", ct);
            Assert.AreEqual(2, server.ClientCount);
            workload.Write("\x1b[2;8H" + KgpTestHelper.BuildCommand("a=p,i=7300,C=1,q=2") + "\x1b[?2026l");

            var replay = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("placements").GetArrayLength() == 1);
            var peer = replay.GetProperty("peer").GetProperty("id").GetString();
            Assert.IsNotNull(peer);
            Assert.AreNotEqual(previousPeer, peer);
            previousPeer = peer;
            var expected = await ReadUntilAsync(direct, frame => frame.GetProperty("placements").GetArrayLength() == 1);
            Assert.AreEqual(expected.GetProperty("placements")[0].GetProperty("key").GetString(),
                replay.GetProperty("placements")[0].GetProperty("key").GetString());
            Assert.AreEqual(4, replay.GetProperty("images")[0].GetProperty("byteLength").GetInt32());

            workload.Write("\x1b[?2026h" + KgpTestHelper.BuildCommand("a=d,d=a,q=2") +
                "\x1b[3;12H" + KgpTestHelper.BuildCommand("a=p,i=7300,C=1,q=2") + "\x1b[?2026l");
            var moved = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("placements").GetArrayLength() == 1 &&
                frame.GetProperty("placements")[0].GetProperty("x").GetDouble() > replay.GetProperty("placements")[0].GetProperty("x").GetDouble());
            Assert.AreEqual(0, moved.GetProperty("images").GetArrayLength(), "Movement must reuse the replayed pixel payload.");
            await relay.DisposeAsync();
            Assert.AreEqual(1, server.ClientCount);
        }
    }

    [TestMethod]
    public async Task Relay_MixedWithDirectView_PreservesInputResizeAndPrimaryLifetime()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).Build();
        var ct = TestContext.Current.CancellationToken;
        await using var direct = await server.CreateBrowserViewAsync("direct", ct);
        await using var relay = await Hmp1BrowserView.CreateAsync(server, "relay", ct);
        var initial = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("peer").GetProperty("id").ValueKind == JsonValueKind.String);
        Assert.IsFalse(initial.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsNull(server.PrimaryPeerId);

        await relay.Presentation.HandleMessageAsync("""{"type":"requestPrimary","columns":40,"rows":12}"""u8.ToArray(), ct);
        var primary = await ReadUntilAsync(relay.Presentation, frame => frame.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.AreEqual(primary.GetProperty("peer").GetProperty("id").GetString(), server.PrimaryPeerId);
        await ReadUntilAsync(direct, frame => frame.GetProperty("columns").GetInt32() == 40);
        await direct.HandleMessageAsync("""{"type":"resize","columns":30,"rows":11}"""u8.ToArray(), ct);
        Assert.AreEqual(40, producer.Width);
        await relay.Presentation.HandleMessageAsync("""{"type":"key","key":"s"}"""u8.ToArray(), ct);
        Hex1bEvent input;
        do
        {
            input = await workload.InputEvents.ReadAsync(ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ct);
        } while (input is Hex1bResizeEvent);
        Assert.AreEqual("s", TestSeq.IsType<Hex1bKeyEvent>(input).Text);

        await relay.DisposeAsync();
        await ReadUntilAsync(direct, frame => frame.GetProperty("peer").GetProperty("primaryId").ValueKind == JsonValueKind.Null);
        Assert.IsNull(server.PrimaryPeerId);
        Assert.AreEqual(1, server.ClientCount);
        Assert.AreEqual(40, producer.Width);
        await direct.HandleMessageAsync("""{"type":"requestPrimary","columns":30,"rows":11}"""u8.ToArray(), ct);
        await ReadUntilAsync(direct, frame => frame.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.AreEqual(30, producer.Width);
    }

    [TestMethod]
    public async Task Relay_FailedOrCancelledHandshake_DoesNotLeakPeersOrHang()
    {
        await using var server = new Hmp1PresentationAdapter(20, 10);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Hmp1BrowserView.CreateAsync(server, "cancelled", cancelled.Token).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, server.ClientCount);
        await server.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            Hmp1BrowserView.CreateAsync(server, "closed", TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static async Task<JsonElement> ReadUntilAsync(Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            var frame = await view.ReadFrameAsync(timeout.Token);
            var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
            using var document = JsonDocument.Parse(frame.Slice(8, length));
            var metadata = document.RootElement;
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""), timeout.Token);
            if (predicate(metadata))
                return metadata.Clone();
        }
    }
}
