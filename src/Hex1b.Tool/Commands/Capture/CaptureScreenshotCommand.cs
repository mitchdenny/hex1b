using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Captures a terminal screen screenshot in various formats.
/// </summary>
internal sealed class CaptureScreenshotCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string> s_formatOption = new("--format") { DefaultValueFactory = _ => "text", Description = "Output format: text, ansi, svg, html, or png" };
    private static readonly Option<string?> s_outputOption = new("--output") { Description = "Save to file instead of stdout (required for png)" };
    private static readonly Option<string?> s_waitOption = new("--wait") { Description = "Wait for text to appear before capturing" };
    private static readonly Option<int> s_timeoutOption = new("--timeout") { DefaultValueFactory = _ => 30, Description = "Timeout in seconds for --wait" };
    private static readonly Option<int> s_scrollbackOption = new("--scrollback") { DefaultValueFactory = _ => 0, Description = "Number of scrollback lines to include" };

    public CaptureScreenshotCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<CaptureScreenshotCommand> logger)
        : base("screenshot", "Capture a terminal screen screenshot", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_formatOption);
        Options.Add(s_outputOption);
        Options.Add(s_waitOption);
        Options.Add(s_timeoutOption);
        Options.Add(s_scrollbackOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var format = parseResult.GetValue(s_formatOption)!;
        var outputPath = parseResult.GetValue(s_outputOption);
        var waitText = parseResult.GetValue(s_waitOption);
        var timeout = parseResult.GetValue(s_timeoutOption);
        var scrollback = parseResult.GetValue(s_scrollbackOption);

        var isPng = string.Equals(format, "png", StringComparison.OrdinalIgnoreCase);

        if (isPng && outputPath == null)
        {
            Formatter.WriteError("--output is required when using --format png");
            return 1;
        }

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

        // For PNG, capture as SVG first then convert
        var captureFormat = isPng ? "svg" : format;

        // When rendering to PNG, resolve a monospace font that's actually installed
        // and pass it through the protocol so the SVG is generated correctly.
        string? fontFamily = isPng ? ResolveMonospaceFont() : null;

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "capture", Format = captureFormat, ScrollbackLines = scrollback > 0 ? scrollback : null, FontFamily = fontFamily }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Capture failed");
            return 1;
        }

        if (isPng)
        {
            var pngBytes = ConvertSvgToPng(response.Data!);
            await File.WriteAllBytesAsync(outputPath!, pngBytes, cancellationToken);
            Formatter.WriteLine($"Saved to {outputPath}");
        }
        else if (outputPath != null)
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

    private static byte[] ConvertSvgToPng(string svgContent) => SvgToPngConverter.Convert(svgContent);

    private static string? ResolveMonospaceFont() => SvgToPngConverter.EmbeddedFontFamily;
}
