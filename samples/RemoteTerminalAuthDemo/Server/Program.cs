using Hex1b;
using Hex1b.Diagnostics;
using Hex1b.Widgets;
using RemoteTerminalAuthDemo.Server;

const int TerminalWidth = 80;
const int TerminalHeight = 24;

await using var diagnostics = new McpDiagnosticsPresentationFilter(
    "RemoteTerminalAuthDemo.Server");
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDimensions(TerminalWidth, TerminalHeight)
    .WithHeadless()
    .AddPresentationFilter(diagnostics)
    .WithHex1bApp(
        _ => { },
        app => context =>
            context.Center(center =>
                center.Border(
                        center.VStack(stack =>
                        [
                            stack.Text("This Hex1b app is running on the server."),
                            stack.Text(""),
                            stack.Text("The WebSocket request included a bearer token."),
                            stack.Text(""),
                            stack.Button("Stop server").OnClick(_ => app.RequestStop())
                        ]))
                    .Title("Authenticated remote terminal")
                    .FixedWidth(62)))
    .Build();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5050");

var webApp = builder.Build();
webApp.UseWebSockets();
webApp.MapGet("/", () => "Remote terminal server is running.");
using var terminalStoppedCts = new CancellationTokenSource();
webApp.Map(
    "/ws/attach",
    context => AuthenticatedTerminalEndpoint.HandleAsync(
        context,
        diagnostics,
        terminalStoppedCts.Token));

var terminalTask = RunTerminalAsync(
    terminal,
    webApp.Lifetime.ApplicationStopping,
    terminalStoppedCts);
await webApp.StartAsync();

Console.WriteLine("Remote terminal server listening on ws://localhost:5050/ws/attach");
Console.WriteLine("The endpoint accepts any non-empty bearer token for demonstration purposes.");

try
{
    return await terminalTask;
}
catch (OperationCanceledException) when (webApp.Lifetime.ApplicationStopping.IsCancellationRequested)
{
    return 0;
}
finally
{
    await webApp.StopAsync();
}

static async Task<int> RunTerminalAsync(
    Hex1bTerminal terminal,
    CancellationToken cancellationToken,
    CancellationTokenSource terminalStoppedCts)
{
    try
    {
        return await terminal.RunAsync(cancellationToken);
    }
    finally
    {
        await terminalStoppedCts.CancelAsync();
    }
}
