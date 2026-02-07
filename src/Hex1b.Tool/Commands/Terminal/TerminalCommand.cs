using System.CommandLine;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Parent command grouping terminal lifecycle and metadata operations.
/// </summary>
internal sealed class TerminalCommand : Command
{
    public TerminalCommand(
        TerminalListCommand listCommand)
        : base("terminal", "Manage terminal lifecycle, metadata, and connections")
    {
        Subcommands.Add(listCommand);
    }
}
