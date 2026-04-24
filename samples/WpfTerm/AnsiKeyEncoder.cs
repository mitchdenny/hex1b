using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;

namespace WpfTerm;

/// <summary>
/// Converts WPF key events into ANSI escape sequences for the PTY.
/// </summary>
public static class AnsiKeyEncoder
{
    [DllImport("user32.dll")]
    private static extern int ToUnicode(
        uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff, uint wFlags);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);
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

    /// <summary>
    /// Converts a WPF KeyEventArgs to a printable character using Win32 ToUnicode.
    /// Returns UTF-8 bytes of the character, or null if the key isn't printable.
    /// This bypasses WPF's TextInput which can be suppressed during mouse activity.
    /// </summary>
    public static byte[]? KeyToText(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var scanCode = (uint)((e.SystemKey != Key.None ? KeyInterop.VirtualKeyFromKey(e.SystemKey) : KeyInterop.VirtualKeyFromKey(e.Key)));

        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
            return null;

        var sb = new StringBuilder(4);
        int result = ToUnicode(virtualKey, 0, keyboardState, sb, sb.Capacity, 0);

        if (result <= 0 || sb.Length == 0)
            return null;

        var ch = sb.ToString(0, result);

        // Filter out control characters that should be handled by Encode()
        if (ch.Length == 1 && ch[0] < 0x20)
            return null;

        return Encoding.UTF8.GetBytes(ch);
    }

    /// <summary>
    /// Encodes a mouse event into an SGR (mode 1006) mouse escape sequence.
    /// Format: ESC [ &lt; button ; x ; y M (press/move) or ESC [ &lt; button ; x ; y m (release)
    /// </summary>
    /// <param name="button">SGR button code (0=left, 1=middle, 2=right, 64=scrollup, 65=scrolldown, +32=motion).</param>
    /// <param name="x">1-based column.</param>
    /// <param name="y">1-based row.</param>
    /// <param name="isRelease">True for button release (lowercase 'm'), false for press/move (uppercase 'M').</param>
    /// <param name="modifiers">Keyboard modifiers to encode (+4=shift, +8=alt, +16=ctrl).</param>
    public static byte[] EncodeMouse(int button, int x, int y, bool isRelease, int modifiers = 0)
    {
        button |= modifiers;
        var terminator = isRelease ? 'm' : 'M';
        var seq = $"\x1b[<{button};{x};{y}{terminator}";
        return Encoding.ASCII.GetBytes(seq);
    }
}
