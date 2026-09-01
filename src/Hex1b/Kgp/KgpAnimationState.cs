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
        long storageSize,
        long totalDurationMilliseconds,
        KgpParsedCommand.AnimationPlaybackState playbackState,
        uint maximumLoops,
        uint completedLoops,
        DateTimeOffset? currentFrameShownAt)
    {
        _frames = frames;
        CurrentFrameIndex = currentFrameIndex;
        StorageSize = storageSize;
        TotalDurationMilliseconds = totalDurationMilliseconds;
        PlaybackState = playbackState;
        MaximumLoops = maximumLoops;
        CompletedLoops = completedLoops;
        CurrentFrameShownAt = currentFrameShownAt;
    }

    internal static KgpAnimationState Create(
        IReadOnlyList<KgpAnimationFrame> frames,
        int currentFrameIndex,
        KgpParsedCommand.AnimationPlaybackState playbackState =
            KgpParsedCommand.AnimationPlaybackState.Stopped,
        uint maximumLoops = 1,
        uint completedLoops = 0,
        DateTimeOffset? currentFrameShownAt = null)
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
        long totalDurationMilliseconds = 0;
        foreach (var frame in frames)
        {
            storageSize = checked(storageSize + frame.StorageSize);
            totalDurationMilliseconds = checked(
                totalDurationMilliseconds + frame.GapMilliseconds);
        }
        return new KgpAnimationState(
            ImmutableList.CreateRange(frames),
            currentFrameIndex,
            storageSize,
            totalDurationMilliseconds,
            playbackState,
            maximumLoops,
            completedLoops,
            currentFrameShownAt);
    }

    internal static KgpAnimationState CreateRoot(KgpAnimationFrame root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new KgpAnimationState(
            ImmutableList.Create(root),
            currentFrameIndex: 0,
            root.StorageSize,
            root.GapMilliseconds,
            KgpParsedCommand.AnimationPlaybackState.Stopped,
            maximumLoops: 1,
            completedLoops: 0,
            currentFrameShownAt: null);
    }

    internal IReadOnlyList<KgpAnimationFrame> Frames => _frames;

    internal int FrameCount => _frames.Count;

    internal int CurrentFrameIndex { get; }

    internal long StorageSize { get; }

    internal long TotalDurationMilliseconds { get; }

    internal KgpParsedCommand.AnimationPlaybackState PlaybackState { get; }

    internal uint MaximumLoops { get; }

    internal uint CompletedLoops { get; }

    internal DateTimeOffset? CurrentFrameShownAt { get; }

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
            checked(StorageSize + frame.StorageSize),
            checked(TotalDurationMilliseconds + frame.GapMilliseconds),
            PlaybackState,
            MaximumLoops,
            CompletedLoops,
            CurrentFrameShownAt);
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
            checked(StorageSize - existing.StorageSize + frame.StorageSize),
            checked(
                TotalDurationMilliseconds -
                existing.GapMilliseconds +
                frame.GapMilliseconds),
            PlaybackState,
            MaximumLoops,
            CompletedLoops,
            CurrentFrameShownAt);
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
            checked(StorageSize - removed.StorageSize),
            checked(TotalDurationMilliseconds - removed.GapMilliseconds),
            PlaybackState,
            MaximumLoops,
            CompletedLoops,
            currentFrameShownAt: null);
    }

    internal KgpAnimationState SetCurrentFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        if (frameIndex == CurrentFrameIndex)
            return this;

        return new KgpAnimationState(
            _frames,
            frameIndex,
            StorageSize,
            TotalDurationMilliseconds,
            PlaybackState,
            MaximumLoops,
            CompletedLoops,
            currentFrameShownAt: null);
    }

    internal KgpAnimationState SetFrameGap(
        int frameIndex,
        int gapMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gapMilliseconds);
        return SetFrame(
            frameIndex,
            _frames[frameIndex].WithGap(gapMilliseconds));
    }

    internal KgpAnimationState SetPlaybackState(
        KgpParsedCommand.AnimationPlaybackState playbackState)
    {
        if (playbackState == KgpParsedCommand.AnimationPlaybackState.None)
            return this;

        var resetShownAt =
            PlaybackState == KgpParsedCommand.AnimationPlaybackState.Stopped ||
            playbackState == KgpParsedCommand.AnimationPlaybackState.Stopped;
        return new KgpAnimationState(
            _frames,
            CurrentFrameIndex,
            StorageSize,
            TotalDurationMilliseconds,
            playbackState,
            MaximumLoops,
            completedLoops: 0,
            resetShownAt ? null : CurrentFrameShownAt);
    }

    internal KgpAnimationState SetMaximumLoops(uint maximumLoops)
        => new(
            _frames,
            CurrentFrameIndex,
            StorageSize,
            TotalDurationMilliseconds,
            PlaybackState,
            maximumLoops,
            CompletedLoops,
            CurrentFrameShownAt);

    internal KgpAnimationState SetPlaybackPosition(
        int currentFrameIndex,
        uint completedLoops,
        DateTimeOffset? currentFrameShownAt)
    {
        if (currentFrameIndex < 0 || currentFrameIndex >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(currentFrameIndex));

        return new KgpAnimationState(
            _frames,
            currentFrameIndex,
            StorageSize,
            TotalDurationMilliseconds,
            PlaybackState,
            MaximumLoops,
            completedLoops,
            currentFrameShownAt);
    }

    internal KgpAnimationState ResetCurrentFrameTimer()
        => SetPlaybackPosition(
            CurrentFrameIndex,
            CompletedLoops,
            currentFrameShownAt: null);
}
