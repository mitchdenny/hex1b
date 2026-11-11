using Hex1b.Nodes;

namespace Hex1b.Animation;

/// <summary>
/// Manages one-shot animation timers for triggering widget redraws.
/// </summary>
/// <remarks>
/// <para>
/// AnimationTimer maintains a list of scheduled callbacks and provides
/// integration with the Hex1bApp run loop. Timers are one-shot - they
/// fire once and are removed.
/// </para>
/// <para>
/// A minimum interval is enforced to prevent CPU spin from overly aggressive
/// animation requests. The default is 16ms (~60 FPS) but can be configured
/// via <see cref="Hex1bAppOptions.FrameRateLimitMs"/>.
/// </para>
/// </remarks>
public sealed class AnimationTimer
{
    /// <summary>
    /// Default minimum interval between frames (16ms = ~60 FPS).
    /// </summary>
    public static readonly TimeSpan DefaultMinimumInterval = TimeSpan.FromMilliseconds(16);
    
    /// <summary>
    /// Absolute minimum interval (1ms) to prevent CPU spin.
    /// </summary>
    public static readonly TimeSpan AbsoluteMinimumInterval = TimeSpan.FromMilliseconds(1);

    private readonly List<ScheduledTimer> _timers = new();
    private readonly object _lock = new();
    private readonly TimeSpan _minimumInterval;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<(Hex1bNode Node, bool ActiveState), ScheduledTimer> _redraws = new();
    private readonly HashSet<(Hex1bNode Node, bool ActiveState)> _visitedRedraws = new();
    private long? _redrawFrameStarted;

    internal int PendingRedrawCount
    {
        get { lock (_lock) return _redraws.Count; }
    }
    
    /// <summary>
    /// Gets the minimum interval between frames for this timer.
    /// </summary>
    public TimeSpan MinimumInterval => _minimumInterval;

    /// <summary>
    /// Creates an AnimationTimer with the default minimum interval (16ms).
    /// </summary>
    public AnimationTimer() : this(DefaultMinimumInterval)
    {
    }
    
    /// <summary>
    /// Creates an AnimationTimer with a custom minimum interval.
    /// </summary>
    /// <param name="minimumInterval">
    /// The minimum interval between frames. Values below 1ms are clamped to 1ms.
    /// </param>
    public AnimationTimer(TimeSpan minimumInterval) : this(minimumInterval, TimeProvider.System)
    {
    }

    internal AnimationTimer(TimeSpan minimumInterval, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _minimumInterval = minimumInterval < AbsoluteMinimumInterval 
            ? AbsoluteMinimumInterval 
            : minimumInterval;
    }

    /// <summary>
    /// Schedules a one-shot timer that fires after the specified delay.
    /// </summary>
    /// <param name="delay">The delay before firing. Clamped to minimum interval.</param>
    /// <param name="callback">The callback to invoke when the timer fires.</param>
    /// <remarks>
    /// The callback is invoked on the main thread during the render loop.
    /// Delays less than the minimum interval are clamped.
    /// </remarks>
    public void Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        // Enforce minimum interval
        if (delay < _minimumInterval)
            delay = _minimumInterval;

        lock (_lock)
        {
            _timers.Add(new ScheduledTimer(_timeProvider.GetTimestamp(), delay, callback));
        }
    }

    internal void BeginRedrawFrame()
    {
        lock (_lock)
        {
            _visitedRedraws.Clear();
            _redrawFrameStarted = _timeProvider.GetTimestamp();
        }
    }

    internal void ScheduleRedraw(Hex1bNode node, bool activeState, TimeSpan? delay, Action? callback)
    {
        lock (_lock)
        {
            var key = (node, activeState);
            if (delay is null)
            {
                _redraws.Remove(key);
                return;
            }

            _visitedRedraws.Add(key);
            ArgumentNullException.ThrowIfNull(callback);
            var requestedDelay = delay.Value < AbsoluteMinimumInterval ? AbsoluteMinimumInterval : delay.Value;
            var requested = new ScheduledTimer(_redrawFrameStarted ?? _timeProvider.GetTimestamp(), requestedDelay, callback);
            if (_redraws.TryGetValue(key, out var existing) && Remaining(existing) <= Remaining(requested))
                _redraws[key] = existing with { Callback = callback };
            else
                _redraws[key] = requested;
        }
    }

    internal void EndRedrawFrame()
    {
        lock (_lock)
        {
            foreach (var key in _redraws.Keys.Where(key => !_visitedRedraws.Contains(key)).ToArray())
                _redraws.Remove(key);
            _redrawFrameStarted = null;
        }
    }

    private TimeSpan Remaining(ScheduledTimer timer) =>
        timer.Delay - _timeProvider.GetElapsedTime(timer.Started);

    /// <summary>
    /// Gets the time until the next timer is due, or null if no timers are scheduled.
    /// </summary>
    /// <returns>
    /// TimeSpan until next timer, or null if no timers.
    /// Returns TimeSpan.Zero if a timer is already due.
    /// </returns>
    public TimeSpan? GetTimeUntilNextDue()
    {
        lock (_lock)
        {
            if (_timers.Count == 0 && _redraws.Count == 0)
                return null;

            var remaining = TimeSpan.MaxValue;

            foreach (var timer in _timers)
                remaining = TimeSpan.FromTicks(Math.Min(remaining.Ticks, Remaining(timer).Ticks));
            foreach (var timer in _redraws.Values)
                remaining = TimeSpan.FromTicks(Math.Min(remaining.Ticks, Remaining(timer).Ticks));

            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    /// <summary>
    /// Gets whether any timers are currently scheduled.
    /// </summary>
    public bool HasScheduledTimers
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count > 0 || _redraws.Count > 0;
            }
        }
    }

    /// <summary>
    /// Fires all timers that are due and removes them from the queue.
    /// </summary>
    /// <returns>The number of timers that were fired.</returns>
    public int FireDue()
    {
        List<Action>? callbacksToFire = null;

        lock (_lock)
        {
            if (_timers.Count == 0 && _redraws.Count == 0)
                return 0;

            callbacksToFire = new List<Action>();

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (Remaining(_timers[i]) <= TimeSpan.Zero)
                {
                    callbacksToFire.Add(_timers[i].Callback);
                    _timers.RemoveAt(i);
                }
            }
            foreach (var key in _redraws.Keys.Where(key => Remaining(_redraws[key]) <= TimeSpan.Zero).ToArray())
            {
                callbacksToFire.Add(_redraws[key].Callback);
                _redraws.Remove(key);
            }
        }

        // Fire callbacks outside of lock
        foreach (var callback in callbacksToFire)
        {
            try
            {
                callback();
            }
            catch
            {
                // Swallow exceptions from callbacks - don't break the render loop
            }
        }

        return callbacksToFire.Count;
    }

    /// <summary>
    /// Clears all scheduled timers without firing them.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _timers.Clear();
            _redraws.Clear();
            _visitedRedraws.Clear();
            _redrawFrameStarted = null;
        }
    }

    private readonly record struct ScheduledTimer(long Started, TimeSpan Delay, Action Callback);
}
