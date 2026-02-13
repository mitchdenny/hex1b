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
    /// the same instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Animations are automatically advanced once per frame by the reconciliation system
    /// (via <see cref="IActiveState.OnFrameAdvance"/>), and re-render scheduling is
    /// automatic via <see cref="IActiveState.IsActive"/>. This method is safe to call
    /// multiple times within the same builder — animations are never double-advanced.
    /// </para>
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
        => ctx.GetState(() => new AnimationCollection());
}
