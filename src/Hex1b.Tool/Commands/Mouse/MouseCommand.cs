using System.CommandLine;

namespace Hex1b.Tool.Commands.Mouse;

/// <summary>
/// Parent command grouping mouse input operations.
/// </summary>
internal sealed class MouseCommand : Command
{
    public MouseCommand(
        MouseClickCommand clickCommand)
        : base("mouse", "Send mouse input to a terminal")
    {
        Subcommands.Add(clickCommand);
    }
}
