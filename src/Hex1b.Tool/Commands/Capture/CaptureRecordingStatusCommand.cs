using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Shows the recording status of a terminal session.
/// </summary>
internal sealed class CaptureRecordingStatusCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };

    public CaptureRecordingStatusCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<CaptureRecordingStatusCommand> logger)
        : base("status", "Show recording status of a terminal session", formatter, logger)
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
            new DiagnosticsRequest { Method = "record-status" }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Failed to get recording status");
            return 1;
        }

        if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new
            {
                recording = response.Recording,
                path = response.RecordingPath
            });
        }
        else if (response.Recording == true)
        {
            Formatter.WriteLine($"Recording: {response.RecordingPath}");
        }
        else
        {
            Formatter.WriteLine("Not recording");
        }

        return 0;
    }
}
