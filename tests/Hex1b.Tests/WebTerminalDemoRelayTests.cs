using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Input;
using Microsoft.Extensions.Time.Testing;
using WebTerminalDemo;

namespace Hex1b.Tests;

[TestClass]
public class WebTerminalDemoRelayTests
{
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
