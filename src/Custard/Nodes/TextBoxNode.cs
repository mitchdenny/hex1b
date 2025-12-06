using Custard.Widgets;

namespace Custard;

public sealed class TextBoxNode : CustardNode
{
    public TextBoxState State { get; set; } = new();
    public bool IsFocused { get; set; } = false;

    public override bool IsFocusable => true;

    public override void Render(CustardRenderContext context)
    {
        // Render the text with a cursor indicator
        var text = State.Text;
        var cursor = State.CursorPosition;

        // Show text with cursor as underscore or block
        var before = text[..cursor];
        var cursorChar = cursor < text.Length ? text[cursor].ToString() : " ";
        var after = cursor < text.Length ? text[(cursor + 1)..] : "";

        // Use reverse video for cursor only if focused
        if (IsFocused)
        {
            context.Write($"[{before}\x1b[7m{cursorChar}\x1b[27m{after}]");
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
