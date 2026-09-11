using System.Text;

namespace Hex1b.Input;

internal static class TerminalInputEncoder
{
    internal static TerminalMouseTracking MouseTracking(bool x10, bool normal, bool button, bool any)
        => any ? TerminalMouseTracking.Any :
            button ? TerminalMouseTracking.Button :
            normal ? TerminalMouseTracking.Normal :
            x10 ? TerminalMouseTracking.X10 : TerminalMouseTracking.None;

    internal static string EncodeKey(Hex1bKeyEvent input, TerminalInputModes modes)
    {
        var modifier = 1 + (input.Shift ? 1 : 0) + (input.Alt ? 2 : 0) + (input.Control ? 4 : 0);
        var final = input.Key switch
        {
            Hex1bKey.UpArrow => 'A',
            Hex1bKey.DownArrow => 'B',
            Hex1bKey.RightArrow => 'C',
            Hex1bKey.LeftArrow => 'D',
            Hex1bKey.Home => 'H',
            Hex1bKey.End => 'F',
            _ => '\0'
        };
        if (final != '\0')
            return modifier != 1 ? $"\x1b[1;{modifier}{final}" :
                modes.ApplicationCursorKeys ? $"\x1bO{final}" : $"\x1b[{final}";

        var tilde = input.Key switch
        {
            Hex1bKey.Insert => 2,
            Hex1bKey.Delete => 3,
            Hex1bKey.PageUp => 5,
            Hex1bKey.PageDown => 6,
            Hex1bKey.F5 => 15,
            Hex1bKey.F6 => 17,
            Hex1bKey.F7 => 18,
            Hex1bKey.F8 => 19,
            Hex1bKey.F9 => 20,
            Hex1bKey.F10 => 21,
            Hex1bKey.F11 => 23,
            Hex1bKey.F12 => 24,
            _ => 0
        };
        if (tilde != 0)
            return modifier == 1 ? $"\x1b[{tilde}~" : $"\x1b[{tilde};{modifier}~";

        if (input.Key is >= Hex1bKey.F1 and <= Hex1bKey.F4)
        {
            var function = (char)('P' + (input.Key - Hex1bKey.F1));
            return modifier == 1 ? $"\x1bO{function}" : $"\x1b[1;{modifier}{function}";
        }

        var text = input.Key switch
        {
            Hex1bKey.Enter => "\r",
            // Preserve a host's original BS/DEL; synthetic keys default to DEL.
            Hex1bKey.Backspace => input.Control ? "\b" :
                input.Text.Length > 0 && input.Text[0] is '\b' or '\x7f' ? input.Text[0].ToString() : "\x7f",
            Hex1bKey.Tab => input.Shift ? "\x1b[Z" : "\t",
            Hex1bKey.Escape => "\x1b",
            _ => input.Text
        };
        if (input.Control && input.Key is >= Hex1bKey.A and <= Hex1bKey.Z)
        {
            text = ((char)(input.Key - Hex1bKey.A + 1)).ToString();
        }
        else if (input.Control && input.Key is not (Hex1bKey.Enter or Hex1bKey.Backspace or Hex1bKey.Tab or Hex1bKey.Escape))
        {
            if (text.Length == 1)
            {
                var character = char.ToUpperInvariant(text[0]);
                text = character is >= '@' and <= '_' ? ((char)(character & 31)).ToString() :
                    character == ' ' ? "\0" : text;
            }
            else if (input.Key == Hex1bKey.Spacebar)
            {
                text = "\0";
            }
        }

        return input.Alt && text.Length > 0 ? "\x1b" + text : text;
    }

    internal static string EncodePaste(string text, TerminalInputModes modes)
        => modes.BracketedPaste ? "\x1b[200~" + text + "\x1b[201~" : text;

    internal static TerminalMouseInput MouseInput(Hex1bMouseEvent input)
        => new(input.Button switch
        {
            MouseButton.Left => TerminalMouseButton.Left,
            MouseButton.Middle => TerminalMouseButton.Middle,
            MouseButton.Right => TerminalMouseButton.Right,
            MouseButton.ScrollUp => TerminalMouseButton.WheelUp,
            MouseButton.ScrollDown => TerminalMouseButton.WheelDown,
            _ => TerminalMouseButton.None
        }, input.Action, input.X, input.Y, input.Modifiers);

    internal static byte[] EncodeMouse(TerminalMouseInput input, TerminalInputModes modes)
    {
        var tracking = modes.MouseTracking;
        var motion = input.Action is MouseAction.Move or MouseAction.Drag;
        var wheel = input.Button is >= TerminalMouseButton.WheelUp and <= TerminalMouseButton.WheelRight;
        if (tracking == TerminalMouseTracking.None ||
            input.X < 0 || input.Y < 0 || input.X >= modes.Width || input.Y >= modes.Height ||
            (tracking == TerminalMouseTracking.X10 && (input.Action != MouseAction.Down || wheel)) ||
            (motion && (tracking == TerminalMouseTracking.Normal ||
                (tracking == TerminalMouseTracking.Button && input.Button == TerminalMouseButton.None))))
            return [];

        var modifiers = tracking == TerminalMouseTracking.X10 ? 0 :
            ((input.Modifiers & Hex1bModifiers.Shift) != 0 ? 4 : 0) |
            ((input.Modifiers & Hex1bModifiers.Alt) != 0 ? 8 : 0) |
            ((input.Modifiers & Hex1bModifiers.Control) != 0 ? 16 : 0);
        var code = (int)input.Button | modifiers | (motion ? 32 : 0);
        byte[] bytes;
        if (modes.MouseEncoding == TerminalMouseEncoding.Sgr)
        {
            bytes = Encoding.ASCII.GetBytes(FormattableString.Invariant(
                $"\x1b[<{code};{input.X + 1};{input.Y + 1}{(input.Action == MouseAction.Up ? 'm' : 'M')}"));
        }
        else
        {
            if (input.Action == MouseAction.Up)
                code = 3 | modifiers;
            if (modes.MouseEncoding == TerminalMouseEncoding.Urxvt)
            {
                bytes = Encoding.ASCII.GetBytes(FormattableString.Invariant(
                    $"\x1b[{code + 32};{input.X + 1};{input.Y + 1}M"));
            }
            else if (modes.MouseEncoding == TerminalMouseEncoding.Utf8)
            {
                // Mode 1005 can represent coordinates through U+07FF.
                if (input.X > 2014 || input.Y > 2014)
                    return [];
                bytes = Encoding.UTF8.GetBytes($"\x1b[M{(char)(code + 32)}{(char)(input.X + 33)}{(char)(input.Y + 33)}");
            }
            else
            {
                // Legacy coordinates are raw bytes, not UTF-8 text.
                if (input.X > 222 || input.Y > 222)
                    return [];
                bytes = [0x1b, (byte)'[', (byte)'M', (byte)(code + 32), (byte)(input.X + 33), (byte)(input.Y + 33)];
            }
        }

        if (input.Count == 1)
            return bytes;
        var repeated = new byte[bytes.Length * input.Count];
        for (var i = 0; i < input.Count; i++)
            bytes.CopyTo(repeated, i * bytes.Length);
        return repeated;
    }
}
