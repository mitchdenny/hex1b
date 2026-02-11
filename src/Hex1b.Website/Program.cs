using System.Net.WebSockets;
using Hex1b.Website;
using Hex1b.Website.Examples;
using Hex1b;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register memory cache and HTTP client for version service
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("NuGet", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register version service as both a singleton and a hosted background service
builder.Services.AddSingleton<VersionService>();
builder.Services.AddSingleton<IVersionService>(sp => sp.GetRequiredService<VersionService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<VersionService>());

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

// Register Rescue widget documentation examples
builder.Services.AddSingleton<IGalleryExample, RescueBasicExample>();
builder.Services.AddSingleton<IGalleryExample, RescueCustomFallbackExample>();
builder.Services.AddSingleton<IGalleryExample, RescueEventHandlersExample>();

// Register Responsive widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ResponsiveBasicExample>();

// Register Getting Started tutorial examples
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep1Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep2Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep3Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep4Example>();
builder.Services.AddSingleton<IGalleryExample, GettingStartedStep5Example>();

// Register Text widget documentation examples
builder.Services.AddSingleton<IGalleryExample, TextBasicExample>();
builder.Services.AddSingleton<IGalleryExample, TextOverflowExample>();
builder.Services.AddSingleton<IGalleryExample, TextCompleteExample>();

// Register Hyperlink widget documentation examples
builder.Services.AddSingleton<IGalleryExample, HyperlinkBasicExample>();
builder.Services.AddSingleton<IGalleryExample, HyperlinkOverflowExample>();
builder.Services.AddSingleton<IGalleryExample, HyperlinkClickExample>();

// Register InfoBar widget documentation examples
builder.Services.AddSingleton<IGalleryExample, InfoBarBasicExample>();
builder.Services.AddSingleton<IGalleryExample, InfoBarSpacerExample>();
builder.Services.AddSingleton<IGalleryExample, InfoBarSpinnerExample>();

// Register Progress widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ProgressBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ProgressIndeterminateExample>();

// Register Slider widget documentation examples
builder.Services.AddSingleton<IGalleryExample, SliderBasicExample>();
builder.Services.AddSingleton<IGalleryExample, SliderAudioMixerExample>();

// Register Spinner widget documentation examples
builder.Services.AddSingleton<IGalleryExample, SpinnerBasicExample>();
builder.Services.AddSingleton<IGalleryExample, SpinnerStylesExample>();

// Register Button widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ButtonBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ButtonCounterExample>();
builder.Services.AddSingleton<IGalleryExample, ButtonAsyncExample>();

// Register List widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ListBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ListSelectionExample>();
builder.Services.AddSingleton<IGalleryExample, ListActivateExample>();
builder.Services.AddSingleton<IGalleryExample, ListLongExample>();

// Register Picker widget documentation examples
builder.Services.AddSingleton<IGalleryExample, PickerBasicExample>();
builder.Services.AddSingleton<IGalleryExample, PickerSelectionExample>();
builder.Services.AddSingleton<IGalleryExample, PickerInitialExample>();

// Register TextBox widget documentation examples
builder.Services.AddSingleton<IGalleryExample, TextBoxBasicExample>();
builder.Services.AddSingleton<IGalleryExample, TextBoxSubmitExample>();
builder.Services.AddSingleton<IGalleryExample, TextBoxFormExample>();
builder.Services.AddSingleton<IGalleryExample, TextBoxUnicodeExample>();

// Register ToggleSwitch widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ToggleSwitchBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ToggleSwitchMultiOptionExample>();
builder.Services.AddSingleton<IGalleryExample, ToggleSwitchEventExample>();
// Register Splitter widget documentation examples
builder.Services.AddSingleton<IGalleryExample, SplitterBasicExample>();
builder.Services.AddSingleton<IGalleryExample, SplitterVerticalExample>();
builder.Services.AddSingleton<IGalleryExample, SplitterNestedExample>();

// Register DragBarPanel widget documentation examples
builder.Services.AddSingleton<IGalleryExample, DragBarBasicExample>();
builder.Services.AddSingleton<IGalleryExample, DragBarVerticalExample>();
builder.Services.AddSingleton<IGalleryExample, DragBarMultiPanelExample>();

// Register ThemePanel widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ThemePanelBasicExample>();

// Register Scroll widget documentation examples
builder.Services.AddSingleton<IGalleryExample, ScrollBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ScrollHorizontalExample>();
builder.Services.AddSingleton<IGalleryExample, ScrollEventExample>();
builder.Services.AddSingleton<IGalleryExample, ScrollTrackingExample>();
builder.Services.AddSingleton<IGalleryExample, ScrollInfiniteExample>();

// Register Align widget documentation examples
builder.Services.AddSingleton<IGalleryExample, AlignDemoExample>();

// Register Terminal widget documentation examples
builder.Services.AddSingleton<IGalleryExample, TerminalBasicExample>();

// Register QrCode widget documentation examples
builder.Services.AddSingleton<IGalleryExample, QrCodeBasicExample>();
builder.Services.AddSingleton<IGalleryExample, QrCodeCustomExample>();

// Register Table widget documentation examples
builder.Services.AddSingleton<IGalleryExample, TableBasicExample>();
builder.Services.AddSingleton<IGalleryExample, TableSelectionExample>();
builder.Services.AddSingleton<IGalleryExample, TableFocusExample>();

// Split Button examples
builder.Services.AddSingleton<IGalleryExample, SplitButtonBasicExample>();
builder.Services.AddSingleton<IGalleryExample, SplitButtonMultipleExample>();

// Notification examples
builder.Services.AddSingleton<IGalleryExample, NotificationsBasicExample>();
builder.Services.AddSingleton<IGalleryExample, NotificationsActionsExample>();
builder.Services.AddSingleton<IGalleryExample, NotificationsLifecycleExample>();

// Surface examples
builder.Services.AddSingleton<IGalleryExample, SurfaceBasicExample>();
builder.Services.AddSingleton<IGalleryExample, SurfaceLayersExample>();
builder.Services.AddSingleton<IGalleryExample, SurfaceMouseExample>();

// Chart examples
builder.Services.AddSingleton<IGalleryExample, ChartColumnBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ChartColumnMultiSeriesExample>();
builder.Services.AddSingleton<IGalleryExample, ChartBarBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ChartBarGroupedExample>();
builder.Services.AddSingleton<IGalleryExample, ChartBreakdownExample>();
builder.Services.AddSingleton<IGalleryExample, ChartTimeSeriesBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ChartTimeSeriesMultiExample>();
builder.Services.AddSingleton<IGalleryExample, ChartTimeSeriesFillExample>();
builder.Services.AddSingleton<IGalleryExample, ChartTimeSeriesStackedExample>();
builder.Services.AddSingleton<IGalleryExample, ChartScatterBasicExample>();
builder.Services.AddSingleton<IGalleryExample, ChartScatterGroupedExample>();

// Tree examples
builder.Services.AddSingleton<IGalleryExample, TreeBasicExample>();
builder.Services.AddSingleton<IGalleryExample, TreeLazyLoadExample>();
builder.Services.AddSingleton<IGalleryExample, TreeMultiSelectExample>();
builder.Services.AddSingleton<IGalleryExample, TreeDataBindingExample>();

// Checkbox examples
builder.Services.AddSingleton<IGalleryExample, CheckboxBasicExample>();
builder.Services.AddSingleton<IGalleryExample, CheckboxStatesExample>();

// Icon examples
builder.Services.AddSingleton<IGalleryExample, IconBasicExample>();
builder.Services.AddSingleton<IGalleryExample, IconClickExample>();

// TabPanel examples
builder.Services.AddSingleton<IGalleryExample, TabPanelBasicExample>();
builder.Services.AddSingleton<IGalleryExample, TabPanelSelectionExample>();
builder.Services.AddSingleton<IGalleryExample, TabPanelDynamicExample>();

// Window widget documentation examples
builder.Services.AddSingleton<IGalleryExample, WindowBasicExample>();
builder.Services.AddSingleton<IGalleryExample, WindowModalExample>();
builder.Services.AddSingleton<IGalleryExample, WindowTitleActionsExample>();
builder.Services.AddSingleton<IGalleryExample, WindowResizableExample>();
builder.Services.AddSingleton<IGalleryExample, WindowPositionExample>();

var app = builder.Build();

// Enable WebSockets
app.UseWebSockets();

// Configure URL rewriting for VitePress clean URLs
var rewriteOptions = new RewriteOptions()
    .Add(context =>
    {
        var request = context.HttpContext.Request;
        var path = request.Path.Value ?? "";
        
        // Skip if it's an API endpoint, WebSocket endpoint, or already has an extension
        if (path.StartsWith("/api") || 
            path.StartsWith("/apps") || 
            path.StartsWith("/examples") ||
            path.StartsWith("/health") ||
            Path.HasExtension(path))
        {
            return;
        }
        
        // For clean URLs, try to find the corresponding .html file
        // Remove trailing slash if present
        var cleanPath = path.TrimEnd('/');
        
        // Skip root path or empty path (handled by UseDefaultFiles)
        if (string.IsNullOrEmpty(cleanPath) || cleanPath == "/")
        {
            return;
        }
        
        // Try exact .html file first (e.g., /reference/Hex1b.Widgets.ButtonWidget.html)
        var htmlPath = cleanPath + ".html";
        var fileInfo = app.Environment.WebRootFileProvider.GetFileInfo(htmlPath);
        
        if (fileInfo.Exists)
        {
            request.Path = htmlPath;
            return;
        }
        
        // Try directory with index.html (e.g., /reference/index.html for /reference/)
        var indexPath = cleanPath + "/index.html";
        fileInfo = app.Environment.WebRootFileProvider.GetFileInfo(indexPath);
        
        if (fileInfo.Exists)
        {
            request.Path = indexPath;
        }
    });

app.UseRewriter(rewriteOptions);

// Serve static files (index.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// API endpoint for version info
app.MapGet("/api/version", (IVersionService versionService) => new
{
    version = versionService.Version
});

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
    var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);
    
    // Create terminal options
    var terminalOptions = new Hex1bTerminalOptions
    {
        PresentationAdapter = presentation,
        WorkloadAdapter = workload
    };
    
    // Create the terminal that bridges presentation ↔ workload
    // Terminal auto-starts I/O pumps when presentation is provided
    using var terminal = new Hex1bTerminal(terminalOptions);
    
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
