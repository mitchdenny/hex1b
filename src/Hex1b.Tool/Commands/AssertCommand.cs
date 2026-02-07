using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands;

/// <summary>
/// Asserts on terminal content for scripting and CI.
/// </summary>
internal sealed class AssertCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string?> s_textPresentOption = new("--text-present") { Description = "Assert text is visible on screen" };
    private static readonly Option<string?> s_textAbsentOption = new("--text-absent") { Description = "Assert text is NOT visible on screen" };
    private static readonly Option<int> s_timeoutOption = new("--timeout") { DefaultValueFactory = _ => 5, Description = "How long to wait in seconds" };

    public AssertCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<AssertCommand> logger)
        : base("assert", "Assert on terminal content for scripting and CI", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_textPresentOption);
        Options.Add(s_textAbsentOption);
        Options.Add(s_timeoutOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var textPresent = parseResult.GetValue(s_textPresentOption);
        var textAbsent = parseResult.GetValue(s_textAbsentOption);
        var timeout = parseResult.GetValue(s_timeoutOption);

        if (textPresent == null && textAbsent == null)
        {
            Formatter.WriteError("Specify --text-present or --text-absent");
            return 1;
        }

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout));

        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                var response = await _client.SendAsync(resolved.SocketPath!,
                    new DiagnosticsRequest { Method = "capture", Format = "text" }, timeoutCts.Token);

                if (!response.Success)
                {
                    Formatter.WriteError(response.Error ?? "Capture failed");
                    return 1;
                }

                var screenText = response.Data ?? "";

                if (textPresent != null && screenText.Contains(textPresent, StringComparison.Ordinal))
                {
                    return 0; // Assertion passed
                }

                if (textAbsent != null && !screenText.Contains(textAbsent, StringComparison.Ordinal))
                {
                    return 0; // Assertion passed
                }

                await Task.Delay(250, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Timeout — assertion failed
        }

        if (textPresent != null)
        {
            Formatter.WriteError($"Assertion failed: text '{textPresent}' not found within {timeout}s");
        }
        else
        {
            Formatter.WriteError($"Assertion failed: text '{textAbsent}' still present after {timeout}s");
        }

        return 1;
    }
}
