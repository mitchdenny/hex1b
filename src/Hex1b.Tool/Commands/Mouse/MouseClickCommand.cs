using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Mouse;

/// <summary>
/// Sends a mouse click to a terminal at specified coordinates.
/// </summary>
internal sealed class MouseClickCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Argument<int> s_xArgument = new("x") { Description = "Column (0-based)" };
    private static readonly Argument<int> s_yArgument = new("y") { Description = "Row (0-based)" };
    private static readonly Option<string> s_buttonOption = new("--button") { DefaultValueFactory = _ => "left", Description = "Mouse button: left, right, or middle" };

    public MouseClickCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<MouseClickCommand> logger)
        : base("click", "Send a mouse click at coordinates", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Arguments.Add(s_xArgument);
        Arguments.Add(s_yArgument);
        Options.Add(s_buttonOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var x = parseResult.GetValue(s_xArgument);
        var y = parseResult.GetValue(s_yArgument);
        var button = parseResult.GetValue(s_buttonOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        var response = await _client.SendAsync(resolved.SocketPath!,
            new DiagnosticsRequest { Method = "click", X = x, Y = y, Button = button }, cancellationToken);

        if (!response.Success)
        {
            Formatter.WriteError(response.Error ?? "Click failed");
            return 1;
        }

        return 0;
    }
}
