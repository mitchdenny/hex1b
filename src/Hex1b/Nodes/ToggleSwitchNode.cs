using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A horizontal toggle switch node that renders options side-by-side
/// and allows selection via left/right arrow keys.
/// Selection state is owned by this node and preserved across reconciliation.
/// </summary>
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

        // Layout: " OptA │ OptB │ OptC " — 1 cell of left padding,
        // each option, with " │ " (3 cells) separators, 1 cell of
        // right padding. We only treat clicks on an option label as
        // a hit; clicks on the surrounding padding/separators are
        // ignored.
        var localX = ctx.MouseX - Bounds.X;
        var currentX = 1; // Skip the leading padding cell
        
        for (int i = 0; i < Options.Count; i++)
        {
            var optionWidth = Options[i].Length;
            var endX = currentX + optionWidth;
            
            if (localX >= currentX && localX < endX)
            {
                var previousIndex = SelectedIndex;
                SelectedIndex = i;
                MarkDirty();
                
                // Fire selection changed if the selection actually changed
                if (previousIndex != SelectedIndex && SelectionChangedAction != null)
                {
                    await SelectionChangedAction(ctx);
                }
                return;
            }
            
            // Move past this option and the " │ " separator (3 chars)
            currentX = endX + 3;
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
    /// Handles mouse click by selecting the option at the clicked X position.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        if (Options.Count == 0) return InputResult.NotHandled;

        // Layout: " OptA │ OptB │ OptC " — 1 cell of left padding,
        // option labels, " │ " (3 cells) separators between them,
        // 1 cell of right padding.
        var currentX = 1; // Skip the leading padding cell
        
        for (int i = 0; i < Options.Count; i++)
        {
            var optionWidth = Options[i].Length;
            var endX = currentX + optionWidth;
            
            if (localX >= currentX && localX < endX)
            {
                SelectedIndex = i;
                MarkDirty();
                return InputResult.Handled;
            }
            
            // Move past this option and the " │ " separator (3 chars)
            currentX = endX + 3;
        }
        
        return InputResult.NotHandled;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (Options.Count == 0)
        {
            return constraints.Constrain(new Size(0, 1));
        }

        // Layout: " OptA │ OptB │ OptC "
        // Width = 1 (left padding) + Σoption_widths + (n-1) * 3 (" │ ")
        //       + 1 (right padding)
        var totalWidth = 1; // left padding
        for (int i = 0; i < Options.Count; i++)
        {
            totalWidth += Options[i].Length;
            if (i < Options.Count - 1)
            {
                totalWidth += 3; // " │ " separator
            }
        }
        totalWidth += 1; // right padding

        return constraints.Constrain(new Size(totalWidth, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;

        if (Options.Count == 0)
        {
            return;
        }

        // Get theme values
        var separator = theme.Get(ToggleSwitchTheme.Separator);
        var fillBg = IsFocused
            ? theme.Get(ToggleSwitchTheme.FocusedFillBackgroundColor)
            : theme.Get(ToggleSwitchTheme.FillBackgroundColor);
        var focusedSelectedFg = theme.Get(ToggleSwitchTheme.FocusedSelectedForegroundColor);
        var focusedSelectedBg = theme.Get(ToggleSwitchTheme.FocusedSelectedBackgroundColor);
        var unfocusedSelectedFg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedForegroundColor);
        var unfocusedSelectedBg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor);
        var unselectedFg = theme.Get(ToggleSwitchTheme.UnselectedForegroundColor);
        var unselectedBgRaw = theme.Get(ToggleSwitchTheme.UnselectedBackgroundColor);
        // Treat the Default sentinel as "follow the field fill background"
        // so unselected segments blend into the surrounding chip.
        var unselectedBg = unselectedBgRaw.IsDefault ? fillBg : unselectedBgRaw;

        var resetToGlobal = theme.GetResetToGlobalCodes();
        var globalColors = theme.GetGlobalColorCodes();
        var fillBgAnsi = fillBg.ToBackgroundAnsi();

        // Build the output string
        var output = new System.Text.StringBuilder();

        // Open with the leading padding cell on the field fill so the
        // chip extends to the left edge of the bounds.
        output.Append($"{globalColors}{fillBgAnsi} {resetToGlobal}");

        // Render each option
        for (int i = 0; i < Options.Count; i++)
        {
            var isSelected = i == SelectedIndex;
            var option = Options[i];

            if (isSelected && IsFocused)
            {
                // Selected + focused: brightest highlight (white-on-black by default)
                var fgCode = focusedSelectedFg.IsDefault ? "" : focusedSelectedFg.ToForegroundAnsi();
                var bgCode = focusedSelectedBg.IsDefault ? "" : focusedSelectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToGlobal}");
            }
            else if (isSelected)
            {
                // Selected + unfocused: subtler highlight (gray band by default)
                var fgCode = unfocusedSelectedFg.IsDefault ? globalColors : unfocusedSelectedFg.ToForegroundAnsi();
                var bgCode = unfocusedSelectedBg.IsDefault ? "" : unfocusedSelectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToGlobal}");
            }
            else
            {
                // Unselected: text on field fill (or themed unselected bg)
                var fgCode = unselectedFg.IsDefault ? globalColors : unselectedFg.ToForegroundAnsi();
                var bgCode = unselectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToGlobal}");
            }

            // Add separator between options on the field fill so the
            // chip stays continuous behind the divider glyph.
            if (i < Options.Count - 1)
            {
                output.Append($"{globalColors}{fillBgAnsi}{separator}{resetToGlobal}");
            }
        }

        // Close with the trailing padding cell on the field fill.
        output.Append($"{globalColors}{fillBgAnsi} {resetToGlobal}");

        // Write to context
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
