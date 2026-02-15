using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for the Slider widget. Manages state, handles input, and renders the slider.
/// </summary>
/// <remarks>
/// <para>
/// SliderNode maintains the current value and handles keyboard/mouse input for
/// adjusting the value. The value is preserved across reconciliation cycles.
/// </para>
/// <para>
/// The slider renders as a horizontal track with a handle (knob) that indicates
/// the current position within the range.
/// </para>
/// </remarks>
/// <seealso cref="SliderWidget"/>
/// <seealso cref="Theming.SliderTheme"/>
public sealed class SliderNode : Hex1bNode
{
    /// <summary>
    /// The source widget for creating typed event args.
    /// </summary>
    public SliderWidget? SourceWidget { get; set; }

    /// <summary>
    /// The current value of the slider.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The minimum value of the range.
    /// </summary>
    public double Minimum { get; set; }

    /// <summary>
    /// The maximum value of the range.
    /// </summary>
    public double Maximum { get; set; } = 100.0;

    /// <summary>
    /// The step increment for keyboard navigation. Null means 1% of range.
    /// </summary>
    public double? Step { get; set; }

    /// <summary>
    /// The large step as a percentage of the range (for PageUp/PageDown).
    /// </summary>
    public double LargeStepPercent { get; set; } = 10.0;

    /// <summary>
    /// Callback invoked when the value changes (with full context).
    /// </summary>
    internal Func<InputBindingActionContext, double, Task>? ValueChangedAction { get; set; }

    private bool _isFocused;

    /// <inheritdoc/>
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;

    /// <inheritdoc/>
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsFocusable => true;

    /// <summary>
    /// Gets the effective step size for small increments.
    /// </summary>
    private double EffectiveStep => Step ?? (Maximum - Minimum) / 100.0;

    /// <summary>
    /// Gets the large step size for PageUp/PageDown.
    /// </summary>
    private double LargeStep => (Maximum - Minimum) * (LargeStepPercent / 100.0);

