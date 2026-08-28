namespace Hex1b;

internal sealed class KgpAnimationFrame
{
    internal KgpAnimationFrame(byte[] data, int gapMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(gapMilliseconds);

        Data = data;
        GapMilliseconds = gapMilliseconds;
    }

    internal byte[] Data { get; }

    internal int GapMilliseconds { get; }

    internal long StorageSize => Data.LongLength;
}
