namespace Hex1b.Animation;

/// <summary>
/// A named collection of animators associated with a StatePanelNode.
/// Provides create-or-retrieve semantics and bulk advance/dispose operations.
/// </summary>
public sealed class AnimationCollection
{
    private readonly Dictionary<string, Hex1bAnimator> _animators = new();
    private readonly AnimationTimer? _timer;
    private readonly Action? _invalidateCallback;
    private bool _timerScheduled;

    /// <summary>
    /// Creates an AnimationCollection, optionally wired to an AnimationTimer for frame scheduling.
    /// </summary>
    public AnimationCollection(AnimationTimer? timer = null, Action? invalidateCallback = null)
    {
        _timer = timer;
        _invalidateCallback = invalidateCallback;
    }

    /// <summary>
    /// Gets or creates an animator by name. On first call, the configure action is invoked
    /// to set initial properties. On subsequent calls, the same animator instance is returned.
    /// </summary>
    public T Get<T>(string name, Action<T>? configure = null) where T : Hex1bAnimator, new()
    {
        if (_animators.TryGetValue(name, out var existing) && existing is T typed)
            return typed;

        var animator = new T();
        configure?.Invoke(animator);
        _animators[name] = animator;
        return animator;
    }

    /// <summary>
    /// Advances all active animators by the given elapsed time.
    /// Schedules a timer callback if any animators are still running.
    /// </summary>
    public void AdvanceAll(TimeSpan elapsed)
    {
        var anyRunning = false;
        foreach (var animator in _animators.Values)
        {
            if (animator.IsRunning)
            {
                animator.Advance(elapsed);
                if (animator.IsRunning)
                    anyRunning = true;
            }
        }

        if (anyRunning)
            ScheduleNextFrame();
    }

    /// <summary>
    /// Returns true if any animator in the collection is currently running.
    /// </summary>
    public bool HasActiveAnimations
    {
        get
        {
            foreach (var animator in _animators.Values)
            {
                if (animator.IsRunning)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Disposes all animators and clears the collection.
    /// </summary>
    public void DisposeAll()
    {
        _animators.Clear();
        _timerScheduled = false;
    }

    private void ScheduleNextFrame()
    {
        if (_timer is null || _invalidateCallback is null || _timerScheduled)
            return;

        _timerScheduled = true;
        _timer.Schedule(_timer.MinimumInterval, () =>
        {
            _timerScheduled = false;
            _invalidateCallback.Invoke();
        });
    }
}
