namespace Hex1b.Animation;

/// <summary>
/// A named collection of animators. Provides create-or-retrieve semantics
/// and bulk advance/dispose operations.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="StatePanelAnimationExtensions.GetAnimations"/> to obtain an
/// instance scoped to a <see cref="StatePanelContext"/>. The extension method
/// handles time advancement and re-render scheduling automatically.
/// </para>
/// </remarks>
public sealed class AnimationCollection : IActiveState, IDisposable
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
    internal void AdvanceAll(TimeSpan elapsed)
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
    internal bool HasActiveAnimations
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

    /// <inheritdoc />
    bool IActiveState.IsActive => HasActiveAnimations;

    /// <summary>
    /// Disposes all animators and clears the collection.
    /// </summary>
    public void Dispose()
    {
        _animators.Clear();
    }
}
