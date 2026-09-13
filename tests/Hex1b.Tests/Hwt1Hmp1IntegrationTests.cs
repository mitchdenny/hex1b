using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b.Input;
using Hex1b.Sixel;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class Hwt1Hmp1IntegrationTests
{
    public static IEnumerable<object[]> KgpPlacementCuts()
    {
        var placement = KgpTestHelper.BuildCommand("a=p,i=7311,X=1,Y=3,z=11,C=1,q=2");
        foreach (var alternate in new[] { false, true })
        foreach (var filtered in new[] { false, true })
        foreach (var split in Enumerable.Range(1, placement.Length - 1))
            yield return [split, alternate, filtered];
    }

    [TestMethod]
    [DynamicData(nameof(KgpPlacementCuts))]
    public async Task LateAttachment_InsideKgpPlacement_DoesNotRenderCommandTail(
        int split, bool alternate, bool filtered)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).WithTimeProvider(new FakeTimeProvider());
        if (filtered)
            builder.AddPresentationFilter(new PassThroughPresentationFilter());
        await using var producer = builder.Build();
        var first = await ConnectAsync(server);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        await using var firstMirror = Hex1bTerminal.CreateBuilder().WithWorkload(firstClient)
            .WithHeadless().Build();
        var placement = KgpTestHelper.BuildCommand("a=p,i=7311,X=1,Y=3,z=11,C=1,q=2");
        workload.Write((alternate ? "\x1b[?1049h" : "") + "\x1b[?2026h" +
            KgpTestHelper.BuildCommand("a=t,f=32,s=1,v=1,i=7311,q=2", [255, 0, 0, 255]) +
            "\x1b[Hready\x1b[4;8H" + placement[..split]);
        await WaitForScreenAsync(producer, snapshot => snapshot.ContainsText("ready"));

        var late = await ConnectAsync(server);
        await using var lateHandle = late.Handle;
        await using var lateClient = late.Client;
        await using var lateView = new Hwt1PresentationAdapter();
        await using var lateMirror = Hex1bTerminal.CreateBuilder().WithWorkload(lateClient)
            .WithPresentation(lateView).Build();
        workload.Write(placement[split..] + "\x1b[10;1Hdone\x1b[?2026l");
        await WaitForScreenAsync(producer, snapshot => snapshot.ContainsText("done"));
        await WaitForScreenAsync(firstMirror, snapshot => snapshot.ContainsText("done"));
        await WaitForScreenAsync(lateMirror, snapshot => snapshot.ContainsText("done"));

        using var expected = producer.CreateSnapshot();
        using var early = firstMirror.CreateSnapshot();
        using var actual = lateMirror.CreateSnapshot();
        for (var row = 0; row < expected.Height; row++)
        {
            Assert.AreEqual(expected.GetLine(row), early.GetLine(row), $"Existing peer row {row}");
            Assert.AreEqual(expected.GetLine(row), actual.GetLine(row), $"Late peer row {row}");
        }
        var image = TestSeq.Single(actual.KgpPlacements);
        Assert.AreEqual(3, image.Row);
        Assert.AreEqual(7, image.Column);
        Assert.AreEqual(1u, image.CellOffsetX);
        Assert.AreEqual(3u, image.CellOffsetY);
        await ReadUntilAsync(lateView, frame => frame.GetProperty("placements").GetArrayLength() == 1);
    }

    [TestMethod]
    [DataRow("\x1b[4;", "8H")]
    [DataRow("\x1b]8;;https://example.test", "\x1b\\")]
    [DataRow("\x1b]8;;https://example.test\x1b", "\\")]
    public async Task LateAttachment_InsideAnsiSequence_PreservesContinuationAcrossTwoHops(string prefix, string suffix)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(40, 12);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient)
            .WithPresentation(relay).Build();
        workload.Write("\x1b[Hready\x1b[2;1H" + prefix);
        await WaitForScreenAsync(replica, snapshot => snapshot.ContainsText("ready"));

        var late = await ConnectAsync(relay);
        await using var lateHandle = late.Handle;
        await using var lateClient = late.Client;
        await using var lateMirror = Hex1bTerminal.CreateBuilder().WithWorkload(lateClient).WithHeadless().Build();
        workload.Write(suffix + "linked\x1b]8;;\x1b\\\x1b[10;1Hdone");
        await WaitForScreenAsync(producer, snapshot => snapshot.ContainsText("done"));
        await WaitForScreenAsync(lateMirror, snapshot => snapshot.ContainsText("done"));
        using var expected = producer.CreateSnapshot();
        using var actual = lateMirror.CreateSnapshot();
        for (var row = 0; row < expected.Height; row++)
        {
            Assert.AreEqual(expected.GetLine(row), actual.GetLine(row), $"Row {row}");
            for (var column = 0; column < expected.Width; column++)
                Assert.AreEqual(expected.GetCell(column, row).HyperlinkData?.Uri,
                    actual.GetCell(column, row).HyperlinkData?.Uri, $"Cell {column},{row}");
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public async Task LateAttachment_DuringKgpPlacementReplacement_RetainsUnusedPalette(
        bool alternateScreen, bool keepPlacement, bool compressed)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).WithTimeProvider(new FakeTimeProvider()).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 10);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient)
            .WithPresentation(relay).WithTimeProvider(new FakeTimeProvider()).Build();
        var first = await ConnectAsync(relay);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        await using var firstView = new Hwt1PresentationAdapter(20, 10);
        await using var firstMirror = Hex1bTerminal.CreateBuilder().WithWorkload(firstClient)
            .WithPresentation(firstView).Build();
        var red = Enumerable.Range(0, 9).SelectMany(_ => new byte[] { 255, 0, 0, 255 }).ToArray();
        var green = Enumerable.Range(0, 9).SelectMany(_ => new byte[] { 0, 255, 0, 255 }).ToArray();
        var encodedRed = compressed ? Compress(red) : red;
        var encodedGreen = compressed ? Compress(green) : green;
        var compression = compressed ? ",o=z" : "";
        workload.Write((alternateScreen ? "\x1b[?1049h" : "") + "\x1b[?2026h" +
            KgpTestHelper.BuildCommand("a=t,f=32,s=3,v=3,i=7300,q=2" + compression, encodedRed) +
            KgpTestHelper.BuildCommand("a=t,f=32,s=3,v=3,i=7301,q=2" + compression, encodedGreen) +
            "\x1b[2;3H" + KgpTestHelper.BuildCommand("a=p,i=7300,C=1,q=2") +
            "\x1b[4;3H" + KgpTestHelper.BuildCommand("a=p,i=7301,C=1,q=2") + "\x1b[?2026l");
        await ReadUntilAsync(firstView, frame => frame.GetProperty("placements").GetArrayLength() == 2);

        // Freeze the clock so attachment occurs inside the actual synchronized
        // frame, not after the terminal's synchronized-output watchdog expires.
        workload.Write("\x1b[?2026h" + KgpTestHelper.BuildCommand("a=d,d=a,q=2") +
            (keepPlacement ? "\x1b[2;3H" + KgpTestHelper.BuildCommand("a=p,i=7300,C=1,q=2") : "") +
            "\x1b[Hpalette-cleared");
        await WaitForScreenAsync(producer, snapshot => snapshot.ContainsText("palette-cleared") &&
            snapshot.KgpPlacements.Count == (keepPlacement ? 1 : 0));
        await WaitForScreenAsync(replica, snapshot => snapshot.ContainsText("palette-cleared") &&
            snapshot.KgpPlacements.Count == (keepPlacement ? 1 : 0));
        Assert.AreEqual(2, producer.KgpImageStore.ImageCount);
        Assert.AreEqual(2, replica.KgpImageStore.ImageCount);

        var late = await ConnectAsync(relay);
        await using var lateHandle = late.Handle;
        await using var lateClient = late.Client;
        await using var lateView = new Hwt1PresentationAdapter(20, 10);
        await using var lateMirror = Hex1bTerminal.CreateBuilder().WithWorkload(lateClient)
            .WithPresentation(lateView).Build();

        workload.Write(KgpTestHelper.BuildCommand("a=d,d=a,q=2") +
            "\x1b[2;8H" + KgpTestHelper.BuildCommand("a=p,i=7300,X=1,Y=2,C=1,q=2") +
            "\x1b[4;8H" + KgpTestHelper.BuildCommand("a=p,i=7301,X=1,Y=2,C=1,q=2") +
            "\x1b[6;1Hreplacement-done\x1b[?2026l");
        await WaitForScreenAsync(firstMirror, snapshot => snapshot.ContainsText("replacement-done") &&
            snapshot.KgpPlacements.Count == 2);
        await WaitForScreenAsync(lateMirror, snapshot => snapshot.ContainsText("replacement-done") &&
            snapshot.KgpPlacements.Count == 2);
        using var actual = lateMirror.CreateSnapshot();
        Assert.AreEqual(alternateScreen, actual.InAlternateScreen);
        TestSeq.AreEqual(red, actual.KgpImages[7300].Data);
        TestSeq.AreEqual(green, actual.KgpImages[7301].Data);
        Assert.AreEqual(compressed, actual.KgpImages[7300].IsZlibCompressed);
        Assert.AreEqual(compressed, actual.KgpImages[7301].IsZlibCompressed);
        if (compressed)
        {
            TestSeq.AreEqual(encodedRed, actual.KgpImages[7300].EncodedData.ToArray());
            TestSeq.AreEqual(encodedGreen, actual.KgpImages[7301].EncodedData.ToArray());
        }
        foreach (var placement in actual.KgpPlacements)
        {
            Assert.AreEqual(7, placement.Column);
            Assert.AreEqual(1u, placement.CellOffsetX);
            Assert.AreEqual(2u, placement.CellOffsetY);
            Assert.IsTrue(placement.UsesNativeSize);
        }
        await ReadUntilAsync(lateView, frame => frame.GetProperty("placements").GetArrayLength() == 2);
    }

    [TestMethod]
    public async Task ProducerBackedView_ReplacementsAndReconnect_SendCurrentResourcesAsIndependentBaselines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand(
            "a=T,f=32,s=1,v=1,o=z,i=1,C=1,q=2", Compress([1, 0, 0, 255]))));
        await using var first = await server.CreateBrowserViewAsync("first");
        var firstBytes = await first.ReadFrameAsync();
        var firstCopy = firstBytes.ToArray();
        await first.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        for (var generation = 2; generation <= 65; generation++)
            producer.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand(
                "a=T,f=32,s=1,v=1,o=z,i=1,C=1,q=2", Compress([(byte)generation, 0, 0, 255]))));
        var latest = await first.ReadFrameAsync();
        using (var metadata = JsonDocument.Parse(latest.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(latest.Span[4..]))))
            Assert.AreEqual(1, metadata.RootElement.GetProperty("retainedImages").GetArrayLength());
        TestSeq.AreEqual(new byte[] { 65, 0, 0, 255 }, latest.Span[^4..].ToArray());

        await using var late = await server.CreateBrowserViewAsync("late");
        var lateBytes = await late.ReadFrameAsync();
        TestSeq.AreEqual(new byte[] { 65, 0, 0, 255 }, lateBytes.Span[^4..].ToArray());
        await first.DisposeAsync();
        TestSeq.AreEqual(firstCopy, firstBytes.ToArray());
        for (var reconnect = 0; reconnect < 5; reconnect++)
        {
            await using var fresh = await server.CreateBrowserViewAsync($"reconnect-{reconnect}");
            var bytes = await fresh.ReadFrameAsync();
            using var metadata = JsonDocument.Parse(bytes.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(bytes.Span[4..])));
            Assert.IsTrue(metadata.RootElement.GetProperty("full").GetBoolean());
            Assert.AreEqual(1, metadata.RootElement.GetProperty("images").GetArrayLength());
            Assert.AreEqual(1, metadata.RootElement.GetProperty("retainedImages").GetArrayLength());
            TestSeq.AreEqual(new byte[] { 65, 0, 0, 255 }, bytes.Span[^4..].ToArray());
        }
        producer.ApplyTokens(AnsiTokenizer.Tokenize(KgpTestHelper.BuildCommand("a=d,d=I,i=1,q=2")));
        await late.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray());
        var empty = await late.ReadFrameAsync();
        using var emptyMetadata = JsonDocument.Parse(empty.Slice(8, BinaryPrimitives.ReadInt32LittleEndian(empty.Span[4..])));
        Assert.AreEqual(0, emptyMetadata.RootElement.GetProperty("retainedImages").GetArrayLength());
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output,
            System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(data);
        return output.ToArray();
    }

    [TestMethod]
    public async Task ProducerBackedView_SplitSynchronizedOutput_PublishesCompleteFrameToAllViews()
    {
        var clock = new FakeTimeProvider();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).WithTimeProvider(clock).Build();
        await using var first = await server.CreateBrowserViewAsync("first");
        await ReadUntilAsync(first, _ => true);

        workload.Write("\x1b[?20");
        workload.Write("26h\x1b[H\x1b[2Jhalf");
        await WaitForScreenAsync(producer, snapshot => snapshot.GetLineTrimmed(0) == "half");
        var firstPending = first.ReadFrameAsync().AsTask();
        await using var late = await server.CreateBrowserViewAsync("late");
        var latePending = late.ReadFrameAsync().AsTask();
        Assert.IsFalse(firstPending.IsCompleted);
        Assert.IsFalse(latePending.IsCompleted);

        // The raw output pump needs the same ordering lock as the browser reader.
        // Waiting for ESU while holding it would deadlock this final chunk.
        workload.Write("\x1b[Hdone\x1b[2;2H\x1bP7;1q\"1;1;2;6#1;2;100;0;0#1BB\x1b\\\x1b[?2026l");
        await WaitForScreenAsync(producer, snapshot => snapshot.GetLineTrimmed(0) == "done");
        var firstBytes = await firstPending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var lateBytes = await latePending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var firstFrame = JsonDocument.Parse(firstBytes.Slice(8,
            BinaryPrimitives.ReadInt32LittleEndian(firstBytes.Span[4..])));
        using var lateFrame = JsonDocument.Parse(lateBytes.Slice(8,
            BinaryPrimitives.ReadInt32LittleEndian(lateBytes.Span[4..])));
        Assert.AreEqual(1, firstFrame.RootElement.GetProperty("placements").GetArrayLength());
        Assert.AreEqual(1, lateFrame.RootElement.GetProperty("placements").GetArrayLength());
        Assert.IsTrue(lateFrame.RootElement.GetProperty("full").GetBoolean());
    }

    [TestMethod]
    public async Task ProducerBackedView_NativePeerAndBrowser_ShareRosterAndPrimaryAuthority()
    {
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter())
            .WithPresentation(server).WithDimensions(20, 10).WithScrollback(100).Build();
        await using var browser = await server.CreateBrowserViewAsync("browser");
        var browserBaseline = await ReadUntilAsync(browser, _ => true);
        var browserId = PeerId(browserBaseline);
        var native = await ConnectAsync(server);
        await using var nativeHandle = native.Handle;
        await using var nativeClient = native.Client;
        Assert.IsTrue(nativeClient.Peers.Any(peer => peer.PeerId == browserId));
        await nativeClient.RequestPrimaryAsync(40, 12);
        Assert.IsTrue(await nativeClient.WaitForRoleAsync(true, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var primary = await ReadUntilAsync(browser, frame => PrimaryId(frame) == nativeClient.PeerId);
        Assert.AreEqual(40, primary.GetProperty("columns").GetInt32());
        Assert.AreEqual(12, primary.GetProperty("rows").GetInt32());
        Assert.IsFalse(primary.GetProperty("peer").GetProperty("isPrimary").GetBoolean());

        await browser.HandleMessageAsync("""{"type":"requestPrimary","columns":30,"rows":10}"""u8.ToArray());
        Assert.IsTrue(await nativeClient.WaitForRoleAsync(false, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.AreEqual(browserId, nativeClient.PrimaryPeerId);
        var left = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        nativeClient.OnPeerLeft += (args, _) =>
        {
            left.TrySetResult(args.PeerId);
            return Task.CompletedTask;
        };
        await browser.DisposeAsync();
        Assert.AreEqual(browserId, await left.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.IsNull(server.PrimaryPeerId);
        Assert.AreEqual(1, server.ClientCount);
        await nativeClient.RequestPrimaryAsync(20, 10);
        Assert.IsTrue(await nativeClient.WaitForRoleAsync(true, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.AreEqual(20, producer.Width);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TabbedOutput_AfterHelloOrRemoteResize_PreservesColumns(bool wideHello)
    {
        var (serverStream, clientStream) = CreateStreams();
        await using var server = serverStream;
        await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(clientStream)
        });
        await HandshakeAsync(server, client, wideHello ? 154 : 80, 34, "viewer", "primary", []);
        await using var view = new Hwt1PresentationAdapter();
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(view).Build();
        if (!wideHello)
            await Hmp1Protocol.WriteResizeAsync(server, 154, 34, TestContext.Current.CancellationToken);

        await Hmp1Protocol.WriteFrameAsync(server, Hmp1FrameType.Output,
            Encoding.UTF8.GetBytes("\x1b[1;73HMouseTest\t\tRescueDemo\r\nDONE"), TestContext.Current.CancellationToken);
        await WaitForScreenAsync(mirror, snapshot => snapshot.ContainsText("DONE"));

        using var snapshot = mirror.CreateSnapshot();
        Assert.AreEqual(154, snapshot.Width);
        Assert.AreEqual("RescueDemo", snapshot.GetLine(0).Substring(96, 10));
        Assert.AreEqual(" ", snapshot.GetCell(153, 0).Character);
        Assert.AreEqual("DONE", snapshot.GetLineTrimmed(1));
        var metadata = await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == 154 &&
            m.GetProperty("cursor").GetProperty("y").GetInt32() == 1);
        Assert.AreEqual(4, metadata.GetProperty("cursor").GetProperty("x").GetInt32());
    }

    [TestMethod]
    public async Task LateAttachment_Hyperlinks_PreserveDestinationsAcrossReconnect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).Build();

        foreach (var destination in new[] { "https://example.com/first", "https://example.com/second" })
        {
            producer.ApplyTokens(AnsiTokenizer.Tokenize(
                $"\x1b[H\x1b]8;id=link;{destination}\x1b\\LINK\x1b]8;;\x1b\\ plain" +
                $"\x1b[2;19H\x1b]8;id=wide;{destination}\x1b\\\u754cAB\x1b]8;;\x1b\\" +
                $"\x1b[10;20H\x1b]8;id=last;{destination}\x1b\\Z\x1b]8;;\x1b\\\x1b[4;1H"));
            var connection = await ConnectAsync(server);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            await using var view = new Hwt1PresentationAdapter(20, 10);
            await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
                .WithPresentation(view).Build();
            await WaitForScreenAsync(mirror, snapshot => snapshot.GetLineTrimmed(0) == "LINK plain" &&
                snapshot.GetLineTrimmed(2) == "AB" && snapshot.GetCell(19, 9).Character == "Z");

            using var expected = producer.CreateSnapshot();
            using var actual = mirror.CreateSnapshot();
            for (var row = 0; row < expected.Height; row++)
            {
                for (var column = 0; column < expected.Width; column++)
                {
                    var expectedLink = expected.GetCell(column, row).HyperlinkData;
                    var actualLink = actual.GetCell(column, row).HyperlinkData;
                    Assert.AreEqual(expectedLink?.Uri, actualLink?.Uri, $"Destination at {column},{row}");
                    Assert.AreEqual(expectedLink?.Parameters, actualLink?.Parameters, $"Parameters at {column},{row}");
                }
            }

            var frame = await ReadUntilAsync(view, metadata => PeerId(metadata) is not null);
            var links = frame.GetProperty("hyperlinks").EnumerateArray().ToArray();
            Assert.IsNotEmpty(links);
            Assert.IsTrue(links.All(link => link.GetProperty("uri").GetString() == destination));

            workload.Write(".");
            await WaitForScreenAsync(mirror, snapshot => snapshot.GetCell(0, 3).Character == ".");
            using var continued = mirror.CreateSnapshot();
            Assert.IsNull(continued.GetCell(0, 3).HyperlinkData);
        }
    }

    [TestMethod]
    public async Task LateAttachment_ActiveHyperlink_PreservesSubsequentOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]8;id=active;https://example.com/active\x1b\\A"));

        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithPresentation(view).Build();
        await WaitForScreenAsync(mirror, snapshot => snapshot.GetLineTrimmed(0) == "A");

        workload.Write("B\x1b]8;;\x1b\\.");
        await WaitForScreenAsync(mirror, snapshot => snapshot.GetLineTrimmed(0) == "AB.");
        using var snapshot = mirror.CreateSnapshot();
        Assert.AreEqual("https://example.com/active", snapshot.GetCell(1, 0).HyperlinkData?.Uri);
        Assert.AreEqual("id=active", snapshot.GetCell(1, 0).HyperlinkData?.Parameters);
        Assert.IsNull(snapshot.GetCell(2, 0).HyperlinkData);
        Assert.IsNull(snapshot.GetCell(3, 0).HyperlinkData);
    }

    [TestMethod]
    public async Task LateAttachment_SilentRunningAnimation_AdvancesHwtPixelsWithoutFreshOutput()
    {
        var producerTime = new FakeTimeProvider();
        var mirrorTime = new FakeTimeProvider();
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(producerWorkload)
            .WithPresentation(server).WithDimensions(20, 10).WithTimeProvider(producerTime).Build();
        producerWorkload.Write(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,p=11,C=1,q=2", [1, 0, 0, 255]) +
            string.Concat(Enumerable.Range(2, 3).Select(frame => KgpTestHelper.BuildCommand(
                "a=f,f=32,s=1,v=1,i=7,X=1,z=100,q=2", [(byte)frame, 0, 0, 255]))) +
            KgpTestHelper.BuildCommand("a=a,i=7,r=1,z=100,c=1,s=3,v=1,q=2"));
        await WaitForScreenAsync(producer, s => s.KgpImages.TryGetValue(7, out var image) &&
            image.FrameCount == 4 && image.AnimationState?.PlaybackState == KgpParsedCommand.AnimationPlaybackState.Running);
        producerTime.Advance(TimeSpan.FromMilliseconds(100));
        producerTime.Advance(TimeSpan.FromMilliseconds(75));
        Assert.AreEqual(2, producer.KgpImageStore.GetImageById(7)!.CurrentFrameNumber);
        var producerBytes = producer.OutputBytesRead;
        Assert.IsGreaterThan(0L, producerBytes);

        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithPresentation(view).WithTimeProvider(mirrorTime).Build();
        await WaitForScreenAsync(mirror, s => s.KgpImages.TryGetValue(7, out var image) &&
            image.FrameCount == 4 && image.CurrentFrameNumber == 2 &&
            s.KgpAnimationTimestamp - image.AnimationState!.CurrentFrameShownAt == TimeSpan.FromMilliseconds(75));
        var mirrorBytes = mirror.OutputBytesRead;
        var cache = new Dictionary<string, byte[]>();
        var previousRevision = 0u;

        foreach (var (advance, pixel) in new[] { (0, 2), (25, 3), (100, 4), (100, 1), (100, 2) })
        {
            producerTime.Advance(TimeSpan.FromMilliseconds(advance));
            mirrorTime.Advance(TimeSpan.FromMilliseconds(advance));
            var (metadata, pixels) = await ReadAnimationFrameAsync(view, cache);
            TestSeq.AreEqual(new byte[] { (byte)pixel, 0, 0, 255 }, pixels);
            TestSeq.AreEqual(producer.KgpImageStore.GetImageById(7)!.CurrentFrameData, pixels);
            Assert.IsGreaterThan(previousRevision, metadata.GetProperty("revision").GetUInt32());
            previousRevision = metadata.GetProperty("revision").GetUInt32();
            Assert.AreEqual(mirrorBytes, metadata.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
            Assert.AreEqual(producerBytes, producer.OutputBytesRead);
            Assert.AreEqual(mirrorBytes, mirror.OutputBytesRead);
        }
        Assert.AreEqual(4, cache.Count, "Every animation image is projected once and then reused.");
    }

    private static async Task<(JsonElement Metadata, byte[] Pixels)> ReadAnimationFrameAsync(
        Hwt1PresentationAdapter view, Dictionary<string, byte[]> cache)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            var frame = await view.ReadFrameAsync(timeout.Token);
            var metadataLength = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
            using var document = JsonDocument.Parse(frame.Slice(8, metadataLength));
            var metadata = document.RootElement;
            var images = metadata.GetProperty("images").EnumerateArray().ToArray();
            var offset = frame.Length - images.Sum(image => image.GetProperty("byteLength").GetInt32());
            foreach (var image in images)
            {
                var length = image.GetProperty("byteLength").GetInt32();
                cache[image.GetProperty("key").GetString()!] = frame.Slice(offset, length).ToArray();
                offset += length;
            }
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{metadata.GetProperty("revision").GetUInt32()}}}"""), timeout.Token);
            if (metadata.GetProperty("placements").GetArrayLength() > 0)
                return (metadata.Clone(), cache[metadata.GetProperty("placements")[0].GetProperty("key").GetString()!]);
        }
    }

    [TestMethod]
    public async Task LateAttachment_UnsupportedKgp_SkipsCheckpointAndContinuesText()
    {
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(producerWorkload)
            .WithPresentation(server).WithDimensions(20, 10).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            KgpTestHelper.BuildCommand("a=T,f=32,s=1,v=1,i=7,C=1,q=2", [1, 0, 0, 255]) +
            KgpTestHelper.BuildCommand("a=f,f=32,s=1,v=1,i=7,z=100,q=2", [2, 0, 0, 255])));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless(new TerminalCapabilities { SupportsKgp = false }).Build();

        producerWorkload.Write("after-animation");

        await WaitForScreenAsync(mirror, s => s.ContainsText("after-animation"));
        Assert.IsTrue(client.IsConnected);
        Assert.AreEqual(0, mirror.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public async Task ProducerCapabilities_HeadlessGraphics_UseCanonicalBrowserCellMetrics()
    {
        await using var server = new Hmp1PresentationAdapter();
        await using var view = new Hwt1PresentationAdapter();

        Assert.IsTrue(server.Capabilities.SupportsSixel);
        Assert.IsTrue(server.Capabilities.SupportsKgp);
        Assert.AreEqual(SixelPresentationSupport.Headless, server.Capabilities.SixelSupport);
        Assert.AreEqual(10, server.Capabilities.CellPixelWidth);
        Assert.AreEqual(20, server.Capabilities.CellPixelHeight);
        Assert.AreEqual(view.Capabilities.SixelCellMetrics, server.Capabilities.SixelCellMetrics);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task AddClient_HandshakeWriteFailsOrCancels_RemovesPartialPeerAndDisposesStream(
        bool failStateSync, bool cancelled)
    {
        await using var server = new Hmp1PresentationAdapter();
        var existing = await ConnectAsync(server);
        await using var existingHandle = existing.Handle;
        await using var existingClient = existing.Client;
        await existingClient.RequestPrimaryAsync(80, 24);
        Assert.IsTrue(await existingClient.WaitForRoleAsync(true, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        var joined = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var left = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        existingClient.OnPeerJoined += (args, _) =>
        {
            joined.TrySetResult(args.PeerId);
            return Task.CompletedTask;
        };
        existingClient.OnPeerLeft += (args, _) =>
        {
            left.TrySetResult(args.PeerId);
            return Task.CompletedTask;
        };
        using var hello = new MemoryStream();
        await Hmp1Protocol.WriteClientHelloAsync(hello, "failing", null, TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var stream = new FailingHandshakeStream(hello.ToArray(),
            failStateSync ? Hmp1FrameType.StateSync : Hmp1FrameType.Hello, cancelled ? cancellation : null);

        if (cancelled)
            await Assert.ThrowsAsync<OperationCanceledException>(() => server.AddClient(stream, cancellation.Token));
        else
            await Assert.ThrowsExactlyAsync<IOException>(() => server.AddClient(stream, cancellation.Token));

        Assert.AreEqual(1, server.ClientCount);
        Assert.IsTrue(stream.IsDisposed);
        Assert.AreEqual(existingClient.PeerId, server.PrimaryPeerId);
        Assert.AreEqual(await joined.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            await left.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(0, existingClient.Peers.Count);
    }

    [TestMethod]
    public async Task MultipleViews_Hmp1OwnsPrimaryResizeAndDisconnect_UpdatesWithoutOutput()
    {
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(80, 24);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(producerWorkload).WithPresentation(server).Build();
        producerWorkload.Write("ready");
        await WaitForScreenAsync(producer, s => s.ContainsText("ready"));

        var first = await ConnectAsync(server);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        await using var firstView = new Hwt1PresentationAdapter();
        await using var firstMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(firstClient).WithPresentation(firstView).Build();
        var second = await ConnectAsync(server);
        await using var secondHandle = second.Handle;
        await using var secondClient = second.Client;
        await using var secondView = new Hwt1PresentationAdapter();
        await using var secondMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(secondClient).WithPresentation(secondView).Build();
        var native = await ConnectAsync(server);
        await using var nativeHandle = native.Handle;
        await using var nativeClient = native.Client;

        await WaitForScreenAsync(firstMirror, s => s.ContainsText("ready"));
        await WaitForScreenAsync(secondMirror, s => s.ContainsText("ready"));
        var firstBaseline = await ReadUntilAsync(firstView, m => PeerId(m) == firstClient.PeerId);
        var secondBaseline = await ReadUntilAsync(secondView, m => PeerId(m) == secondClient.PeerId);
        Assert.IsFalse(firstBaseline.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsFalse(secondBaseline.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsNull(server.PrimaryPeerId);

        await firstView.HandleMessageAsync("""{"type":"requestPrimary","columns":80,"rows":24}"""u8.ToArray());
        var firstPrimary = await ReadUntilAsync(firstView, m => PrimaryId(m) == firstClient.PeerId);
        await ReadUntilAsync(secondView, m => PrimaryId(m) == firstClient.PeerId);
        Assert.IsTrue(firstPrimary.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsFalse(firstPrimary.GetProperty("full").GetBoolean(), "A role-only change need not reset the grid.");

        await firstView.HandleMessageAsync("""{"type":"resize","columns":100,"rows":30}"""u8.ToArray());
        var firstResized = await ReadUntilAsync(firstView, m => m.GetProperty("columns").GetInt32() == 100);
        await ReadUntilAsync(secondView, m => m.GetProperty("columns").GetInt32() == 100);
        Assert.IsTrue(firstResized.GetProperty("full").GetBoolean());
        Assert.AreEqual(firstBaseline.GetProperty("stats").GetProperty("workloadBytes").GetInt64(),
            firstResized.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
        Assert.AreEqual(100, firstMirror.Width);
        Assert.AreEqual(30, secondMirror.Height);
        Assert.AreEqual(100, secondView.Width);

        await secondView.HandleMessageAsync("""{"type":"resize","columns":40,"rows":12}"""u8.ToArray());
        Assert.AreEqual(100, secondMirror.Width);
        Assert.AreEqual(100, server.Width);
        await secondView.HandleMessageAsync("""{"type":"key","key":"s"}"""u8.ToArray());
        Hex1bEvent input;
        do
        {
            input = await producerWorkload.InputEvents.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        } while (input is Hex1bResizeEvent);
        Assert.AreEqual("s", TestSeq.IsType<Hex1bKeyEvent>(input).Text);

        await nativeClient.RequestPrimaryAsync(120, 40);
        Assert.IsTrue(await nativeClient.WaitForRoleAsync(true, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await ReadUntilAsync(firstView, m => PrimaryId(m) == nativeClient.PeerId);
        await ReadUntilAsync(secondView, m => PrimaryId(m) == nativeClient.PeerId);
        await firstView.HandleMessageAsync("""{"type":"resize","columns":50,"rows":15}"""u8.ToArray());
        Assert.AreEqual(120, firstMirror.Width);
        Assert.AreEqual(120, server.Width);

        await nativeClient.DisposeAsync();
        var unassignedFirst = await ReadUntilAsync(firstView, m => PrimaryId(m) is null);
        var unassignedSecond = await ReadUntilAsync(secondView, m => PrimaryId(m) is null);
        Assert.IsNull(server.PrimaryPeerId);
        Assert.AreEqual(120, unassignedFirst.GetProperty("columns").GetInt32());
        Assert.AreEqual(40, unassignedSecond.GetProperty("rows").GetInt32());
        Assert.IsFalse(unassignedFirst.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.IsFalse(unassignedSecond.GetProperty("peer").GetProperty("isPrimary").GetBoolean());

        await secondView.HandleMessageAsync("""{"type":"requestPrimary","columns":120,"rows":40}"""u8.ToArray());
        await ReadUntilAsync(firstView, m => PrimaryId(m) == secondClient.PeerId);
        var secondPrimary = await ReadUntilAsync(secondView, m => PrimaryId(m) == secondClient.PeerId);
        Assert.IsTrue(secondPrimary.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        Assert.AreEqual(secondClient.PeerId, server.PrimaryPeerId);

        await secondMirror.DisposeAsync();
        var browserClosed = await ReadUntilAsync(firstView, m => PrimaryId(m) is null);
        Assert.IsNull(server.PrimaryPeerId);
        Assert.AreEqual(120, browserClosed.GetProperty("columns").GetInt32());
        Assert.AreEqual(40, browserClosed.GetProperty("rows").GetInt32());
        Assert.IsFalse(browserClosed.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
    }

    [TestMethod]
    public async Task OrderedControls_QueuedOldOutput_AppliesBeforeResizeAndRoleMetadata()
    {
        var (serverStream, clientStream) = CreateStreams();
        await using var server = serverStream;
        await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(clientStream)
        });
        await HandshakeAsync(server, client, 20, 10, "viewer", null, []);
        var gate = new GatedOutputFilter();
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithPresentation(view).AddWorkloadFilter(gate).Build();
        gate.Width = () => mirror.Width;
        await ReadUntilAsync(view, m => PeerId(m) == "viewer");

        var roleReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.OnRoleChanged += (_, _) =>
        {
            roleReceived.TrySetResult();
            return Task.CompletedTask;
        };
        await Hmp1Protocol.WriteFrameAsync(server, Hmp1FrameType.Output,
            "ABCDEFGHIJKLMNOPQRSTUVWXY"u8.ToArray(), TestContext.Current.CancellationToken);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            await Hmp1Protocol.WriteResizeAsync(server, 40, 12, TestContext.Current.CancellationToken);
            await Hmp1Protocol.WriteRoleChangeAsync(server, "viewer", 40, 12, "test", TestContext.Current.CancellationToken);
            await Hmp1Protocol.WriteFrameAsync(server, Hmp1FrameType.Output,
                "\x1b[3;1HNEW"u8.ToArray(), TestContext.Current.CancellationToken);
            await roleReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.AreEqual(40, client.CurrentWidth, "Public HMP callbacks remain receipt-time notifications.");
            Assert.IsTrue(client.IsPrimary);
            Assert.AreEqual(20, mirror.Width, "Queued controls must not overtake blocked old output.");
            using var snapshot = mirror.CreateSnapshot(out var appliedState);
            Assert.IsFalse(appliedState!.IsPrimary);
            Assert.AreEqual(20, snapshot.Width);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        await WaitForScreenAsync(mirror, s => s.Width == 40 && s.GetCell(0, 2).Character == "N");
        using var result = mirror.CreateSnapshot();
        Assert.AreEqual("U", result.GetCell(0, 1).Character);
        Assert.AreEqual("Y", result.GetCell(4, 1).Character);
        Assert.AreEqual(" ", result.GetCell(20, 0).Character);
        Assert.AreEqual("NEW", string.Concat(Enumerable.Range(0, 3).Select(x => result.GetCell(x, 2).Character)));
        TestSeq.AreEqual(new[] { 20, 40 }, gate.OutputWidths);
        var frame = await ReadUntilAsync(view, m => PrimaryId(m) == "viewer" && m.GetProperty("columns").GetInt32() == 40);
        Assert.IsTrue(frame.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
    }

    [TestMethod]
    [DataRow(360, 120)]
    [DataRow(400, 130)]
    public async Task FreshAttachment_LargeAuthoritativeGrid_DoesNotResizeProducerOrEchoRemoteResize(
        int width, int height)
    {
        var (serverStream, clientStream) = CreateStreams();
        await using var server = serverStream;
        await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(clientStream)
        });
        await HandshakeAsync(server, client, width, height, "primary", "primary",
            Encoding.UTF8.GetBytes($"\x1b[{height};{width}HX"));
        await using var view = new Hwt1PresentationAdapter();
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(view).Build();
        await WaitForScreenAsync(mirror, s => s.Width == width && s.Height == height &&
            s.GetCell(width - 1, height - 1).Character == "X");
        var initial = await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == width);
        Assert.AreEqual(height, initial.GetProperty("rows").GetInt32());
        Assert.AreEqual(width, view.Width);

        await Hmp1Protocol.WriteResizeAsync(server, width + 40, height + 10, TestContext.Current.CancellationToken);
        await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == width + 40);
        await view.HandleMessageAsync("""{"type":"input","text":"marker"}"""u8.ToArray());
        var next = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.Input, next!.Value.Type,
            "Constructing the mirror and applying remote geometry must not send Resize frames.");
        Assert.AreEqual("marker", Encoding.UTF8.GetString(next.Value.Payload.Span));
        Assert.AreEqual(width + 40, mirror.Width);
        Assert.AreEqual(height + 10, mirror.Height);
    }

    [TestMethod]
    public async Task IsReadOnly_RemotePrimary_GatesCommandsWithoutChangingAuthorityOrDirectInput()
    {
        var (serverStream, clientStream) = CreateStreams();
        await using var server = serverStream;
        await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(clientStream)
        });
        await HandshakeAsync(server, client, 20, 10, "viewer", "viewer",
            Encoding.UTF8.GetBytes("\x1b[?1;2004;1003;1006h"));
        await using var view = new Hwt1PresentationAdapter(20, 10) { IsReadOnly = true };
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(view).Build();
        var baseline = await ReadUntilAsync(view, m => PeerId(m) == "viewer");
        Assert.IsTrue(baseline.GetProperty("peer").GetProperty("isPrimary").GetBoolean());

        foreach (var json in new[]
        {
            """{"type":"input","text":"blocked"}""",
            """{"type":"paste","text":"blocked"}""",
            """{"type":"key","key":"ArrowUp"}""",
            """{"type":"mouse","action":"down","button":"left","x":1,"y":2}""",
            """{"type":"resize","columns":40,"rows":12}""",
            """{"type":"requestPrimary","columns":50,"rows":15}"""
        })
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(json));
        await mirror.SendInputAsync("direct"u8.ToArray(), TestContext.Current.CancellationToken);
        var input = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.Input, input!.Value.Type);
        Assert.AreEqual("direct", Encoding.UTF8.GetString(input.Value.Payload.Span));
        Assert.IsTrue(client.IsPrimary);
        Assert.AreEqual("viewer", client.PrimaryPeerId);
        Assert.AreEqual(20, mirror.Width);
        Assert.AreEqual(10, mirror.Height);

        view.IsReadOnly = false;
        await view.HandleMessageAsync("""{"type":"resize","columns":40,"rows":12}"""u8.ToArray());
        var resize = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.Resize, resize!.Value.Type);
        Assert.AreEqual((40, 12), Hmp1Protocol.ParseResize(resize.Value.Payload));
        await view.HandleMessageAsync("""{"type":"requestPrimary","columns":50,"rows":15}"""u8.ToArray());
        var claim = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.RequestPrimary, claim!.Value.Type);

        view.IsReadOnly = true;
        await Hmp1Protocol.WriteResizeAsync(server, 40, 12, TestContext.Current.CancellationToken);
        var resized = await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == 40);
        Assert.IsTrue(resized.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
        await Hmp1Protocol.WriteRoleChangeAsync(server, "native", 40, 12, "RequestPrimary",
            TestContext.Current.CancellationToken);
        var secondary = await ReadUntilAsync(view, m => PrimaryId(m) == "native");
        Assert.AreEqual("viewer", PeerId(secondary));
        Assert.IsFalse(secondary.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
    }

    [TestMethod]
    public async Task ResizeAndPrimaryRequests_WaitForProducerConfirmation_IgnoreSecondaryResize()
    {
        var (serverStream, clientStream) = CreateStreams();
        await using var server = serverStream;
        await using var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult(clientStream)
        });
        await HandshakeAsync(server, client, 20, 10, "viewer", "viewer", []);
        await using var view = new Hwt1PresentationAdapter(20, 10);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(view).Build();
        await ReadUntilAsync(view, m => PeerId(m) == "viewer");

        await view.HandleMessageAsync("""{"type":"resize","columns":40,"rows":12}"""u8.ToArray());
        var resize = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.Resize, resize!.Value.Type);
        Assert.AreEqual((40, 12), Hmp1Protocol.ParseResize(resize.Value.Payload));
        Assert.AreEqual(20, mirror.Width);
        Assert.AreEqual(20, view.Width);
        await Hmp1Protocol.WriteResizeAsync(server, 40, 12, TestContext.Current.CancellationToken);
        await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == 40);

        await view.HandleMessageAsync("""{"type":"requestPrimary","columns":50,"rows":15}"""u8.ToArray());
        var claim = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.RequestPrimary, claim!.Value.Type);
        Assert.AreEqual(40, mirror.Width);
        await Hmp1Protocol.WriteRoleChangeAsync(server, "viewer", 50, 15, "RequestPrimary", TestContext.Current.CancellationToken);
        await ReadUntilAsync(view, m => m.GetProperty("columns").GetInt32() == 50);

        await Hmp1Protocol.WriteRoleChangeAsync(server, "native", 50, 15, "RequestPrimary", TestContext.Current.CancellationToken);
        await ReadUntilAsync(view, m => PrimaryId(m) == "native");
        await view.HandleMessageAsync("""{"type":"resize","columns":20,"rows":10}"""u8.ToArray());
        await view.HandleMessageAsync("""{"type":"input","text":"secondary"}"""u8.ToArray());
        var input = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(Hmp1FrameType.Input, input!.Value.Type);
        Assert.AreEqual("secondary", Encoding.UTF8.GetString(input.Value.Payload.Span));
        Assert.AreEqual(50, mirror.Width);
    }

    [TestMethod]
    public async Task ProducerOrdering_ConcurrentClaimsAndFreshAttachment_PreserveAppliedOutputAndFinalGeometry()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 10);
        var gate = new GatedOutputFilter();
        // Exercise a scheduler handoff before the terminal's synchronous resize handler.
        server.Resized += (_, _) => Thread.Yield();
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).AddWorkloadFilter(gate).Build();
        gate.Width = () => producer.Width;
        var first = await ConnectAsync(server);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        var second = await ConnectAsync(server);
        await using var secondHandle = second.Handle;
        await using var secondClient = second.Client;

        workload.Write("ABCDEFGHIJKLMNOPQRSTUVWXY");
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var freshConnection = ConnectAsync(server);
        try
        {
            for (var i = 0; i < 16; i++)
            {
                await Task.WhenAll(firstClient.RequestPrimaryAsync(40 + i, 12),
                    secondClient.RequestPrimaryAsync(60 + i, 15));
            }
            Assert.AreEqual(20, producer.Width,
                "Control transactions must wait until already-forwarded output is applied.");
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        var fresh = await freshConnection.WaitAsync(TimeSpan.FromSeconds(5));
        await using var freshHandle = fresh.Handle;
        await using var freshClient = fresh.Client;
        await using var freshView = new Hwt1PresentationAdapter();
        await using var freshMirror = Hex1bTerminal.CreateBuilder()
            .WithWorkload(freshClient).WithPresentation(freshView).Build();
        var firstFinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondFinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.OnResized += (args, _) =>
        {
            if (args.Width == 90) firstFinal.TrySetResult();
            if (args.Width == 110) secondFinal.TrySetResult();
            return Task.CompletedTask;
        };
        // These markers use the same per-peer order as the earlier claims.
        await Task.WhenAll(firstClient.RequestPrimaryAsync(90, 18), secondClient.RequestPrimaryAsync(110, 20));
        await Task.WhenAll(firstFinal.Task, secondFinal.Task)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var finalWidth = server.Width;
        var finalHeight = server.Height;
        Assert.AreEqual(finalWidth, producer.Width);
        Assert.AreEqual(finalHeight, producer.Height);
        using var snapshot = producer.CreateSnapshot();
        Assert.AreEqual("U", snapshot.GetCell(0, 1).Character,
            "The 25-character output must have wrapped at its original 20-column grid.");

        await WaitForScreenAsync(freshMirror, s => s.Width == finalWidth &&
            s.Height == finalHeight && s.GetCell(0, 1).Character == "U");
        var frame = await ReadUntilAsync(freshView, m => m.GetProperty("columns").GetInt32() == finalWidth);
        Assert.AreEqual(finalHeight, frame.GetProperty("rows").GetInt32());
    }

    [TestMethod]
    public async Task BuilderIntegration_UnconnectedThenConnected_ReportsHmpPeerWithoutBindingCallbacks()
    {
        await using var server = new Hmp1PresentationAdapter(100, 30);
        var (serverStream, clientStream) = CreateStreams();
        await using var view = new Hwt1PresentationAdapter();
        await using var mirror = Hex1bTerminal.CreateBuilder()
            .WithHmp1Stream(clientStream).WithPresentation(view).Build();
        var unconnected = await ReadUntilAsync(view, _ => true);
        Assert.IsNull(PeerId(unconnected));
        Assert.IsFalse(unconnected.GetProperty("peer").GetProperty("isPrimary").GetBoolean());

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var accepted = server.AddClient(serverStream, cancellation.Token);
        var running = mirror.RunAsync(cancellation.Token);
        await using var handle = await accepted.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var connected = await ReadUntilAsync(view, m => PeerId(m) is not null);
            Assert.AreEqual(100, connected.GetProperty("columns").GetInt32());
            Assert.AreEqual(30, connected.GetProperty("rows").GetInt32());
            await view.HandleMessageAsync("""{"type":"requestPrimary","columns":100,"rows":30}"""u8.ToArray());
            var primary = await ReadUntilAsync(view, m => m.GetProperty("peer").GetProperty("isPrimary").GetBoolean());
            Assert.AreEqual(PeerId(primary), server.PrimaryPeerId);
        }
        finally
        {
            cancellation.Cancel();
            try { await running.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
    }

    private static string? PeerId(JsonElement metadata) => metadata.GetProperty("peer").GetProperty("id").GetString();
    private static string? PrimaryId(JsonElement metadata) => metadata.GetProperty("peer").GetProperty("primaryId").GetString();

    private static async Task<JsonElement> ReadUntilAsync(Hwt1PresentationAdapter view, Func<JsonElement, bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            var frame = await view.ReadFrameAsync(timeout.Token);
            var length = BinaryPrimitives.ReadInt32LittleEndian(frame.Span[4..]);
            using var metadata = JsonDocument.Parse(frame.Slice(8, length));
            var root = metadata.RootElement;
            await view.HandleMessageAsync(Encoding.UTF8.GetBytes(
                $$"""{"type":"ack","revision":{{root.GetProperty("revision").GetUInt32()}}}"""), timeout.Token);
            if (predicate(root))
                return root.Clone();
        }
    }

    private static async Task WaitForScreenAsync(Hex1bTerminal terminal, Func<Hex1b.Automation.Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, TimeSpan.FromSeconds(5), "ordered remote state applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(Hmp1PresentationAdapter server)
    {
        var (serverStream, clientStream) = CreateStreams();
        var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions { StreamFactory = _ => Task.FromResult(clientStream) });
        var accepted = server.AddClient(serverStream, TestContext.Current.CancellationToken);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        return (await accepted, client);
    }

    private static async Task HandshakeAsync(Stream server, Hmp1WorkloadAdapter client,
        int width, int height, string peerId, string? primaryId, byte[] state)
    {
        var connect = client.ConnectAsync(TestContext.Current.CancellationToken);
        var hello = await Hmp1Protocol.ReadFrameAsync(server, TestContext.Current.CancellationToken);
        Assert.AreEqual(Hmp1FrameType.ClientHello, hello!.Value.Type);
        await Hmp1Protocol.WriteHelloAsync(server, width, height, peerId, primaryId, [], TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteFrameAsync(server, Hmp1FrameType.StateSync, state, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteActivityStateAsync(server, Hmp1ActivityState.Default, TestContext.Current.CancellationToken);
        await connect.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static (Stream Server, Stream Client) CreateStreams()
    {
        var toClient = new Pipe();
        var toServer = new Pipe();
        return (new DuplexStream(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
            new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream()));
    }

    private sealed class DuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => output.WriteAsync(buffer, cancellationToken);
        public override void Flush() => output.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { input.Dispose(); output.Dispose(); }
            base.Dispose(disposing);
        }
    }

    private sealed class FailingHandshakeStream(
        byte[] hello, Hmp1FrameType failureFrame, CancellationTokenSource? cancellation) : Stream
    {
        private readonly MemoryStream _input = new(hello);
        internal bool IsDisposed { get; private set; }
        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => !IsDisposed;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == Hmp1Protocol.HeaderSize && buffer.Span[0] == (byte)failureFrame)
            {
                cancellation?.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("Injected handshake write failure.");
            }
            return ValueTask.CompletedTask;
        }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _input.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class PassThroughPresentationFilter : IHex1bTerminalPresentationFilter
    {
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens.Select(token => token.Token).ToArray());
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private sealed class GatedOutputFilter : IHex1bTerminalWorkloadFilter
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Func<int> Width { get; set; } = () => -1;
        internal List<int> OutputWidths { get; } = [];

        public async ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            OutputWidths.Add(Width());
            if (OutputWidths.Count == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(ct);
            }
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
