namespace Hex1b;

internal sealed class KgpAnimationState
{
    private readonly KgpAnimationFrame[] _frames;

    internal KgpAnimationState(
        IReadOnlyList<KgpAnimationFrame> frames,
        int currentFrameIndex)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("Animation state requires a root frame.", nameof(frames));
        if (currentFrameIndex < 0 || currentFrameIndex >= frames.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentFrameIndex),
                currentFrameIndex,
                "The current frame index must identify a stored frame.");
        }

        _frames = [.. frames];
        CurrentFrameIndex = currentFrameIndex;

        long storageSize = 0;
        foreach (var frame in _frames)
            storageSize = checked(storageSize + frame.StorageSize);
        StorageSize = storageSize;
    }

    internal IReadOnlyList<KgpAnimationFrame> Frames => _frames;

    internal int FrameCount => _frames.Length;

    internal int CurrentFrameIndex { get; }

    internal long StorageSize { get; }

    internal KgpAnimationFrame GetFrame(int frameIndex)
        => _frames[frameIndex];
}
