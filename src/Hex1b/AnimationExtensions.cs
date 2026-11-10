namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for animation and timed redraws.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable widgets to request periodic redraws for animations.
/// Timers are one-shot - for continuous animation, call one of the RedrawAfter methods
/// on each widget tree build.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Animate at ~20 FPS
/// ctx.Surface(s => [...])
///     .RedrawAfter(TimeSpan.FromMilliseconds(50))
/// 
/// // Or using milliseconds shorthand
/// ctx.Surface(s => [...])
///     .RedrawAfter(50)
/// </code>
/// </example>
public static class AnimationExtensions
{
    /// <summary>
    /// Requests a redraw of this widget after the specified delay.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to redraw.</param>
    /// <param name="delay">
    /// The delay from the start of the current frame before requesting another redraw.
    /// Values below 1ms are clamped to 1ms. Rendering respects the app's frame-rate limit.
    /// </param>
    /// <returns>The widget with the redraw delay set.</returns>
    /// <remarks>
    /// <para>
    /// This schedules a one-shot timer. When the timer fires, the widget is
    /// marked dirty and a re-render is triggered.
    /// </para>
    /// <para>
    /// For continuous animation, keep this setting on each widget tree build.
    /// Rebuilding the same node retains its pending deadline rather than adding timers
    /// or postponing the redraw. Removing the setting or the node cancels its request.
    /// </para>
    /// </remarks>
    public static TWidget RedrawAfter<TWidget>(this TWidget widget, TimeSpan delay)
        where TWidget : Hex1bWidget
        => (TWidget)(widget with { RedrawDelay = delay });

    /// <summary>
    /// Requests a redraw of this widget after the specified delay in milliseconds.
    /// </summary>
    /// <typeparam name="TWidget">The widget type.</typeparam>
    /// <param name="widget">The widget to redraw.</param>
    /// <param name="milliseconds">
    /// The delay in milliseconds from the current frame's start before requesting a redraw.
    /// Values below 1ms are clamped to 1ms. Rendering respects the app's frame-rate limit.
    /// </param>
    /// <returns>The widget with the redraw delay set.</returns>
    public static TWidget RedrawAfter<TWidget>(this TWidget widget, int milliseconds)
        where TWidget : Hex1bWidget
        => widget.RedrawAfter(TimeSpan.FromMilliseconds(milliseconds));
}
