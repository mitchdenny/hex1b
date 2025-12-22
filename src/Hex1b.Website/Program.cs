using System.Net.WebSockets;
using Hex1b.Website;
using Hex1b.Website.Examples;
using Hex1b;
using Hex1b.Terminal;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register all gallery examples
builder.Services.AddSingleton<IGalleryExample, MinimalExample>();
builder.Services.AddSingleton<IGalleryExample, TodoExample>();
builder.Services.AddSingleton<IGalleryExample, HelloWorldExample>();
builder.Services.AddSingleton<IGalleryExample, TextInputExample>();
builder.Services.AddSingleton<IGalleryExample, ThemingExample>();
builder.Services.AddSingleton<IGalleryExample, NavigatorExample>();
builder.Services.AddSingleton<IGalleryExample, SixelExample>();
builder.Services.AddSingleton<IGalleryExample, ResponsiveTodoExample>();
builder.Services.AddSingleton<IGalleryExample, LayoutExample>();
builder.Services.AddSingleton<IGalleryExample, SplittersExample>();
builder.Services.AddSingleton<IGalleryExample, ScrollExample>();
builder.Services.AddSingleton<IGalleryExample, RescueExample>();
builder.Services.AddSingleton<IGalleryExample, ReactiveBarChartExample>();
builder.Services.AddSingleton<IGalleryExample, MouseExample>();

// Register Getting Started tutorial examples
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep1Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep2Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep3Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep4Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep5Example>();

var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

// Serve static files (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// API endpoint to list all examples (new URL)
app.MapGet("/examples", (IEnumerable<IGalleryExample> examples, HttpRequest request) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var wsScheme = request.Scheme == "https" ? "wss" : "ws";

    return examples.Select(e => new
    {
        id = e.Id,
        title = e.Title,
        description = e.Description,
        websocketUrl = $"{wsScheme}://{request.Host}/examples/{e.Id}"
    });
});

// Legacy API endpoint (backwards compatibility for existing gallery)
app.MapGet("/apps", (IEnumerable<IGalleryExample> examples, HttpRequest request) =>
{
    var baseUrl = $"{request.Scheme}://{request.Host}";
    var wsScheme = request.Scheme == "https" ? "wss" : "ws";

    return examples.Select(e => new
    {
        id = e.Id,
        title = e.Title,
        description = e.Description,
        websocketUrl = $"{wsScheme}://{request.Host}/apps/{e.Id}"
    });
});

// WebSocket endpoint for terminal apps (new URL)
app.Map("/examples/{exampleId}", async (HttpContext context, string exampleId, IEnumerable<IGalleryExample> examples) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    var example = examples.FirstOrDefault(e => e.Id == exampleId);
    if (example == null)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($"Example '{exampleId}' not found");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    // Handle Hex1b-based example
    await HandleHex1bExampleAsync(webSocket, example, context.RequestAborted);
});

// Legacy WebSocket endpoint (backwards compatibility for existing gallery)
app.Map("/apps/{exampleId}", async (HttpContext context, string exampleId, IEnumerable<IGalleryExample> examples) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("WebSocket connection required");
        return;
    }

    var example = examples.FirstOrDefault(e => e.Id == exampleId);
    if (example == null)
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync($"Example '{exampleId}' not found");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    // Handle Hex1b-based example
    await HandleHex1bExampleAsync(webSocket, example, context.RequestAborted);
});

app.MapDefaultEndpoints();

app.Run();

async Task HandleHex1bExampleAsync(WebSocket webSocket, IGalleryExample example, CancellationToken cancellationToken)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    
    // Create the presentation adapter for WebSocket I/O
    await using var presentation = new WebSocketPresentationAdapter(webSocket, 80, 24, enableMouse: example.EnableMouse);
    
    // Create the workload adapter that Hex1bApp will use
    var workload = new Hex1bAppWorkloadAdapter(presentation.Width, presentation.Height, presentation.Capabilities);
    
    // Create the terminal that bridges presentation ↔ workload
    // Terminal auto-starts I/O pumps when presentation is provided
    using var terminal = new Hex1bTerminal(presentation, workload);
    
    // Check if the example manages its own app lifecycle
    var runTask = example.RunAsync(workload, cts.Token);
    
    Task appTask;
    if (runTask != null)
    {
        // Example manages its own Hex1bApp
        appTask = runTask;
    }
    else
    {
        // Use the traditional widget builder pattern
        var widgetBuilder = example.CreateWidgetBuilder()!;
        var themeProvider = example.CreateThemeProvider();
        var options = new Hex1bAppOptions 
        { 
            WorkloadAdapter = workload,
            ThemeProvider = themeProvider,
            EnableMouse = example.EnableMouse
        };
        var hex1bApp = new Hex1bApp(ctx => widgetBuilder(), options);
        
        appTask = hex1bApp.RunAsync(cts.Token);
    }
    
    try
    {
        // Wait for the app to complete
        await appTask;
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
        logger.LogError(ex, "Error in example {ExampleId}", example.Id);
        throw;
    }
    finally
    {
        cts.Cancel();
    }
}
