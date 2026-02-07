using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands;

/// <summary>
/// Captures terminal screen output in various formats.
/// </summary>
internal sealed class CaptureCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string> s_formatOption = new("--format") { DefaultValueFactory = _ => "text", Description = "Output format: text, ansi, or svg" };
    private static readonly Option<string?> s_outputOption = new("--output") { Description = "Save to file instead of stdout" };
    private static readonly Option<string?> s_waitOption = new("--wait") { Description = "Wait for text to appear before capturing" };
    private static readonly Option<int> s_timeoutOption = new("--timeout") { DefaultValueFactory = _ => 30, Description = "Timeout in seconds for --wait" };

    public CaptureCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<CaptureCommand> logger)
        : base("capture", "Capture terminal screen output", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_formatOption);
        Options.Add(s_outputOption);
        Options.Add(s_waitOption);
        Options.Add(s_timeoutOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var format = parseResult.GetValue(s_formatOption)!;
        var outputPath = parseResult.GetValue(s_outputOption);
        var waitText = parseResult.GetValue(s_waitOption);
        var timeout = parseResult.GetValue(s_timeoutOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        // Wait for text if requested
        if (waitText != null)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));

            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var textResponse = await _client.SendAsync(resolved.SocketPath!,
                    new DiagnosticsRequest { Method = "capture", Format = "text" }, timeoutCts.Token);

                if (textResponse is { Success: true, Data: not null } && textResponse.Data.Contains(waitText, StringComparison.Ordinal))
                {
                    break;
                }

                await Task.Delay(250, timeoutCts.Token);
            }
        }

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "capture", Format = format }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Capture failed");
            return 1;
        }

        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, response.Data, cancellationToken);
            Formatter.WriteLine($"Saved to {outputPath}");
        }
        else if (parseResult.GetValue(RootCommand.JsonOption))
        {
            Formatter.WriteJson(new
            {
                width = response.Width,
                height = response.Height,
                format,
                data = response.Data
            });
        }
        else
        {
            Console.Write(response.Data);
        }

        return 0;
    }
}
