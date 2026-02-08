using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Removes stale socket files from exited terminals.
/// </summary>
internal sealed class TerminalCleanCommand : BaseCommand
{
    private readonly TerminalDiscovery _discovery;
    private readonly TerminalClient _client;

    public TerminalCleanCommand(
        TerminalDiscovery discovery,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalCleanCommand> logger)
        : base("clean", "Remove stale terminal sockets", formatter, logger)
    {
        _discovery = discovery;
        _client = client;
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var removed = await _discovery.CleanStaleAsync(_client, cancellationToken);

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new { removed });
        }
        else if (removed > 0)
        {
            Formatter.WriteLine($"Removed {removed} stale socket(s).");
        }
        else
        {
            Formatter.WriteLine("No stale sockets found.");
        }

        return 0;
    }
}
