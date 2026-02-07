using System.CommandLine;
using System.CommandLine.Help;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;
using BaseRootCommand = System.CommandLine.RootCommand;

namespace Hex1b.Tool.Commands;

/// <summary>
/// The root command for the Hex1b CLI tool.
/// Receives all top-level and group commands via dependency injection.
/// </summary>
internal sealed class RootCommand : BaseRootCommand
{
    public static readonly Option<bool> JsonOption = new("--json")
    {
        Description = "Output results as JSON",
        Recursive = true
    };

    public RootCommand(
        Terminal.TerminalCommand terminalCommand,
        App.AppCommand appCommand,
        CaptureCommand captureCommand,
        KeysCommand keysCommand,
        Mouse.MouseCommand mouseCommand,
        AssertCommand assertCommand,
        Record.RecordCommand recordCommand,
        Agent.AgentCommand agentCommand)
        : base("Hex1b CLI tool for managing and interacting with terminal applications.")
    {
        Options.Add(JsonOption);

        Subcommands.Add(terminalCommand);
        Subcommands.Add(appCommand);
        Subcommands.Add(captureCommand);
        Subcommands.Add(keysCommand);
        Subcommands.Add(mouseCommand);
        Subcommands.Add(assertCommand);
        Subcommands.Add(recordCommand);
        Subcommands.Add(agentCommand);

        SetAction((parseResult, _) =>
        {
            new HelpAction().Invoke(parseResult);
            return Task.FromResult(1);
        });
    }
}
