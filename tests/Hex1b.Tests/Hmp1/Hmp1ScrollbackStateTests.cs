using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1ScrollbackStateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(true, true, 100, 100)]
    [DataRow(true, false, 100, 100)]
    [DataRow(false, true, 100, 100)]
    [DataRow(true, true, 0, 100)]
    [DataRow(true, true, 3, 100)]
    [DataRow(true, true, 100, 2)]
    [DataRow(true, true, 100, 0)]
    public async Task LateAttach_OptionalHistory_RespectsNegotiationAndLocalCapacity(
        bool producerHistory, bool enabled, int requested, int capacity)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5) { EnableScrollbackHistory = enabled };
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithPresentation(server).WithDimensions(20, 5);
        if (producerHistory)
            builder.WithScrollback(100);
        await using var producer = builder.Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("OLD", 50) + "READY"));
        var connection = await ConnectAsync(server, requested);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        var mirrorBuilder = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless();
        if (capacity > 0)
            mirrorBuilder.WithScrollback(capacity);
        await using var mirror = mirrorBuilder.Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        var expectedCount = producerHistory && enabled ? Math.Min(Math.Min(requested, capacity), producer.ScrollbackCount) : 0;
        Assert.AreEqual(expectedCount, mirror.ScrollbackCount);
        AssertHistoryEqual(producer.GetScrollbackRows(expectedCount), mirror.GetScrollbackRows(expectedCount));
        using (var expected = producer.CreateSnapshot())
        using (var actual = mirror.CreateSnapshot())
        {
            Assert.AreEqual(expected.CursorX, actual.CursorX);
            Assert.AreEqual(expected.CursorY, actual.CursorY);
            for (var row = 0; row < expected.Height; row++)
                Assert.AreEqual(expected.GetLine(row), actual.GetLine(row));
        }
        workload.Write("\r\nLIVE");
        await WaitAsync(mirror, s => s.ContainsText("LIVE"));
        Assert.AreEqual(capacity == 0 ? 0 : Math.Min(capacity, expectedCount + 1), mirror.ScrollbackCount);
    }

    [TestMethod]
    public async Task Server_LegacyClient_OmitsNewFieldsAndFrames()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("OLD", 20) + "READY"));
        var streams = CreateStreams();
        await using var wire = streams.Client;
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(streams.Server, TestContext.Current.CancellationToken);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.ClientHello,
            """{"displayName":"old","defaultRole":"secondary"}"""u8.ToArray(), timeout.Token);
        var hello = await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        Assert.AreEqual(Hmp1FrameType.Hello, hello!.Value.Type);
        using var json = JsonDocument.Parse(hello.Value.Payload);
        Assert.IsFalse(json.RootElement.TryGetProperty("scrollbackHistoryVersion", out _));
        Assert.IsFalse(json.RootElement.TryGetProperty("scrollbackHistoryRows", out _));
        Assert.AreEqual(Hmp1FrameType.StateSync, (await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token))!.Value.Type);
        Assert.AreEqual(Hmp1FrameType.ActivityState, (await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token))!.Value.Type);
        await using var handle = await accepting.WaitAsync(timeout.Token);
        workload.Write("live");
        var next = await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        Assert.AreEqual(Hmp1FrameType.Output, next!.Value.Type);
        Assert.AreEqual("live", Encoding.UTF8.GetString(next.Value.Payload.Span));
    }

    [TestMethod]
    public async Task Client_LegacyServer_ConnectsWithoutWaitingForHistory()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        var request = await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        Assert.AreEqual(1, Hmp1Protocol.ParseClientHello(request!.Value.Payload).ScrollbackHistoryVersion);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Hello,
            """{"version":1,"width":20,"height":5,"peerId":"old","peers":[]}"""u8.ToArray(), timeout.Token);
        await WriteScreenAsync(wire, "READY", timeout.Token);
        await connecting.WaitAsync(timeout.Token);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        Assert.AreEqual(0, mirror.ScrollbackCount);
        await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.Output,
            Encoding.UTF8.GetBytes(Lines("NEW", 20) + "LIVE"), timeout.Token);
        await WaitAsync(mirror, s => s.ContainsText("LIVE"));
        Assert.IsTrue(mirror.ScrollbackCount > 0);
    }

    [TestMethod]
    public async Task LateAttach_ReconnectAndTwoHops_ReplaceRatherThanDuplicateHistory()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("OLD", 30) + "READY"));
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient)
            .WithPresentation(relay).WithScrollback(100).Build();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var connection = await ConnectAsync(relay);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
                .WithHeadless().WithScrollback(100).Build();
            await WaitAsync(mirror, s => s.ContainsText("READY"));
            Assert.AreEqual(producer.ScrollbackCount, mirror.ScrollbackCount);
            AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
        }
    }

    [TestMethod]
    public async Task LateAttach_OutputDuringHistoryTransfer_IsAppliedExactlyOnceAfterBaseline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("OLD", 20) + "BASELINE"));
        var toClient = new Pipe();
        var toServer = new Pipe();
        await using var gate = new HistoryGateStream(toClient.Writer.AsStream());
        await using var serverStream = new DuplexStream(toServer.Reader.AsStream(), gate);
        await using var clientStream = new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream());
        await using var client = Hmp1TestHelpers.NewClient(clientStream);
        using var timeout = new CancellationTokenSource(Timeout);
        var accepting = server.AddClient(serverStream, timeout.Token);
        var connecting = client.ConnectAsync(timeout.Token);
        try
        {
            await gate.Entered.Task.WaitAsync(timeout.Token);
            Assert.IsFalse(client.IsConnected);
            workload.Write("\r\n" + Lines("NEW", 20) + "LIVE");
            await WaitAsync(producer, s => s.ContainsText("LIVE"));
            gate.Release.TrySetResult();
            await connecting.WaitAsync(timeout.Token);
            await using var handle = await accepting.WaitAsync(timeout.Token);
            await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
                .WithHeadless().WithScrollback(100).Build();
            await WaitAsync(mirror, s => s.ContainsText("LIVE"));
            AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
        }
        finally
        {
            gate.Release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task LateAttach_AlternateScreen_KeepsMainHistoryForReturn()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("OLD", 30) + "\x1b[?1049hALT"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.InAlternateScreen && s.ContainsText("ALT"));
        AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
        workload.Write("\x1b[?1049l\x1b[HMAIN");
        await WaitAsync(mirror, s => !s.InAlternateScreen && s.ContainsText("MAIN"));
        AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
    }

    [TestMethod]
    public async Task LateAttach_RichWrappedRows_PreserveCellsAndReflow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(12, 4).WithReflow(GhosttyReflowStrategy.Instance);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(12, 4).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b[1;3;4:3;38;5;123;48;2;1;2;3;58;2;4;5;6m\x1b]8;id=history;https://example.test/a?b=1&c=2\a" +
            "abcdefghijk界e\u0301abcdefghijk界e\u0301\r\n" + Lines("WRAP", 15) +
            "\x1b]8;;\a\x1b[0m\x1b[2J\x1b[HREADY"));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithHeadless()
            .WithReflow(GhosttyReflowStrategy.Instance).WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
        foreach (var width in new[] { 8, 17, 12 })
        {
            producer.Resize(width, 4);
            mirror.Resize(width, 4);
            AssertHistoryEqual(producer.GetScrollbackRows(100), mirror.GetScrollbackRows(100));
        }
        Assert.IsTrue(mirror.TrackedHyperlinkCount > 0);
        mirror.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3J\x1b[2J"));
        Assert.AreEqual(0, mirror.ScrollbackCount);
        Assert.AreEqual(0, mirror.TrackedHyperlinkCount);
    }

    [TestMethod]
    public async Task StateSync_RepeatedAndUnavailableCheckpoints_CommitAtomicallyAcrossRelay()
    {
        using var seedWorkload = new Hex1bAppWorkloadAdapter();
        await using var seed = Hex1bTerminal.CreateBuilder().WithWorkload(seedWorkload)
            .WithHeadless().WithDimensions(20, 5).WithScrollback(100).Build();
        seed.ApplyTokens(AnsiTokenizer.Tokenize(Lines("HISTORY", 20)));
        var state = seed.CaptureHmp1Scrollback(100);
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var upstream = Hmp1TestHelpers.NewClient(streams.Client);
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = upstream.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "upstream", null, [], timeout.Token, 100);
        await WriteScreenAsync(wire, "INITIAL", timeout.Token);
        await state.WriteAsync(wire, 100, timeout.Token);
        await connecting.WaitAsync(timeout.Token);
        await using var relay = new Hmp1PresentationAdapter(20, 5);
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(upstream)
            .WithPresentation(relay).WithScrollback(100).Build();
        var downstream = await ConnectAsync(relay);
        await using var handle = downstream.Handle;
        await using var client = downstream.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("INITIAL"));

        await WriteScreenAsync(wire, "REPEATED", timeout.Token);
        using (var before = mirror.CreateSnapshot())
            Assert.IsTrue(before.ContainsText("INITIAL"));
        await state.WriteAsync(wire, 100, timeout.Token);
        await WaitAsync(mirror, s => s.ContainsText("REPEATED"));
        Assert.AreEqual(state.Rows.Count, mirror.ScrollbackCount);
        AssertHistoryEqual(seed.GetScrollbackRows(100), mirror.GetScrollbackRows(100));

        await WriteScreenAsync(wire, "UNAVAILABLE", timeout.Token);
        await Hmp1ScrollbackState.Unavailable.WriteAsync(wire, 100, timeout.Token);
        await WaitAsync(mirror, s => s.ContainsText("UNAVAILABLE"));
        Assert.AreEqual(state.Rows.Count, mirror.ScrollbackCount);

        await WriteScreenAsync(wire, "CLEARED", timeout.Token);
        await new Hmp1ScrollbackState(0, []).WriteAsync(wire, 100, timeout.Token);
        await WaitAsync(mirror, s => s.ContainsText("CLEARED"));
        Assert.AreEqual(0, mirror.ScrollbackCount);
    }

    [TestMethod]
    public async Task Checkpoint_MultipleChunks_PreservesOrderAndRequestedTail()
    {
        var row = Hmp1ScrollbackRowCodec.Encode(new(Enumerable.Repeat(TerminalCell.Empty, 100).ToArray(), 100, default));
        var state = new Hmp1ScrollbackState(3000, Enumerable.Repeat(row, 2000).ToArray());
        using var wire = new MemoryStream();
        await state.WriteAsync(wire, 1500, TestContext.Current.CancellationToken);
        wire.Position = 0;
        var frames = new List<Hmp1Frame>();
        while (await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken) is { } frame)
            frames.Add(frame);
        Assert.IsTrue(frames.Count > 2);
        Assert.IsTrue(frames.All(f => f.Payload.Length <= Hmp1ScrollbackState.MaxChunkBytes));
        wire.Position = 0;
        var received = await Hmp1ScrollbackState.ReadAsync(wire, 1500, TestContext.Current.CancellationToken);
        Assert.AreEqual(3000, received.AvailableRows);
        Assert.AreEqual(1500, received.Rows.Count);
        CollectionAssert.AreEqual(row, received.Rows[0]);
        CollectionAssert.AreEqual(row, received.Rows[^1]);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Capture_ByteAndCellBudgets_ReturnsExplicitlyCountedNewestSuffix(bool byteBudget)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless().WithDimensions(20, 5).WithScrollback(3000).Build();
        var cells = byteBudget
            ? new[] { TerminalCell.Empty with { Character = new string('x', 16_000) } }
            : Enumerable.Repeat(TerminalCell.Empty, 1000).ToArray();
        var row = new ScrollbackRow(cells, cells.Length, default);
        for (var i = 0; i < 3000; i++)
            terminal.Scrollback!.Push(cells, cells.Length, default);
        var state = terminal.CaptureHmp1Scrollback(3000);
        var expected = Math.Min(3000, Math.Min(Hmp1ScrollbackState.MaxCells / cells.Length,
            Hmp1ScrollbackState.MaxTotalBytes / (4 + Hmp1ScrollbackRowCodec.Encode(row).Length)));
        Assert.AreEqual(3000, state.AvailableRows);
        Assert.AreEqual(expected, state.Rows.Count);
        Assert.IsTrue(state.Rows.Count < state.AvailableRows);
        Assert.IsTrue(state.Rows.Sum(r => (long)r.Length + 4) <= Hmp1ScrollbackState.MaxTotalBytes);
    }

    [TestMethod]
    public async Task LateAttach_MixedOriginalWidths_DoesNotReinterpretHistoricalRows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var server = new Hmp1PresentationAdapter(20, 5);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(20, 5).WithScrollback(100).Build();
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("WIDE", 20)));
        producer.Resize(10, 5);
        producer.ApplyTokens(AnsiTokenizer.Tokenize(Lines("SMALL", 20) + "READY"));
        var expected = producer.GetScrollbackRows(100);
        Assert.IsTrue(expected.Any(row => row.OriginalWidth == 20));
        Assert.IsTrue(expected.Any(row => row.OriginalWidth == 10));
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        await WaitAsync(mirror, s => s.ContainsText("READY"));
        AssertHistoryEqual(expected, mirror.GetScrollbackRows(100));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void RowCodec_MalformedRow_RejectsBeforeImport(int mutation)
    {
        var bytes = Hmp1ScrollbackRowCodec.Encode(new([TerminalCell.Empty], 1, default));
        switch (mutation)
        {
            case 0:
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 65_537);
                break;
            case 1:
                bytes[20] = 0xFF; // Invalid UTF-8.
                break;
            case 2:
                bytes = [.. bytes, 0];
                break;
        }
        Assert.ThrowsExactly<InvalidDataException>(() => Hmp1ScrollbackRowCodec.Decode(bytes));
    }

    [TestMethod]
    public void RowCodec_OversizedString_RejectsExplicitly()
        => Assert.ThrowsExactly<InvalidDataException>(() => Hmp1ScrollbackRowCodec.Encode(
            new([TerminalCell.Empty with { Character = new string('x', 65_537) }], 1, default)));

    [TestMethod]
    public async Task Protocol_OversizedHistoryChunk_RejectsBeforeAllocatingPayload()
    {
        using var wire = new MemoryStream();
        var header = new byte[5];
        header[0] = (byte)Hmp1FrameType.ScrollbackRows;
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(1), Hmp1ScrollbackState.MaxChunkBytes + 1);
        wire.Write(header);
        wire.Position = 0;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await Hmp1Protocol.ReadFrameAsync(wire, TestContext.Current.CancellationToken));
    }

    [TestMethod]
    public async Task Client_UnrequestedHistoryCapability_RejectsHandshake()
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = new Hmp1WorkloadAdapter(new()
        {
            StreamFactory = _ => Task.FromResult(streams.Client),
            ScrollbackHistoryRows = 0
        });
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "peer", null, [], timeout.Token, 100);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
    }

    [TestMethod]
    [DataRow(-2, 0)]
    [DataRow(-1, 1)]
    [DataRow(1, 2)]
    [DataRow(200_000, 100_001)]
    [DataRow(10, -1)]
    public async Task Checkpoint_InvalidCounts_RejectsWithoutReadingRows(int available, int count)
    {
        using var wire = new MemoryStream();
        await WriteHeaderAsync(wire, available, count);
        wire.Position = 0;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            Hmp1ScrollbackState.ReadAsync(wire, 100_000, TestContext.Current.CancellationToken));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Checkpoint_MissingOrMalformedRows_RejectsWithoutInstallingScreen(bool malformed)
    {
        var streams = CreateStreams();
        await using var wire = streams.Server;
        await using var client = Hmp1TestHelpers.NewClient(streams.Client);
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client)
            .WithHeadless().WithScrollback(100).Build();
        using var timeout = new CancellationTokenSource(Timeout);
        var connecting = client.ConnectAsync(timeout.Token);
        await Hmp1Protocol.ReadFrameAsync(wire, timeout.Token);
        await Hmp1Protocol.WriteHelloAsync(wire, 20, 5, "peer", null, [], timeout.Token, 100);
        await WriteScreenAsync(wire, "SHOULD NOT APPEAR", timeout.Token);
        await WriteHeaderAsync(wire, 1, 1);
        if (malformed)
            await Hmp1Protocol.WriteFrameAsync(wire, Hmp1FrameType.ScrollbackRows, new byte[] { 1, 0, 0, 0, 0 }, timeout.Token);
        else
            await wire.DisposeAsync();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => connecting.WaitAsync(timeout.Token));
        Assert.IsFalse(client.IsConnected);
        using var snapshot = mirror.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("SHOULD"));
        Assert.AreEqual(0, mirror.ScrollbackCount);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(100_001)]
    public void ClientOptions_InvalidRowLimit_Throws(int limit)
        => Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Hmp1ClientOptions
        {
            StreamFactory = _ => Task.FromResult<Stream>(Stream.Null),
            ScrollbackHistoryRows = limit
        });

    private static string Lines(string prefix, int count)
        => string.Concat(Enumerable.Range(0, count).Select(i => $"{prefix}-{i:D3}\r\n"));

    private static void AssertHistoryEqual(ScrollbackRow[] expected, ScrollbackRow[] actual)
    {
        Assert.AreEqual(expected.Length, actual.Length);
        for (var row = 0; row < expected.Length; row++)
        {
            Assert.AreEqual(expected[row].OriginalWidth, actual[row].OriginalWidth);
            Assert.AreEqual(expected[row].Cells.Length, actual[row].Cells.Length);
            for (var col = 0; col < expected[row].Cells.Length; col++)
            {
                var a = expected[row].Cells[col];
                var b = actual[row].Cells[col];
                Assert.AreEqual(a.Character, b.Character, $"Row {row}, cell {col}");
                Assert.AreEqual(a.Attributes, b.Attributes);
                Assert.AreEqual(a.IsWideWrapPadding, b.IsWideWrapPadding);
                Assert.AreEqual(a.Foreground, b.Foreground);
                Assert.AreEqual(a.Background, b.Background);
                Assert.AreEqual(a.UnderlineColor, b.UnderlineColor);
                Assert.AreEqual(a.UnderlineStyle, b.UnderlineStyle);
                Assert.AreEqual(a.HyperlinkData?.Uri, b.HyperlinkData?.Uri);
                Assert.AreEqual(a.HyperlinkData?.Parameters, b.HyperlinkData?.Parameters);
            }
        }
    }

    private static async Task WriteHeaderAsync(Stream stream, int available, int count)
    {
        var header = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header, available);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), count);
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.ScrollbackState, header, TestContext.Current.CancellationToken);
    }

    private static async Task WriteScreenAsync(Stream stream, string text, CancellationToken ct)
    {
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync,
            Encoding.UTF8.GetBytes("\x1b[2J\x1b[H" + text), ct);
        await Hmp1Protocol.WriteActivityStateAsync(stream, Hmp1ActivityState.Default, ct);
    }

    private static async Task WaitAsync(Hex1bTerminal terminal, Func<Hex1bTerminalSnapshot, bool> predicate)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(predicate, Timeout, "HMP history baseline applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(
        Hmp1PresentationAdapter server, int requested = 100)
    {
        var streams = CreateStreams();
        var client = new Hmp1WorkloadAdapter(new()
        {
            StreamFactory = _ => Task.FromResult(streams.Client),
            ScrollbackHistoryRows = requested
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

    private sealed class HistoryGateStream(Stream output) : Stream
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => false;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 5 && buffer.Span[0] == (byte)Hmp1FrameType.ScrollbackState)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            await output.WriteAsync(buffer, cancellationToken);
        }
        public override void Flush() => output.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                output.Dispose();
            base.Dispose(disposing);
        }
    }
}
