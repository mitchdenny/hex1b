using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Stops a hosted terminal by sending a shutdown signal.
/// </summary>
internal sealed class TerminalStopCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalStopCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalStopCommand> logger)
        : base("stop", "Stop a hosted terminal", formatter, logger)
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

        // Send shutdown request
        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "shutdown" }, cancellationToken);

        if (!response.Success)
        {
            // Try sending input of exit command as fallback
            Formatter.WriteError(response.Error ?? "Failed to stop terminal");
            return 1;
        }

        Formatter.WriteLine($"Terminal {resolved.Id} stopped");
        return 0;
    }
}
