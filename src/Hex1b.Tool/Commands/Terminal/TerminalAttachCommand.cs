using System.CommandLine;
using System.Diagnostics;
using System.Runtime.Versioning;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Attaches to a terminal, streaming its output to the local terminal
/// and forwarding local input to it. Ctrl+] to enter command mode.
/// </summary>
internal sealed class TerminalAttachCommand : BaseCommand
{
    private static readonly Argument<string?> s_idArgument = new("id") { Description = "Terminal ID (or prefix)", Arity = ArgumentArity.ZeroOrOne };
    private static readonly Option<bool> s_resizeOption = new("--resize") { Description = "Resize remote terminal to match local terminal dimensions" };
    private static readonly Option<bool> s_leadOption = new("--lead") { Description = "Claim resize leadership (only the leader's resize events control the remote terminal)" };
    private static readonly Option<bool> s_webOption = new("--web") { Description = "Attach via a web browser using xterm.js instead of the TUI" };
    private static readonly Option<int> s_webPortOption = new("--web-port") { Description = "Port for the web server (0 for random, default: 0). Only used with --web" };
    private static readonly Option<int> s_portOption = new("--port") { Description = "Connect to a WebSocket-exposed host at ws://localhost:{port}/ws/attach" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalAttachCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalAttachCommand> logger)
        : base("attach", "Attach to a terminal (Ctrl+] for commands)", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_resizeOption);
        Options.Add(s_leadOption);
        Options.Add(s_webOption);
        Options.Add(s_webPortOption);
        Options.Add(s_portOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Formatter.WriteError("Attach is only supported on Linux and macOS");
            return 1;
        }

        var id = parseResult.GetValue(s_idArgument);
        var resize = parseResult.GetValue(s_resizeOption);
        var lead = parseResult.GetValue(s_leadOption);
        var web = parseResult.GetValue(s_webOption);
        var webPort = parseResult.GetValue(s_webPortOption);
        var port = parseResult.GetValue(s_portOption);

        if (port > 0)
        {
            // WebSocket transport — no PID-based discovery needed
            var displayId = id ?? $"localhost:{port}";

            if (web)
            {
                // Open browser directly to the host's web interface
                var url = $"http://localhost:{port}";
                Console.Error.WriteLine($"Opening {url} ...");
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
                return 0;
            }

            var transport = new WebSocketAttachTransport(new Uri($"ws://localhost:{port}/ws/attach"));
            return await RunAttachAsync(transport, displayId, _client, resize, lead, cancellationToken);
        }

        // Unix socket transport — resolve terminal by ID
        if (string.IsNullOrEmpty(id))
        {
            Formatter.WriteError("Terminal ID is required (or use --port for WebSocket connection)");
            return 1;
        }

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        if (web)
        {
            return await RunWebAttachAsync(resolved.SocketPath!, resolved.Id!, _client, webPort, cancellationToken);
        }

        var socketTransport = new UnixSocketAttachTransport(resolved.SocketPath!);
        return await RunAttachAsync(socketTransport, resolved.Id!, _client, resize, lead, cancellationToken);
    }

    /// <summary>
    /// Core attach logic, usable from both the attach command and terminal start --attach.
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static async Task<int> RunAttachAsync(
        IAttachTransport transport, string displayId, TerminalClient client,
        bool resize, bool lead, CancellationToken cancellationToken)
    {
        await using var app = new AttachTuiApp(transport, displayId, client, resize, lead);
        return await app.RunAsync(cancellationToken);
    }

    /// <summary>
    /// Web-based attach: starts a Kestrel server with xterm.js frontend.
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static async Task<int> RunWebAttachAsync(
        string socketPath, string displayId, TerminalClient client,
        int port, CancellationToken cancellationToken)
    {
        await using var app = new AttachWebApp(socketPath, displayId, client, port);
        return await app.RunAsync(cancellationToken);
    }
}
