using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A horizontal toggle switch node that renders options side-by-side
/// and allows selection via left/right arrow keys.
/// </summary>
public sealed class ToggleSwitchNode : Hex1bNode
{
    public ToggleSwitchState State { get; set; } = new();
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    public override bool IsFocusable => true;

    public override Size Measure(Constraints constraints)
    {
        var options = State.Options;
        if (options.Count == 0)
        {
            return constraints.Constrain(new Size(0, 1));
        }

        // Calculate total width: each option + separators
        // Format: "[ Option1 | Option2 | Option3 ]" or "< Option1 | Option2 | Option3 >"
        // Each option has padding around it, separators between options
        // Left bracket (2) + options with padding + separators + right bracket (2)
        var totalWidth = 2; // left bracket + space
        for (int i = 0; i < options.Count; i++)
        {
            totalWidth += options[i].Length;
            if (i < options.Count - 1)
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
        var options = State.Options;
        
        if (options.Count == 0)
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
        for (int i = 0; i < options.Count; i++)
        {
            var isSelected = i == State.SelectedIndex;
            var option = options[i];

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
            if (i < options.Count - 1)
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

    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        if (!IsFocused) return InputResult.NotHandled;

        switch (keyEvent.Key)
        {
            case Hex1bKey.LeftArrow:
                State.MovePrevious();
                return InputResult.Handled;
            case Hex1bKey.RightArrow:
                State.MoveNext();
                return InputResult.Handled;
        }
        return InputResult.NotHandled;
    }
}
