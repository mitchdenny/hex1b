using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1ProgramStatusTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task StateSync_LateAttachAndReconnect_PreservesHierarchyAndEvictionOrder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;state=working:app=agent\a" +
            "\x1b]7501;state=done:id=job/old\a" +
            "\x1b]7501;state=blocked:id=job/new:kind=permission:progress=42:title=UmV2aWV3\a" +
            "\x1b]7501;state=working:app=agent\aready"));
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var connection = await ConnectAsync(server);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("ready") && snapshot.ProgramStatus.Records.Count == 3);
            TestSeq.AreEqual(producer.ProgramStatus.Records, mirror.ProgramStatus.Records);
            Assert.AreEqual("agent", mirror.ProgramStatus.Find("job/new")!.EffectiveApp);
            Assert.AreEqual("Review", mirror.ProgramStatus.Find("job/new")!.Title);
            TestSeq.AreEqual(new[] { "job/old", "job/new", "" }, mirror.ProgramStatus.Records.Select(record => record.Id));
            // Restoring the inventory must not reorder it: the next insertion evicts
            // the same oldest record at both the producer and the replica.
            producer.RestoreProgramStatus(new(Enumerable.Range(0, 256).Select(i =>
                new TerminalProgramStatusRecord("job" + i, TerminalProgramStatusState.Done))));
            await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Records.Count == 256);
            workload.Write("\x1b]7501;state=done:id=overflow\a");
            await WaitAsync(producer, snapshot => snapshot.ProgramStatus.Find("overflow") is not null);
            await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Find("overflow") is not null);
            TestSeq.AreEqual(producer.ProgramStatus.Records, mirror.ProgramStatus.Records);
            Assert.IsNull(mirror.ProgramStatus.Find("job0"));
            if (attempt == 0)
                producer.ApplyTokens(AnsiTokenizer.Tokenize(
                    "\x1b" + "c\x1b]7501;state=working:app=agent\a" +
                    "\x1b]7501;state=done:id=job/old\a" +
                    "\x1b]7501;state=blocked:id=job/new:kind=permission:progress=42:title=UmV2aWV3\a" +
                    "\x1b]7501;state=working:app=agent\aready"));
        }
    }

    [TestMethod]
    public async Task Live_TwoHops_PromptResetAndByteFreeLifecyclePropagateWithoutDuplicateEvents()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient).WithPresentation(relay).Build();
        var downstream = await ConnectAsync(relay);
        await using var downstreamHandle = downstream.Handle;
        await using var downstreamClient = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(downstreamClient).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;

        workload.Write("\x1b]7501;state=working:id=job:progress=25\alive");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("live") && snapshot.ProgramStatus.Find("job")?.Progress == 25);
        Assert.AreEqual(1, changes.Count);
        workload.Write("\x1b]7501;state=done:id=finished\a\x1b]133;A\aprompt");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("prompt") &&
            snapshot.ProgramStatus.Find("job") is null && snapshot.ProgramStatus.Find("finished") is not null);
        Assert.AreEqual(3, changes.Count);

        // Direct terminal lifecycle changes produce no ANSI bytes to relay.
        producer.RestoreProgramStatus(new([
            new TerminalProgramStatusRecord("finished", TerminalProgramStatusState.Done),
            new TerminalProgramStatusRecord("wait", TerminalProgramStatusState.Blocked, TerminalProgramStatusKind.Auth)
        ]));
        await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Find("wait")?.Kind == TerminalProgramStatusKind.Auth);
        Assert.AreEqual(4, changes.Count);
        producer.RestoreProgramStatus(new([new TerminalProgramStatusRecord("finished", TerminalProgramStatusState.Done)]));
        await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Records.Count == 1);
        Assert.AreEqual(5, changes.Count);
        // RIS applied without a workload write proves empty checkpoints really clear.
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b" + "c"));
        await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Records.Count == 0);
        Assert.AreEqual(6, changes.Count);
        TestSeq.AreEqual(producer.ProgramStatus.Records, replica.ProgramStatus.Records);
    }

    [TestMethod]
    public async Task StateSync_CheckpointPaused_CommitsOnlyAuthoritativeInventoryAfterAnsiReplay()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, client, true,
            """{"records":[{"id":"keep","state":3}]}""");
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        await LiteralFrameAsync(wire, 0x02, "\x1b" + "c\x1b]7501;state=working:id=bogus\areplacement");
        await LiteralFrameAsync(wire, 0x0D, ActivityJson);
        using (var pending = mirror.CreateSnapshot())
        {
            Assert.IsTrue(pending.ContainsText("initial"));
            Assert.IsNotNull(pending.ProgramStatus.Find("keep"));
            Assert.IsFalse(pending.ContainsText("replacement"));
        }
        await LiteralFrameAsync(wire, 0x11, """{"records":[{"id":"keep","state":3}]}""");
        await LiteralFrameAsync(wire, 0x03, "after");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("after") && snapshot.ContainsText("replacement"));
        Assert.IsNotNull(mirror.ProgramStatus.Find("keep"));
        Assert.IsNull(mirror.ProgramStatus.Find("bogus"));
        Assert.IsTrue(changes.IsEmpty);
        await LiteralFrameAsync(wire, 0x02, "\x1b[2J\x1b[Hempty");
        await LiteralFrameAsync(wire, 0x0D, ActivityJson);
        await LiteralFrameAsync(wire, 0x11, """{"records":[]}""");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("empty") && snapshot.ProgramStatus.Records.Count == 0);
        Assert.AreEqual(1, changes.Count);
    }

    [TestMethod]
    public async Task Disconnect_EofRetainsReportsAndDoesNotSynthesizeWorkloadExit()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, client, true, """{"records":[{"id":"job","state":1}]}""");
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        await wire.DisposeAsync();
        await client.DisconnectedTask.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Find("job")?.State == TerminalProgramStatusState.Working);
        Assert.IsTrue(changes.IsEmpty);
    }

    [TestMethod]
    public async Task WorkloadExit_RealProcess_ExpiresActiveReportsBeforeExitFrame()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("The controlled process fixture uses /bin/sh.");
        using var timeout = new CancellationTokenSource(Timeout);
        var start = new ProcessStartInfo("/bin/sh");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(
            "printf '\\033]7501;state=working:id=job\\007\\033]7501;state=blocked:id=wait:kind=question\\007" +
            "\\033]7501;state=done:id=finished\\007\\033]7501;state=idle:id=idle\\007ready\\n'; read line");
        await using var workload = new StandardProcessWorkloadAdapter(start);
        await workload.StartAsync(timeout.Token);
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload, PresentationAdapter = server, Width = 20, Height = 5,
            RunCallback = ct => workload.WaitForExitAsync(ct)
        });
        var running = producer.RunAsync(timeout.Token);
        await WaitAsync(producer, snapshot => snapshot.ContainsText("ready") && snapshot.ProgramStatus.Records.Count == 4);
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(timeout.Token);
        Assert.AreEqual(4, mirror.ProgramStatus.Records.Count);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        await workload.WriteInputAsync("\n"u8.ToArray(), timeout.Token);
        Assert.AreEqual(0, await running.WaitAsync(timeout.Token));
        await client.DisconnectedTask.WaitAsync(timeout.Token);
        await WaitAsync(mirror, snapshot => snapshot.ProgramStatus.Records.Count == 2);
        TestSeq.AreEqual(new[] { "finished", "idle" }, mirror.ProgramStatus.Records.Select(record => record.Id));
        Assert.AreEqual(1, changes.Count);
    }

    [TestMethod]
    public async Task Live_FragmentedReportAcrossLateRelayAttach_ParsesExactlyOnce()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient).WithPresentation(relay).Build();
        workload.Write("ready\x1b]7501;state=blocked:id=tool");
        await WaitAsync(replica, snapshot => snapshot.ContainsText("ready"));
        var downstream = await ConnectAsync(relay);
        await using var downstreamHandle = downstream.Handle;
        await using var downstreamClient = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(downstreamClient).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        workload.Write(":kind=permission\x1b\\done");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("done") &&
            snapshot.ProgramStatus.Find("tool")?.Kind == TerminalProgramStatusKind.Permission);
        Assert.AreEqual(1, changes.Count);
        TestSeq.AreEqual(producer.ProgramStatus.Records, mirror.ProgramStatus.Records);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StateSync_LateAttachDuringOversizedReportDiscard_RelaysBoundedContinuation(bool pendingEscape)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient).WithPresentation(relay).Build();
        workload.Write("ready\x1b]7501;state=working:msg=" + new string('A', 5000) +
            (pendingEscape ? "\x1b" : ""));
        await WaitAsync(replica, snapshot => snapshot.ContainsText("ready"));
        var downstream = await ConnectAsync(relay);
        await using var downstreamHandle = downstream.Handle;
        await using var downstreamClient = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(downstreamClient).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        workload.Write(pendingEscape ? "\\after" : "TAIL\aafter");
        await WaitAsync(producer, snapshot => snapshot.ContainsText("after"));
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("after"));
        using var expected = producer.CreateSnapshot();
        using var actual = mirror.CreateSnapshot();
        for (var row = 0; row < expected.Height; row++)
            Assert.AreEqual(expected.GetLine(row), actual.GetLine(row));
        Assert.IsFalse(actual.ContainsText("TAIL"));
        Assert.AreEqual(0, actual.ProgramStatus.Records.Count);
        Assert.IsTrue(changes.IsEmpty);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StateSync_ResyncDuringOversizedReportDiscard_ReplacesParserStateAcrossRelay(bool pendingEscape)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var upstream = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, upstream, true, """{"records":[]}""");
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstream).WithPresentation(relay).Build();
        var downstream = await ConnectAsync(relay);
        await using var handle = downstream.Handle;
        await using var client = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        await LiteralFrameAsync(wire, 0x03, "ready\x1b]7501;state=working:msg=" + new string('A', 5000) +
            (pendingEscape ? "\x1b" : ""));
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("ready"));
        var changes = new ConcurrentQueue<TerminalProgramStatusSnapshot>();
        mirror.ProgramStatusChanged += changes.Enqueue;
        // A fresh baseline supersedes the abandoned OSC. No terminator from the
        // old stream is necessary, even when its last byte was ESC.
        await LiteralFrameAsync(wire, 0x02, "new");
        await LiteralFrameAsync(wire, 0x0D, ActivityJson);
        await LiteralFrameAsync(wire, 0x11, """{"records":[{"id":"restored","state":3}]}""");
        await LiteralFrameAsync(wire, 0x03, "\x1b]7501;state=working:id=next\aafter");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("new") && snapshot.ContainsText("after") &&
            snapshot.ProgramStatus.Find("restored") is not null && snapshot.ProgramStatus.Find("next") is not null);
        TestSeq.AreEqual(replica.ProgramStatus.Records, mirror.ProgramStatus.Records);
        Assert.AreEqual(2, changes.Count);
    }

    [TestMethod]
    public async Task Connect_LiteralLegacyServer_NoProgramCheckpointRequiredAndLiveAnsiStillWorks()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, client, false, "");
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await LiteralFrameAsync(wire, 0x03, "\x1b]7501;state=working:id=legacy\alive");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("live") && snapshot.ProgramStatus.Find("legacy") is not null);
        Assert.IsTrue(client.IsConnected);
    }

    [TestMethod]
    public async Task AddClient_LiteralLegacyClient_DoesNotReceiveProgramCheckpoint()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7501;state=working\a"));
        var streams = CreateStreams();
        await using var wire = streams.Client;
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await LiteralFrameAsync(wire, 0x0B, """{"displayName":"legacy","defaultRole":null}""");
        Assert.AreEqual(0x01, (await ReadLiteralFrameAsync(wire, timeout.Token)).Type);
        Assert.AreEqual(0x02, (await ReadLiteralFrameAsync(wire, timeout.Token)).Type);
        Assert.AreEqual(0x0D, (await ReadLiteralFrameAsync(wire, timeout.Token)).Type);
        await using var handle = await accepting.WaitAsync(timeout.Token);
        workload.Write("sentinel");
        var frame = await ReadLiteralFrameAsync(wire, timeout.Token);
        Assert.AreEqual(0x03, frame.Type);
        Assert.AreEqual("sentinel", Encoding.UTF8.GetString(frame.Bytes));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{}")]
    [DataRow("""{"records":null}""")]
    [DataRow("""{"records":[null]}""")]
    [DataRow("""{"records":[{"state":1}]}""")]
    [DataRow("""{"records":[{"id":"x"}]}""")]
    [DataRow("""{"records":[{"id":"x","state":99}]}""")]
    [DataRow("""{"records":[{"id":"x","state":1,"progress":101}]}""")]
    [DataRow("""{"records":[{"id":"x","state":3,"kind":0}]}""")]
    [DataRow("""{"records":[{"id":"x","state":1,"id":"y"}]}""")]
    [DataRow("""{"records":[{"id":"x","state":1,"extra":true}]}""")]
    [DataRow("""{"records":[{"id":"x","state":1},{"id":"x","state":3}]}""")]
    [DataRow("""{"records":[],"records":[]}""")]
    public async Task Connect_MalformedNegotiatedCheckpoint_RejectsWithoutPartialReplay(string payload)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ScriptHandshakeAsync(wire, client, true, payload));
        Assert.IsFalse(client.IsConnected);
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("initial"));
    }

    [TestMethod]
    public async Task ReadFrame_OversizedProgramCheckpoint_RejectsBeforePayloadAllocation()
    {
        var header = new byte[5];
        header[0] = 0x11;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), Hmp1ProgramStatusState.MaxPayloadSize + 1);
        using var wire = new MemoryStream(header);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await Hmp1Protocol.ReadFrameAsync(wire));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Live_InvalidOrUnnegotiatedCheckpoint_DisconnectsWithoutReplacingInventory(bool negotiated)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, client, negotiated, """{"records":[{"id":"keep","state":3}]}""");
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        var previous = mirror.ProgramStatus;
        await LiteralFrameAsync(wire, 0x11, negotiated
            ? """{"records":[{"id":"bad","state":99}]}"""
            : """{"records":[{"id":"bad","state":1}]}""");
        await client.DisconnectedTask.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        TestSeq.AreEqual(previous.Records, mirror.ProgramStatus.Records);
    }

    [TestMethod]
    public async Task StateSync_MalformedLaterCheckpoint_DoesNotInstallScreenOrInventory()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await ScriptHandshakeAsync(wire, client, true, """{"records":[{"id":"keep","state":3}]}""");
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();
        await mirror.WaitForHmp1InitialReplayAsync(TestContext.Current.CancellationToken);
        await LiteralFrameAsync(wire, 0x02, "\x1b[2J\x1b[Hreplacement");
        await LiteralFrameAsync(wire, 0x0D, ActivityJson);
        await LiteralFrameAsync(wire, 0x11, "{}");
        await client.DisconnectedTask.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("initial"));
        Assert.IsFalse(snapshot.ContainsText("replacement"));
        Assert.IsNotNull(snapshot.ProgramStatus.Find("keep"));
    }

    [TestMethod]
    public void Parse_RecordAndTextBounds_RejectsOverLimitAndAcceptsLargestInventory()
    {
        var records = Enumerable.Range(0, 256).Select(i => new TerminalProgramStatusRecord(
            "job" + i, TerminalProgramStatusState.Blocked, TerminalProgramStatusKind.Question,
            100, "agent", new string('t', 192), new string('m', 2048))).ToArray();
        var expected = new TerminalProgramStatusSnapshot(records);
        var bytes = Hmp1ProgramStatusState.Encode(expected);
        Assert.IsTrue(bytes.Length <= Hmp1ProgramStatusState.MaxPayloadSize);
        TestSeq.AreEqual(expected.Records, Hmp1ProgramStatusState.Parse(bytes).Records);
        var tooMany = """{"records":[""" + string.Join(",", Enumerable.Range(0, 257)
            .Select(i => $$"""{"id":"job{{i}}","state":3}""")) + "]}";
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1ProgramStatusState.Parse(Encoding.UTF8.GetBytes(tooMany)));
        var tooLong = $$"""{"records":[{"id":"x","state":1,"message":"{{new string('x', 2049)}}"}]}""";
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1ProgramStatusState.Parse(Encoding.UTF8.GetBytes(tooLong)));
    }

    private const string ActivityJson =
        """{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":null},"workingDirectory":{"uri":null}}""";

    private static async Task ScriptHandshakeAsync(Stream wire, Hmp1WorkloadAdapter client, bool negotiated, string payload)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        Assert.AreEqual(0x0B, (await ReadLiteralFrameAsync(wire, timeout.Token)).Type);
        await LiteralFrameAsync(wire, 0x01, negotiated
            ? """{"version":1,"width":20,"height":5,"peerId":"script","primaryPeerId":null,"peers":[],"programStatusVersion":1}"""
            : """{"version":1,"width":20,"height":5,"peerId":"script","primaryPeerId":null,"peers":[]}""");
        await LiteralFrameAsync(wire, 0x02, "initial");
        await LiteralFrameAsync(wire, 0x0D, ActivityJson);
        if (negotiated)
            await LiteralFrameAsync(wire, 0x11, payload);
        await connecting.WaitAsync(timeout.Token);
    }

    private static async Task LiteralFrameAsync(Stream wire, byte type, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var header = new byte[5];
        header[0] = type;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), bytes.Length);
        await wire.WriteAsync(header, TestContext.Current.CancellationToken);
        await wire.WriteAsync(bytes, TestContext.Current.CancellationToken);
        await wire.FlushAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(int Type, byte[] Bytes)> ReadLiteralFrameAsync(Stream wire, CancellationToken ct)
    {
        var header = new byte[5];
        await wire.ReadExactlyAsync(header, ct);
        var bytes = new byte[BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1))];
        await wire.ReadExactlyAsync(bytes, ct);
        return (header[0], bytes);
    }

    private static async Task WaitAsync(Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, "authoritative program inventory applied").Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(Hmp1PresentationAdapter server)
    {
        var streams = CreateStreams();
        var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await client.ConnectAsync(timeout.Token);
        return (await accepting.WaitAsync(timeout.Token), client);
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
        public override bool CanWrite => true;
        public override bool CanSeek => false;
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
            if (disposing)
            {
                input.Dispose();
                output.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
