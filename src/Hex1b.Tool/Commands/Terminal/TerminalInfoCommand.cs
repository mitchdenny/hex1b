using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Shows detailed information about a terminal.
/// </summary>
internal sealed class TerminalInfoCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalInfoCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalInfoCommand> logger)
        : base("info", "Show terminal details", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "info" }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Failed to get info");
            return 1;
        }

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new
            {
                id = resolved.Id,
                type = resolved.Type,
                name = response.AppName,
                pid = response.ProcessId,
                width = response.Width,
                height = response.Height,
                startTime = response.StartTime,
                uptime = response.StartTime.HasValue
                    ? (DateTimeOffset.UtcNow - response.StartTime.Value).ToString()
                    : null
            });
        }
        else
        {
            Formatter.WriteLine($"ID:     {resolved.Id}");
            Formatter.WriteLine($"Type:   {resolved.Type}");
            Formatter.WriteLine($"Name:   {response.AppName ?? "-"}");
            Formatter.WriteLine($"PID:    {response.ProcessId}");
            Formatter.WriteLine($"Size:   {response.Width}x{response.Height}");
            if (response.StartTime.HasValue)
            {
                var uptime = DateTimeOffset.UtcNow - response.StartTime.Value;
                Formatter.WriteLine($"Uptime: {FormatUptime(uptime)}");
            }
        }

        return 0;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1) return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        if (uptime.TotalMinutes >= 1) return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        return $"{uptime.Seconds}s";
    }
}
