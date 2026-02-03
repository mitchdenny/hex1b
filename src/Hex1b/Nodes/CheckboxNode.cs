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
    /// The current state of the checkbox.
    /// </summary>
    public CheckboxState State { get; set; }

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
        bindings.Key(Hex1bKey.Enter).Action(Toggle, "Toggle checkbox");
        bindings.Key(Hex1bKey.Spacebar).Action(Toggle, "Toggle checkbox");
        bindings.Mouse(MouseButton.Left).Action(HandleClick, "Toggle checkbox");
    }

    private async Task Toggle(InputBindingActionContext ctx)
    {
        var previousState = State;
        // Toggle: Checked -> Unchecked, anything else -> Checked
        State = State == CheckboxState.Checked ? CheckboxState.Unchecked : CheckboxState.Checked;
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
        var boxText = State switch
        {
            CheckboxState.Checked => theme.Get(CheckboxTheme.CheckedBox),
            CheckboxState.Indeterminate => theme.Get(CheckboxTheme.IndeterminateBox),
            _ => theme.Get(CheckboxTheme.UncheckedBox)
        };
        return DisplayWidth.GetStringWidth(boxText);
    }

    public override Size Measure(Constraints constraints)
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
        var boxText = State switch
        {
            CheckboxState.Checked => checkedBox,
            CheckboxState.Indeterminate => indeterminateBox,
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
