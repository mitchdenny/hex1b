using Hex1b.Events;

namespace Hex1b.Widgets;

/// <summary>
/// A slider widget for selecting numeric values within a range.
/// </summary>
/// <remarks>
/// <para>
/// The slider displays a horizontal track with a movable handle. Users can adjust
/// the value using keyboard controls or mouse clicks on the track.
/// </para>
/// <para>
/// <strong>Keyboard controls:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Left/Down arrow: Decrease value by step</description></item>
/// <item><description>Right/Up arrow: Increase value by step</description></item>
/// <item><description>Home: Jump to minimum value</description></item>
/// <item><description>End: Jump to maximum value</description></item>
/// <item><description>PageUp: Increase by large step (10% of range)</description></item>
/// <item><description>PageDown: Decrease by large step (10% of range)</description></item>
/// </list>
/// <para>
/// The slider state (current value) is owned by the node and preserved across
/// re-renders. Use <see cref="InitialValue"/> to set the starting value when
/// the slider is first created.
/// </para>
/// </remarks>
/// <example>
/// <para>Simple slider (0-100):</para>
/// <code>
/// ctx.Slider(50)
///     .OnValueChanged(e =&gt; Console.WriteLine($"Value: {e.Value}"));
/// </code>
/// <para>Custom range with step:</para>
/// <code>
/// ctx.Slider(initialValue: 25, min: 0, max: 100, step: 5)
/// </code>
/// </example>
/// <seealso cref="SliderNode"/>
/// <seealso cref="SliderValueChangedEventArgs"/>
/// <seealso cref="Theming.SliderTheme"/>
public sealed record SliderWidget : Hex1bWidget
{
    /// <summary>
    /// The minimum value of the slider range.
    /// </summary>
    public double Minimum { get; init; } = 0.0;

    /// <summary>
    /// The maximum value of the slider range.
    /// </summary>
    public double Maximum { get; init; } = 100.0;

    /// <summary>
    /// The initial value when the slider is first created.
    /// </summary>
    /// <remarks>
    /// This value is only applied on initial creation. The slider preserves
    /// its value across re-renders.
    /// </remarks>
    public double InitialValue { get; init; } = 0.0;

    /// <summary>
    /// The step increment for keyboard navigation.
    /// </summary>
    /// <remarks>
    /// When null, the step is calculated as 1% of the range.
    /// When set, values snap to multiples of this step.
    /// </remarks>
    public double? Step { get; init; }

    /// <summary>
    /// The large step increment for PageUp/PageDown navigation, as a percentage of the range.
    /// </summary>
    /// <remarks>
    /// Default is 10 (meaning 10% of the range).
    /// </remarks>
    public double LargeStepPercent { get; init; } = 10.0;

    /// <summary>
    /// Handler invoked when the slider value changes.
    /// </summary>
    internal Func<SliderValueChangedEventArgs, Task>? ValueChangedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the value changes.
    /// This is called for all input types: keyboard, click, drag, and scroll wheel.
    /// </summary>
    /// <param name="handler">The handler to invoke on value change.</param>
    /// <returns>A new SliderWidget with the handler configured.</returns>
    public SliderWidget OnValueChanged(Action<SliderValueChangedEventArgs> handler)
        => this with { ValueChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the value changes.
    /// This is called for all input types: keyboard, click, drag, and scroll wheel.
    /// </summary>
    /// <param name="handler">The async handler to invoke on value change.</param>
    /// <returns>A new SliderWidget with the handler configured.</returns>
    public SliderWidget OnValueChanged(Func<SliderValueChangedEventArgs, Task> handler)
        => this with { ValueChangedHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SliderNode ?? new SliderNode();

        // Mark dirty if configuration changed
        if (node.Minimum != Minimum ||
            node.Maximum != Maximum ||
            node.Step != Step ||
            node.LargeStepPercent != LargeStepPercent)
        {
            node.MarkDirty();
        }

        // Apply initial value only on first creation
        if (context.IsNew)
        {
            node.Value = Math.Clamp(InitialValue, Minimum, Maximum);
        }

        // Clamp value if range changed
        node.Value = Math.Clamp(node.Value, Minimum, Maximum);

        node.Minimum = Minimum;
        node.Maximum = Maximum;
        node.Step = Step;
        node.LargeStepPercent = LargeStepPercent;
        node.SourceWidget = this;

        // Set up value changed handler (works for all input types including drag)
        if (ValueChangedHandler != null)
        {
            node.ValueChangedAction = (ctx, previousValue) =>
            {
                var args = new SliderValueChangedEventArgs(
                    this, node, ctx,
                    node.Value, previousValue,
                    node.Minimum, node.Maximum);
                return ValueChangedHandler(args);
            };
        }
        else
        {
            node.ValueChangedAction = null;
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(SliderNode);
}
