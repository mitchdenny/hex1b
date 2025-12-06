using Custard.Widgets;

namespace Custard;

public sealed class TextBoxNode : CustardNode
{
    public TextBoxState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

    public override bool IsFocusable => true;

    public override void Render(CustardRenderContext context)
    {
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
                
                // Use reverse video for selection, and underline for cursor position within selection
                context.Write($"[{beforeSel}\x1b[7m{selected}\x1b[27m{afterSel}]");
            }
            else
            {
                // Show text with cursor as reverse video block
                var before = text[..cursor];
                var cursorChar = cursor < text.Length ? text[cursor].ToString() : " ";
                var after = cursor < text.Length ? text[(cursor + 1)..] : "";
                
                context.Write($"[{before}\x1b[7m{cursorChar}\x1b[27m{after}]");
            }
        }
        else
        {
            context.Write($"[{text}]");
        }
    }

    public override bool HandleInput(CustardInputEvent evt)
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
