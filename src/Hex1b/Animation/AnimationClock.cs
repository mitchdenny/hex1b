namespace Hex1b.Animation;

internal struct AnimationClock(TimeProvider timeProvider)
{
    private TimeProvider _timeProvider = timeProvider;
    private long _started = timeProvider.GetTimestamp();

    internal readonly TimeSpan Elapsed => _timeProvider.GetElapsedTime(_started);

    internal void Use(TimeProvider provider)
    {
        if (ReferenceEquals(_timeProvider, provider))
            return;
        _timeProvider = provider;
        Restart();
    }

    internal void Restart() => _started = _timeProvider.GetTimestamp();
}
