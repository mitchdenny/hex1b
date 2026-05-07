using WebMuxerDemo;
using WebMuxerDemo.Cli;

// First-arg dispatch matches MuxerDemo's pattern (`args is ["--server", ...]`).
// Subcommands:
//   webmuxerdemo                       -> serve (default)
//   webmuxerdemo serve                 -> serve
//   webmuxerdemo connect               -> list discoverable sessions
//   webmuxerdemo connect --session NAME [--display-name LABEL]
//                                      -> CLI viewer over UDS
if (args.Length > 0 && args[0].Equals("connect", StringComparison.OrdinalIgnoreCase))
{
    var rest = args.AsSpan(1).ToArray();
    return await CliViewerCommand.RunAsync(rest);
}

// Strip an optional leading "serve" subcommand; everything else is treated as
// WebApplication args (e.g. --urls, --environment, etc.).
string[] webArgs = args;
if (args.Length > 0 && args[0].Equals("serve", StringComparison.OrdinalIgnoreCase))
{
    webArgs = args.AsSpan(1).ToArray();
}

return await ServeAsync(webArgs);

static async Task<int> ServeAsync(string[] args)
{
    SessionPaths.EnsureRootExists();

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
    // each session has its own PTY + UDS socket; multiple browser tabs (or CLI
    // viewers) on the same session share the same PTY via HMP1 multi-head.
    var sessions = new Dictionary<string, SessionHost>(StringComparer.Ordinal)
    {
        ["shell"] = SessionHost.Start(
            name: "shell",
            socketPath: SessionPaths.ForSession("shell"),
            shell: DefaultShell()),
    };

    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
    {
        sessions["python"] = SessionHost.Start(
            name: "python",
            socketPath: SessionPaths.ForSession("python"),
            shell: "python3",
            args: new[] { "-q" });
    }
    else if (OperatingSystem.IsWindows())
    {
        sessions["cmd"] = SessionHost.Start(
            name: "cmd",
            socketPath: SessionPaths.ForSession("cmd"),
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

    // Clean shutdown: tear down the per-session terminals and unlink only the
    // .sock files we own. We deliberately do NOT delete the well-known root
    // because other (concurrent) processes may be using it.
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        foreach (var s in sessions.Values)
        {
            try { s.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
            try { File.Delete(s.SocketPath); } catch { }
        }
    });

    Console.WriteLine();
    Console.WriteLine($"  WebMuxerDemo (serve)");
    Console.WriteLine($"  ─────────────────────────────");
    Console.WriteLine($"  UDS root: {SessionPaths.Root}");
    Console.WriteLine($"  Sessions: {string.Join(", ", sessions.Keys)}");
    Console.WriteLine();
    Console.WriteLine($"  CLI viewer:  webmuxerdemo connect --session <name>");
    Console.WriteLine();

    await app.RunAsync();
    return 0;
}
