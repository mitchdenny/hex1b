using System.Collections.Immutable;

namespace Hex1b;

internal sealed class KgpAnimationState
{
    /// <summary>
    /// Maximum frames retained by one image, including its root frame.
    /// This bounds persistent-tree metadata for tiny frames that consume
    /// negligible pixel quota.
    /// </summary>
    internal const int MaximumFrameCount = 4096;

    private readonly ImmutableList<KgpAnimationFrame> _frames;

    private KgpAnimationState(
        ImmutableList<KgpAnimationFrame> frames,
        int currentFrameIndex,
        long storageSize)
    {
        _frames = frames;
        CurrentFrameIndex = currentFrameIndex;
        StorageSize = storageSize;
    }

    internal static KgpAnimationState Create(
        IReadOnlyList<KgpAnimationFrame> frames,
        int currentFrameIndex)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("Animation state requires a root frame.", nameof(frames));
        if (frames.Count > MaximumFrameCount)
        {
            throw new ArgumentException(
                $"Animation state cannot exceed {MaximumFrameCount} frames.",
                nameof(frames));
        }
        if (currentFrameIndex < 0 || currentFrameIndex >= frames.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentFrameIndex),
                currentFrameIndex,
                "The current frame index must identify a stored frame.");
        }

        long storageSize = 0;
        foreach (var frame in frames)
            storageSize = checked(storageSize + frame.StorageSize);
        return new KgpAnimationState(
            ImmutableList.CreateRange(frames),
            currentFrameIndex,
            storageSize);
    }

    internal static KgpAnimationState CreateRoot(KgpAnimationFrame root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new KgpAnimationState(
            ImmutableList.Create(root),
            currentFrameIndex: 0,
            root.StorageSize);
    }

    internal IReadOnlyList<KgpAnimationFrame> Frames => _frames;

    internal int FrameCount => _frames.Count;

    internal int CurrentFrameIndex { get; }

    internal long StorageSize { get; }

    internal KgpAnimationFrame GetFrame(int frameIndex)
        => _frames[frameIndex];

    internal KgpAnimationState AddFrame(KgpAnimationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (FrameCount >= MaximumFrameCount)
        {
            throw new InvalidOperationException(
                $"Animation state cannot exceed {MaximumFrameCount} frames.");
        }

        return new KgpAnimationState(
            _frames.Add(frame),
            CurrentFrameIndex,
            checked(StorageSize + frame.StorageSize));
    }

    internal KgpAnimationState SetFrame(
        int frameIndex,
        KgpAnimationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var existing = _frames[frameIndex];
        return new KgpAnimationState(
            _frames.SetItem(frameIndex, frame),
            CurrentFrameIndex,
            checked(StorageSize - existing.StorageSize + frame.StorageSize));
    }

    internal KgpAnimationState RemoveFrame(
        int frameIndex,
        int currentFrameIndex)
    {
        if (FrameCount == 1)
        {
            throw new InvalidOperationException(
                "Animation state cannot remove its only frame.");
        }

        var removed = _frames[frameIndex];
        return new KgpAnimationState(
            _frames.RemoveAt(frameIndex),
            currentFrameIndex,
            checked(StorageSize - removed.StorageSize));
    }
}
