namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating Slider widgets.
/// </summary>
/// <remarks>
/// <para>
/// Sliders allow users to select a numeric value within a range by moving a
/// handle along a track. They support keyboard navigation (arrow keys, Home/End,
/// PageUp/PageDown) and mouse click-to-position.
/// </para>
/// </remarks>
/// <example>
/// <para>Simple slider with default 0-100 range:</para>
/// <code>
/// ctx.Slider(50)
/// </code>
/// <para>Slider with custom range:</para>
/// <code>
/// ctx.Slider(25, min: 0, max: 50)
/// </code>
/// <para>Slider with discrete steps:</para>
/// <code>
/// ctx.Slider(0, min: 0, max: 100, step: 10)
/// </code>
/// </example>
/// <seealso cref="SliderWidget"/>
/// <seealso cref="SliderNode"/>
public static class SliderExtensions
{
    /// <summary>
    /// Creates a slider with a 0-100 range.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="initialValue">The initial value (0-100). Default is 0.</param>
    /// <returns>A new SliderWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.Slider(50) // Slider at 50%
    /// </code>
    /// </example>
    public static SliderWidget Slider<TParent>(
        this WidgetContext<TParent> ctx,
        double initialValue = 0)
        where TParent : Hex1bWidget
        => new() { InitialValue = initialValue, Minimum = 0, Maximum = 100 };

    /// <summary>
    /// Creates a slider with a custom range.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="initialValue">The initial value.</param>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <returns>A new SliderWidget.</returns>
    /// <example>
    /// <code>
    /// ctx.Slider(5, min: 0, max: 10)
    /// </code>
    /// </example>
    public static SliderWidget Slider<TParent>(
        this WidgetContext<TParent> ctx,
        double initialValue,
        double min,
        double max)
        where TParent : Hex1bWidget
        => new() { InitialValue = initialValue, Minimum = min, Maximum = max };

    /// <summary>
    /// Creates a slider with a custom range and step increment.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="initialValue">The initial value.</param>
    /// <param name="min">The minimum value of the range.</param>
    /// <param name="max">The maximum value of the range.</param>
    /// <param name="step">The step increment for keyboard navigation.</param>
    /// <returns>A new SliderWidget.</returns>
    /// <remarks>
    /// When a step is specified, values will snap to multiples of the step.
    /// For example, with step=5, valid values are 0, 5, 10, 15, etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Slider(0, min: 0, max: 100, step: 5)
    /// </code>
    /// </example>
    public static SliderWidget Slider<TParent>(
        this WidgetContext<TParent> ctx,
        double initialValue,
        double min,
        double max,
        double step)
        where TParent : Hex1bWidget
        => new() { InitialValue = initialValue, Minimum = min, Maximum = max, Step = step };
}
