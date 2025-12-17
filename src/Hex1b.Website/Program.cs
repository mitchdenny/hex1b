using System.Net.WebSockets;
using Hex1b.Website;
using Hex1b.Website.Exhibits;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register all gallery exhibits
builder.Services.AddSingleton<IGalleryExhibit, HelloWorldExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, TextInputExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ThemingExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, NavigatorExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, SixelExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ResponsiveTodoExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, LayoutExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, SplittersExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ScrollExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, RescueExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, ReactiveBarChartExhibit>();
builder.Services.AddSingleton<IGalleryExhibit, MouseExhibit>();

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

app.MapDefaultEndpoints();

app.Run();

async Task HandleHex1bExhibitAsync(WebSocket webSocket, IGalleryExhibit exhibit, CancellationToken cancellationToken)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    using var terminal = new WebSocketHex1bTerminal(webSocket, 80, 24, enableMouse: exhibit.EnableMouse);
    
    // Check if the exhibit manages its own app lifecycle
    var runTask = exhibit.RunAsync(terminal, cts.Token);
    
    Task appTask;
    if (runTask != null)
    {
        // Exhibit manages its own Hex1bApp
        appTask = runTask;
    }
    else
    {
        // Use the traditional widget builder pattern
        var widgetBuilder = exhibit.CreateWidgetBuilder()!;
        var themeProvider = exhibit.CreateThemeProvider();
        var options = new Hex1bAppOptions 
        { 
            Terminal = terminal, 
            ThemeProvider = themeProvider,
            EnableMouse = exhibit.EnableMouse
        };
        var hex1bApp = new Hex1bApp(ctx => widgetBuilder(), options);
        
        appTask = hex1bApp.RunAsync(cts.Token);
    }
    
    // Run input processing and the Hex1b app concurrently
    var inputTask = terminal.ProcessInputAsync(cts.Token);
    
    try
    {
        // Wait for either to complete and observe any exceptions
        var completedTask = await Task.WhenAny(inputTask, appTask);
        await completedTask; // This will throw if the completed task faulted
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown
    }
    catch (WebSocketException)
    {
        // Connection closed
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in exhibit {ExhibitId}", exhibit.Id);
        throw;
    }
    finally
    {
        cts.Cancel();
    }
}
