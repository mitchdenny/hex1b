namespace Hex1b;

/// <summary>Identifies an ordered live-terminal observation.</summary>
internal enum TerminalCaptureEventKind
{
    State,
    Output,
    Resize
}

/// <summary>
/// Carries owned ANSI text, terminal dimensions, and a monotonic time relative to attachment.
/// State replaces the receiver's state; Output continues it. Resize has empty output.
/// </summary>
internal sealed record TerminalCaptureEvent(
    TerminalCaptureEventKind Kind,
    string Output,
    int Width,
    int Height,
    long Sequence,
    TimeSpan Elapsed);
