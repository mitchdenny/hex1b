using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

// Serve static files (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// WebSocket endpoint for terminal apps
app.Map("/apps/{appName}", async (HttpContext context, string appName) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await HandleTerminalSession(webSocket, appName);
});

app.Run();

async Task HandleTerminalSession(WebSocket webSocket, string appName)
{
    var buffer = new byte[1024 * 4];
    
    // Send hello world message
    var helloMessage = $"\x1b[32mHello World!\x1b[0m\r\n\r\nConnected to: {appName}\r\n";
    var helloBytes = Encoding.UTF8.GetBytes(helloMessage);
    await webSocket.SendAsync(helloBytes, WebSocketMessageType.Text, true, CancellationToken.None);

    // Echo loop - receive input and echo it back
    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var input = Encoding.UTF8.GetString(buffer, 0, result.Count);
                // Echo the input back to the terminal
                await webSocket.SendAsync(
                    Encoding.UTF8.GetBytes(input),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
    catch (WebSocketException)
    {
        // Connection closed unexpectedly
    }
}
