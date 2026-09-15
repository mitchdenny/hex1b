using System.IO.Pipelines;
using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class Hmp1ProtocolOwnershipTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Output_Da1Query_ProducerAnswersWithoutBroadcastingQuery(bool filtered)
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12);
        if (filtered)
            builder.AddPresentationFilter(new PassThroughFilter());
        await using var producer = builder.Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;

        workload.Emit("\x1b[cDONE");
        var output = await ReadThroughAsync(client, "DONE");
        Assert.AreEqual("\x1b[?62;4c", await workload.ReadInputAsync());
        Assert.DoesNotContain("\x1b[c", output);
    }

    [TestMethod]
    public async Task Output_PrimaryChanges_OnlyGeometryChangesAndBothPeersCanType()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var first = await ConnectAsync(server);
        await using var firstHandle = first.Handle;
        await using var firstClient = first.Client;
        var second = await ConnectAsync(server);
        await using var secondHandle = second.Handle;
        await using var secondClient = second.Client;
        for (var index = 0; index < 3; index++)
        {
            var primary = index == 1 ? secondClient : firstClient;
            var columns = 40 + index * 10;
            var rows = 12 + index;
            await primary.RequestPrimaryAsync(columns, rows);
            Assert.IsTrue(await primary.WaitForRoleAsync(true, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            workload.Emit($"\x1b[c\x1b[18t\x1b[HDONE{index}");
            Assert.AreEqual("\x1b[?62;4c", await workload.ReadInputAsync());
            Assert.AreEqual($"\x1b[8;{rows};{columns}t", await workload.ReadInputAsync());
            var firstOutput = await ReadThroughAsync(firstClient, $"DONE{index}");
            var secondOutput = await ReadThroughAsync(secondClient, $"DONE{index}");
            Assert.DoesNotContain("\x1b[c", firstOutput);
            Assert.DoesNotContain("\x1b[c", secondOutput);
            await firstClient.WriteInputAsync("first"u8.ToArray());
            Assert.AreEqual("first", await workload.ReadInputAsync());
            await secondClient.WriteInputAsync("second"u8.ToArray());
            Assert.AreEqual("second", await workload.ReadInputAsync());
        }
    }

    [TestMethod]
    public async Task Output_NoClientsAndReconnect_ProducerRemainsAuthoritative()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();

        workload.Emit("\x1b[c\x1b[18t\x1b[Hdetached");
        Assert.AreEqual("\x1b[?62;4c", await workload.ReadInputAsync());
        Assert.AreEqual("\x1b[8;12;40t", await workload.ReadInputAsync());
        await WaitForTextAsync(producer, "detached");

        for (var reconnect = 0; reconnect < 2; reconnect++)
        {
            var connection = await ConnectAsync(server);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            var replay = await ReadThroughAsync(client, "detached");
            Assert.DoesNotContain("\x1b[c", replay);
            await client.RequestPrimaryAsync(60, 20);
            Assert.IsTrue(await client.WaitForRoleAsync(true, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
            workload.Emit("\x1b[18t\x1b[16t\x1b[14t\x1b[Hattached");
            Assert.AreEqual("\x1b[8;20;60t", await workload.ReadInputAsync());
            Assert.AreEqual("\x1b[6;20;10t", await workload.ReadInputAsync());
            Assert.AreEqual("\x1b[4;400;600t", await workload.ReadInputAsync());
            var output = await ReadThroughAsync(client, "attached");
            Assert.DoesNotContain("\x1b[18t", output);
            await client.WriteInputAsync("barrier"u8.ToArray());
            Assert.AreEqual("barrier", await workload.ReadInputAsync());
            await handle.DisposeAsync();
            workload.Emit("\x1b[c\x1b[Hdetached");
            Assert.AreEqual("\x1b[?62;4c", await workload.ReadInputAsync());
            await WaitForTextAsync(producer, "detached");
        }
    }

    [TestMethod]
    [DataRow("\x1b[c", "\x1b[?62;4c")]
    [DataRow("\x1b[0c", "\x1b[?62;4c")]
    [DataRow("\x1b[5n", "\x1b[0n")]
    [DataRow("\x1b[6n", "\x1b[1;1R")]
    [DataRow("\x1b[18t", "\x1b[8;12;40t")]
    [DataRow("\x1b_Ga=q,i=73,f=32,s=1,v=1;AAAAAA==\x1b\\", "\x1b_Gi=73;OK\x1b\\")]
    public async Task Output_MultipleHopsAndNativeClient_OnlyProducerResponds(string query, string reply)
    {
        await using var workload = new QueryWorkload { EchoDuplicateReplies = true };
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var upstream = await ConnectAsync(server);
        await using var upstreamHandle = upstream.Handle;
        await using var upstreamClient = upstream.Client;
        await using var relayServer = new Hmp1PresentationAdapter(40, 12);
        await using var relay = Hex1bTerminal.CreateBuilder().WithWorkload(upstreamClient)
            .WithPresentation(relayServer).Build();
        var downstream = await ConnectAsync(relayServer);
        await using var downstreamHandle = downstream.Handle;
        await using var downstreamClient = downstream.Client;
        await using var native = new NativePresentation();
        await using var nativeTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(native.Workload)
            .WithHeadless(native.Capabilities).WithDimensions(40, 12).Build();
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(downstreamClient)
            .WithPresentation(native).Build();

        var browser = await ConnectAsync(server);
        await using var browserHandle = browser.Handle;
        await using var browserClient = browser.Client;
        await using var view = new Hwt1PresentationAdapter(40, 12);
        await using var browserTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(browserClient)
            .WithPresentation(view).Build();
        await browserClient.RequestPrimaryAsync(40, 12);
        Assert.IsTrue(await browserClient.WaitForRoleAsync(true, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        workload.Emit(query + "DONE");
        Assert.AreEqual(reply, await workload.ReadInputAsync());
        await WaitForTextAsync(nativeTerminal, "DONE");
        await WaitForTextAsync(browserTerminal, "DONE");
        native.SendInput("native-barrier");
        Assert.AreEqual("native-barrier", await workload.ReadInputAsync());
        await view.HandleMessageAsync("""{"type":"input","text":"browser-barrier"}"""u8.ToArray());
        Assert.AreEqual("browser-barrier", await workload.ReadInputAsync());
        Assert.AreEqual(0, native.Workload.InputCount, "The external emulator must have no reason to answer.");
        using var snapshot = producer.CreateSnapshot();
        Assert.IsFalse(snapshot.ContainsText("^[[?62;4c"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task Output_KittyStateChanges_ProducerQuietModeAndNativeStateArePreserved(int quiet)
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var native = new NativePresentation();
        await using var nativeTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(native.Workload)
            .WithHeadless(native.Capabilities).WithDimensions(40, 12).Build();
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(native).Build();

        workload.Emit($"\x1b_Ga=t,i=73,f=32,s=1,v=1,q={quiet},m=1;AAAA\x1b\\" +
            $"\x1b_Gm=0,q={quiet};AA==\x1b\\" +
            $"\x1b_Ga=p,i=73,C=1,q={quiet};\x1b\\" +
            $"\x1b_Ga=p,i=999,q={quiet};\x1b\\\x1b[5;1HDONE");
        if (quiet == 0)
        {
            Assert.AreEqual("\x1b_Gi=73;OK\x1b\\", await workload.ReadInputAsync());
            Assert.AreEqual("\x1b_Gi=73;OK\x1b\\", await workload.ReadInputAsync());
        }
        if (quiet != 2)
            Assert.AreEqual("\x1b_Gi=999;ENOENT:Image not found\x1b\\", await workload.ReadInputAsync());
        await WaitForTextAsync(nativeTerminal, "DONE");
        native.SendInput("barrier");
        Assert.AreEqual("barrier", await workload.ReadInputAsync());
        Assert.AreEqual(0, native.Workload.InputCount);
        using var expected = producer.CreateSnapshot();
        using var actual = nativeTerminal.CreateSnapshot();
        Assert.AreEqual(1, expected.KgpPlacements.Count);
        Assert.AreEqual(1, actual.KgpPlacements.Count);
        TestSeq.AreEqual(expected.KgpImages[73].Data, actual.KgpImages[73].Data);
        Assert.AreEqual(expected.KgpPlacements[0].Column, actual.KgpPlacements[0].Column);
        Assert.AreEqual(expected.CursorX, actual.CursorX);
    }

    [TestMethod]
    public async Task Output_KittyAnimationAndDeletion_NativeClientDoesNotAcknowledge()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await using var native = new NativePresentation();
        await using var nativeTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(native.Workload)
            .WithHeadless(native.Capabilities).WithDimensions(40, 12).Build();
        await using var mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(native).Build();
        workload.Emit("\x1b_Ga=T,i=73,f=32,s=1,v=1,C=1;AAAA/w==\x1b\\" +
            "\x1b_Ga=f,i=73,f=32,s=1,v=1;/////w==\x1b\\" +
            "\x1b_Ga=a,i=73,c=2,s=1;\x1b\\\x1b[5;1HDONE");
        Assert.AreEqual("\x1b_Gi=73;OK\x1b\\", await workload.ReadInputAsync());
        Assert.AreEqual("\x1b_Gi=73,r=2;OK\x1b\\", await workload.ReadInputAsync());
        await WaitForTextAsync(nativeTerminal, "DONE");
        native.SendInput("barrier");
        Assert.AreEqual("barrier", await workload.ReadInputAsync());
        using (var snapshot = nativeTerminal.CreateSnapshot())
        {
            Assert.AreEqual(2, snapshot.KgpImages[73].FrameCount);
            Assert.AreEqual(2, snapshot.KgpImages[73].CurrentFrameNumber);
        }
        workload.Emit("\x1b_Ga=d,d=I,i=73;\x1b\\\x1b[6;1HDELETED");
        await WaitForTextAsync(nativeTerminal, "DELETED");
        native.SendInput("deleted-barrier");
        Assert.AreEqual("deleted-barrier", await workload.ReadInputAsync());
        Assert.AreEqual(0, native.Workload.InputCount);
        Assert.AreEqual(0, nativeTerminal.KgpImageStore.ImageCount);
    }

    [TestMethod]
    public async Task Output_QuietHeaderGrowsMaximumFrame_SplitsIntoValidFrames()
    {
        await using var workload = new QueryWorkload();
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(server).WithDimensions(40, 12).Build();
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        const string header = "\x1b_Ga=t,i=73;";
        const string suffix = "\x1b\\DONE";
        var bytes = Encoding.ASCII.GetBytes(header +
            new string('A', Hmp1Protocol.MaxPayloadSize - header.Length - suffix.Length) + suffix);
        // Exercise the framing boundary without asking the producer to decode
        // a deliberately oversized, synthetic image payload.
        await server.WriteOutputAsync(bytes);
        var output = await ReadThroughAsync(client, "DONE");
        var start = output.IndexOf("\x1b_G", StringComparison.Ordinal);
        Assert.AreEqual(Hmp1Protocol.MaxPayloadSize + 4, output.Length - start);
        Assert.IsTrue(output.AsSpan(start).StartsWith("\x1b_Ga=t,i=73,q=2;"));
    }

    [TestMethod]
    public async Task Output_TransportOnlyServer_ForwardsQueriesUnchanged()
    {
        await using var server = new Hmp1PresentationAdapter(40, 12);
        var connection = await ConnectAsync(server);
        await using var handle = connection.Handle;
        await using var client = connection.Client;
        await server.WriteOutputAsync("\x1b[cDONE"u8.ToArray());
        Assert.AreEqual("\x1b[cDONE", await ReadThroughAsync(client, "DONE"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Output_DirectAndRelayedHwt1_ReceiveSameSingleReply(bool relayed)
    {
        await using var workload = new QueryWorkload { EchoDuplicateReplies = true };
        await using var view = new Hwt1PresentationAdapter(40, 12);
        await using var server = new Hmp1PresentationAdapter(40, 12);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithPresentation(relayed ? server : view).WithDimensions(40, 12).Build();
        Hmp1ClientHandle? handle = null;
        Hmp1WorkloadAdapter? client = null;
        Hex1bTerminal? mirror = null;
        try
        {
            if (relayed)
            {
                (handle, client) = await ConnectAsync(server);
                mirror = Hex1bTerminal.CreateBuilder().WithWorkload(client).WithPresentation(view).Build();
            }
            workload.Emit("\x1b[cDONE");
            Assert.AreEqual("\x1b[?62;4c", await workload.ReadInputAsync());
            await WaitForTextAsync(mirror ?? producer, "DONE");
            await view.HandleMessageAsync("""{"type":"input","text":"barrier"}"""u8.ToArray());
            Assert.AreEqual("barrier", await workload.ReadInputAsync());
            using var snapshot = (mirror ?? producer).CreateSnapshot();
            Assert.IsFalse(snapshot.ContainsText("^[[?62;4c"));
        }
        finally
        {
            if (mirror is not null) await mirror.DisposeAsync();
            if (client is not null) await client.DisposeAsync();
            if (handle is not null) await handle.DisposeAsync();
        }
    }

    [TestMethod]
    [DataRow("\x1b[c", "\x1b[?62;4c")]
    [DataRow("\x1b[16t", "\x1b[6;20;10t")]
    [DataRow("\x1b_Ga=q,i=73,f=32,s=1,v=1;AAAAAA==\x1b\\", "\x1b_Gi=73;OK\x1b\\")]
    public async Task Attach_InsideQueryAtEveryBoundary_DoesNotSeedQueryOrTail(string query, string expectedReply)
    {
        for (var split = 1; split < query.Length; split++)
        {
            await using var workload = new QueryWorkload();
            await using var server = new Hmp1PresentationAdapter(40, 12);
            await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(workload)
                .WithPresentation(server).WithDimensions(40, 12).Build();
            workload.Emit("ready" + query[..split]);
            await WaitForTextAsync(producer, "ready");
            // Taking the output-state lock makes attachment wait for the whole read,
            // including its pending query prefix, not just the leading text token.
            var connection = await ConnectAsync(server);
            await using var handle = connection.Handle;
            await using var client = connection.Client;
            workload.Emit(query[split..] + "DONE");
            var output = await ReadThroughAsync(client, "DONE");
            Assert.AreEqual(expectedReply, await workload.ReadInputAsync());
            var tokens = AnsiTokenizer.Tokenize(output);
            Assert.IsFalse(tokens.Any(token => token is DeviceAttributesQueryToken or WindowOperationToken or KgpToken),
                $"Split {split}: no query may be replayed.");
            Assert.IsTrue(output.EndsWith("DONE", StringComparison.Ordinal));
            await using var replayWorkload = new QueryWorkload();
            await using var replay = Hex1bTerminal.CreateBuilder().WithWorkload(replayWorkload)
                .WithHeadless().WithDimensions(40, 12).Build();
            replay.ApplyTokens(tokens);
            using var snapshot = replay.CreateSnapshot();
            Assert.AreEqual("readyDONE", snapshot.GetLineTrimmed(0), $"Split {split}: no visible query tail.");
        }
    }

    private static async Task WaitForTextAsync(Hex1bTerminal terminal, string text)
    {
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(text), TimeSpan.FromSeconds(10), text)
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadThroughAsync(Hmp1WorkloadAdapter client, string marker)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var bytes = new List<byte>();
        while (true)
        {
            bytes.AddRange((await client.ReadOutputAsync(timeout.Token)).ToArray());
            var text = Encoding.UTF8.GetString(bytes.ToArray());
            if (text.Contains(marker, StringComparison.Ordinal))
                return text;
        }
    }

    private static async Task<(Hmp1ClientHandle Handle, Hmp1WorkloadAdapter Client)> ConnectAsync(
        Hmp1PresentationAdapter server)
    {
        var toClient = new Pipe();
        var toServer = new Pipe();
        var serverStream = new DuplexStream(toServer.Reader.AsStream(), toClient.Writer.AsStream());
        var clientStream = new DuplexStream(toClient.Reader.AsStream(), toServer.Writer.AsStream());
        var client = new Hmp1WorkloadAdapter(new Hmp1ClientOptions { StreamFactory = _ => Task.FromResult<Stream>(clientStream) });
        var accepted = server.AddClient(serverStream, TestContext.Current.CancellationToken);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        return (await accepted, client);
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
            .AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) =>
            await _output.Reader.ReadAsync(ct);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _inputCount);
            var text = Encoding.UTF8.GetString(data.Span);
            if (EchoDuplicateReplies && text.StartsWith('\x1b') &&
                Interlocked.Increment(ref _protocolReplyCount) > 1)
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

    private sealed class NativePresentation : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        internal QueryWorkload Workload { get; }
        public NativePresentation() => Workload = new QueryWorkload
        {
            InputReceived = data => _input.Writer.TryWrite(data.ToArray())
        };
        internal void SendInput(string text) => _input.Writer.TryWrite(Encoding.UTF8.GetBytes(text));
        public int Width => 40;
        public int Height => 12;
        public TerminalCapabilities Capabilities => new()
        {
            SupportsKgp = true, SupportsSixel = true, CellPixelWidth = 10, CellPixelHeight = 20
        };
        public bool AnswersProtocolQueriesDirectly => true;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Workload.Emit(data);
            return ValueTask.CompletedTask;
        }
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) =>
            await _input.Reader.ReadAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
