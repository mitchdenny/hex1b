namespace Hex1b;

using Hex1b.Animation;

/// <summary>
/// Extension methods that layer animation support on top of <see cref="StatePanelContext"/>.
/// </summary>
public static class StatePanelAnimationExtensions
{
    /// <summary>
    /// Gets the animation collection for this state panel scope. On first call, a new
    /// <see cref="AnimationCollection"/> is created and stored. Subsequent calls return
    /// the same instance. Animations are automatically advanced by the elapsed time since
    /// the last reconciliation frame.
    /// </summary>
    /// <remarks>
    /// Re-render scheduling is automatic: after the builder completes, the reconciliation
    /// system checks whether any stored state (including animations) is still active and
    /// schedules a timer callback if needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.StatePanel(viewModel, sp =>
    /// {
    ///     var fade = sp.GetAnimations().Get&lt;OpacityAnimator&gt;("fade", a =>
    ///     {
    ///         a.Duration = TimeSpan.FromMilliseconds(300);
    ///     });
    ///     return sp.Text($"Opacity: {fade.Value:F2}");
    /// });
    /// </code>
    /// </example>
    public static AnimationCollection GetAnimations(this StatePanelContext ctx)
    {
        var animations = ctx.GetState(() => new AnimationCollection());

        if (ctx.Elapsed > TimeSpan.Zero && animations.HasActiveAnimations)
        {
            animations.AdvanceAll(ctx.Elapsed);
        }

        return animations;
    }
}
