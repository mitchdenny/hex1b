using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    public TextBoxState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

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
                
                // Use theme colors for selection
                context.Write($"{leftBracket}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}\x1b[0m{afterSel}{rightBracket}");
            }
            else
            {
                // Show text with cursor as themed block
                var before = text[..cursor];
                var cursorChar = cursor < text.Length ? text[cursor].ToString() : " ";
                var after = cursor < text.Length ? text[(cursor + 1)..] : "";
                
                context.Write($"{leftBracket}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorChar}\x1b[0m{after}{rightBracket}");
            }
        }
        else
        {
            context.Write($"{leftBracket}{text}{rightBracket}");
        }
    }

    public override bool HandleInput(Hex1bInputEvent evt)
    {
        if (!IsFocused) return false;

        if (evt is KeyInputEvent keyEvent)
        {
            State.HandleInput(keyEvent);
            return true;
        }
        return false;
    }
}
