using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Mouse;

/// <summary>
/// Sends a mouse drag from one coordinate to another.
/// </summary>
internal sealed class MouseDragCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Argument<int> s_x1Argument = new("x1") { Description = "Start column (0-based)" };
    private static readonly Argument<int> s_y1Argument = new("y1") { Description = "Start row (0-based)" };
    private static readonly Argument<int> s_x2Argument = new("x2") { Description = "End column (0-based)" };
    private static readonly Argument<int> s_y2Argument = new("y2") { Description = "End row (0-based)" };
    private static readonly Option<string> s_buttonOption = new("--button") { DefaultValueFactory = _ => "left", Description = "Mouse button: left, right, or middle" };

    public MouseDragCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<MouseDragCommand> logger)
        : base("drag", "Drag from one coordinate to another", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Arguments.Add(s_x1Argument);
        Arguments.Add(s_y1Argument);
        Arguments.Add(s_x2Argument);
        Arguments.Add(s_y2Argument);
        Options.Add(s_buttonOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var x1 = parseResult.GetValue(s_x1Argument);
        var y1 = parseResult.GetValue(s_y1Argument);
        var x2 = parseResult.GetValue(s_x2Argument);
        var y2 = parseResult.GetValue(s_y2Argument);
        var button = parseResult.GetValue(s_buttonOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "drag", X = x1, Y = y1, X2 = x2, Y2 = y2, Button = button }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Drag failed");
            return 1;
        }

        return 0;
    }
}
