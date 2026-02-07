using System.CommandLine;
using Hex1b.Tool.Hosting;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Internal host command — runs a headless terminal with PTY and MCP diagnostics.
/// Not meant to be invoked directly; spawned by <see cref="TerminalStartCommand"/>.
/// </summary>
internal sealed class TerminalHostCommand : BaseCommand
{
    private static readonly Option<int> s_widthOption = new("--width") { DefaultValueFactory = _ => 120, Description = "Terminal width" };
    private static readonly Option<int> s_heightOption = new("--height") { DefaultValueFactory = _ => 30, Description = "Terminal height" };
    private static readonly Option<string?> s_cwdOption = new("--cwd") { Description = "Working directory" };
    private static readonly Option<string?> s_recordOption = new("--record") { Description = "Record to asciinema file" };
    private static readonly Argument<string[]> s_commandArgument = new("command") { Description = "Command and arguments to run" };

    public TerminalHostCommand(
        OutputFormatter formatter,
        ILogger<TerminalHostCommand> logger)
        : base("host", "Run as a terminal host process (internal)", formatter, logger)
    {
        Hidden = true;

        Options.Add(s_widthOption);
        Options.Add(s_heightOption);
        Options.Add(s_cwdOption);
        Options.Add(s_recordOption);
        Arguments.Add(s_commandArgument);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var width = parseResult.GetValue(s_widthOption);
        var height = parseResult.GetValue(s_heightOption);
        var cwd = parseResult.GetValue(s_cwdOption);
        var record = parseResult.GetValue(s_recordOption);
        var command = parseResult.GetValue(s_commandArgument) ?? ["/bin/bash"];

        var config = new TerminalHostConfig
        {
            Command = command[0],
            Arguments = command.Length > 1 ? command[1..] : [],
            Width = width,
            Height = height,
            WorkingDirectory = cwd,
            RecordPath = record
        };

        Logger.LogInformation("Starting terminal host: {Command} ({Width}x{Height})", config.Command, config.Width, config.Height);

        return await TerminalHost.RunAsync(config, cancellationToken);
    }
}
