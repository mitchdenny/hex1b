using WebMuxerDemo;

// Spin up a per-run temp directory for UDS socket paths so multiple instances
// of the demo don't collide and so paths stay short on macOS / Linux.
var sockRoot = Directory.CreateTempSubdirectory("hex1b-webmux-").FullName;

string DefaultShell()
{
    if (OperatingSystem.IsWindows())
    {
        return "pwsh.exe";
    }

    var shell = Environment.GetEnvironmentVariable("SHELL");
    return !string.IsNullOrEmpty(shell) ? shell : "bash";
}

// Two named sessions to demonstrate multi-session in addition to multi-head:
// each session has its own PTY + UDS socket; multiple browser tabs on the same
// session share the same PTY via HMP1 multi-head.
var sessions = new Dictionary<string, SessionHost>(StringComparer.Ordinal)
{
    ["shell"] = SessionHost.Start(
        name: "shell",
        socketPath: Path.Combine(sockRoot, "shell.sock"),
        shell: DefaultShell()),
};

if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
{
    // Add a second session as a useful contrast.
    sessions["python"] = SessionHost.Start(
        name: "python",
        socketPath: Path.Combine(sockRoot, "python.sock"),
        shell: "python3",
        args: new[] { "-q" });
}
else if (OperatingSystem.IsWindows())
{
    sessions["cmd"] = SessionHost.Start(
        name: "cmd",
        socketPath: Path.Combine(sockRoot, "cmd.sock"),
        shell: "cmd.exe");
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IReadOnlyDictionary<string, SessionHost>>(sessions);

var app = builder.Build();
app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/sessions", (IReadOnlyDictionary<string, SessionHost> all) =>
    Results.Json(all.Keys.Select(k => new { name = k }).ToArray()));

app.MapGet("/ws/{name}", async (HttpContext ctx, string name, IReadOnlyDictionary<string, SessionHost> all) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsync("WebSocket required.");
        return;
    }
    if (!all.TryGetValue(name, out var session))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsync($"Unknown session '{name}'.");
        return;
    }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    await WebSocketProxy.BridgeAsync(ws, session, ctx.RequestAborted);
});

// Clean shutdown: tear down the common terminals.
app.Lifetime.ApplicationStopping.Register(() =>
{
    foreach (var s in sessions.Values)
    {
        try { s.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
    }
    try { Directory.Delete(sockRoot, recursive: true); } catch { }
});

Console.WriteLine();
Console.WriteLine($"  WebMuxerDemo");
Console.WriteLine($"  ─────────────────────────────");
Console.WriteLine($"  UDS root: {sockRoot}");
Console.WriteLine($"  Sessions: {string.Join(", ", sessions.Keys)}");
Console.WriteLine();

app.Run();
