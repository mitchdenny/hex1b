using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    public TextBoxState State { get; set; } = new();
    
    private bool _isFocused;
    public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

    public override bool IsFocusable => true;

    public override Size Measure(Constraints constraints)
    {
        // TextBox renders as "[text]" - 2 chars for brackets + text length (or at least 1 for cursor)
        var textWidth = Math.Max(State.Text.Length, 1);
        var width = textWidth + 2;
        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(TextBoxTheme.LeftBracket);
        var rightBracket = theme.Get(TextBoxTheme.RightBracket);
        var cursorFg = theme.Get(TextBoxTheme.CursorForegroundColor);
        var cursorBg = theme.Get(TextBoxTheme.CursorBackgroundColor);
        var selFg = theme.Get(TextBoxTheme.SelectionForegroundColor);
        var selBg = theme.Get(TextBoxTheme.SelectionBackgroundColor);
        
        var text = State.Text;
        var cursor = State.CursorPosition;
        var inheritedColors = context.GetInheritedColorCodes();
        var resetToInherited = context.GetResetToInheritedCodes();

        string output;
        if (IsFocused)
        {
            if (State.HasSelection)
            {
                // Render with selection highlight
                var selStart = State.SelectionStart;
                var selEnd = State.SelectionEnd;
                
                var beforeSel = text[..selStart];
                var selected = text[selStart..selEnd];
                var afterSel = text[selEnd..];
                
                // Use theme colors for selection, inherit for rest
                output = $"{inheritedColors}{leftBracket}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToInherited}{afterSel}{rightBracket}";
            }
            else
            {
                // Show text with cursor as themed block
                var before = text[..cursor];
                var cursorChar = cursor < text.Length ? text[cursor].ToString() : " ";
                var after = cursor < text.Length ? text[(cursor + 1)..] : "";
                
                output = $"{inheritedColors}{leftBracket}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorChar}{resetToInherited}{after}{rightBracket}";
            }
        }
        else
        {
            output = $"{inheritedColors}{leftBracket}{text}{rightBracket}{resetToInherited}";
        }
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        if (!IsFocused) return InputResult.NotHandled;

        return State.HandleInput(keyEvent) ? InputResult.Handled : InputResult.NotHandled;
    }
}
