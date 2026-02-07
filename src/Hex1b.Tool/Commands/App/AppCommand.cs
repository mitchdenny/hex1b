using System.CommandLine;

namespace Hex1b.Tool.Commands.App;

/// <summary>
/// Parent command grouping TUI-app-specific diagnostics.
/// </summary>
internal sealed class AppCommand : Command
{
    public AppCommand(
        AppTreeCommand treeCommand)
        : base("app", "TUI application diagnostics (widget tree, focus, state)")
    {
        Subcommands.Add(treeCommand);
    }
}
