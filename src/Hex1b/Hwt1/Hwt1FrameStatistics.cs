namespace Hex1b;

internal sealed record Hwt1FrameStatistics(
    long WorkloadBytes, long OutputBatches, double ElapsedMs, double CaptureMs);
