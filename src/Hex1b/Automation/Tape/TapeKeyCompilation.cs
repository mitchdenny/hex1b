using Hex1b.Input;

namespace Hex1b.Automation;

internal static class TapeKeyCompilation
{
    internal static Hex1bKeyEvent? CreateEvent(string name, Hex1bModifiers modifiers = Hex1bModifiers.None)
    {
        var key = name switch
        {
            "Backspace" => Hex1bKey.Backspace,
            "Delete" => Hex1bKey.Delete,
            "Insert" => Hex1bKey.Insert,
            "Enter" => Hex1bKey.Enter,
            "Escape" => Hex1bKey.Escape,
            "Tab" => Hex1bKey.Tab,
            "Space" => Hex1bKey.Spacebar,
            "Up" => Hex1bKey.UpArrow,
            "Down" => Hex1bKey.DownArrow,
            "Left" => Hex1bKey.LeftArrow,
            "Right" => Hex1bKey.RightArrow,
            "PageUp" => Hex1bKey.PageUp,
            "PageDown" => Hex1bKey.PageDown,
            _ => Hex1bKey.None
        };
        if (key != Hex1bKey.None)
        {
            var text = key switch
            {
                Hex1bKey.Enter => "\r",
                Hex1bKey.Tab => "\t",
                Hex1bKey.Spacebar => " ",
                _ => ""
            };
            return new(key, text, modifiers);
        }
        if (name.Length == 1)
        {
            var character = TextInputStep.CharToKeyEvent(name[0]);
            var text = (modifiers & Hex1bModifiers.Shift) != 0
                ? Hex1bTerminalInputSequenceBuilder.GetDefaultTextForKey(character.Key, Hex1bModifiers.Shift)
                : character.Text;
            return new(character.Key, text, modifiers);
        }
        return null;
    }
}
