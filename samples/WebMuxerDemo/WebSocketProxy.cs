using System.Net.WebSockets;
using Hex1b;

namespace WebMuxerDemo;

/// <summary>
/// Bridges a single WebSocket to a single Unix domain socket connection,
/// forwarding raw HMP1 frames in both directions. The browser speaks HMP1
/// directly via its JavaScript HMP1 client; the producer's multi-head
/// roster + role frames flow through transparently.
/// </summary>
internal static class WebSocketProxy
{
    public static async Task BridgeAsync(WebSocket socket, SessionHost session, CancellationToken ct)
    {
        await using var upstream = await Hmp1Transports
            .ConnectUnixSocket(session.SocketPath, ct)
            .ConfigureAwait(false);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;

        // Browser → upstream
        var inbound = Task.Run(async () =>
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var msg = await socket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                    if (msg.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }
                    if (msg.Count > 0)
                    {
                        await upstream.WriteAsync(buffer.AsMemory(0, msg.Count), token).ConfigureAwait(false);
                        await upstream.FlushAsync(token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (IOException) { }
        }, token);

        // Upstream → browser
        var outbound = Task.Run(async () =>
        {
            var buffer = new byte[16 * 1024];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var read = await upstream.ReadAsync(buffer, token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }
                    await socket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, read),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (IOException) { }
        }, token);

        var first = await Task.WhenAny(inbound, outbound).ConfigureAwait(false);
        await linkedCts.CancelAsync().ConfigureAwait(false);

        try { await Task.WhenAll(inbound, outbound).ConfigureAwait(false); } catch { }

        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "upstream closed",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }
    }
}
