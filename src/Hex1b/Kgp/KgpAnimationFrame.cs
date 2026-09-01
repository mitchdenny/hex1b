namespace Hex1b;

/// <summary>
/// Describes one full RGBA32 frame in a Kitty Graphics Protocol animation.
/// </summary>
public sealed class KgpAnimationFrame
{
    /// <summary>
    /// Creates an animation frame.
    /// </summary>
    /// <param name="data">The full-frame RGBA32 pixel data.</param>
    /// <param name="gapMilliseconds">The duration of the frame in milliseconds.</param>
    public KgpAnimationFrame(
        byte[] data,
        int gapMilliseconds)
        : this(data, gapMilliseconds, KgpFormat.Rgba32)
    {
    }

    internal KgpAnimationFrame(byte[] data, int gapMilliseconds, KgpFormat format)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(gapMilliseconds);

        Data = data;
        GapMilliseconds = gapMilliseconds;
        Format = format;
    }

    /// <summary>
    /// Gets the full-frame RGBA32 pixel data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the duration of the frame in milliseconds.
    /// </summary>
    public int GapMilliseconds { get; }

    internal KgpFormat Format { get; }

    internal long StorageSize => Data.LongLength;

    internal KgpAnimationFrame WithGap(int gapMilliseconds)
        => new(Data, gapMilliseconds, Format);
}
