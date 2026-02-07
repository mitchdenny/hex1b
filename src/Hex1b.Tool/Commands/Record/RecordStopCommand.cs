using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Record;

/// <summary>
/// Stops recording a terminal session.
/// </summary>
internal sealed class RecordStopCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };

    public RecordStopCommand(
        OutputFormatter formatter,
        ILogger<RecordStopCommand> logger)
        : base("stop", "Stop recording a terminal session", formatter, logger)
    {
        Arguments.Add(s_idArgument);
    }

    protected override Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Formatter.WriteError("Recording is not yet implemented. See: https://github.com/mitchdenny/hex1b/issues/155");
        return Task.FromResult(1);
    }
}
