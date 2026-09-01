using Hex1b;
using System.Diagnostics;

sealed class TgifSearchState : IDisposable
{
    private static readonly TimeSpan AnimationRefreshInterval = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan CopyFlashDuration = TimeSpan.FromMilliseconds(180);

    private readonly TgifDataset _dataset;
    private readonly int _resultCount;
    private readonly string _readyStatus;
    private readonly Timer _animationTimer;
    private readonly Timer _copyFlashTimer;
    private TgifResult? _softwareAnimationResult;
    private TgifResult? _flashedResult;
    private long _softwareAnimationStartedAt;

    internal TgifSearchState(TgifDataset dataset, int resultCount)
    {
        _dataset = dataset;
        _resultCount = resultCount;
        _readyStatus = $"Ready. {_dataset.Count} bundled TGIF animations.";
        Status = _readyStatus;
        _animationTimer = new Timer(
            _ => App?.Invalidate(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _copyFlashTimer = new Timer(
            _ =>
            {
                Volatile.Write(ref _flashedResult, null);
                App?.Invalidate();
            },
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    internal Hex1bApp? App { get; set; }

    internal string Query { get; private set; } = "";

    internal string Status { get; private set; }

    internal IReadOnlyList<TgifResult> Results { get; private set; } = [];

    internal void Search(string query)
    {
        StopSoftwareAnimation();
        Query = query;
        Results = _dataset.Search(query.Trim(), _resultCount);
        Status = string.IsNullOrWhiteSpace(query)
            ? _readyStatus
            : Results.Count == 0
                ? $"No bundled descriptions match \"{query.Trim()}\"."
                : $"Hover a result to animate. Showing {Results.Count} result(s).";
        App?.Invalidate();
    }

    internal void SetSoftwareAnimation(TgifResult result, bool isHovered)
    {
        if (isHovered)
        {
            _softwareAnimationResult = result;
            _softwareAnimationStartedAt = Stopwatch.GetTimestamp();
            _animationTimer.Change(TimeSpan.Zero, AnimationRefreshInterval);
        }
        else if (ReferenceEquals(_softwareAnimationResult, result))
        {
            StopSoftwareAnimation();
        }
    }

    internal KgpAnimationFrame GetSoftwareAnimationFrame(TgifResult result)
    {
        var frames = result.GetAnimationFrames();
        if (!ReferenceEquals(_softwareAnimationResult, result))
            return frames[0];

        var cycleDuration = frames.Sum(frame => (long)frame.GapMilliseconds);
        var elapsed = Stopwatch.GetElapsedTime(_softwareAnimationStartedAt);
        var position = (long)elapsed.TotalMilliseconds % cycleDuration;
        foreach (var frame in frames)
        {
            if (position < frame.GapMilliseconds)
                return frame;
            position -= frame.GapMilliseconds;
        }

        return frames[^1];
    }

    internal bool IsFlashing(TgifResult result)
        => ReferenceEquals(Volatile.Read(ref _flashedResult), result);

    internal void ShowCopiedFlash(TgifResult result, bool supportsNativeAnimation)
    {
        Volatile.Write(ref _flashedResult, result);
        Status = supportsNativeAnimation
            ? "Copied native KGP playback command."
            : "Copied Ghostty-compatible playback command. Press Ctrl+C to stop it.";
        _copyFlashTimer.Change(CopyFlashDuration, Timeout.InfiniteTimeSpan);
        App?.Invalidate();
    }

    public void Dispose()
    {
        _animationTimer.Dispose();
        _copyFlashTimer.Dispose();
    }

    private void StopSoftwareAnimation()
    {
        _softwareAnimationResult = null;
        _animationTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }
}
