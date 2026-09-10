namespace Hex1b.Automation;

internal sealed record TapePreparedCommand(
    TapeCommand Command,
    Hex1bTerminalInputSequence? Sequence = null,
    TapeCaptureControl CaptureControl = TapeCaptureControl.None,
    string? GoldenPath = null);
