using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Attach transport over WebSocket using JSON text frames for control and binary frames for terminal I/O.
/// </summary>
internal sealed class WebSocketAttachTransport : IAttachTransport
{
    private readonly Uri _uri;
    private readonly ClientWebSocket _ws = new();

    public WebSocketAttachTransport(Uri uri)
    {
        _uri = uri;
    }

    public async Task<AttachResult> ConnectAsync(CancellationToken ct)
    {
        try
        {
            await _ws.ConnectAsync(_uri, ct);
        }
        catch (Exception ex)
        {
            return new AttachResult(false, 0, 0, false, null, $"WebSocket connect failed: {ex.Message}");
        }

        // Read initial JSON text frame: {"type":"attached","width":W,"height":H,"leader":BOOL,"data":"..."}
        var message = await ReceiveFullMessageAsync(ct);
        if (message is not { } msg || msg.Type != WebSocketMessageType.Text)
            return new AttachResult(false, 0, 0, false, null, "Expected text frame for handshake");

        var json = Encoding.UTF8.GetString(msg.Data);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString();
        if (type != "attached")
            return new AttachResult(false, 0, 0, false, null, $"Unexpected handshake type: {type}");

        var width = root.GetProperty("width").GetInt32();
        var height = root.GetProperty("height").GetInt32();
        var isLeader = root.GetProperty("leader").GetBoolean();
        var data = root.TryGetProperty("data", out var dataProp) ? dataProp.GetString() : null;

        return new AttachResult(true, width, height, isLeader, data, null);
    }

    public async Task SendInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => await _ws.SendAsync(data, WebSocketMessageType.Binary, true, ct);

    public async Task SendResizeAsync(int width, int height, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new { type = "resize", cols = width, rows = height });
        await SendTextAsync(json, ct);
    }

    public async Task ClaimLeadAsync(CancellationToken ct)
        => await SendTextAsync("""{"type":"lead"}""", ct);

    public async Task DetachAsync(CancellationToken ct)
        => await SendTextAsync("""{"type":"detach"}""", ct);

    public async Task ShutdownAsync(CancellationToken ct)
        => await SendTextAsync("""{"type":"shutdown"}""", ct);

    public async IAsyncEnumerable<AttachFrame> ReadFramesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            (WebSocketMessageType Type, byte[] Data)? message;
            try
            {
                message = await ReceiveFullMessageAsync(ct);
            }
            catch (WebSocketException)
            {
                message = null;
            }

            if (message is not { } msg)
            {
                yield return AttachFrame.Exit();
                yield break;
            }

            if (msg.Type == WebSocketMessageType.Binary)
            {
                yield return AttachFrame.Output(msg.Data);
            }
            else if (msg.Type == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(msg.Data);
                using var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "resize":
                        var w = doc.RootElement.GetProperty("cols").GetInt32();
                        var h = doc.RootElement.GetProperty("rows").GetInt32();
                        yield return AttachFrame.Resize(w, h);
                        break;
                    case "leader":
                        var isLeader = doc.RootElement.GetProperty("isLeader").GetBoolean();
                        yield return AttachFrame.LeaderChanged(isLeader);
                        break;
                    case "exit":
                        yield return AttachFrame.Exit();
                        yield break;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            }
            catch { }
        }

        _ws.Dispose();
    }

    private async Task SendTextAsync(string text, CancellationToken ct)
        => await _ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    /// <summary>
    /// Receives a complete WebSocket message, assembling multiple fragments if needed.
    /// Returns null on close.
    /// </summary>
    private async Task<(WebSocketMessageType Type, byte[] Data)?> ReceiveFullMessageAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return (result.MessageType, ms.ToArray());
    }
}
