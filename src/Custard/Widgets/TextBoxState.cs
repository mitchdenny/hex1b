namespace Custard.Widgets;

/// <summary>
/// Holds the mutable state for a TextBox. Create once and reuse across renders.
/// </summary>
public class TextBoxState
{
    public string Text { get; set; } = "";
    public int CursorPosition { get; set; } = 0;

    public void HandleInput(KeyInputEvent evt)
    {
        switch (evt.Key)
        {
            case ConsoleKey.Backspace:
                if (CursorPosition > 0)
                {
                    Text = Text.Remove(CursorPosition - 1, 1);
                    CursorPosition--;
                }
                break;

            case ConsoleKey.Delete:
                if (CursorPosition < Text.Length)
                {
                    Text = Text.Remove(CursorPosition, 1);
                }
                break;

            case ConsoleKey.LeftArrow:
                if (CursorPosition > 0)
                {
                    CursorPosition--;
                }
                break;

            case ConsoleKey.RightArrow:
                if (CursorPosition < Text.Length)
                {
                    CursorPosition++;
                }
                break;

            case ConsoleKey.Home:
                CursorPosition = 0;
                break;

            case ConsoleKey.End:
                CursorPosition = Text.Length;
                break;

            default:
                // Insert printable characters
                if (!char.IsControl(evt.KeyChar))
                {
                    Text = Text.Insert(CursorPosition, evt.KeyChar.ToString());
                    CursorPosition++;
                }
                break;
        }
    }
}
