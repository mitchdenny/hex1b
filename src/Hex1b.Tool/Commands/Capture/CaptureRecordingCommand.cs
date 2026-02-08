using System.CommandLine;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Parent command grouping recording operations (start, stop, status, playback).
/// </summary>
internal sealed class CaptureRecordingCommand : Command
{
    public CaptureRecordingCommand(
        CaptureRecordingStartCommand startCommand,
        CaptureRecordingStopCommand stopCommand,
        CaptureRecordingStatusCommand statusCommand,
        CaptureRecordingPlaybackCommand playbackCommand)
        : base("recording", "Manage asciinema recordings")
    {
        Subcommands.Add(startCommand);
        Subcommands.Add(stopCommand);
        Subcommands.Add(statusCommand);
        Subcommands.Add(playbackCommand);
    }
}
