using System.CommandLine;

namespace Hex1b.Tool.Commands.Capture;

/// <summary>
/// Parent command grouping capture operations (screenshot and recording).
/// </summary>
internal sealed class CaptureCommand : Command
{
    public CaptureCommand(
        CaptureScreenshotCommand screenshotCommand,
        CaptureRecordingCommand recordingCommand)
        : base("capture", "Capture terminal output (screenshots and recordings)")
    {
        Subcommands.Add(screenshotCommand);
        Subcommands.Add(recordingCommand);
    }
}
