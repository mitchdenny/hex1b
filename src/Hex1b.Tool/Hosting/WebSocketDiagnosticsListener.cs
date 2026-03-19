using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Hex1b.Tool.Hosting;

/// <summary>
/// Exposes the diagnostics attach interface over HTTP WebSocket.
/// Allows clients to connect by port number instead of Unix domain socket path.
/// </summary>
internal sealed class WebSocketDiagnosticsListener : IAsyncDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly int _port;
    private readonly McpDiagnosticsPresentationFilter _filter;
    private WebApplication? _app;

    public WebSocketDiagnosticsListener(int port, McpDiagnosticsPresentationFilter filter)
    {
        _port = port;
        _filter = filter;
    }

    /// <summary>
    /// Starts the Kestrel web server with WebSocket support.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, _port);
        });
        builder.Logging.ClearProviders();

        _app = builder.Build();
        _app.UseWebSockets();

        _app.MapGet("/api/info", () =>
        {
            return Results.Json(new
            {
                appName = _filter.AppName,
                width = _filter.TerminalWidth,
                height = _filter.TerminalHeight,
                processId = Environment.ProcessId
            }, s_jsonOptions);
        });

        _app.Map("/ws/attach", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connection required");
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await HandleAttachWebSocketAsync(ws, context.RequestAborted);
        });

        try
        {
            await _app.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WebSocket listener failed to start on port {_port}: {ex.Message}");
            _app = null;
            throw;
        }
    }

    /// <summary>
    /// Handles a WebSocket attach session, bridging between WebSocket frames and an AttachSession.
    /// </summary>
    private async Task HandleAttachWebSocketAsync(WebSocket ws, CancellationToken ct)
    {
        AttachSession session;
        try
        {
            session = _filter.CreateAttachSession();
        }
        catch (InvalidOperationException)
        {
            await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Terminal not initialized", ct);
            return;
        }

        await using (session)
        {
            // Send initial state as JSON text frame
            var attached = JsonSerializer.Serialize(new
            {
                type = "attached",
                width = session.Width,
                height = session.Height,
                leader = session.IsLeader,
                data = session.InitialScreen
            }, s_jsonOptions);
            await ws.SendAsync(
                Encoding.UTF8.GetBytes(attached),
                WebSocketMessageType.Text, true, ct);

            // Bridge frames between session and WebSocket
            using var bridgeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var sessionToWs = PumpSessionToWebSocketAsync(session, ws, bridgeCts);
            var wsToSession = PumpWebSocketToSessionAsync(ws, session, bridgeCts);

            await Task.WhenAny(sessionToWs, wsToSession);
            await bridgeCts.CancelAsync();

            try { await Task.WhenAll(sessionToWs, wsToSession); }
            catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Pumps frames from the AttachSession to the WebSocket client.
    /// Terminal output → binary frames, control messages → JSON text frames.
    /// </summary>
    private static async Task PumpSessionToWebSocketAsync(
        AttachSession session, WebSocket ws, CancellationTokenSource bridgeCts)
    {
        try
        {
            await foreach (var frame in session.Frames.ReadAllAsync(bridgeCts.Token))
            {
                if (ws.State != WebSocketState.Open)
                {
                    await bridgeCts.CancelAsync();
                    return;
                }

                switch (frame.Type)
                {
                    case AttachFrameType.Output:
                        var outputBytes = Encoding.UTF8.GetBytes(frame.Data ?? "");
                        await ws.SendAsync(outputBytes, WebSocketMessageType.Binary, true, bridgeCts.Token);
                        break;

                    case AttachFrameType.Resize:
                        var parts = (frame.Data ?? "").Split(',');
                        if (parts.Length == 2 && int.TryParse(parts[0], out var cols) && int.TryParse(parts[1], out var rows))
                        {
                            var json = JsonSerializer.Serialize(
                                new { type = "resize", cols, rows }, s_jsonOptions);
                            await ws.SendAsync(
                                Encoding.UTF8.GetBytes(json),
                                WebSocketMessageType.Text, true, bridgeCts.Token);
                        }
                        break;

                    case AttachFrameType.LeaderChanged:
                        var isLeader = frame.Data == "true";
                        var leaderJson = JsonSerializer.Serialize(
                            new { type = "leader", isLeader }, s_jsonOptions);
                        await ws.SendAsync(
                            Encoding.UTF8.GetBytes(leaderJson),
                            WebSocketMessageType.Text, true, bridgeCts.Token);
                        break;

                    case AttachFrameType.Exit:
                        var exitJson = JsonSerializer.Serialize(new { type = "exit" }, s_jsonOptions);
                        await ws.SendAsync(
                            Encoding.UTF8.GetBytes(exitJson),
                            WebSocketMessageType.Text, true, bridgeCts.Token);
                        await bridgeCts.CancelAsync();
                        return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { await bridgeCts.CancelAsync(); }
    }

    /// <summary>
    /// Pumps input from the WebSocket client to the AttachSession.
    /// Binary frames → raw input, JSON text frames → control commands.
    /// </summary>
    private static async Task PumpWebSocketToSessionAsync(
        WebSocket ws, AttachSession session, CancellationTokenSource bridgeCts)
    {
        var buffer = new byte[4096];
        try
        {
            while (!bridgeCts.Token.IsCancellationRequested)
            {
                // Accumulate fragments until EndOfMessage
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, bridgeCts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await bridgeCts.CancelAsync();
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var data = ms.ToArray();

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Raw terminal input
                    await session.SendInputAsync(data);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var text = Encoding.UTF8.GetString(data);
                    await HandleTextFrameAsync(session, text, bridgeCts);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { await bridgeCts.CancelAsync(); }
    }

    private static async Task HandleTextFrameAsync(
        AttachSession session, string text, CancellationTokenSource bridgeCts)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();
            switch (type)
            {
                case "resize":
                    if (root.TryGetProperty("cols", out var cols) && root.TryGetProperty("rows", out var rows))
                    {
                        await session.SendResizeAsync(cols.GetInt32(), rows.GetInt32());
                    }
                    break;

                case "lead":
                    await session.ClaimLeadAsync();
                    break;

                case "detach":
                    await bridgeCts.CancelAsync();
                    break;

                case "shutdown":
                    session.RequestShutdown();
                    await bridgeCts.CancelAsync();
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed JSON
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