    /// <summary>
    /// Gets the current value as a percentage (0.0 to 1.0) of the range.
    /// </summary>
    public double Percentage => Maximum > Minimum
        ? Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0.0, 1.0)
        : 0.0;

    /// <inheritdoc/>
    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.LeftArrow).Action(DecreaseSmall, "Decrease");
        bindings.Key(Hex1bKey.RightArrow).Action(IncreaseSmall, "Increase");
        bindings.Key(Hex1bKey.DownArrow).Action(DecreaseSmall, "Decrease");
        bindings.Key(Hex1bKey.UpArrow).Action(IncreaseSmall, "Increase");
        bindings.Key(Hex1bKey.Home).Action(JumpToMinimum, "Minimum");
        bindings.Key(Hex1bKey.End).Action(JumpToMaximum, "Maximum");
        bindings.Key(Hex1bKey.PageUp).Action(IncreaseLarge, "Large increase");
        bindings.Key(Hex1bKey.PageDown).Action(DecreaseLarge, "Large decrease");
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Set value");
        bindings.Mouse(MouseButton.ScrollUp).Action(IncreaseSmall, "Scroll up to increase");
        bindings.Mouse(MouseButton.ScrollDown).Action(DecreaseSmall, "Scroll down to decrease");
        bindings.Drag(MouseButton.Left).Action(HandleDragStart, "Drag to adjust");
    }

    private DragHandler HandleDragStart(int startX, int startY)
    {
        // Set initial value on drag start (no context yet, value will be set on first move)
        SetValueFromLocalXSync(startX);

        return new DragHandler(
            onMove: (ctx, deltaX, deltaY) =>
            {
                // Calculate absolute position from start + delta
                var currentX = startX + deltaX;
                SetValueFromLocalXWithContext(currentX, ctx);
            }
        );
    }

    private void SetValueFromLocalXSync(int localX)
    {
        if (Bounds.Width <= 0) return;

        var percentage = Math.Clamp((double)localX / (Bounds.Width - 1), 0.0, 1.0);
        var newValue = Minimum + percentage * (Maximum - Minimum);

        // Snap to step if configured
        if (Step.HasValue && Step.Value > 0)
        {
            newValue = Math.Round((newValue - Minimum) / Step.Value) * Step.Value + Minimum;
        }

        var clampedValue = Math.Clamp(newValue, Minimum, Maximum);
        if (Math.Abs(clampedValue - Value) > double.Epsilon)
        {
            Value = clampedValue;
            MarkDirty();
        }
    }

    private void SetValueFromLocalXWithContext(int localX, InputBindingActionContext ctx)
    {
        if (Bounds.Width <= 0) return;

        var percentage = Math.Clamp((double)localX / (Bounds.Width - 1), 0.0, 1.0);
        var newValue = Minimum + percentage * (Maximum - Minimum);

        // Snap to step if configured
        if (Step.HasValue && Step.Value > 0)
        {
            newValue = Math.Round((newValue - Minimum) / Step.Value) * Step.Value + Minimum;
        }

        var clampedValue = Math.Clamp(newValue, Minimum, Maximum);
        if (Math.Abs(clampedValue - Value) > double.Epsilon)
        {
            var previousValue = Value;
            Value = clampedValue;
            MarkDirty();
            
            // Fire the value changed event with proper context
            if (ValueChangedAction != null)
            {
                _ = ValueChangedAction(ctx, previousValue);
            }
        }
    }

    private async Task DecreaseSmall(InputBindingActionContext ctx)
    {
        await SetValueAsync(Value - EffectiveStep, ctx);
    }

    private async Task IncreaseSmall(InputBindingActionContext ctx)
    {
        await SetValueAsync(Value + EffectiveStep, ctx);
    }

    private async Task DecreaseLarge(InputBindingActionContext ctx)
    {
        await SetValueAsync(Value - LargeStep, ctx);
    }

    private async Task IncreaseLarge(InputBindingActionContext ctx)
    {
        await SetValueAsync(Value + LargeStep, ctx);
    }

    private async Task JumpToMinimum(InputBindingActionContext ctx)
    {
        await SetValueAsync(Minimum, ctx);
    }

    private async Task JumpToMaximum(InputBindingActionContext ctx)
    {
        await SetValueAsync(Maximum, ctx);
    }

    private async Task HandleMouseClick(InputBindingActionContext ctx)
    {
        if (Bounds.Width <= 0) return;

        // Calculate the clicked position as a percentage of the track
        var localX = ctx.MouseX - Bounds.X;
        var percentage = Math.Clamp((double)localX / (Bounds.Width - 1), 0.0, 1.0);
        var newValue = Minimum + percentage * (Maximum - Minimum);

        // Snap to step if configured
        if (Step.HasValue && Step.Value > 0)
        {
            newValue = Math.Round(newValue / Step.Value) * Step.Value;
        }

        await SetValueAsync(newValue, ctx);
    }

    private async Task SetValueAsync(double newValue, InputBindingActionContext ctx)
    {
        var clampedValue = Math.Clamp(newValue, Minimum, Maximum);

        // Snap to step if configured
        if (Step.HasValue && Step.Value > 0)
        {
            clampedValue = Math.Round((clampedValue - Minimum) / Step.Value) * Step.Value + Minimum;
            clampedValue = Math.Clamp(clampedValue, Minimum, Maximum);
        }

        if (Math.Abs(clampedValue - Value) < double.Epsilon) return;

        var previousValue = Value;
        Value = clampedValue;
        MarkDirty();

        if (ValueChangedAction != null)
        {
            await ValueChangedAction(ctx, previousValue);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraints constraints)
    {
        // Fill available width, height is always 1
        var width = constraints.MaxWidth == int.MaxValue ? 20 : constraints.MaxWidth;
        return constraints.Constrain(new Size(width, 1));
    }

    /// <inheritdoc/>
    public override void Render(Hex1bRenderContext context)
    {
        if (Bounds.Width <= 0) return;

        var theme = context.Theme;

        // Get theme values
        var trackChar = theme.Get(SliderTheme.TrackCharacter);
        var handleChar = theme.Get(SliderTheme.HandleCharacter);
        var trackFg = IsFocused
            ? theme.Get(SliderTheme.FocusedTrackForegroundColor)
            : theme.Get(SliderTheme.TrackForegroundColor);
        var trackBg = theme.Get(SliderTheme.TrackBackgroundColor);
        var handleFg = IsFocused
            ? theme.Get(SliderTheme.FocusedHandleForegroundColor)
            : theme.Get(SliderTheme.HandleForegroundColor);
        var handleBg = IsFocused
            ? theme.Get(SliderTheme.FocusedHandleBackgroundColor)
            : theme.Get(SliderTheme.HandleBackgroundColor);

        var resetCodes = theme.GetResetToGlobalCodes();

        // Get display widths for characters
        var handleWidth = DisplayWidth.GetGraphemeWidth(handleChar.ToString());
        var trackWidth = DisplayWidth.GetGraphemeWidth(trackChar.ToString());
        
        // Calculate effective track length accounting for handle width
        // The handle occupies handleWidth cells, so we have (Bounds.Width - handleWidth) cells for track
        var trackCells = Math.Max(0, Bounds.Width - handleWidth);
        var trackCharCount = trackWidth > 0 ? trackCells / trackWidth : trackCells;
        
        // Calculate handle position within the track
        var handlePos = trackCharCount == 0 ? 0 : (int)Math.Round(Percentage * trackCharCount);
        handlePos = Math.Clamp(handlePos, 0, trackCharCount);

        // Build output string
        var output = new System.Text.StringBuilder();

        // Track colors
        var trackFgCode = trackFg.IsDefault ? "" : trackFg.ToForegroundAnsi();
        var trackBgCode = trackBg.IsDefault ? "" : trackBg.ToBackgroundAnsi();

        // Handle colors
        var handleFgCode = handleFg.IsDefault ? "" : handleFg.ToForegroundAnsi();
        var handleBgCode = handleBg.IsDefault ? "" : handleBg.ToBackgroundAnsi();

        // Render track before handle
        for (int i = 0; i < handlePos; i++)
        {
            output.Append(trackFgCode);
            output.Append(trackBgCode);
            output.Append(trackChar);
            output.Append(resetCodes);
        }
        
        // Render handle
        output.Append(handleFgCode);
        output.Append(handleBgCode);
        output.Append(handleChar);
        output.Append(resetCodes);
        
        // Render track after handle
        for (int i = handlePos; i < trackCharCount; i++)
        {
            output.Append(trackFgCode);
            output.Append(trackBgCode);
            output.Append(trackChar);
            output.Append(resetCodes);
        }

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output.ToString());
        }
        else
        {
            context.Write(output.ToString());
        }
    }
}
