using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;

namespace RemoteTerminalAuthDemo.Server;

internal static class AuthenticatedTerminalEndpoint
{
    public static async Task HandleAsync(
        HttpContext context,
        McpDiagnosticsPresentationFilter diagnostics,
        CancellationToken terminalStopped)
    {
        if (!HasBearerToken(context.Request.Headers.Authorization))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync("A bearer token is required.");
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("A WebSocket request is required.");
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await using var session = diagnostics.CreateAttachSession();
        await SendJsonAsync(
            webSocket,
            new
            {
                type = "attached",
                width = session.Width,
                height = session.Height,
                leader = session.IsLeader,
                data = session.InitialScreen
            },
            context.RequestAborted);

        using var bridgeCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.RequestAborted);
        var sendTask = SendFramesAsync(session, webSocket, bridgeCts);
        var receiveTask = ReceiveFramesAsync(session, webSocket, bridgeCts);
        var terminalStoppedTask = Task.Delay(Timeout.InfiniteTimeSpan, terminalStopped);

        await Task.WhenAny(sendTask, receiveTask, terminalStoppedTask);
        if (terminalStopped.IsCancellationRequested)
            await NotifyTerminalExitedAsync(webSocket);

        await bridgeCts.CancelAsync();

        try
        {
            await Task.WhenAll(sendTask, receiveTask);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task NotifyTerminalExitedAsync(WebSocket webSocket)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await SendJsonAsync(
                webSocket,
                new { type = "exit" },
                closeCts.Token);
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "The hosted terminal exited.",
                closeCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    private static bool HasBearerToken(string? authorization)
        => AuthenticationHeaderValue.TryParse(authorization, out var value) &&
           string.Equals(value.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(value.Parameter);

    private static async Task SendFramesAsync(
        AttachSession session,
        WebSocket webSocket,
        CancellationTokenSource bridgeCts)
    {
        try
        {
            await foreach (var frame in session.Frames.ReadAllAsync(bridgeCts.Token))
            {
                switch (frame.Type)
                {
                    case AttachFrameType.Output:
                        await webSocket.SendAsync(
                            Encoding.UTF8.GetBytes(frame.Data ?? ""),
                            WebSocketMessageType.Binary,
                            endOfMessage: true,
                            bridgeCts.Token);
                        break;
                    case AttachFrameType.Resize:
                        var dimensions = (frame.Data ?? "").Split(',');
                        if (dimensions.Length == 2 &&
                            int.TryParse(dimensions[0], out var columns) &&
                            int.TryParse(dimensions[1], out var rows))
                        {
                            await SendJsonAsync(
                                webSocket,
                                new { type = "resize", cols = columns, rows },
                                bridgeCts.Token);
                        }
                        break;
                    case AttachFrameType.LeaderChanged:
                        await SendJsonAsync(
                            webSocket,
                            new
                            {
                                type = "leader",
                                isLeader = string.Equals(
                                    frame.Data,
                                    "true",
                                    StringComparison.Ordinal)
                            },
                            bridgeCts.Token);
                        break;
                    case AttachFrameType.Exit:
                        await SendJsonAsync(
                            webSocket,
                            new { type = "exit" },
                            bridgeCts.Token);
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
            await bridgeCts.CancelAsync();
        }
    }

    private static async Task ReceiveFramesAsync(
        AttachSession session,
        WebSocket webSocket,
        CancellationTokenSource bridgeCts)
    {
        try
        {
            while (!bridgeCts.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(webSocket, bridgeCts.Token);
                if (message is null)
                    return;

                if (message.Value.Type == WebSocketMessageType.Binary)
                {
                    await session.SendInputAsync(message.Value.Data);
                }
                else if (message.Value.Type == WebSocketMessageType.Text)
                {
                    await HandleControlFrameAsync(
                        session,
                        message.Value.Data,
                        bridgeCts);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
            await bridgeCts.CancelAsync();
        }
    }

    private static async Task HandleControlFrameAsync(
        AttachSession session,
        byte[] data,
        CancellationTokenSource bridgeCts)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty))
                return;

            switch (typeProperty.GetString())
            {
                case "lead":
                    await session.ClaimLeadAsync();
                    break;
                case "resize":
                    if (root.TryGetProperty("cols", out var columns) &&
                        root.TryGetProperty("rows", out var rows))
                    {
                        await session.SendResizeAsync(
                            columns.GetInt32(),
                            rows.GetInt32());
                    }
                    break;
                case "shutdown":
                    session.RequestShutdown();
                    break;
                case "detach":
                    await bridgeCts.CancelAsync();
                    break;
            }
        }
        catch (JsonException)
        {
        }
    }

    private static async Task<(WebSocketMessageType Type, byte[] Data)?> ReceiveMessageAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return (result.MessageType, message.ToArray());
    }

    private static Task SendJsonAsync(
        WebSocket webSocket,
        object value,
        CancellationToken cancellationToken)
        => webSocket.SendAsync(
            JsonSerializer.SerializeToUtf8Bytes(value),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
}
