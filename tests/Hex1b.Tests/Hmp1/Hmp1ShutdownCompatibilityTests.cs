using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using WebTerminalDemo;

namespace Hex1b.Tests.Hmp1;

[TestClass]
public class Hmp1ShutdownCompatibilityTests
{
    private const string Initial = "\x1b[2J\x1b[HINITIAL\r\n";
    private const string First = "FIRST\r\n";
    private const string Final = "FINAL-e\u0301-\u754c-\U0001f31c-WITHOUT-NEWLINE";

    [TestMethod]
    [DataRow(0)]
    [DataRow(7)]
    public async Task TerminalCompleted_LegacyWireClient_SendsAllOutputBeforeUnchangedExitAndEof(int exitCode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var (serverPipe, clientPipe) = DuplexPipeStream.CreatePair();
        await using var serverStream = new GatedHmp1Stream(serverPipe);
        await using var legacyClient = clientPipe;
        await using var server = new Hmp1PresentationAdapter(80, 10);
        var attaching = server.AddClient(serverStream, ct);
        await WriteLegacyFrameAsync(legacyClient, 0x0B,
            """{"displayName":"legacy-client","defaultRole":"secondary"}"""u8.ToArray(), ct);
        var hello = await ReadLegacyFrameAsync(legacyClient, ct);
        Assert.AreEqual((byte)0x01, hello.Type);
        using (var json = JsonDocument.Parse(hello.Payload))
        {
            Assert.AreEqual(1, json.RootElement.GetProperty("version").GetInt32());
            Assert.AreEqual(80, json.RootElement.GetProperty("width").GetInt32());
            Assert.AreEqual(10, json.RootElement.GetProperty("height").GetInt32());
            Assert.AreEqual(0, json.RootElement.GetProperty("peers").GetArrayLength());
        }
        var state = await ReadLegacyFrameAsync(legacyClient, ct);
        Assert.AreEqual((byte)0x02, state.Type);
        Assert.AreEqual(0, state.Payload.Length);
        var activity = await ReadLegacyFrameAsync(legacyClient, ct);
        Assert.AreEqual((byte)0x0D, activity.Type);
        using (var json = JsonDocument.Parse(activity.Payload))
            Assert.AreEqual(0, json.RootElement.GetProperty("progress").GetProperty("state").GetInt32());
        await using var handle = await attaching.WaitAsync(ct);

        var input = "legacy-input\r"u8.ToArray();
        await WriteLegacyFrameAsync(legacyClient, 0x04, input, ct);
        TestSeq.AreEqual(input, (await server.ReadInputAsync(ct)).ToArray());
        var first = Encoding.UTF8.GetBytes(new string('A', 128 * 1024));
        var final = Encoding.UTF8.GetBytes(Final);
        serverStream.Block = true;
        await server.WriteOutputAsync(first, ct);
        await serverStream.WriteStarted.Task.WaitAsync(ct);
        await server.WriteOutputAsync(final, ct);
        try
        {
            server.TerminalCompleted(exitCode);
            Assert.AreEqual(1, serverStream.ActiveWrites,
                "Exit must not write concurrently with an outstanding Output frame.");
            serverStream.ReleaseWrite.TrySetResult();
            var firstFrame = await ReadLegacyFrameAsync(legacyClient, ct);
            var finalFrame = await ReadLegacyFrameAsync(legacyClient, ct);
            var exitFrame = await ReadLegacyFrameAsync(legacyClient, ct);
            Assert.AreEqual((byte)0x03, firstFrame.Type);
            Assert.AreEqual((byte)0x03, finalFrame.Type);
            TestSeq.AreEqual(first, firstFrame.Payload);
            TestSeq.AreEqual(final, finalFrame.Payload);
            Assert.AreEqual((byte)0x06, exitFrame.Type);
            Assert.AreEqual(4, exitFrame.Payload.Length);
            var expectedExit = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(expectedExit, exitCode);
            TestSeq.AreEqual(expectedExit, exitFrame.Payload);
            Assert.AreEqual(0, await legacyClient.ReadAsync(new byte[1], ct),
                "A legacy client must reach EOF without sending a new acknowledgement.");
        }
        finally
        {
            serverStream.ReleaseWrite.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(0, false, true)]
    [DataRow(7, false, true)]
    [DataRow(0, true, true)]
    [DataRow(7, true, true)]
    [DataRow(0, false, false)]
    [DataRow(0, true, false)]
    public async Task RunAsync_LegacyWireServerExitOrEof_DrainsSplitUtf8BeforeLifecycleCompletion(
        int exitCode, bool disconnectedBeforeAttach, bool sendExit)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var (serverPipe, clientPipe) = DuplexPipeStream.CreatePair();
        await using var legacyServer = serverPipe;
        await using var clientStream = clientPipe;
        await using var client = Hmp1TestHelpers.NewClient(clientStream);
        await ConnectLegacyServerAsync(legacyServer, client, ct);
        if (disconnectedBeforeAttach)
        {
            await SendLegacyFinalOutputAsync(legacyServer, exitCode, sendExit, ct);
            await client.DisconnectedTask.WaitAsync(ct);
        }

        var presentation = new GatedPresentationAdapter();
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithPresentation(presentation).WithDimensions(80, 10).Build();
        var run = replica.RunAsync(ct);
        try
        {
            await presentation.WriteStarted.Task.WaitAsync(ct);
            if (!disconnectedBeforeAttach)
            {
                await SendLegacyFinalOutputAsync(legacyServer, exitCode, sendExit, ct);
                await client.DisconnectedTask.WaitAsync(ct);
            }
            Assert.IsFalse(run.IsCompleted, "Remote Exit does not mean buffered output has been presented.");
            Assert.IsFalse(presentation.Completed.Task.IsCompleted);
            presentation.ReleaseWrite.TrySetResult();
            await run.WaitAsync(ct);
            TestSeq.AreEqual(new[] { "INITIAL", "FIRST", Final }, await presentation.Completed.Task.WaitAsync(ct));
            using var snapshot = replica.CreateSnapshot();
            Assert.AreEqual("INITIAL", snapshot.GetLineTrimmed(0));
            Assert.AreEqual("FIRST", snapshot.GetLineTrimmed(1));
            Assert.AreEqual(Final, snapshot.GetLineTrimmed(2));
            Assert.AreEqual((long)Encoding.UTF8.GetByteCount(Initial + First + Final), replica.OutputBytesRead);
            var finalBytes = Encoding.UTF8.GetBytes(First + Final);
            TestSeq.AreEqual(finalBytes, presentation.Output.TakeLast(finalBytes.Length).ToArray(),
                "Lifecycle completion must follow presentation of every final output byte.");
        }
        finally
        {
            presentation.ReleaseWrite.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_LegacyServerPrematureExitOrTruncatedFrame_PreservesOnlyCompleteReceivedOutput(
        bool prematureExit)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var (serverPipe, clientPipe) = DuplexPipeStream.CreatePair();
        await using var legacyServer = serverPipe;
        await using var clientStream = clientPipe;
        await using var client = Hmp1TestHelpers.NewClient(clientStream);
        await ConnectLegacyServerAsync(legacyServer, client, ct);
        await WriteLegacyFrameAsync(legacyServer, 0x03, Encoding.UTF8.GetBytes(First), ct);
        var finalFrame = EncodeLegacyFrame(0x03, Encoding.UTF8.GetBytes(Final));
        if (prematureExit)
        {
            await legacyServer.WriteAsync(new byte[] { 0x06, 0x04, 0, 0, 0, 0x07, 0, 0, 0 }, ct);
            await legacyServer.WriteAsync(finalFrame, ct);
        }
        else
        {
            await legacyServer.WriteAsync(finalFrame.AsMemory(0, finalFrame.Length - 2), ct);
            await legacyServer.DisposeAsync();
        }
        await client.DisconnectedTask.WaitAsync(ct);
        await using var replica = Hex1bTerminal.CreateBuilder()
            .WithWorkload(client).WithHeadless().WithDimensions(80, 10).Build();
        await replica.RunAsync(ct);
        using var snapshot = replica.CreateSnapshot();
        Assert.AreEqual("INITIAL", snapshot.GetLineTrimmed(0));
        Assert.AreEqual("FIRST", snapshot.GetLineTrimmed(1));
        Assert.AreEqual("", snapshot.GetLineTrimmed(2));
        Assert.AreEqual((long)Encoding.UTF8.GetByteCount(Initial + First), replica.OutputBytesRead,
            "The client cannot recover output after Exit or apply an incomplete wire frame.");
    }

    // Pin the pre-change wire contract rather than sharing the production codec.
    private static byte[] EncodeLegacyFrame(byte type, ReadOnlyMemory<byte> payload)
    {
        var bytes = new byte[5 + payload.Length];
        bytes[0] = type;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1, 4), payload.Length);
        payload.CopyTo(bytes.AsMemory(5));
        return bytes;
    }

    private static ValueTask WriteLegacyFrameAsync(Stream stream, byte type, ReadOnlyMemory<byte> payload,
        CancellationToken ct) => stream.WriteAsync(EncodeLegacyFrame(type, payload), ct);

    private static async Task<(byte Type, byte[] Payload)> ReadLegacyFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[5];
        await stream.ReadExactlyAsync(header, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));
        Assert.IsTrue(length is >= 0 and <= 16 * 1024 * 1024, "Invalid legacy HMP1 frame length.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, ct);
        return (header[0], payload);
    }

    private static async Task ConnectLegacyServerAsync(Stream stream, Hmp1WorkloadAdapter client, CancellationToken ct)
    {
        var connecting = client.ConnectAsync(ct);
        var hello = await ReadLegacyFrameAsync(stream, ct);
        Assert.AreEqual((byte)0x0B, hello.Type);
        await WriteLegacyFrameAsync(stream, 0x01,
            """{"version":1,"width":80,"height":10,"peerId":"legacy-peer","primaryPeerId":null,"peers":[]}"""u8.ToArray(), ct);
        await WriteLegacyFrameAsync(stream, 0x02, Encoding.UTF8.GetBytes(Initial), ct);
        await WriteLegacyFrameAsync(stream, 0x0D,
            """{"progress":{"state":0,"percentage":null},"shellIntegration":{"phase":0,"lastExitCode":null},"workingDirectory":{"uri":null}}"""u8.ToArray(), ct);
        await connecting.WaitAsync(ct);
    }

    private static async Task SendLegacyFinalOutputAsync(Stream stream, int exitCode, bool sendExit, CancellationToken ct)
    {
        await WriteLegacyFrameAsync(stream, 0x03, Encoding.UTF8.GetBytes(First), ct);
        var final = Encoding.UTF8.GetBytes(Final);
        var split = Encoding.UTF8.GetByteCount("FINAL-e\u0301-\u754c-") + 2;
        await WriteLegacyFrameAsync(stream, 0x03, final.AsMemory(0, split), ct);
        await WriteLegacyFrameAsync(stream, 0x03, final.AsMemory(split), ct);
        if (sendExit)
        {
            var exit = new byte[] { 0x06, 0x04, 0, 0, 0, 0, 0, 0, 0 };
            BinaryPrimitives.WriteInt32LittleEndian(exit.AsSpan(5), exitCode);
            await stream.WriteAsync(exit, ct);
        }
        else
            await stream.DisposeAsync();
    }

    private sealed class GatedPresentationAdapter : IHex1bTerminalPresentationAdapter, ITerminalLifecycleAwarePresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new(80, 10);
        private Hex1bTerminal? _terminal;
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string[]> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte> Output { get; } = [];
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => _headless.Capabilities;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WriteStarted.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(ct);
            Output.AddRange(data.ToArray());
        }
        public void TerminalCreated(Hex1bTerminal terminal) => _terminal = terminal;
        public void TerminalStarted() { }
        public void TerminalCompleted(int exitCode)
        {
            using var snapshot = _terminal!.CreateSnapshot();
            Completed.TrySetResult(Enumerable.Range(0, 3).Select(snapshot.GetLineTrimmed).ToArray());
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => _headless.FlushAsync(ct);
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _headless.EnterRawModeAsync(ct);
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => _headless.ExitRawModeAsync(ct);
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }
}
