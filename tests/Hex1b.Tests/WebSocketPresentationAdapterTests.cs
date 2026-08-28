using System.Net.WebSockets;

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
        await adapter.WriteOutputAsync(bytes.AsMemory(splitIndex));

        Assert.AreEqual(2, webSocket.SentFrames.Count);
        Assert.AreEqual(WebSocketMessageType.Text, webSocket.SentFrames[0].MessageType);
        Assert.IsFalse(webSocket.SentFrames[0].EndOfMessage);
        Assert.AreEqual(WebSocketMessageType.Text, webSocket.SentFrames[1].MessageType);
        Assert.IsTrue(webSocket.SentFrames[1].EndOfMessage);
        TestSeq.AreEqual(
            bytes,
            webSocket.SentFrames.SelectMany(frame => frame.Data));
    }

    private sealed class StubWebSocket : WebSocket
    {
        internal readonly record struct SentFrame(
            byte[] Data,
            WebSocketMessageType MessageType,
            bool EndOfMessage);

        internal List<SentFrame> SentFrames { get; } = [];

        public override WebSocketCloseStatus? CloseStatus => null;

        public override string? CloseStatusDescription => null;

        public override WebSocketState State => WebSocketState.Open;

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
        {
            SentFrames.Add(new SentFrame(
                buffer.ToArray(),
                messageType,
                endOfMessage));
            return Task.CompletedTask;
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            SentFrames.Add(new SentFrame(
                buffer.ToArray(),
                messageType,
                endOfMessage));
            return ValueTask.CompletedTask;
        }
    }
}
