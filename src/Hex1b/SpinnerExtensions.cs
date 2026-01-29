using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Extension methods for creating Spinner widgets.
/// </summary>
/// <remarks>
/// <para>
/// Spinners display animated characters to indicate ongoing activity.
/// By default, animation is time-based and automatic - the spinner advances frames
/// based on the style's interval and schedules its own redraws.
/// </para>
/// </remarks>
/// <example>
/// <para>Time-based spinner (recommended, self-animating):</para>
/// <code>
/// ctx.Spinner()
/// </code>
/// <para>Spinner with explicit style:</para>
/// <code>
/// ctx.Spinner(SpinnerStyle.Arrow)
/// </code>
/// <para>Manual frame control (no auto-redraw):</para>
/// <code>
/// ctx.Spinner(frameIndex: myCounter)
/// </code>
/// </example>
public static class SpinnerExtensions
{
    /// <summary>
    /// Creates a self-animating spinner using the theme's default style.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <returns>A new SpinnerWidget with time-based animation and automatic redraws.</returns>
    /// <example>
    /// <code>
    /// ctx.Spinner()
    /// </code>
    /// </example>
    public static SpinnerWidget Spinner<TParent>(
        this WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
        => new();

    /// <summary>
    /// Creates a self-animating spinner with an explicit style.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="style">The spinner style to use.</param>
    /// <returns>A new SpinnerWidget with time-based animation and automatic redraws.</returns>
    /// <example>
    /// <code>
    /// ctx.Spinner(SpinnerStyle.Arrow)
    /// </code>
    /// </example>
    public static SpinnerWidget Spinner<TParent>(
        this WidgetContext<TParent> ctx,
        SpinnerStyle style)
        where TParent : Hex1bWidget
        => new() { Style = style };

    /// <summary>
    /// Creates a spinner with manual frame control using the theme's default style.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="frameIndex">The frame index to display (wraps automatically).</param>
    /// <returns>A new SpinnerWidget with manual frame control (no auto-redraw).</returns>
    /// <remarks>
    /// When using manual frame control, you are responsible for updating the frame index
    /// and calling <see cref="Hex1bApp.Invalidate"/> or using <see cref="AnimationExtensions.RedrawAfter{TWidget}(TWidget, int)"/>.
    /// </remarks>
    public static SpinnerWidget Spinner<TParent>(
        this WidgetContext<TParent> ctx,
        int frameIndex)
        where TParent : Hex1bWidget
        => new() { FrameIndex = frameIndex };

    /// <summary>
    /// Creates a spinner with manual frame control and explicit style.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="style">The spinner style to use.</param>
    /// <param name="frameIndex">The frame index to display (wraps automatically).</param>
    /// <returns>A new SpinnerWidget with manual frame control (no auto-redraw).</returns>
    /// <remarks>
    /// When using manual frame control, you are responsible for updating the frame index
    /// and calling <see cref="Hex1bApp.Invalidate"/> or using <see cref="AnimationExtensions.RedrawAfter{TWidget}(TWidget, int)"/>.
    /// </remarks>
    public static SpinnerWidget Spinner<TParent>(
        this WidgetContext<TParent> ctx,
        SpinnerStyle style,
        int frameIndex)
        where TParent : Hex1bWidget
        => new() { Style = style, FrameIndex = frameIndex };
}
