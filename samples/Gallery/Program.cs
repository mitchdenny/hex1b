using System.Net.WebSockets;
using Gallery;
using Gallery.Exhibits;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);

// Register all gallery exhibits
builder.Services.AddSingleton<IGalleryExhibit, HelloWorldExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, TextInputExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ThemingExhibit>();

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

    // Handle Hex1b-based exhibit
    await HandleHex1bExhibitAsync(webSocket, exhibit, context.RequestAborted);
});

app.Run();

async Task HandleHex1bExhibitAsync(WebSocket webSocket, IGalleryExhibit exhibit, CancellationToken cancellationToken)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    using var terminal = new WebSocketHex1bTerminal(webSocket, 80, 24);
    
    var widgetBuilder = exhibit.CreateWidgetBuilder();
    var themeProvider = exhibit.CreateThemeProvider();
    using var hex1bApp = themeProvider != null
        ? new Hex1bApp(widgetBuilder, terminal, themeProvider)
        : new Hex1bApp(widgetBuilder, terminal);
    
    // Run input processing and the Hex1b app concurrently
    var inputTask = terminal.ProcessInputAsync(cts.Token);
    var appTask = hex1bApp.RunAsync(cts.Token);
    
    try
    {
        // Wait for either to complete (usually the app exits first on user action)
        await Task.WhenAny(inputTask, appTask);
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown
    }
    catch (WebSocketException)
    {
        // Connection closed
    }
    finally
    {
        cts.Cancel();
    }
}
