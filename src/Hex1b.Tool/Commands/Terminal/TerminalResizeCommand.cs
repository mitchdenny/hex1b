using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Resizes a terminal.
/// </summary>
internal sealed class TerminalResizeCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<int?> s_widthOption = new("--width") { Description = "New width in columns" };
    private static readonly Option<int?> s_heightOption = new("--height") { Description = "New height in rows" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalResizeCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalResizeCommand> logger)
        : base("resize", "Resize a terminal", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_widthOption);
        Options.Add(s_heightOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var width = parseResult.GetValue(s_widthOption);
        var height = parseResult.GetValue(s_heightOption);

        if (width == null && height == null)
        {
            Formatter.WriteError("Specify --width and/or --height");
            return 1;
        }

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "resize", X = width, Y = height }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Resize failed");
            return 1;
        }

        Formatter.WriteLine($"Resized to {response.Width}x{response.Height}");
        return 0;
    }
}
