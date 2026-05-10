using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for CheckboxWidget.
/// </summary>
public sealed class CheckboxNode : Hex1bNode
{
    /// <summary>
    /// The current state of the checkbox. The instance can either be owned by
    /// the framework (when no <see cref="CheckboxWidget.State(CheckboxState)"/> was
    /// supplied) or by a composite parent (when it was). Either way, toggle
    /// gestures mutate <see cref="CheckboxState.Value"/> in place so external
    /// observers see the change immediately.
    /// </summary>
    public CheckboxState State { get; set; } = new();

    /// <summary>
    /// Tracks the last value supplied via the widget's
    /// <see cref="CheckboxWidget.Value"/> constructor argument. Used by
    /// reconcile to drift-detect the framework-managed path. <c>null</c> while
    /// the widget is in hoisted-state mode.
    /// </summary>
    internal CheckboxValue? LastWidgetValue { get; set; }

    /// <summary>
    /// Optional label displayed after the checkbox.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// The source widget for typed event args.
    /// </summary>
    public CheckboxWidget? SourceWidget { get; set; }

    /// <summary>
    /// Callback for the toggle event.
    /// </summary>
    public Func<InputBindingActionContext, Task>? ToggledCallback { get; set; }

    // Focus tracking
    private bool _isFocused;
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

    public override bool IsFocusable => true;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.Enter).Triggers(CheckboxWidget.ToggleActionId, Toggle, "Toggle checkbox");
        bindings.Key(Hex1bKey.Spacebar).Triggers(CheckboxWidget.ToggleActionId);
        bindings.Mouse(MouseButton.Left).Triggers(CheckboxWidget.ToggleActionId, HandleClick, "Toggle checkbox");
    }

    private async Task Toggle(InputBindingActionContext ctx)
    {
        // Toggle: Checked -> Unchecked, anything else -> Checked. We mutate the
        // existing State instance's Value so a parent that lifted the state via
        // CheckboxWidget.State(...) observes the change immediately, without
        // needing an OnToggled handler to shadow-sync.
        State.Value = State.Value == CheckboxValue.Checked
            ? CheckboxValue.Unchecked
            : CheckboxValue.Checked;
        MarkDirty();

        if (ToggledCallback != null)
        {
            await ToggledCallback(ctx);
        }
    }

    private async Task HandleClick(InputBindingActionContext ctx)
    {
        // Only toggle on the visual checkbox box (e.g., "[x]" = 3 chars), not trailing space
        var localX = ctx.MouseX - Bounds.X;
        if (localX >= 0 && localX < 3)
        {
            await Toggle(ctx);
        }
    }

    /// <summary>
    /// Gets the width of the checkbox box (e.g., "[x]" = 3).
    /// </summary>
    public int GetCheckboxWidth(Hex1bTheme theme)
    {
        var boxText = State.Value switch
        {
            CheckboxValue.Checked => theme.Get(CheckboxTheme.CheckedBox),
            CheckboxValue.Indeterminate => theme.Get(CheckboxTheme.IndeterminateBox),
            _ => theme.Get(CheckboxTheme.UncheckedBox)
        };
        return DisplayWidth.GetStringWidth(boxText);
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        // Checkbox is typically 3 chars for "[x]" plus optional label
        var checkboxWidth = 3; // Default width
        var labelWidth = string.IsNullOrEmpty(Label) ? 0 : DisplayWidth.GetStringWidth(Label) + 1; // +1 for space
        var totalWidth = checkboxWidth + labelWidth;
        return constraints.Constrain(new Size(totalWidth, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;

        // Get theme values
        var checkedBox = theme.Get(CheckboxTheme.CheckedBox);
        var uncheckedBox = theme.Get(CheckboxTheme.UncheckedBox);
        var indeterminateBox = theme.Get(CheckboxTheme.IndeterminateBox);

        var fg = IsFocused
            ? theme.Get(CheckboxTheme.FocusedForegroundColor)
            : theme.Get(CheckboxTheme.ForegroundColor);
        var bg = IsFocused
            ? theme.Get(CheckboxTheme.FocusedBackgroundColor)
            : theme.Get(CheckboxTheme.BackgroundColor);

        // Select box text based on state
        var boxText = State.Value switch
        {
            CheckboxValue.Checked => checkedBox,
            CheckboxValue.Indeterminate => indeterminateBox,
            _ => uncheckedBox
        };

        // Build output
        var output = new System.Text.StringBuilder();
        output.Append(fg.ToForegroundAnsi());
        output.Append(bg.ToBackgroundAnsi());
        output.Append(boxText);

        if (!string.IsNullOrEmpty(Label))
        {
            output.Append(' ');
            output.Append(Label);
        }

        output.Append(theme.GetResetToGlobalCodes());

        // Render
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output.ToString());
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write(output.ToString());
        }
    }
}
