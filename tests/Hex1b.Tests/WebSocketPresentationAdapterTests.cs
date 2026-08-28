using System.Net.WebSockets;
using System.Text;

namespace Hex1b.Tests;

[TestClass]
public class WebSocketPresentationAdapterTests
{
    [TestMethod]
    public void Resize_WithInvalidTracePath_DoesNotThrow()
    {
        using var webSocket = new StubWebSocket();
        var originalTracePath = Environment.GetEnvironmentVariable("HEX1B_WEBSOCKET_RESIZE_TRACE_FILE");

        try
        {
            Environment.SetEnvironmentVariable(
                "HEX1B_WEBSOCKET_RESIZE_TRACE_FILE",
                Path.GetTempPath());

            var adapter = new WebSocketPresentationAdapter(webSocket, 80, 24);

            adapter.Resize(120, 40, 9, 18, 9.5);

            Assert.AreEqual(120, adapter.Width);
            Assert.AreEqual(40, adapter.Height);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_WEBSOCKET_RESIZE_TRACE_FILE", originalTracePath);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task WriteOutputAsync_SplitUtf8Scalar_UsesFragmentedTextMessage(
        int splitIndex)
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var bytes = "\U0010EEEE"u8.ToArray();

        await adapter.WriteOutputAsync(bytes.AsMemory(0, splitIndex));
        Assert.IsEmpty(webSocket.SentFrames);
        await adapter.WriteOutputAsync(bytes.AsMemory(splitIndex));

        Assert.AreEqual(1, webSocket.SentFrames.Count);
        Assert.AreEqual(WebSocketMessageType.Text, webSocket.SentFrames[0].MessageType);
        Assert.IsTrue(webSocket.SentFrames[0].EndOfMessage);
        TestSeq.AreEqual(bytes, webSocket.SentFrames[0].Data);
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_CompletePrefixBeforeSplitScalar_SendsPrefixImmediately()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var scalar = "\U0010EEEE"u8.ToArray();

        await adapter.WriteOutputAsync(
            "ready:"u8.ToArray().Concat(scalar[..1]).ToArray());
        await adapter.WriteOutputAsync(scalar.AsMemory(1));

        Assert.AreEqual(2, webSocket.SentFrames.Count);
        TestSeq.AreEqual("ready:"u8.ToArray(), webSocket.SentFrames[0].Data);
        TestSeq.AreEqual(scalar, webSocket.SentFrames[1].Data);
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_InvalidContinuation_UsesReplacementBeforeFollowingEscape()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);

        await adapter.WriteOutputAsync(new byte[] { 0xF0 });
        await adapter.WriteOutputAsync(new byte[] { 0x1B });

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\uFFFD\x1b", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task DisposeAsync_IncompleteScalar_FlushesValidReplacementWithoutDeadlock()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        await adapter.WriteOutputAsync(new byte[] { 0xF0 });

        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\uFFFD", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_CancelledSend_DoesNotCommitPendingUtf8()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        using var cancellation = new CancellationTokenSource();
        webSocket.BlockSends = true;

        var cancelledWrite = adapter.WriteOutputAsync(
            new byte[] { (byte)'A', 0xF0 },
            cancellation.Token).AsTask();
        await webSocket.SendStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await cancelledWrite;
        webSocket.BlockSends = false;
        webSocket.ReleaseSend.TrySetResult();

        await adapter.WriteOutputAsync(new byte[] { 0x1B })
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(2));

        var frame = TestSeq.Single(webSocket.SentFrames);
        Assert.AreEqual("\x1b", Encoding.UTF8.GetString(frame.Data));
        AssertValidUtf8(webSocket.SentFrames);
    }

    [TestMethod]
    public async Task WriteOutputAsync_ConcurrentCompleteWrites_AreSerializedAndValid()
    {
        using var webSocket = new StubWebSocket();
        await using var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        var expected = Enumerable.Range(0, 32)
            .Select(index => $"value-{index:D2}-\U0010EEEE")
            .ToArray();

        await Task.WhenAll(expected.Select(
                value => adapter.WriteOutputAsync(
                    Encoding.UTF8.GetBytes(value)).AsTask()))
            .WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(expected.Length, webSocket.SentFrames.Count);
        AssertValidUtf8(webSocket.SentFrames);
        TestSeq.AreEqual(
            expected.Order(),
            webSocket.SentFrames
                .Select(frame => Encoding.UTF8.GetString(frame.Data))
                .Order());
    }

    [TestMethod]
    public async Task DisposeAsync_WriterAdmittedBeforeDisposal_ObservesCancellation()
    {
        using var webSocket = new StubWebSocket();
        var adapter = new WebSocketPresentationAdapter(
            webSocket,
            80,
            24);
        using var releaseState = new ManualResetEventSlim();
        var firstStateRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateReadCount = 0;
        webSocket.StateProvider = () =>
        {
            if (Interlocked.Increment(ref stateReadCount) == 1)
            {
                firstStateRead.TrySetResult();
                releaseState.Wait();
            }
            return WebSocketState.Open;
        };

        var write = Task.Run(
            async () => await adapter.WriteOutputAsync("value"u8.ToArray()));
        await firstStateRead.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await adapter.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        releaseState.Set();
        await write.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsEmpty(webSocket.SentFrames);
    }

    private static void AssertValidUtf8(
        IEnumerable<StubWebSocket.SentFrame> frames)
    {
        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        foreach (var frame in frames)
        {
            Assert.AreEqual(WebSocketMessageType.Text, frame.MessageType);
            Assert.IsTrue(frame.EndOfMessage);
            _ = strictUtf8.GetString(frame.Data);
        }
    }

    private sealed class StubWebSocket : WebSocket
    {
        internal readonly record struct SentFrame(
            byte[] Data,
            WebSocketMessageType MessageType,
            bool EndOfMessage);

        internal List<SentFrame> SentFrames { get; } = [];
        internal bool BlockSends { get; set; }
        internal TaskCompletionSource SendStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        internal Func<WebSocketState>? StateProvider { get; set; }

        public override WebSocketState State =>
            StateProvider?.Invoke() ?? WebSocketState.Open;

        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => SendAsync(
                buffer.AsMemory(),
                messageType,
                endOfMessage,
                cancellationToken).AsTask();

        public override async ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken = default)
        {
            if (BlockSends)
            {
                SendStarted.TrySetResult();
                await ReleaseSend.Task.WaitAsync(cancellationToken);
            }

            SentFrames.Add(new SentFrame(
                buffer.ToArray(),
                messageType,
                endOfMessage));
        }
    }
}
