using System.CommandLine;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Parent command grouping terminal lifecycle and metadata operations.
/// </summary>
internal sealed class TerminalCommand : Command
{
    public TerminalCommand(
        TerminalListCommand listCommand,
        TerminalStartCommand startCommand,
        TerminalStopCommand stopCommand,
        TerminalInfoCommand infoCommand,
        TerminalResizeCommand resizeCommand,
        TerminalHostCommand hostCommand)
        : base("terminal", "Manage terminal lifecycle, metadata, and connections")
    {
        Subcommands.Add(listCommand);
        Subcommands.Add(startCommand);
        Subcommands.Add(stopCommand);
        Subcommands.Add(infoCommand);
        Subcommands.Add(resizeCommand);
        Subcommands.Add(hostCommand);
    }
}
