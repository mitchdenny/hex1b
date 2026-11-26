using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Kitty keyboard protocol presentation across HMP1. Expectations are literal wire
/// bytes derived from the public protocol specification.
/// </summary>
[TestClass]
public class Hmp1KittyKeyboardTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const string Reset = Hmp1KeyboardPresentation.FullResetReplacement;

    // ---- Scanner and stripper ----

    [TestMethod]
    public void Scanner_SplitAtEveryBoundary_ClassifiesLikeWholeInput()
    {
        const string input = "a\x1b[>5ub\x1b]0;x[u\x07c\x1b_Gq=1;u\x1b\\\x1b[?1049hd\x1b[=3;2ue\u001bcf\x1b[?ug\x1b[31mh\x1b[?25;47l";
        string[] expected =
        [
            "T:a", "K:\x1b[>5u", "T:b\x1b]0;x[u\x07c\x1b_Gq=1;u\x1b\\", "S:\x1b[?1049h", "T:d",
            "K:\x1b[=3;2u", "T:e", "R:\u001bc", "T:f", "K:\x1b[?u", "T:g\x1b[31mh", "S:\x1b[?25;47l",
        ];
        var bytes = Encoding.ASCII.GetBytes(input);
        TestSeq.AreEqual(expected, Scan(bytes, []));
        for (var a = 1; a < bytes.Length; a++)
        {
            TestSeq.AreEqual(expected, Scan(bytes, [a]), $"split at {a}");
            for (var b = a + 1; b < bytes.Length; b++)
                TestSeq.AreEqual(expected, Scan(bytes, [a, b]), $"split at {a},{b}");
        }
    }

    [TestMethod]
    public void Scanner_OverlongCsi_PassesThroughUnclassified()
    {
        var overlong = "\x1b[>" + new string('1', Hmp1KeyboardControlScanner.MaxCsiLength) + "u";
        var events = Scan(Encoding.ASCII.GetBytes(overlong + "\x1b[>2u"), []);
        TestSeq.AreEqual(new[] { "T:" + overlong, "K:\x1b[>2u" }, events);
    }

    [TestMethod]
    public void Stripper_FragmentedControls_RemovesOnlyKeyboardControls()
    {
        const string input = "A\x1b[>1u\x1b]2;x[>1u\x07\x1b[?25hB\x1b[?u\x1b[=7;3u\x1b[<9u\x1b[?1049h\u001bcC\x1b[5u";
        const string expected = "A\x1b]2;x[>1u\x07\x1b[?25hB\x1b[?1049h\u001bcC\x1b[5u";
        Assert.AreEqual(expected, Encoding.ASCII.GetString(
            Hmp1KeyboardControlStripper.StripComplete(Encoding.ASCII.GetBytes(input)).Span));
        var bytes = Encoding.ASCII.GetBytes(input);
        for (var split = 1; split < bytes.Length; split++)
        {
            var stripper = new Hmp1KeyboardControlStripper();
            var output = Encoding.ASCII.GetString(stripper.Process(bytes.AsMemory(0, split)).Span) +
                Encoding.ASCII.GetString(stripper.Process(bytes.AsMemory(split)).Span);
            Assert.AreEqual(expected, output, $"split at {split}");
            Assert.IsFalse(stripper.HasPending);
        }
    }

    // ---- Viewer-side presentation (literal host wire bytes) ----

    [TestMethod]
    public void Presentation_FirstOutput_PushesOwnedEntryAndSetsRemoteFlags()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        Assert.AreEqual("\x1b[>0u\x1b[=5uA", Rewrite(presentation, "\x1b[>5uA"));
        Assert.AreEqual(Hmp1KeyboardOwnership.Owned, presentation.Ownership);
        // The query is never forwarded: the producer answers it.
        Assert.AreEqual("B", Rewrite(presentation, "\x1b[?uB"));
        // Replace, OR, and clear modify the top entry; only the result reaches the host.
        Assert.AreEqual("\x1b[=12u", Rewrite(presentation, "\x1b[=8u\x1b[=4;2u"));
        Assert.AreEqual("\x1b[=8u", Rewrite(presentation, "\x1b[=4;3u"));
        Assert.AreEqual("\x1b[=127u", Rewrite(presentation, "\x1b[>255u"));
        // Popping more entries than exist yields zero flags.
        Assert.AreEqual("\x1b[=0uC", Rewrite(presentation, "\x1b[<99uC"));
        Assert.AreEqual(string.Empty, Rewrite(presentation, "\x1b[>3u\x1b[<u"));
    }

    [TestMethod]
    public void Presentation_ScreenSwitches_MoveOwnedEntryBetweenIndependentStacks()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        Rewrite(presentation, "\x1b[>5uA");
        Assert.AreEqual(
            "\x1b[<u\x1b[?1049h\x1b[>0u\x1b[=2uX\x1b[<u\x1b[?1049l\x1b[>5uY",
            Rewrite(presentation, "\x1b[?1049h\x1b[>2uX\x1b[?1049lY"));
        Assert.AreEqual("\x1b[<u\x1b[?47h\x1b[>2u", Rewrite(presentation, "\x1b[?47h"));
        Assert.IsTrue(presentation.RemoteAlternate);
        Assert.AreEqual(2, presentation.RemoteFlags);
    }

    [TestMethod]
    public void Presentation_UnsupportedHost_StripsControlsAndForwardsReset()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: false);
        Assert.AreEqual("A\x1b[?1049hB\u001bcC", Rewrite(presentation, "\x1b[>9uA\x1b[?1049h\x1b[>2uB\u001bcC\x1b[?u"));
        Assert.AreEqual(Hmp1KeyboardOwnership.None, presentation.Ownership);
    }

    [TestMethod]
    public void Presentation_RemoteReset_PreservesHostEntriesBelowOwnedEntry()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        Rewrite(presentation, "\x1b[>9u\x1b[?1049h\x1b[>2uA");
        Assert.AreEqual("\x1b[<u" + Reset + "\x1b[>0uB", Rewrite(presentation, "\u001bcB"));
        Assert.IsFalse(presentation.RemoteAlternate);
        Assert.AreEqual(0, presentation.RemoteFlags);
        Assert.AreEqual(Hmp1KeyboardOwnership.Owned, presentation.Ownership);
    }

    [TestMethod]
    public async Task Presentation_CancelledBeforeDelivery_RollsBackOwnership()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        var host = new RecordingPresentation();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await presentation.WriteAsync(host, "\x1b[>5uA"u8.ToArray(), stateSync: false, cancelled.Token));
        Assert.AreEqual(Hmp1KeyboardOwnership.None, presentation.Ownership);
        Assert.AreEqual(0, host.Writes.Count);

        await presentation.WriteAsync(host, "B"u8.ToArray(), stateSync: false, CancellationToken.None);
        TestSeq.AreEqual(new[] { "\x1b[>5uB" }, host.Writes);
        await presentation.CompleteAsync(host);
        Assert.AreEqual("\x1b[<u", host.Writes[^1]);
    }

    [TestMethod]
    public async Task Presentation_AmbiguousPartialPushWrite_StopsManagingAndNeverPops()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        var host = new RecordingPresentation { Fail = true };
        await Assert.ThrowsAsync<IOException>(async () =>
            await presentation.WriteAsync(host, "\x1b[>5uA"u8.ToArray(), stateSync: false, CancellationToken.None));
        Assert.AreEqual(Hmp1KeyboardOwnership.Unknown, presentation.Ownership);

        host.Fail = false;
        await presentation.WriteAsync(host, "\x1b[>1uB\x1b[?1049hC"u8.ToArray(), stateSync: false, CancellationToken.None);
        await presentation.CompleteAsync(host);
        TestSeq.AreEqual(new[] { "B\x1b[?1049hC" }, host.Writes);
    }

    [TestMethod]
    public async Task Presentation_FailedWriteWithoutStackChange_KeepsOwnershipAndResendsFlags()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        var host = new RecordingPresentation();
        await presentation.WriteAsync(host, "A"u8.ToArray(), stateSync: false, CancellationToken.None);
        host.Fail = true;
        await Assert.ThrowsAsync<IOException>(async () =>
            await presentation.WriteAsync(host, "\x1b[=7uB"u8.ToArray(), stateSync: false, CancellationToken.None));
        Assert.AreEqual(Hmp1KeyboardOwnership.Owned, presentation.Ownership);

        host.Fail = false;
        await presentation.WriteAsync(host, "C"u8.ToArray(), stateSync: false, CancellationToken.None);
        await presentation.CompleteAsync(host);
        TestSeq.AreEqual(new[] { "\x1b[>0uA", "\x1b[=7uC", "\x1b[<u" }, host.Writes);
    }

    [TestMethod]
    public async Task Presentation_FailedFinalPop_LeavesOwnershipUnknown()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        var host = new RecordingPresentation();
        await presentation.WriteAsync(host, "A"u8.ToArray(), stateSync: false, CancellationToken.None);
        host.Fail = true;
        await Assert.ThrowsAsync<IOException>(async () => await presentation.CompleteAsync(host));
        Assert.AreEqual(Hmp1KeyboardOwnership.Unknown, presentation.Ownership);
    }

    [TestMethod]
    public async Task Presentation_StateSync_DiscardsHeldFragmentAndRemoteModels()
    {
        var presentation = new Hmp1KeyboardPresentation(hostSupportsKeyboard: true);
        var host = new RecordingPresentation();
        await presentation.WriteAsync(host, "\x1b[>5u\x1b[?1049hA\x1b[>"u8.ToArray(), stateSync: false, CancellationToken.None);
        await presentation.WriteAsync(host, "\x1b[?1049l\x1b[<128u\x1b[>3uB"u8.ToArray(), stateSync: true, CancellationToken.None);
        TestSeq.AreEqual(new[]
        {
            "\x1b[>0u\x1b[<u\x1b[?1049h\x1b[>0uA",
            "\x1b[<u\x1b[?1049l\x1b[>0u\x1b[=3uB",
        }, host.Writes);
    }

    // ---- Negotiation and backward compatibility ----

    [TestMethod]
    public async Task Handshake_ModernPeers_NegotiateVersionAndReplayStacks()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>1u\x1b[>3u\x1b[?1049h\x1b[>4u"));
        var streams = CreateStreams();
        await using var wire = streams.Client;
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteClientHelloAsync(wire, null, null, TestContext.Current.CancellationToken, kittyKeyboard: true);
        var frames = await ReadFramesUntilAsync(wire, Hmp1FrameType.StateSync);
        await using var handle = await accepting.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        using var hello = JsonDocument.Parse(frames.First(f => f.Type == Hmp1FrameType.Hello).Payload);
        Assert.AreEqual(1, hello.RootElement.GetProperty("kittyKeyboardVersion").GetInt32());
        var sync = Encoding.UTF8.GetString(frames[^1].Payload.Span);
        StringAssert.StartsWith(sync, "\x1b[?1049l\x1b[<128u\x1b[>1u\x1b[>3u\x1b[?1049h\x1b[<128u\x1b[>4u");
    }

    [TestMethod]
    public async Task Handshake_LegacyClient_ReceivesNoKeyboardControls()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>1u\x1b[?1049h\x1b[>4u"));
        var streams = CreateStreams();
        await using var wire = streams.Client;
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteClientHelloAsync(wire, null, null, TestContext.Current.CancellationToken);
        var frames = await ReadFramesUntilAsync(wire, Hmp1FrameType.StateSync);
        await using var handle = await accepting.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        using var hello = JsonDocument.Parse(frames.First(f => f.Type == Hmp1FrameType.Hello).Payload);
        Assert.IsFalse(hello.RootElement.TryGetProperty("kittyKeyboardVersion", out _));
        workload.Emit("\x1b[>7u\x1b[=3;2u\x1b[?1049lLIVE\x1b[<u");
        var output = new StringBuilder();
        foreach (var frame in await ReadFramesUntilAsync(wire, Hmp1FrameType.Output, "LIVE"))
        {
            if (frame.Type is Hmp1FrameType.StateSync or Hmp1FrameType.Output)
                output.Append(Encoding.UTF8.GetString(frame.Payload.Span));
        }
        output.Insert(0, Encoding.UTF8.GetString(frames[^1].Payload.Span));
        Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(output.ToString(), "\x1b\\[[?=<>][0-9;:]*u"),
            output.ToString());
        StringAssert.Contains(output.ToString(), "\x1b[?1049lLIVE");
    }

    [TestMethod]
    public async Task Handshake_LegacyServer_ViewerStripsUnnegotiatedControls()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        var connecting = client.ConnectAsync(TestContext.Current.CancellationToken);
        var clientHello = await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken);
        Assert.AreEqual(1, Hmp1Protocol.ParseClientHello(clientHello!.Value.Payload).KittyKeyboardVersion);
        await Hmp1Protocol.WriteHelloAsync(wire, 40, 12, "legacy", null, [], TestContext.Current.CancellationToken);
        await WriteSyncAsync(wire, "\x1b[>3uOLD");
        await connecting.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.IsFalse(client.KittyKeyboardNegotiated);

        await using var hostPresentation = new HostPresentation(supportsKeyboard: true);
        await using var host = BuildHost(hostPresentation);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output, "\x1b[>5uLIVE"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await WaitAsync(host, s => s.ContainsText("LIVE"), "legacy output");
        using var viewer = mirror.CreateSnapshot();
        Assert.AreEqual(0, viewer.KittyKeyboardFlags);
        using var hostSnapshot = host.CreateSnapshot();
        Assert.AreEqual(0, hostSnapshot.KittyKeyboardFlags);
    }

    [TestMethod]
    public async Task Handshake_UnsupportedServerVersion_IsRejected()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        var connecting = client.ConnectAsync(TestContext.Current.CancellationToken);
        await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Hello,
            """{"kittyKeyboardVersion":2,"version":1,"width":40,"height":12,"peerId":"future","peers":[]}"""u8.ToArray(),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => connecting.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    // ---- Producer authority ----

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task Output_ModernAndLegacyPeers_ProducerAloneRepliesToQuery(bool alternate, bool filtered)
    {
        await using var workload = new QueryWorkload { EchoDuplicateReplies = true };
        await using var server = new Hmp1PresentationAdapter(40, 12);
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12);
        if (filtered)
            builder.AddPresentationFilter(new PassThroughFilter());
        await using var producer = builder.Build();
        var modern = await ConnectAsync(server);
        await using var modernHandle = modern.Handle;
        await using var modernClient = modern.Client;
        Assert.IsTrue(modernClient.KittyKeyboardNegotiated);
        await using var hostPresentation = new HostPresentation(supportsKeyboard: true);
        await using var host = BuildHost(hostPresentation);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(modernClient)
            .WithPresentation(hostPresentation).Build();

        var legacyStreams = CreateStreams();
        await using var legacyWire = legacyStreams.Client;
        var accepting = server.AddClient(legacyStreams.Server, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteClientHelloAsync(legacyWire, null, null, TestContext.Current.CancellationToken);
        await ReadFramesUntilAsync(legacyWire, Hmp1FrameType.StateSync);
        await using var legacyHandle = await accepting.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        var legacyFrames = ReadFramesUntilAsync(legacyWire, Hmp1FrameType.Output, "DONE");

        var screen = alternate ? "\x1b[?1049h" : string.Empty;
        workload.Emit(screen + "\x1b[>21u\x1b[?uDONE");
        Assert.AreEqual("\x1b[?21u", await workload.ReadInputAsync());
        await WaitAsync(host, s => s.ContainsText("DONE") && s.KittyKeyboardFlags == 21, "modern output");
        var legacyText = string.Concat((await legacyFrames).Select(f => Encoding.UTF8.GetString(f.Payload.Span)));
        Assert.DoesNotContain("\x1b[?u", legacyText);
        Assert.DoesNotContain("\x1b[>21u", legacyText);

        hostPresentation.SendInput("barrier");
        Assert.AreEqual("barrier", await workload.ReadInputAsync());
        Assert.AreEqual(0, hostPresentation.Workload.InputCount, "The physical host must not answer the query.");
        using var producerSnapshot = producer.CreateSnapshot();
        Assert.IsFalse(producerSnapshot.ContainsText("^["), "Only one query reply may reach the producer.");
        using var hostSnapshot = host.CreateSnapshot();
        Assert.AreEqual(alternate, hostSnapshot.InAlternateScreen);
    }

    [TestMethod]
    public async Task Replay_LateJoinOnAlternate_ReproducesBothStacksIncludingOverflowAndExcessPops()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var commands = new StringBuilder();
        for (var i = 0; i < 130; i++)
            commands.Append("\x1b[>").Append(i + 100).Append('u');
        commands.Append("\x1b[=64;2u\x1b[?1049h\x1b[>5u\x1b[<200u\x1b[>6u\x1b[>255u\x1b[=3;3u");
        producer.ApplyTokens(AnsiTokenizer.Tokenize(commands.ToString()));
        using var expected = producer.CreateSnapshot();
        Assert.AreEqual(128, expected.MainKittyKeyboardStack.Length);
        TestSeq.AreEqual(new[] { 6, 124 }, expected.AlternateKittyKeyboardStack);

        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().WithDimensions(40, 12).Build();
        workload.Emit("JOINED");
        await WaitAsync(mirror, s => s.ContainsText("JOINED"), "late join");
        using var actual = mirror.CreateSnapshot();
        Assert.IsTrue(actual.InAlternateScreen);
        Assert.AreEqual(124, actual.KittyKeyboardFlags);
        TestSeq.AreEqual(expected.MainKittyKeyboardStack, actual.MainKittyKeyboardStack);
        TestSeq.AreEqual(expected.AlternateKittyKeyboardStack, actual.AlternateKittyKeyboardStack);
    }

    [TestMethod]
    public async Task Output_LiveStackCommands_ViewerTracksProducerPerScreen()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().WithDimensions(40, 12).Build();

        workload.Emit("\x1b[>1u\x1b[=6;2u\x1b[>200u\x1b[=8;3u\x1b[?1049h\x1b[=9u\x1b[<5u\x1b[>2u\x1b[?1049l\x1b[<uLIVE");
        await WaitAsync(mirror, s => s.ContainsText("LIVE"), "live stack commands");
        using var expected = producer.CreateSnapshot();
        using var actual = mirror.CreateSnapshot();
        TestSeq.AreEqual(new[] { 7 }, expected.MainKittyKeyboardStack);
        TestSeq.AreEqual(new[] { 2 }, expected.AlternateKittyKeyboardStack);
        TestSeq.AreEqual(expected.MainKittyKeyboardStack, actual.MainKittyKeyboardStack);
        TestSeq.AreEqual(expected.AlternateKittyKeyboardStack, actual.AlternateKittyKeyboardStack);
    }

    [TestMethod]
    public async Task Input_RemoteFlags_ViewerEncodesKeysInOrder()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().WithDimensions(40, 12).Build();
        var automator = new Hex1bTerminalAutomator(mirror, Timeout);

        workload.Emit("\x1b[>1uREADY");
        await WaitAsync(mirror, s => s.ContainsText("READY") && s.KittyKeyboardFlags == 1, "flags 1");
        await automator.Ctrl().KeyAsync(Hex1bKey.A, TestContext.Current.CancellationToken);
        await automator.KeyAsync(Hex1bKey.B, TestContext.Current.CancellationToken);
        Assert.AreEqual("\x1b[97;5u", await workload.ReadInputAsync());
        Assert.AreEqual("b", await workload.ReadInputAsync());

        workload.Emit("\x1b[<uPOPPED");
        await WaitAsync(mirror, s => s.ContainsText("POPPED") && s.KittyKeyboardFlags == 0, "flags 0");
        await automator.Ctrl().KeyAsync(Hex1bKey.A, TestContext.Current.CancellationToken);
        Assert.AreEqual("\x01", await workload.ReadInputAsync());
    }

    // ---- Physical host ownership and restoration ----

    [TestMethod]
    public async Task Host_RemoteRisThenDetach_RestoresPreexistingStacks()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        await using var hostPresentation = new HostPresentation(supportsKeyboard: true);
        await using var host = await BuildSeededHostAsync(hostPresentation);
        var connection = await ConnectAsync(server);
        var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();

        workload.Emit("\x1b[>9uMAIN\x1b[?1049h\x1b[>2uALT");
        await WaitAsync(host, s => s.ContainsText("ALT") && s.InAlternateScreen && s.KittyKeyboardFlags == 2, "remote alt");
        AssertStacks(host, [16, 3], [4, 5, 2]);

        // The viewer replaces RIS with a reset that spares keyboard stacks, so no raw RIS
        // reaches the host (the host model would apply RisToken.Instance for one).
        workload.Emit("\u001bcAFTER");
        await WaitAsync(host, s => s.ContainsText("AFTER") && !s.InAlternateScreen, "after remote RIS");
        Assert.AreEqual(0, hostPresentation.RisCount, "A raw RIS must not reach the physical host.");
        AssertStacks(host, [16, 3, 0], [4, 5]);

        await handle.DisposeAsync();
        await WaitAsync(host, s => s.MainKittyKeyboardStack.Length == 2, "owned entry popped");
        AssertStacks(host, [16, 3], [4, 5]);

        // Control: a raw RIS reaching the host model is applied as RisToken.Instance and empties
        // both pre-existing stacks, which is exactly what the replacement above avoids.
        await hostPresentation.WriteOutputAsync("\u001bc"u8.ToArray(), TestContext.Current.CancellationToken);
        Assert.AreEqual(1, hostPresentation.RisCount);
        AssertStacks(host, [], []);
    }

    [TestMethod]
    public async Task Host_ResyncWhileOnAlternate_KeepsSingleOwnedEntry()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        var connecting = client.ConnectAsync(TestContext.Current.CancellationToken);
        await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteHelloAsync(wire, 40, 12, "modern", null, [], TestContext.Current.CancellationToken,
            kittyKeyboard: true);
        await WriteSyncAsync(wire, "\x1b[?1049l\x1b[<128u\x1b[>1u\x1b[?1049h\x1b[<128u\x1b[>6uFIRST");
        await connecting.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.IsTrue(client.KittyKeyboardNegotiated);

        await using var hostPresentation = new HostPresentation(supportsKeyboard: true);
        await using var host = await BuildSeededHostAsync(hostPresentation);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output, "\x1b[>2uLIVE"u8.ToArray(),
            TestContext.Current.CancellationToken);
        await WaitAsync(host, s => s.ContainsText("LIVE") && s.InAlternateScreen && s.KittyKeyboardFlags == 2, "live on alt");

        await WriteSyncAsync(wire, "\x1b[?1049l\x1b[<128u\x1b[>3u\x1b[?1049h\x1b[<128u\x1b[>7uSECOND");
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output, "!"u8.ToArray(), TestContext.Current.CancellationToken);
        await WaitAsync(host, s => s.ContainsText("SECOND!") && s.KittyKeyboardFlags == 7, "resynced on alt");
        AssertStacks(host, [16, 3], [4, 5, 7]);
        using (var viewer = mirror.CreateSnapshot())
        {
            TestSeq.AreEqual(new[] { 3 }, viewer.MainKittyKeyboardStack);
            TestSeq.AreEqual(new[] { 7 }, viewer.AlternateKittyKeyboardStack);
        }

        await wire.DisposeAsync();
        await WaitAsync(host, s => s.AlternateKittyKeyboardStack.Length == 2, "owned entry popped");
        AssertStacks(host, [16, 3], [4, 5]);
    }

    [TestMethod]
    public async Task Host_Unsupported_ReceivesNoKeyboardControls()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        await using var hostPresentation = new HostPresentation(supportsKeyboard: false);
        await using var host = await BuildSeededHostAsync(hostPresentation);
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();

        workload.Emit("\x1b[>9u\x1b[?1049h\x1b[>2u\x1b[?uALT");
        await WaitAsync(host, s => s.ContainsText("ALT") && s.InAlternateScreen, "remote alt");
        AssertStacks(host, [16, 3], [4, 5]);
        Assert.AreEqual("\x1b[?2u", await workload.ReadInputAsync());
        Assert.AreEqual(0, hostPresentation.Workload.InputCount);
    }

    [TestMethod]
    public async Task Host_ViewerDisposed_PopsOwnedEntry()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        await using var hostPresentation = new HostPresentation(supportsKeyboard: true);
        await using var host = await BuildSeededHostAsync(hostPresentation);
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();

        workload.Emit("\x1b[>11uHERE");
        await WaitAsync(host, s => s.ContainsText("HERE") && s.KittyKeyboardFlags == 11, "remote flags");
        AssertStacks(host, [16, 3, 11], [4, 5]);
        await mirror.DisposeAsync();
        await WaitAsync(host, s => s.MainKittyKeyboardStack.Length == 2, "owned entry popped");
        AssertStacks(host, [16, 3], [4, 5]);
    }

    // ---- Raw-mode entry ordering ----

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Replay_HostProbePending_WaitsForEntryThenPushesOwnedEntry(bool filtered)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        var connecting = client.ConnectAsync(TestContext.Current.CancellationToken);
        await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteHelloAsync(wire, 40, 12, "modern", null, [], TestContext.Current.CancellationToken,
            kittyKeyboard: true);
        await WriteSyncAsync(wire, "\x1b[>1uSYNC");
        await connecting.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hostPresentation = new HostPresentation(supportsKeyboard: false)
        {
            EntryGate = gate,
            SupportsKeyboardAfterEntry = true,
        };
        await using var host = await BuildSeededHostAsync(hostPresentation);
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation);
        if (filtered)
            builder.AddPresentationFilter(new PassThroughFilter());
        await using var mirror = builder.Build();

        await AssertBlockedOnEntryAsync(mirror, hostPresentation);
        Assert.IsFalse(client.InitialReplay.IsCompleted, "Replay must not complete before raw-mode entry.");
        gate.SetResult();
        await WaitAsync(host, s => s.ContainsText("SYNC") && s.KittyKeyboardFlags == 1, "replay after entry");
        Assert.IsFalse(hostPresentation.WroteBeforeEntry);
        AssertStacks(host, [16, 3, 1], [4, 5]);
    }

    [TestMethod]
    public async Task Output_HostProbePending_WaitsForEntryThenPushesOwnedEntry()
    {
        // A placeholder stands in before a remote connection, so its live output takes the
        // HMP1 presentation path without a preceding StateSync replay.
        var primary = new UnconnectedWorkload();
        var placeholder = new QueryWorkload();
#pragma warning disable HEX1B002 // Placeholder workload API is experimental.
        var workload = new PlaceholderWorkloadAdapter(primary, placeholder, PlaceholderResumePolicy.OnDisconnect);
#pragma warning restore HEX1B002
        placeholder.Emit("\x1b[>1uLIVE");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hostPresentation = new HostPresentation(supportsKeyboard: false)
        {
            EntryGate = gate,
            SupportsKeyboardAfterEntry = true,
        };
        await using var host = await BuildSeededHostAsync(hostPresentation);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithPresentation(hostPresentation).Build();

        await AssertBlockedOnEntryAsync(mirror, hostPresentation);
        gate.SetResult();
        await WaitAsync(host, s => s.ContainsText("LIVE") && s.KittyKeyboardFlags == 1, "live output after entry");
        Assert.IsFalse(hostPresentation.WroteBeforeEntry);
        AssertStacks(host, [16, 3, 1], [4, 5]);
    }

    // ---- Restoration failures ----

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Lifecycle_FinalPopFails_DisposeSurfacesErrorAndStillExitsRawMode(bool async)
    {
        var (server, producer, workload, handle, client, hostPresentation, host) = await AttachFailingHostAsync();
        await using var serverScope = server;
        await using var producerScope = producer;
        await using var handleScope = handle;
        await using var clientScope = client;
        await using var hostScope = host;
        var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();
        workload.Emit("\x1b[>11uHERE");
        await WaitAsync(host, s => s.ContainsText("HERE") && s.KittyKeyboardFlags == 11, "remote flags");
        var popAttempts = FailPops(hostPresentation);

        var error = async
            ? await Assert.ThrowsAsync<IOException>(async () => await mirror.DisposeAsync())
            : Assert.Throws<IOException>(mirror.Dispose);
        Assert.Contains("HMP1", error.Message);
        Assert.AreEqual(1, popAttempts());
        Assert.AreEqual(1, hostPresentation.ExitRawModeCount, "Raw mode must be restored after a failed pop.");
        Assert.IsTrue(hostPresentation.IsDisposed);
        AssertStacks(host, [16, 3, 11], [4, 5]);
    }

    [TestMethod]
    public async Task Lifecycle_FinalPopFails_RunAsyncSurfacesErrorAndStillExitsRawMode()
    {
        var (server, producer, workload, handle, client, hostPresentation, host) = await AttachFailingHostAsync();
        await using var serverScope = server;
        await using var producerScope = producer;
        await using var handleScope = handle;
        await using var clientScope = client;
        await using var hostScope = host;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();
        workload.Emit("\x1b[>11uHERE");
        await WaitAsync(host, s => s.ContainsText("HERE") && s.KittyKeyboardFlags == 11, "remote flags");
        var popAttempts = FailPops(hostPresentation);

        var error = await Assert.ThrowsAsync<AggregateException>(() => mirror.RunAsync(new CancellationToken(canceled: true)));
        Assert.IsInstanceOfType<OperationCanceledException>(error.InnerExceptions[0]);
        Assert.IsInstanceOfType<IOException>(error.InnerExceptions[1]);
        Assert.AreEqual(1, popAttempts());
        Assert.AreEqual(1, hostPresentation.ExitRawModeCount);
        await mirror.DisposeAsync();
        Assert.AreEqual(1, popAttempts(), "A failed pop leaves ownership unknown and is never retried.");
    }

    [TestMethod]
    public async Task Lifecycle_DisconnectPopFails_DefersErrorToDisposeWithoutRetry()
    {
        var (server, producer, workload, handle, client, hostPresentation, host) = await AttachFailingHostAsync();
        await using var serverScope = server;
        await using var producerScope = producer;
        await using var clientScope = client;
        await using var hostScope = host;
        var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(hostPresentation).Build();
        workload.Emit("\x1b[>11uHERE");
        await WaitAsync(host, s => s.ContainsText("HERE") && s.KittyKeyboardFlags == 11, "remote flags");
        var popAttempts = FailPops(hostPresentation);

        // Best effort on disconnect: the failure neither faults the output pump nor is lost.
        await handle.DisposeAsync();
        await WaitAsync(mirror, s => s.ContainsText("HERE") && popAttempts() == 1, "disconnect pop attempted");
        Assert.AreEqual(0, hostPresentation.ExitRawModeCount);

        var error = await Assert.ThrowsAsync<IOException>(async () => await mirror.DisposeAsync());
        Assert.Contains("HMP1", error.Message);
        Assert.AreEqual(1, popAttempts(), "A failed pop leaves ownership unknown and is never retried.");
        Assert.AreEqual(1, hostPresentation.ExitRawModeCount);
        AssertStacks(host, [16, 3, 11], [4, 5]);
    }

    // ---- Helpers ----

    private static async Task<(Hmp1PresentationAdapter Server, Hex1bTerminal Producer, QueryWorkload Workload,
        Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client, HostPresentation HostPresentation, Hex1bTerminal Host)>
        AttachFailingHostAsync()
    {
        var workload = new QueryWorkload();
        var server = new Hmp1PresentationAdapter(40, 12);
        var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var hostPresentation = new HostPresentation(supportsKeyboard: true);
        var host = await BuildSeededHostAsync(hostPresentation);
        var connection = await ConnectAsync(server);
        return (server, producer, workload, connection.Handle, connection.Client, hostPresentation, host);
    }

    // Makes every later pop write fail before delivery and counts the attempts.
    private static Func<int> FailPops(HostPresentation presentation)
    {
        var attempts = 0;
        presentation.FailWrite = text =>
        {
            if (!text.Contains("\x1b[<u", StringComparison.Ordinal))
                return false;
            Interlocked.Increment(ref attempts);
            return true;
        };
        return () => Volatile.Read(ref attempts);
    }

    // The output pump holds the HMP1 output-state lock while it waits for raw-mode entry.
    private static async Task AssertBlockedOnEntryAsync(Hex1bTerminal terminal, HostPresentation presentation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        while (terminal.Hmp1OutputStateLock.CurrentCount != 0)
            await Task.Delay(10, timeout.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.AreEqual(0, terminal.Hmp1OutputStateLock.CurrentCount, "The output pump must still be waiting for entry.");
        Assert.IsFalse(presentation.WroteBeforeEntry, "No remote byte may reach the host before raw-mode entry.");
    }

    private static string Rewrite(Hmp1KeyboardPresentation presentation, string input) =>
        Encoding.ASCII.GetString(presentation.Rewrite(Encoding.ASCII.GetBytes(input)).Span);

    private static List<string> Scan(byte[] bytes, int[] splits)
    {
        var scanner = new Hmp1KeyboardControlScanner();
        var sink = new RecordingSink();
        var start = 0;
        foreach (var split in splits.Append(bytes.Length))
        {
            scanner.Process(bytes.AsSpan(start, split - start), sink);
            start = split;
        }
        Assert.IsFalse(scanner.HasPending);
        return sink.Events;
    }

    private static void AssertStacks(Hex1bTerminal host, int[] main, int[] alternate)
    {
        using var snapshot = host.CreateSnapshot();
        TestSeq.AreEqual(main, snapshot.MainKittyKeyboardStack, "host main stack");
        TestSeq.AreEqual(alternate, snapshot.AlternateKittyKeyboardStack, "host alternate stack");
    }

    private static Hex1bTerminal BuildHost(HostPresentation presentation)
    {
        var host = Hex1bTerminal.CreateBuilder().WithWorkload(presentation.Workload)
            .WithHeadless(presentation.Capabilities with { SupportsKittyKeyboard = true }).WithDimensions(40, 12).Build();
        presentation.Host = host;
        return host;
    }

    // The physical host's own program pushed entries on both screens before the viewer attached.
    private static async Task<Hex1bTerminal> BuildSeededHostAsync(HostPresentation presentation)
    {
        var host = BuildHost(presentation);
        presentation.Workload.Emit("\x1b[>16u\x1b[>3u\x1b[?1049h\x1b[>4u\x1b[>5u\x1b[?1049l");
        await WaitAsync(host, s => s.MainKittyKeyboardStack.Length == 2 && s.AlternateKittyKeyboardStack.Length == 2 &&
            !s.InAlternateScreen, "seeded host stacks");
        return host;
    }

    private static async Task WaitAsync(Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate, string description)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, description)
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task WriteSyncAsync(Stream stream, string ansi)
    {
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync,
            Encoding.UTF8.GetBytes(ansi), TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteActivityStateAsync(stream, new Hmp1ActivityState
        {
            Progress = new() { State = 0, Percentage = null },
            ShellIntegration = new() { Phase = 0, LastExitCode = null },
            WorkingDirectory = new() { Uri = null }
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<List<Hmp1Frame>> ReadFramesUntilAsync(Stream stream, Hmp1FrameType type, string? marker = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        var frames = new List<Hmp1Frame>();
        while (true)
        {
            var frame = await Hmp1Protocol.ReadFrameAsync(stream, timeout.Token)
                ?? throw new EndOfStreamException();
            frames.Add(frame with { Payload = frame.Payload.ToArray() });
            if (frame.Type == type &&
                (marker is null || Encoding.UTF8.GetString(frame.Payload.Span).Contains(marker, StringComparison.Ordinal)))
                return frames;
        }
    }

    private static (Stream Server, Stream Client) CreateStreams()
    {
        var toClient = new Pipe();
        var toServer = new Pipe();
        return (new DuplexStream(toServer.Reader.AsStream(), toClient.Writer.AsStream()),
            new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream()));
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(Hmp1PresentationAdapter server)
    {
        var streams = CreateStreams();
        var client = Hmp1TestHelpers.NewClient(streams.Client);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await client.ConnectAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout);
        return (await accepting.WaitAsync(Timeout), client);
    }

    private sealed class RecordingSink : IHmp1KeyboardControlSink
    {
        internal List<string> Events { get; } = [];
        public void Text(ReadOnlySpan<byte> bytes)
        {
            var text = Encoding.ASCII.GetString(bytes);
            if (Events.Count > 0 && Events[^1].StartsWith("T:", StringComparison.Ordinal))
                Events[^1] += text;
            else
                Events.Add("T:" + text);
        }
        public void Keyboard(ReadOnlySpan<byte> sequence) => Events.Add("K:" + Encoding.ASCII.GetString(sequence));
        public void ScreenMode(ReadOnlySpan<byte> sequence) => Events.Add("S:" + Encoding.ASCII.GetString(sequence));
        public void FullReset(ReadOnlySpan<byte> sequence) => Events.Add("R:" + Encoding.ASCII.GetString(sequence));
    }

    private sealed class RecordingPresentation : IHex1bTerminalPresentationAdapter
    {
        internal List<string> Writes { get; } = [];
        internal bool Fail { get; set; }
        public int Width => 40;
        public int Height => 12;
        public TerminalCapabilities Capabilities => new() { SupportsKittyKeyboard = true };
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            if (Fail)
                throw new IOException("host write failed after an unknown prefix");
            Writes.Add(Encoding.ASCII.GetString(data.Span));
            return ValueTask.CompletedTask;
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // A primary that never connects keeps a placeholder active.
    private sealed class UnconnectedWorkload : IHex1bTerminalWorkloadAdapter, IConnectableWorkloadAdapter
    {
        private readonly TaskCompletionSource _never = new();
        public Task ConnectedTask => _never.Task;
        public Task DisconnectedTask => _never.Task;
        public bool IsConnected => false;
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, ct);
            return default;
        }
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class QueryWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly Channel<string> _input = Channel.CreateUnbounded<string>();
        private int _inputCount;
        private int _protocolReplyCount;
        internal int InputCount => Volatile.Read(ref _inputCount);
        internal bool EchoDuplicateReplies { get; init; }
        internal Action<ReadOnlyMemory<byte>>? InputReceived { get; init; }
        public event Action? Disconnected { add { } remove { } }
        public void Emit(string text) => _output.Writer.TryWrite(Encoding.UTF8.GetBytes(text));
        public void Emit(ReadOnlyMemory<byte> bytes) => _output.Writer.TryWrite(bytes.ToArray());
        public async Task<string> ReadInputAsync() => await _input.Reader.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(Timeout);
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) =>
            await _output.Reader.ReadAsync(ct);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _inputCount);
            var text = Encoding.UTF8.GetString(data.Span);
            // A second protocol reply means a viewer or host also answered; make it visible.
            if (EchoDuplicateReplies && text.StartsWith('\x1b') && Interlocked.Increment(ref _protocolReplyCount) > 1)
                Emit(text.Replace("\x1b", "^[", StringComparison.Ordinal));
            _input.Writer.TryWrite(text);
            InputReceived?.Invoke(data);
            return ValueTask.CompletedTask;
        }
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            _output.Writer.TryComplete();
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A physical host terminal: bytes written to the presentation are applied to a separate
    /// Hex1b terminal whose snapshot exposes the host's keyboard stacks. A full reset is
    /// applied as an explicit <see cref="RisToken"/>, so the model empties both stacks
    /// exactly as a real host does whenever a raw RIS reaches it.
    /// </summary>
    private sealed class HostPresentation(bool supportsKeyboard) : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private volatile bool _supportsKeyboard = supportsKeyboard;
        private volatile bool _entered;
        private int _risCount;
        internal QueryWorkload Workload { get; } = new();
        internal Hex1bTerminal? Host { get; set; }
        internal int RisCount => Volatile.Read(ref _risCount);
        // Gates raw-mode entry like a console capability probe; support is known only after it.
        internal TaskCompletionSource? EntryGate { get; init; }
        internal bool SupportsKeyboardAfterEntry { get; init; } = supportsKeyboard;
        internal bool WroteBeforeEntry { get; private set; }
        internal Func<string, bool>? FailWrite { get; set; }
        internal int ExitRawModeCount { get; private set; }
        internal bool IsDisposed { get; private set; }
        internal void SendInput(string text) => _input.Writer.TryWrite(Encoding.UTF8.GetBytes(text));
        public int Width => 40;
        public int Height => 12;
        public TerminalCapabilities Capabilities => new() { SupportsKittyKeyboard = _supportsKeyboard };
        public bool AnswersProtocolQueriesDirectly => true;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var text = Encoding.UTF8.GetString(data.Span);
            if (EntryGate is not null && !_entered)
                WroteBeforeEntry = true;
            if (FailWrite?.Invoke(text) == true)
                throw new IOException("host write failed");
            var host = Host ?? throw new InvalidOperationException("Host terminal not attached.");
            var start = 0;
            int index;
            while ((index = text.IndexOf("\u001bc", start, StringComparison.Ordinal)) >= 0)
            {
                host.ApplyTokens(AnsiTokenizer.Tokenize(text[start..index]));
                host.ApplyTokens([RisToken.Instance]);
                Interlocked.Increment(ref _risCount);
                start = index + 2;
            }
            host.ApplyTokens(AnsiTokenizer.Tokenize(text[start..]));
            return ValueTask.CompletedTask;
        }
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) =>
            await _input.Reader.ReadAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public async ValueTask EnterRawModeAsync(CancellationToken ct = default)
        {
            if (EntryGate is not null)
                await EntryGate.Task.WaitAsync(ct);
            _supportsKeyboard = SupportsKeyboardAfterEntry;
            _entered = true;
        }
        public ValueTask ExitRawModeAsync(CancellationToken ct = default)
        {
            ExitRawModeCount++;
            return ValueTask.CompletedTask;
        }
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PassThroughFilter : IHex1bTerminalPresentationFilter
    {
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.FromResult<IReadOnlyList<AnsiToken>>(appliedTokens.Select(token => token.Token).ToArray());
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class DuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            input.ReadAsync(buffer, cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            output.WriteAsync(buffer, cancellationToken);
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
}
