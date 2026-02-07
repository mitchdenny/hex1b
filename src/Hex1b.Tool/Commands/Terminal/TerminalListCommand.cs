using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Lists all discovered terminals (TUI apps and hosted terminals).
/// </summary>
internal sealed class TerminalListCommand : BaseCommand
{
    private readonly TerminalDiscovery _discovery;
    private readonly TerminalClient _client;

    public TerminalListCommand(
        TerminalDiscovery discovery,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalListCommand> logger)
        : base("list", "List all known terminals", formatter, logger)
    {
        _discovery = discovery;
        _client = client;
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var terminals = _discovery.Scan();

        if (terminals.Count == 0)
        {
            Formatter.WriteLine("No terminals found.");
            return 0;
        }

        var headers = new[] { "ID", "TYPE", "NAME", "PID", "SIZE", "UPTIME" };
        var rows = new List<string[]>();
        var reachable = new List<TerminalDiscovery.DiscoveredTerminal>();
        var staleSocketPaths = new List<string>();

        foreach (var terminal in terminals)
        {
            var info = await _client.TryProbeAsync(terminal.SocketPath, cancellationToken);
            if (info is { Success: true })
            {
                var uptime = info.StartTime.HasValue
                    ? FormatUptime(DateTimeOffset.UtcNow - info.StartTime.Value)
                    : "-";

                reachable.Add(terminal);
                rows.Add([
                    terminal.Id,
                    terminal.Type,
                    info.AppName ?? "-",
                    info.ProcessId?.ToString() ?? "-",
                    $"{info.Width}x{info.Height}",
                    uptime
                ]);
            }
            else
            {
                // Stale socket — clean it up silently
                staleSocketPaths.Add(terminal.SocketPath);
            }
        }

        // Remove stale sockets
        foreach (var path in staleSocketPaths)
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }

        if (staleSocketPaths.Count > 0)
        {
            Logger.LogDebug("Cleaned {Count} stale socket(s)", staleSocketPaths.Count);
        }

        if (rows.Count == 0)
        {
            Formatter.WriteLine("No terminals found.");
            return 0;
        }

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(reachable.Select((t, i) => new
            {
                id = t.Id,
                type = t.Type,
                socketPath = t.SocketPath,
                name = rows[i][2],
                pid = rows[i][3],
                size = rows[i][4],
                uptime = rows[i][5]
            }));
        }
        else
        {
            Formatter.WriteTable(headers, rows);
        }

        return 0;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        if (uptime.TotalMinutes >= 1)
        {
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        }

        return $"{uptime.Seconds}s";
    }
}
