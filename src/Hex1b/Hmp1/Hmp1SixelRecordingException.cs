namespace Hex1b;

/// <summary>
/// Thrown when a Sixel state recording cannot be serialized or deserialized. Never
/// thrown as a success-shaped fallback: every throw site corresponds to a specific,
/// named <see cref="Hmp1SixelRecordingFailureReason"/>.
/// </summary>
internal sealed class Hmp1SixelRecordingException(Hmp1SixelRecordingFailureReason reason, string message)
    : Exception(message)
{
    /// <summary>The specific reason the recording operation failed.</summary>
    public Hmp1SixelRecordingFailureReason Reason { get; } = reason;
}
