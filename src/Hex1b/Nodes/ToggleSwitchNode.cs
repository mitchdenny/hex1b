using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A horizontal toggle switch node that renders options as adjacent
/// per-option chips and allows selection via left/right arrow keys
/// or mouse click. Selection state is owned by this node and
/// preserved across reconciliation.
/// </summary>
/// <remarks>
/// Each option occupies <c>1 + label_length + 1</c> cells (a single
/// padding cell on each side of the label) and is painted in its own
/// colours — the selected option gets the
/// <see cref="ToggleSwitchTheme.FocusedSelectedBackgroundColor"/> /
/// <see cref="ToggleSwitchTheme.UnfocusedSelectedBackgroundColor"/>
/// pair (depending on whether the toggle has focus), and the
/// unselected options get the
/// <see cref="ToggleSwitchTheme.UnselectedBackgroundColor"/>. There is
/// no separator glyph between options.
/// </remarks>
public sealed class ToggleSwitchNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public ToggleSwitchWidget? SourceWidget { get; set; }

    /// <summary>
    /// The available options for the toggle switch.
    /// </summary>
    public IReadOnlyList<string> Options { get; set; } = [];

    /// <summary>
    /// The currently selected option index.
    /// </summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// Gets the currently selected option, or null if no options exist.
    /// </summary>
    public string? SelectedOption => SelectedIndex >= 0 && SelectedIndex < Options.Count 
        ? Options[SelectedIndex] 
        : null;

    /// <summary>
    /// Internal action invoked when selection changes.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SelectionChangedAction { get; set; }
    
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

    /// <summary>
    /// Moves the selection to the previous option (wraps around).
    /// </summary>
    private void MovePrevious()
    {
        if (Options.Count == 0) return;
        SelectedIndex = SelectedIndex <= 0 ? Options.Count - 1 : SelectedIndex - 1;
    }

    /// <summary>
    /// Moves the selection to the next option (wraps around).
    /// </summary>
    private void MoveNext()
    {
        if (Options.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % Options.Count;
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.LeftArrow).Triggers(ToggleSwitchWidget.PreviousOptionActionId, MovePreviousWithEvent, "Previous option");
        bindings.Key(Hex1bKey.RightArrow).Triggers(ToggleSwitchWidget.NextOptionActionId, MoveNextWithEvent, "Next option");
        bindings.Mouse(MouseButton.Left).Triggers(ToggleSwitchWidget.SelectOptionActionId, MouseSelectWithEvent, "Select option");
    }

    private async Task MouseSelectWithEvent(InputBindingActionContext ctx)
    {
        if (Options.Count == 0) return;

        // Layout: per-option chips of width (1 + label + 1) tiled left
        // to right with no separator. The whole strip is clickable —
        // every cell of an option's chip (including its leading and
        // trailing padding cells) selects that option.
        var localX = ctx.MouseX - Bounds.X;
        var currentX = 0;

        for (int i = 0; i < Options.Count; i++)
        {
            var chipWidth = Options[i].Length + 2;
            var endX = currentX + chipWidth; // exclusive

            if (localX >= currentX && localX < endX)
            {
                var previousIndex = SelectedIndex;
                SelectedIndex = i;
                MarkDirty();

                if (previousIndex != SelectedIndex && SelectionChangedAction != null)
                {
                    await SelectionChangedAction(ctx);
                }
                return;
            }

            currentX = endX;
        }
    }

    private async Task MovePreviousWithEvent(InputBindingActionContext ctx)
    {
        MovePrevious();
        MarkDirty();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    private async Task MoveNextWithEvent(InputBindingActionContext ctx)
    {
        MoveNext();
        MarkDirty();
        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
    }

    /// <summary>
    /// Handles mouse click by selecting the option whose chip contains the click.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        if (Options.Count == 0) return InputResult.NotHandled;

        // Per-option chips of width (1 + label + 1) tiled left to right
        // with no separator. Padding cells are part of the chip's hit
        // region, so the entire strip is clickable.
        var currentX = 0;

        for (int i = 0; i < Options.Count; i++)
        {
            var chipWidth = Options[i].Length + 2;
            var endX = currentX + chipWidth; // exclusive

            if (localX >= currentX && localX < endX)
            {
                SelectedIndex = i;
                MarkDirty();
                return InputResult.Handled;
            }

            currentX = endX;
        }

        return InputResult.NotHandled;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Options.Count == 0)
        {
            return constraints.Constrain(new Size(0, 1));
        }

        // Per-option chips of width (1 + label + 1) tiled with no
        // separator. Total width = Σ(label_len + 2) = Σlabel_len + 2n.
        var totalWidth = 0;
        for (int i = 0; i < Options.Count; i++)
        {
            totalWidth += Options[i].Length + 2;
        }

        return constraints.Constrain(new Size(totalWidth, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;

        if (Options.Count == 0)
        {
            return;
        }

        var focusedSelectedFg = theme.Get(ToggleSwitchTheme.FocusedSelectedForegroundColor);
        var focusedSelectedBg = theme.Get(ToggleSwitchTheme.FocusedSelectedBackgroundColor);
        var unfocusedSelectedFg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedForegroundColor);
        var unfocusedSelectedBg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor);
        var unselectedFg = theme.Get(ToggleSwitchTheme.UnselectedForegroundColor);
        var unselectedBg = theme.Get(ToggleSwitchTheme.UnselectedBackgroundColor);

        var resetToGlobal = theme.GetResetToGlobalCodes();
        var globalColors = theme.GetGlobalColorCodes();

        var output = new System.Text.StringBuilder();

        for (int i = 0; i < Options.Count; i++)
        {
            var isSelected = i == SelectedIndex;
            var label = Options[i];

            Hex1bColor fg;
            Hex1bColor bg;
            if (isSelected && IsFocused)
            {
                fg = focusedSelectedFg;
                bg = focusedSelectedBg;
            }
            else if (isSelected)
            {
                fg = unfocusedSelectedFg;
                bg = unfocusedSelectedBg;
            }
            else
            {
                fg = unselectedFg;
                bg = unselectedBg;
            }

            var fgCode = fg.IsDefault ? globalColors : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? "" : bg.ToBackgroundAnsi();

            // Each chip is " label " — leading pad, label, trailing pad —
            // all painted in this option's colours.
            output.Append($"{fgCode}{bgCode} {label} {resetToGlobal}");
        }

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
