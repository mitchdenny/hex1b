using System.Text;
using System.Windows.Input;

namespace WpfTerm;

/// <summary>
/// Converts WPF key events into ANSI escape sequences for the PTY.
/// </summary>
public static class AnsiKeyEncoder
{
    /// <summary>
    /// Encodes a WPF key event into the ANSI byte sequence that a terminal would send.
    /// Returns null if the key should be ignored.
    /// </summary>
    public static byte[]? Encode(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        bool ctrl = (modifiers & ModifierKeys.Control) != 0;
        bool shift = (modifiers & ModifierKeys.Shift) != 0;
        bool alt = (modifiers & ModifierKeys.Alt) != 0;

        // Ctrl+letter → control character (0x01-0x1A)
        if (ctrl && !alt && key >= Key.A && key <= Key.Z)
        {
            byte controlChar = (byte)(key - Key.A + 1);
            return [controlChar];
        }

        // Special keys
        string? sequence = key switch
        {
            Key.Enter => "\r",
            Key.Escape => "\x1b",
            Key.Back => "\x7f",
            Key.Tab when shift => "\x1b[Z",
            Key.Tab => "\t",
            Key.Space => " ",

            // Arrow keys
            Key.Up when ctrl => "\x1b[1;5A",
            Key.Down when ctrl => "\x1b[1;5B",
            Key.Right when ctrl => "\x1b[1;5C",
            Key.Left when ctrl => "\x1b[1;5D",
            Key.Up when shift => "\x1b[1;2A",
            Key.Down when shift => "\x1b[1;2B",
            Key.Right when shift => "\x1b[1;2C",
            Key.Left when shift => "\x1b[1;2D",
            Key.Up => "\x1b[A",
            Key.Down => "\x1b[B",
            Key.Right => "\x1b[C",
            Key.Left => "\x1b[D",

            // Navigation
            Key.Home => "\x1b[H",
            Key.End => "\x1b[F",
            Key.Insert => "\x1b[2~",
            Key.Delete => "\x1b[3~",
            Key.PageUp => "\x1b[5~",
            Key.PageDown => "\x1b[6~",

            // Function keys
            Key.F1 => "\x1bOP",
            Key.F2 => "\x1bOQ",
            Key.F3 => "\x1bOR",
            Key.F4 => "\x1bOS",
            Key.F5 => "\x1b[15~",
            Key.F6 => "\x1b[17~",
            Key.F7 => "\x1b[18~",
            Key.F8 => "\x1b[19~",
            Key.F9 => "\x1b[20~",
            Key.F10 => "\x1b[21~",
            Key.F11 => "\x1b[23~",
            Key.F12 => "\x1b[24~",

            _ => null
        };

        if (sequence != null)
        {
            // Wrap in Alt (ESC prefix) if alt is held and it's not already an escape sequence
            if (alt && !sequence.StartsWith("\x1b"))
                sequence = "\x1b" + sequence;

            return Encoding.UTF8.GetBytes(sequence);
        }

        return null;
    }

    /// <summary>
    /// Encodes a text input string (from TextInput event) into UTF-8 bytes for the PTY.
    /// </summary>
    public static byte[]? EncodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;

        return Encoding.UTF8.GetBytes(text);
    }
}
