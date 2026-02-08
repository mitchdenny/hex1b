using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Starts recording a terminal session to an asciinema .cast file.
/// </summary>
internal sealed class CaptureRecordingStartCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string> s_outputOption = new("--output") { Description = "Output .cast file path", Required = true };
    private static readonly Option<string?> s_titleOption = new("--title") { Description = "Recording title" };
    private static readonly Option<double?> s_idleLimitOption = new("--idle-limit") { Description = "Max idle time in seconds between frames" };

    public CaptureRecordingStartCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<CaptureRecordingStartCommand> logger)
        : base("start", "Start recording a terminal session", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_outputOption);
        Options.Add(s_titleOption);
        Options.Add(s_idleLimitOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var output = parseResult.GetValue(s_outputOption)!;
        var title = parseResult.GetValue(s_titleOption);
        var idleLimit = parseResult.GetValue(s_idleLimitOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        // Resolve to absolute path so the remote host writes to the right place
        var filePath = Path.GetFullPath(output);

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest
            {
                Method = "record-start",
                FilePath = filePath,
                Title = title,
                IdleLimit = idleLimit
            }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Failed to start recording");
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
        else
        {
            Formatter.WriteLine(response.Data ?? "Recording started");
        }

        return 0;
    }
}
