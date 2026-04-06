using System.Net.WebSockets;

namespace Hex1b.Tests;

public class WebSocketPresentationAdapterTests
{
    [Fact]
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

            Assert.Equal(120, adapter.Width);
            Assert.Equal(40, adapter.Height);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEX1B_WEBSOCKET_RESIZE_TRACE_FILE", originalTracePath);
        }
    }

    private sealed class StubWebSocket : WebSocket
    {
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
            throw new NotSupportedException();
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
