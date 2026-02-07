using System.CommandLine;

namespace Hex1b.Tool.Commands.Record;

/// <summary>
/// Parent command grouping asciinema recording operations.
/// </summary>
internal sealed class RecordCommand : Command
{
    public RecordCommand(
        RecordStartCommand startCommand,
        RecordStopCommand stopCommand)
        : base("record", "Manage asciinema recordings")
    {
        Subcommands.Add(startCommand);
        Subcommands.Add(stopCommand);
    }
}
