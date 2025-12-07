using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Gallery;
using Gallery.Exhibits;

var builder = WebApplication.CreateBuilder(args);

// Register all gallery exhibits
builder.Services.AddSingleton<IGalleryExhibit, HelloWorldExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ColorPaletteExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ProgressBarExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, SpinnerExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, BoxDrawingExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, TextInputExhibit>();

var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

// Serve static files (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// API endpoint to list all exhibits
app.MapGet("/apps", (IEnumerable<IGalleryExhibit> exhibits, HttpRequest request) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var wsScheme = request.Scheme == "https" ? "wss" : "ws";

    return exhibits.Select(e => new
    {
        id = e.Id,
        title = e.Title,
        description = e.Description,
        sourceCode = e.SourceCode,
        websocketUrl = $"{wsScheme}://{request.Host}/apps/{e.Id}"
    });
});

// WebSocket endpoint for terminal apps
app.Map("/apps/{exhibitId}", async (HttpContext context, string exhibitId, IEnumerable<IGalleryExhibit> exhibits) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    var exhibit = exhibits.FirstOrDefault(e => e.Id == exhibitId);
    if (exhibit == null)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($"Exhibit '{exhibitId}' not found");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var session = new TerminalSession();
    
    // Start a task to handle resize messages
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    var resizeHandler = HandleResizeMessagesAsync(webSocket, session, cts.Token);
    
    try
    {
        await exhibit.HandleSessionAsync(webSocket, session, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
    }
    catch (WebSocketException)
    {
        // Connection closed unexpectedly
    }
    finally
    {
        cts.Cancel();
    }
});

app.Run();

async Task HandleResizeMessagesAsync(WebSocket webSocket, TerminalSession session, CancellationToken cancellationToken)
{
    var buffer = new byte[256];
    
    try
    {
        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Try to parse as resize message
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    if (doc.RootElement.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "resize")
                    {
                        var cols = doc.RootElement.GetProperty("cols").GetInt32();
                        var rows = doc.RootElement.GetProperty("rows").GetInt32();
                        session.Resize(cols, rows);
                    }
                }
                catch (JsonException)
                {
                    // Not a JSON message, ignore
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected when cancelled
    }
    catch (WebSocketException)
    {
        // Connection closed
    }
}
