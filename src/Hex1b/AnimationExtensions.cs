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
    /// The delay before redrawing. Clamped to minimum of 16ms (60 FPS cap).
    /// </param>
    /// <returns>The widget with the redraw delay set.</returns>
    /// <remarks>
    /// <para>
    /// This schedules a one-shot timer. When the timer fires, the widget is
    /// marked dirty and a re-render is triggered.
    /// </para>
    /// <para>
    /// For continuous animation, call this method on each widget tree build.
    /// The timer only fires once per call.
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
    /// The delay in milliseconds before redrawing. Clamped to minimum of 16ms (60 FPS cap).
    /// </param>
    /// <returns>The widget with the redraw delay set.</returns>
    public static TWidget RedrawAfter<TWidget>(this TWidget widget, int milliseconds)
        where TWidget : Hex1bWidget
        => widget.RedrawAfter(TimeSpan.FromMilliseconds(milliseconds));
}
