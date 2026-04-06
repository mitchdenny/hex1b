using System.Net.WebSockets;
using System.Text;
using Hex1b;

const string WorkloadLogDirectoryEnvironmentVariable = "HEX1B_QUADTERMINAL_WORKLOAD_LOG_DIR";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

if (OperatingSystem.IsWindows())
{
    var disableShim = Environment.GetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM");
    var requireShim = Environment.GetEnvironmentVariable("HEX1B_REQUIRE_WINDOWS_PTY_SHIM");

    // Default this demo to the proxy/shim PTY path on Windows while we
    // compare multi-terminal behavior. Explicit env overrides win.
    if (string.IsNullOrWhiteSpace(disableShim) && string.IsNullOrWhiteSpace(requireShim))
    {
        Environment.SetEnvironmentVariable("HEX1B_DISABLE_WINDOWS_PTY_SHIM", null);
        Environment.SetEnvironmentVariable("HEX1B_REQUIRE_WINDOWS_PTY_SHIM", "1");
    }

    if (!IsExecutableOnPath("pwsh.exe"))
    {
        throw new InvalidOperationException(
            "QuadTerminalDemo requires pwsh.exe on Windows so the four terminals can start in PowerShell 7.");
    }
}

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

var terminalConfigs = GetTerminalConfigs();

app.MapGet("/terminal-config", () => terminalConfigs.Select(config => new
{
    endpoint = config.Endpoint,
    label = config.Label,
    windowsPty = config.WindowsPty
}));

// Helper to create WebSocket terminal endpoint
async Task HandleTerminal(HttpContext context, TerminalConfig config)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    var (initialWidth, initialHeight) = GetInitialTerminalSize(context);
    await using var presentation = new WebSocketPresentationAdapter(webSocket, initialWidth, initialHeight);

    var terminalBuilder = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess(config.Command[0], config.Command[1..]);

    ConfigureWorkloadLogging(terminalBuilder, config.Endpoint);

    using var terminal = terminalBuilder.Build();

    await terminal.RunAsync(context.RequestAborted);
}

foreach (var config in terminalConfigs)
{
    app.Map(config.Endpoint, ctx => HandleTerminal(ctx, config));
}

app.Run();

return;

static TerminalConfig[] GetTerminalConfigs()
{
    if (OperatingSystem.IsWindows())
    {
        var windowsPty = new WindowsPtyConfig("conpty", Environment.OSVersion.Version.Build);
        return
        [
            new("/ws/term1", "pwsh.exe (Proxy 1)", ["pwsh.exe", "-NoLogo", "-NoProfile"], windowsPty),
            new("/ws/term2", "pwsh.exe (Proxy 2)", ["pwsh.exe", "-NoLogo", "-NoProfile"], windowsPty),
            new("/ws/term3", "pwsh.exe (Proxy 3)", ["pwsh.exe", "-NoLogo", "-NoProfile"], windowsPty),
            new("/ws/term4", "pwsh.exe (Proxy 4)", ["pwsh.exe", "-NoLogo", "-NoProfile"], windowsPty)
        ];
    }

    return
    [
        new("/ws/starwars", "Star Wars (SSH)", ["ssh", "starwarstel.net"]),
        new("/ws/cmatrix", "CMatrix", ["docker", "run", "-it", "--rm", "--log-driver", "none", "--net", "none", "--read-only", "--cap-drop=ALL", "willh/cmatrix"]),
        new("/ws/pipes", "Pipes", ["docker", "run", "--rm", "-it", "joonas/pipes.sh"]),
        new("/ws/asciiquarium", "Asciiquarium", ["docker", "run", "-it", "--rm", "vanessa/asciiquarium"])
    ];
}

static bool IsExecutableOnPath(string fileName)
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return false;
    }

    foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        try
        {
            if (File.Exists(Path.Combine(entry, fileName)))
            {
                return true;
            }
        }
        catch (ArgumentException)
        {
        }
    }

    return false;
}

static void ConfigureWorkloadLogging(Hex1bTerminalBuilder builder, string endpoint)
{
    var logDirectory = Environment.GetEnvironmentVariable(WorkloadLogDirectoryEnvironmentVariable);
    if (string.IsNullOrWhiteSpace(logDirectory))
    {
        return;
    }

    Directory.CreateDirectory(logDirectory);
    var endpointName = SanitizeFileName(endpoint.Trim('/'));
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var logPath = Path.Combine(logDirectory, $"{endpointName}-{timestamp}.workload.log");
    builder.WithWorkloadLogging(logPath, includeHexDump: true);
}

static (int Width, int Height) GetInitialTerminalSize(HttpContext context)
{
    const int defaultWidth = 80;
    const int defaultHeight = 24;

    var query = context.Request.Query;
    var width = TryParseDimension(query["cols"], minimum: 2) ?? defaultWidth;
    var height = TryParseDimension(query["rows"], minimum: 2) ?? defaultHeight;
    return (width, height);
}

static int? TryParseDimension(string? value, int minimum)
{
    if (!int.TryParse(value, out var parsed))
    {
        return null;
    }

    return parsed >= minimum ? parsed : null;
}

static string SanitizeFileName(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "terminal";
    }

    var invalidChars = Path.GetInvalidFileNameChars();
    var builder = new StringBuilder(value.Length);
    foreach (var ch in value)
    {
        builder.Append(invalidChars.Contains(ch) || ch is '/' or '\\' ? '_' : ch);
    }

    return builder.ToString();
}

internal sealed record TerminalConfig(
    string Endpoint,
    string Label,
    string[] Command,
    WindowsPtyConfig? WindowsPty = null);

internal sealed record WindowsPtyConfig(string Backend, int BuildNumber);
