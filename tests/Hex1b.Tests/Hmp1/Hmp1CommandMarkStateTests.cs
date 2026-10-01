using System.IO.Pipelines;
using System.Text;
using Hex1b.Automation;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1CommandMarkStateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(300)]
    [DataRow(1000)]
    public async Task Reattach_LargeInventory_PreservesRetainedRowsAndLiveContinuation(int historyRows)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(2000).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Commands(320) + "READY"));
        Assert.AreEqual(640, producer.CommandMarks.Count);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var connection = await ConnectAsync(server, historyRows);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
                .WithHeadless().WithScrollback(historyRows).Build();
            await WaitAsync(mirror, snapshot => snapshot.ContainsText("READY"));
            var expected = Capture(producer, historyRows);
            var actual = Capture(mirror, historyRows);
            Assert.IsTrue(actual.Marks.Count > 200);
            TestSeq.AreEqual(expected.Marks, actual.Marks);
            Assert.AreEqual(0, actual.Marks[0].Row);
            Assert.AreEqual(expected.LastId, actual.LastId);

            workload.Write($"\r\n\x1b]133;C;cmdline_url=live{attempt}\aLIVE-{attempt}\r\nREADY");
            await WaitAsync(mirror, snapshot => snapshot.ContainsText($"LIVE-{attempt}"));
            TestSeq.AreEqual(Capture(producer, historyRows).Marks, Capture(mirror, historyRows).Marks);
        }
    }

    [TestMethod]
    [DataRow(9999)]
    [DataRow(10000)]
    [DataRow(10001)]
    public async Task Checkpoint_CountBoundary_TransfersNewestEligibleMarksWithOriginalIds(int count)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(8, 2).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(string.Concat(
            Enumerable.Repeat("\x1b]133;C\a", count)) + "retained"));
        Assert.AreEqual(count, terminal.CommandMarks.Count);
        var checkpoint = Capture(terminal, 0);
        Assert.AreEqual(count, checkpoint.AvailableMarks);
        Assert.AreEqual((long)count, checkpoint.LastId);
        Assert.AreEqual(Math.Min(count, 10_000), checkpoint.Marks.Count);
        TestSeq.AreEqual(Enumerable.Range(Math.Max(1, count - 9999), Math.Min(count, 10_000))
            .Select(id => (long)id), checkpoint.Marks.Select(mark => mark.Id));
        Assert.IsTrue(checkpoint.Marks.All(mark => mark.Row == 0 && mark.Column == 0));
        var parsed = Hmp1CommandMarkState.Parse(checkpoint.Serialize(),
            new("peer", null, 8, 2, true), null);
        TestSeq.AreEqual(checkpoint.Marks, parsed.Marks);
    }

    [TestMethod]
    public void Checkpoint_ByteBudget_RejectsOversizedPayloadWithoutTruncatingDetails()
    {
        var parameters = new string('x', 65_536);
        var marks = Enumerable.Range(1, 128).Select(id =>
            new Hmp1CommandMark(id, false, 0, 0, TerminalShellIntegrationPhase.Executing, null, parameters))
            .ToArray();
        var checkpoint = new Hmp1CommandMarkState(true, 0, 8, 2, false, 128, 128, marks);
        var withinBudget = checkpoint with { Marks = marks[..127] };
        Assert.IsTrue(withinBudget.Serialize().Length < Hmp1CommandMarkState.MaxPayloadSize);
        Assert.ThrowsExactly<InvalidDataException>(() => checkpoint.Serialize());
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1CommandMarkState.Parse(
            new byte[Hmp1CommandMarkState.MaxPayloadSize + 1], new("peer", null, 8, 2, true), null));
    }

    [TestMethod]
    public async Task HorizontalEdits_CheckpointAndLiveOutput_PreserveAndExpireSameMarks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(8, 3);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(8, 3).WithScrollback(20).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("AB\x1b]133;C\aTARGET\x1b[H\x1b[2P\r\nREADY"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(20).Build();

        await WaitAsync(mirror, snapshot => snapshot.ContainsText("READY"));
        TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
        Assert.AreEqual(0, TestSeq.Single(Capture(mirror).Marks).Column);

        workload.Write("\x1b[H\x1b[@\x1b[3;1HLIVE");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("LIVE"));
        TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
        Assert.AreEqual(1, TestSeq.Single(Capture(mirror).Marks).Column);

        workload.Write("\x1b[H\x1b[8P\x1b[3;1HGONE");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("GONE"));
        Assert.AreEqual(0, producer.CommandMarks.Count);
        Assert.AreEqual(0, mirror.CommandMarks.Count);
    }

    [TestMethod]
    public async Task InsertMode_AfterPendingPositionCheckpoint_BindsBothMarksToNewText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(8, 3);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(8, 3).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]133;C\a\x1b[2;1HREADY\x1b[H"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().Build();

        await WaitAsync(mirror, snapshot => snapshot.ContainsText("READY"));
        workload.Write("\x1b[4hTEXT\x1b[4l");
        await WaitAsync(mirror, snapshot => snapshot.ContainsText("TEXT"));
        TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
        Assert.AreEqual(0, TestSeq.Single(Capture(mirror).Marks).Column);
    }

    [TestMethod]
    [DataRow(true, true, 100, 100, 200)]
    [DataRow(false, true, 100, 100, 200)]
    [DataRow(true, false, 100, 100, 200)]
    [DataRow(true, true, 0, 100, 200)]
    [DataRow(true, true, 100, 0, 200)]
    [DataRow(true, true, 3, 2, 200)]
    [DataRow(true, true, 100, 100, 2)]
    [DataRow(true, true, 100, 100, 0)]
    public async Task LateAttach_CommandMarks_RespectNegotiationAndReceiverRetention(
        bool clientEnabled, bool serverEnabled, int requestedRows, int historyCapacity, int markCapacity)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5) { EnableCommandMarkHistory = serverEnabled };
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Commands(10) + "READY"));
        var connection = await ConnectAsync(server, requestedRows, clientEnabled);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = client, PresentationAdapter = new HeadlessPresentationAdapter(20, 5),
            Width = 20, Height = 5, ScrollbackCapacity = historyCapacity > 0 ? historyCapacity : null,
            CommandMarkHistoryCapacity = markCapacity
        });
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        var expected = Capture(producer, Math.Min(requestedRows, historyCapacity));
        var actual = Capture(mirror);
        TestSeq.AreEqual(clientEnabled && serverEnabled ? expected.Marks.TakeLast(markCapacity) : [], actual.Marks);
        Assert.AreEqual(clientEnabled && serverEnabled ? expected.LastId : 0, actual.LastId);
        workload.Write("\r\n\x1b]133;C;cmdline_url=echo%20LIVE\aLIVE");
        await WaitAsync(mirror, s => s.ContainsText("LIVE"));
        if (markCapacity > 0)
            Assert.AreEqual("echo%20LIVE", mirror.CommandMarks[^1].CmdlineUrl);
    }

    [TestMethod]
    public async Task Reattach_LastViewClosed_RestoresMarksAndFutureIdsWithoutDuplicates()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Commands(10) + "READY"));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var connection = await ConnectAsync(server);
            await using (var handle = connection.Handle)
            await using (var client = connection.Client)
            await using (var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
                .WithHeadless().WithScrollback(100).Build())
            {
                await WaitAsync(mirror, s => s.ContainsText("READY"));
                TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
                workload.Write($"\r\n\x1b]133;C;cmdline_url=echo%20LIVE-{attempt}\aLIVE-{attempt}\r\nREADY");
                await WaitAsync(mirror, s => s.ContainsText($"LIVE-{attempt}"));
                TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
            }
            Assert.AreEqual(0, server.ClientCount);
        }
    }

    [TestMethod]
    public async Task LateAttach_LatestMarkErased_PreservesIdHighWaterForSubsequentOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Commands(2) +
            "\x1b]133;C;cmdline_url=erased\a\x1b[2KREADY"));
        var expected = Capture(producer);
        Assert.IsTrue(expected.LastId > expected.Marks.Max(mark => mark.Id));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        Assert.AreEqual(expected.LastId, Capture(mirror).LastId);
        workload.Write("\r\n\x1b]133;C;cmdline_url=echo%20LIVE\aLIVE");
        await WaitAsync(mirror, s => s.ContainsText("LIVE"));
        TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
        Assert.AreEqual(expected.LastId + 1, Capture(mirror).LastId);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LateAttach_ReflowAndAlternateHistory_KeepRetainedAnchors(bool alternate)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5).WithReflow(GhosttyReflowStrategy.Instance);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Commands(10) +
            (alternate ? "\x1b[?1049h\x1b]133;C;cmdline_url=alt\a" : "") + "READY"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithReflow(GhosttyReflowStrategy.Instance).WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
        if (alternate)
        {
            workload.Write("\x1b[?1049l\x1b[HMAIN");
            await WaitAsync(mirror, s => !s.InAlternateScreen && s.ContainsText("MAIN"));
            // Saved main-screen cells are not in an alternate-screen StateSync,
            // but main-history anchors must survive the return.
            TestSeq.AreEqual(Capture(producer).Marks.Where(m => m.Row < producer.ScrollbackCount),
                Capture(mirror).Marks.Where(m => m.Row < mirror.ScrollbackCount));
        }
        else
        {
            foreach (var width in new[] { 12, 30, 20 })
            {
                producer.Resize(width, 5);
                mirror.Resize(width, 5);
                TestSeq.AreEqual(Capture(producer).Marks, Capture(mirror).Marks);
            }
        }
        workload.Write(string.Concat(Enumerable.Repeat("\r\nnew row", 150)) + "\r\nEXPIRED");
        await WaitAsync(mirror, s => s.ContainsText("EXPIRED"));
        Assert.AreEqual(0, mirror.CommandMarks.Count);
        Assert.AreEqual(0, mirror.TextAnchorCount);
    }

    [TestMethod]
    public async Task StateSync_RepeatedAcrossRelay_ReplacesMarksWithoutSynthesizedEvents()
    {
        using var seedWorkload = new Hex1bAppWorkloadAdapter();
        await using var seed = Hex1bTerminal.CreateBuilder().WithWorkload(seedWorkload)
            .WithHeadless().WithDimensions(20, 5).WithScrollback(100).Build();
        seed.ApplyTokens(AnsiTokenizer.Tokenize(Commands(10)));
        using var seedSnapshot = seed.CaptureHmp1Snapshot(100, true, out var history, out var commands);
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var upstream = Hmp1TestHelpers.NewClient(streams.Client);
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstream)
            .WithPresentation(relay).WithScrollback(100).Build();
        var events = 0;
        replica.CommandMarkAdded += _ => Interlocked.Increment(ref events);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = upstream.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "upstream", null, [], timeout.Token, 100, true);
        await WriteBaselineAsync("FIRST");
        await connecting.WaitAsync(timeout.Token);
        var connection = await ConnectAsync(relay, 2);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("FIRST"));
        TestSeq.AreEqual(commands!.ForHistoryRows(2).Marks, Capture(mirror).Marks);
        await WriteBaselineAsync("SECOND");
        await WaitAsync(mirror, s => s.ContainsText("SECOND"));
        TestSeq.AreEqual(commands.ForHistoryRows(2).Marks, Capture(mirror).Marks);
        Assert.AreEqual(0, events);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output,
            "\r\n\x1b]133;C;cmdline_url=live\aLIVE"u8.ToArray(), timeout.Token);
        await WaitAsync(mirror, s => s.ContainsText("LIVE"));
        Assert.AreEqual(1, events);
        Assert.AreEqual(commands.LastId + 1, Capture(mirror).LastId);

        async Task WriteBaselineAsync(string text)
        {
            await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync,
                Encoding.UTF8.GetBytes("\x1b[2J\x1b[H" + text), timeout.Token);
            await Hmp1Protocol.WriteActivityStateAsync(wire, Hmp1ActivityState.Default, timeout.Token);
            await history!.WriteAsync(wire, 100, timeout.Token);
            await commands!.WriteAsync(wire, history.Rows.Count, timeout.Token);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    public void Checkpoint_InvalidMarkData_RejectsBeforePublishing(int variant)
    {
        var mark = new Hmp1CommandMark(1, false, 1, 0, TerminalShellIntegrationPhase.Executing, null, "cmdline_url=x");
        var state = new Hmp1CommandMarkState(true, 0, 20, 5, false, 1, 1, [mark]);
        state = variant switch
        {
            0 => state with { HistoryRows = 1 },
            1 => state with { Marks = [mark with { Row = 5 }] },
            2 => state with { Marks = [mark with { Column = 21 }] },
            3 => state with { Marks = [mark with { ExitCode = 3 }] },
            4 => state with { AvailableMarks = 2, Marks = [mark, mark] },
            _ => state with { LastId = 0 }
        };
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1CommandMarkState.Parse(state.Serialize(),
            new("peer", null, 20, 5, true), null));
    }

    [TestMethod]
    [DataRow(0, 2)]
    [DataRow(13, 2)]
    [DataRow(38, 2)]
    [DataRow(48, 2)]
    [DataRow(49, 2)]
    [DataRow(47, 0)]
    [DataRow(54, 255)]
    [DataRow(50, 255)]
    [DataRow(26, 255)]
    public void Checkpoint_MalformedBinaryFields_RejectsInvalidFlagsUtf8AndLengths(int offset, int value)
    {
        var mark = new Hmp1CommandMark(1, false, 1, 0, TerminalShellIntegrationPhase.Executing, null, "x");
        var payload = new Hmp1CommandMarkState(true, 0, 20, 5, false, 1, 1, [mark]).Serialize();
        payload[offset] = (byte)value;
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1CommandMarkState.Parse(payload,
            new("peer", null, 20, 5, true), null));
    }

    [TestMethod]
    public void Checkpoint_UnavailableAndEmpty_AreDistinctAndRejectTrailingBytes()
    {
        var terminal = new Hmp1TerminalState("peer", null, 20, 5, true);
        Assert.IsFalse(Hmp1CommandMarkState.Parse(new byte[] { 0 }, terminal, null).Available);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Hmp1CommandMarkState.Parse(new byte[] { 0, 0 }, terminal, null));
        var empty = new Hmp1CommandMarkState(true, 0, 20, 5, false, 100, 0, []);
        var parsed = Hmp1CommandMarkState.Parse(empty.Serialize(), terminal, null);
        Assert.IsTrue(parsed.Available);
        Assert.AreEqual(100L, parsed.LastId);
        Assert.AreEqual(0, parsed.Marks.Count);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            Hmp1CommandMarkState.Parse(empty.Serialize().Append((byte)0).ToArray(), terminal, null));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Checkpoint_AlternateMismatch_DoesNotPublishPartialBaseline(bool alternate)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithDimensions(20, 5).Build();
        mirror.ApplyTokens(AnsiTokenizer.Tokenize("ORIGINAL"));
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "peer", null, [], timeout.Token, 0, true);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync,
            Encoding.UTF8.GetBytes((alternate ? "\x1b[?1049h" : "") + "\x1b[2J\x1b[HPARTIAL"), timeout.Token);
        await Hmp1Protocol.WriteActivityStateAsync(wire, Hmp1ActivityState.Default, timeout.Token);
        await new Hmp1CommandMarkState(true, 0, 20, 5, !alternate, 0, 0, [])
            .WriteAsync(wire, 0, timeout.Token);
        await connecting.WaitAsync(timeout.Token);
        Assert.IsInstanceOfType<InvalidDataException>(await client.InitialReplay.WaitAsync(timeout.Token));
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsTrue(snapshot.ContainsText("ORIGINAL"));
        Assert.IsFalse(snapshot.ContainsText("PARTIAL"));
    }

    [TestMethod]
    public async Task Handshake_PausedOrTruncatedCommandCheckpoint_DoesNotPublishPartialBaseline()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().WithScrollback(100).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "peer", null, [], timeout.Token, 0, true);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.StateSync, "PARTIAL"u8.ToArray(), timeout.Token);
        await Hmp1Protocol.WriteActivityStateAsync(wire, Hmp1ActivityState.Default, timeout.Token);
        Assert.IsFalse(client.IsConnected);
        using (var before = mirror.CreateSnapshot())
            Assert.IsFalse(before.ContainsText("PARTIAL"));
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.CommandMarkState, new byte[] { 1 }, timeout.Token);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
        using var after = mirror.CreateSnapshot();
        Assert.IsFalse(after.ContainsText("PARTIAL"));
        Assert.AreEqual(0, mirror.CommandMarks.Count);
    }

    private static string Commands(int count) => string.Concat(Enumerable.Range(0, count).Select(i =>
        $"\x1b]133;C;cmdline_url=echo%20ROW-{i:D3}\aROW-{i:D3}\r\n\x1b]133;D;{i % 2}\aDONE-{i:D3}\r\n"));

    private static Hmp1CommandMarkState Capture(Hex1bTerminal terminal, int history = 100)
    {
        using var snapshot = terminal.CaptureHmp1Snapshot(history, true, out _, out var marks);
        return marks!;
    }

    private static async Task WaitAsync(Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, "HMP command checkpoint applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(
        Hmp1PresentationAdapter server, int historyRows = 100, bool commands = true)
    {
        var streams = CreateStreams();
        var client = new Hmp1WorkloadAdapter(new()
        {
            StreamFactory = _ => Task.FromResult(streams.Client),
            ScrollbackHistoryRows = historyRows, EnableCommandMarkHistory = commands
        });
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        try
        {
            await client.ConnectAsync(timeout.Token);
            return (await accepting.WaitAsync(timeout.Token), client);
        }
        catch
        {
            await client.DisposeAsync();
            await streams.Server.DisposeAsync();
            throw;
        }
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
