using Hex1b;
using Hex1b.Input;
using Hex1b.Logging;
using Hex1b.Theming;
using Hex1b.Widgets;
using LoggerPanelDemo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Set up hosting with Hex1b logging
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddHex1b(out var logStore);
builder.Services.AddHostedService<LogGeneratorService>();

var host = builder.Build();

var statusMessage = "Ready";
var drawerExpanded = true;
var demoRandom = new Random();
var selectedTab = 0;

// Create a diagnostic terminal for the Console tab
using var cts = new CancellationTokenSource();
var consoleTerminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(80, 10)
    .WithPtyProcess("bash")
    .WithTerminalWidget(out var consoleHandle)
    .Build();

_ = Task.Run(async () =>
{
    try { await consoleTerminal.RunAsync(cts.Token); }
    catch (OperationCanceledException) { }
});

// Run the TUI alongside the host
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = new Hex1bTheme("LoggerPanelDemo")
            .Set(DrawerTheme.BackgroundColor, Hex1bColor.Black);
        
        return ctx =>
    {
        return ctx.VStack(outer => [
            // ── MENU BAR ──
            outer.MenuBar(m => [
                m.Menu("File", m => [
                    m.MenuItem("Clear Logs").OnActivated(_ => statusMessage = "Logs cleared"),
                    m.Separator(),
                    m.MenuItem("Quit").OnActivated(_ => app.RequestStop())
                ]),
                m.Menu("View", m => [
                    m.MenuItem("Toggle Panel").OnActivated(_ => drawerExpanded = !drawerExpanded)
                ]),
                m.Menu("Help", m => [
                    m.MenuItem("About").OnActivated(_ => statusMessage = "LoggerPanel Demo v1.0")
                ])
            ]),

            // ── WINDOW PANEL with animated background ──
            outer.WindowPanel()
                .Background(b =>
                    b.Surface(s => SlimeMoldBackground.BuildLayers(s, demoRandom))
                        .RedrawAfter(SlimeMoldBackground.RecommendedRedrawMs)
                ).Unbounded().FillHeight(3),

            // ── FLOATING DRAWER with tabbed Logs/Console (toggled with F12) ──
            outer.Drawer()
                .AsOverlay()
                .Expanded(drawerExpanded)
                .OnExpanded(() => drawerExpanded = true)
                .OnCollapsed(() => drawerExpanded = false)
                .ExpandedContent(d => [
                    d.DragBarPanel(
                        d.TabPanel(tp => [
                            tp.Tab("Logs", t => [
                                t.LoggerPanel(logStore).Fill()
                            ]).Selected(selectedTab == 0),
                            tp.Tab("Console", t => [
                                t.Terminal(consoleHandle).Fill()
                            ]).Selected(selectedTab == 1)
                        ])
                        .OnSelectionChanged(e => selectedTab = e.SelectedIndex)
                        .TabsOnBottom()
                        .Compact()
                        .Fill()
                    )
                    .InitialSize(14)
                    .MinSize(6)
                    .MaxSize(30)
                    .HandleEdge(DragBarEdge.Top)
                ]),

            // ── STATUS BAR ──
            outer.InfoBar([
                "Status", statusMessage,
                "Logger", "Hex1b LogStore",
                "Panel", drawerExpanded ? "F12 to hide" : "F12 to show"
            ])
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.F12).Global()
                .Action(_ => drawerExpanded = !drawerExpanded, "Toggle panel");
        });
    };
    })
    .WithMouse()
    .WithDiagnostics()
    .Build();

// Start host in background, run TUI in foreground
var hostTask = host.RunAsync();
await terminal.RunAsync();
cts.Cancel();
consoleTerminal.Dispose();

/// <summary>
/// Background service that generates log messages at various levels for demonstration.
/// </summary>
internal sealed class LogGeneratorService : BackgroundService
{
    private readonly ILogger<LogGeneratorService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private int _counter;

    public LogGeneratorService(ILogger<LogGeneratorService> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpLogger = _loggerFactory.CreateLogger("Microsoft.AspNetCore.HttpClient");
        var dbLogger = _loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Database");
        var appLogger = _loggerFactory.CreateLogger("LoggerPanelDemo.Services.OrderProcessor");

        _logger.LogInformation("LogGeneratorService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(800), stoppingToken);
            _counter++;

            switch (_counter % 7)
            {
                case 0:
                    _logger.LogDebug("Processing batch #{Counter}", _counter);
                    break;
                case 1:
                    httpLogger.LogInformation("GET /api/orders → 200 OK ({Elapsed}ms)", Random.Shared.Next(5, 200));
                    break;
                case 2:
                    dbLogger.LogDebug("Executed DbCommand ({Elapsed}ms) SELECT * FROM Orders", Random.Shared.Next(1, 50));
                    break;
                case 3:
                    appLogger.LogInformation("Order #{OrderId} processed successfully", _counter * 100);
                    break;
                case 4:
                    httpLogger.LogWarning("Slow response from upstream: {Elapsed}ms", Random.Shared.Next(500, 2000));
                    break;
                case 5:
                    appLogger.LogError("Failed to process order #{OrderId}: timeout", _counter * 100);
                    break;
                case 6:
                    _logger.LogTrace("Heartbeat tick #{Counter}", _counter);
                    break;
            }
        }
    }
}
