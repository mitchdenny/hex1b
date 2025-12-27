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
        bindings.Key(Hex1bKey.LeftArrow).Action(MovePreviousWithEvent, "Previous option");
        bindings.Key(Hex1bKey.RightArrow).Action(MoveNextWithEvent, "Next option");
        bindings.Mouse(MouseButton.Left).Action(MouseSelectWithEvent, "Select option");
    }

    private async Task MouseSelectWithEvent(InputBindingActionContext ctx)
    {
        if (Options.Count == 0) return;

        // Calculate which option was clicked based on the X position
        // Format: "[ Option1 | Option2 | Option3 ]"
        var localX = ctx.MouseX - Bounds.X;
        var currentX = 2; // Start after "[ "
        
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
            
            // Move past this option and the separator " | " (3 chars)
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

        // Calculate which option was clicked based on the X position
        // Format: "[ Option1 | Option2 | Option3 ]"
        // Left bracket takes 2 chars: "[ "
        var currentX = 2; // Start after "[ "
        
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
            
            // Move past this option and the separator " | " (3 chars)
            currentX = endX + 3;
        }
        
        return InputResult.NotHandled;
    }

    public override Size Measure(Constraints constraints)
    {
        if (Options.Count == 0)
        {
            return constraints.Constrain(new Size(0, 1));
        }

        // Calculate total width: each option + separators
        // Format: "[ Option1 | Option2 | Option3 ]" or "< Option1 | Option2 | Option3 >"
        // Each option has padding around it, separators between options
        // Left bracket (2) + options with padding + separators + right bracket (2)
        var totalWidth = 2; // left bracket + space
        for (int i = 0; i < Options.Count; i++)
        {
            totalWidth += Options[i].Length;
            if (i < Options.Count - 1)
            {
                totalWidth += 3; // " | " separator
            }
        }
        totalWidth += 2; // space + right bracket

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
        var leftBracket = theme.Get(ToggleSwitchTheme.LeftBracket);
        var rightBracket = theme.Get(ToggleSwitchTheme.RightBracket);
        var separator = theme.Get(ToggleSwitchTheme.Separator);
        var focusedSelectedFg = theme.Get(ToggleSwitchTheme.FocusedSelectedForegroundColor);
        var focusedSelectedBg = theme.Get(ToggleSwitchTheme.FocusedSelectedBackgroundColor);
        var unfocusedSelectedFg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedForegroundColor);
        var unfocusedSelectedBg = theme.Get(ToggleSwitchTheme.UnfocusedSelectedBackgroundColor);
        var unselectedFg = theme.Get(ToggleSwitchTheme.UnselectedForegroundColor);
        var unselectedBg = theme.Get(ToggleSwitchTheme.UnselectedBackgroundColor);
        var focusedBracketFg = theme.Get(ToggleSwitchTheme.FocusedBracketForegroundColor);
        var focusedBracketBg = theme.Get(ToggleSwitchTheme.FocusedBracketBackgroundColor);
        var unfocusedBracketFg = theme.Get(ToggleSwitchTheme.UnfocusedBracketForegroundColor);
        var unfocusedBracketBg = theme.Get(ToggleSwitchTheme.UnfocusedBracketBackgroundColor);

        var resetToInherited = context.GetResetToInheritedCodes();
        var inheritedColors = context.GetInheritedColorCodes();

        // Build the output string
        var output = new System.Text.StringBuilder();

        // Render left bracket with focus-dependent styling
        if (IsFocused)
        {
            var bracketFgCode = focusedBracketFg.IsDefault ? "" : focusedBracketFg.ToForegroundAnsi();
            var bracketBgCode = focusedBracketBg.IsDefault ? "" : focusedBracketBg.ToBackgroundAnsi();
            output.Append($"{bracketFgCode}{bracketBgCode}{leftBracket}{resetToInherited}");
        }
        else
        {
            var bracketFgCode = unfocusedBracketFg.IsDefault ? inheritedColors : unfocusedBracketFg.ToForegroundAnsi();
            var bracketBgCode = unfocusedBracketBg.IsDefault ? "" : unfocusedBracketBg.ToBackgroundAnsi();
            output.Append($"{bracketFgCode}{bracketBgCode}{leftBracket}{resetToInherited}");
        }

        // Render each option
        for (int i = 0; i < Options.Count; i++)
        {
            var isSelected = i == SelectedIndex;
            var option = Options[i];

            if (isSelected && IsFocused)
            {
                // Selected option when focused: use focused selected colors (bright highlight)
                var fgCode = focusedSelectedFg.IsDefault ? "" : focusedSelectedFg.ToForegroundAnsi();
                var bgCode = focusedSelectedBg.IsDefault ? "" : focusedSelectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToInherited}");
            }
            else if (isSelected)
            {
                // Selected option when unfocused: use unfocused selected colors (subtle highlight)
                var fgCode = unfocusedSelectedFg.IsDefault ? inheritedColors : unfocusedSelectedFg.ToForegroundAnsi();
                var bgCode = unfocusedSelectedBg.IsDefault ? "" : unfocusedSelectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToInherited}");
            }
            else
            {
                // Unselected option: use unselected colors or inherited
                var fgCode = unselectedFg.IsDefault ? inheritedColors : unselectedFg.ToForegroundAnsi();
                var bgCode = unselectedBg.IsDefault ? "" : unselectedBg.ToBackgroundAnsi();
                output.Append($"{fgCode}{bgCode}{option}{resetToInherited}");
            }

            // Add separator between options
            if (i < Options.Count - 1)
            {
                output.Append($"{inheritedColors}{separator}{resetToInherited}");
            }
        }

        // Render right bracket with focus-dependent styling
        if (IsFocused)
        {
            var bracketFgCode = focusedBracketFg.IsDefault ? "" : focusedBracketFg.ToForegroundAnsi();
            var bracketBgCode = focusedBracketBg.IsDefault ? "" : focusedBracketBg.ToBackgroundAnsi();
            output.Append($"{bracketFgCode}{bracketBgCode}{rightBracket}{resetToInherited}");
        }
        else
        {
            var bracketFgCode = unfocusedBracketFg.IsDefault ? inheritedColors : unfocusedBracketFg.ToForegroundAnsi();
            var bracketBgCode = unfocusedBracketBg.IsDefault ? "" : unfocusedBracketBg.ToBackgroundAnsi();
            output.Append($"{bracketFgCode}{bracketBgCode}{rightBracket}{resetToInherited}");
        }

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
