using System.CommandLine;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Record;

/// <summary>
/// Starts recording a terminal session to an asciinema file.
/// </summary>
internal sealed class RecordStartCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string> s_outputOption = new("--output") { Description = "Output .cast file path", Required = true };
    private static readonly Option<string?> s_titleOption = new("--title") { Description = "Recording title" };
    private static readonly Option<double?> s_idleLimitOption = new("--idle-limit") { Description = "Max idle time in seconds between frames" };

    public RecordStartCommand(
        OutputFormatter formatter,
        ILogger<RecordStartCommand> logger)
        : base("start", "Start recording a terminal session", formatter, logger)
    {
        Arguments.Add(s_idArgument);
        Options.Add(s_outputOption);
        Options.Add(s_titleOption);
        Options.Add(s_idleLimitOption);
    }

    protected override Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        Formatter.WriteError("Recording is not yet implemented. See: https://github.com/mitchdenny/hex1b/issues/155");
        return Task.FromResult(1);
    }
}
