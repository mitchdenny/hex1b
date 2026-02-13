namespace Hex1b.Animation;

/// <summary>
/// A named collection of animators associated with a StatePanelNode.
/// Provides create-or-retrieve semantics and bulk advance/dispose operations.
/// </summary>
/// <remarks>
/// Animation frame scheduling is handled by <c>StatePanelWidget.ReconcileAsync</c>,
/// which uses <c>ReconcileContext.ScheduleTimerCallback</c> to trigger re-renders
/// when animations are active. This collection is purely a data structure.
/// </remarks>
public sealed class AnimationCollection
{
    private readonly Dictionary<string, Hex1bAnimator> _animators = new();

    /// <summary>
    /// Gets or creates an animator by name. On first call, the configure action is invoked
    /// to set initial properties and the animator is optionally auto-started.
    /// On subsequent calls, the same animator instance is returned.
    /// </summary>
    /// <param name="name">Unique name for this animator within the collection.</param>
    /// <param name="configure">Called once on creation to set Duration, Easing, From/To, etc.</param>
    /// <param name="autoStart">If true, calls Start() after configure on creation. Default is true.</param>
    public T Get<T>(string name, Action<T>? configure = null, bool autoStart = true) where T : Hex1bAnimator, new()
    {
        if (_animators.TryGetValue(name, out var existing) && existing is T typed)
            return typed;

        var animator = new T();
        configure?.Invoke(animator);
        if (autoStart)
            animator.Start();
        _animators[name] = animator;
        return animator;
    }

    /// <summary>
    /// Advances all active animators by the given elapsed time.
    /// </summary>
    public void AdvanceAll(TimeSpan elapsed)
    {
        foreach (var animator in _animators.Values)
        {
            if (animator.IsRunning)
                animator.Advance(elapsed);
        }
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
    }
}
