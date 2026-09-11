namespace Hex1b;

internal static class Hmp1SixelLimits
{
    internal const int MaximumPlacementCount = 4_096;
    internal const int MaximumImageCount = 4_096;
    internal const int MaximumDamagedCellCount = 1 << 20;
    internal const int MaximumSequenceBytes = 64 * 1024 * 1024;
    internal const int MaximumTotalPayloadBytes = 64 * 1024 * 1024;
    internal const int MaximumRecordingBytes = 72 * 1024 * 1024;
    internal const int TargetFrameBytes = 1024 * 1024;
}
