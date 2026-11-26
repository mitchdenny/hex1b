using System.Globalization;
using System.Text;

namespace Hex1b.Input;

internal static class KittyKeyboardInput
{
    internal static bool IsCursorPositionReport(string sequence)
    {
        if (!sequence.StartsWith("\x1b[", StringComparison.Ordinal) || !sequence.EndsWith('R'))
            return false;
        var parameters = sequence.AsSpan(2, sequence.Length - 3);
        var separator = parameters.IndexOf(';');
        return separator > 0 &&
            int.TryParse(parameters[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out var row) && row > 0 &&
            int.TryParse(parameters[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var column) && column > 0;
    }

    internal static bool TryDecode(string sequence, out Hex1bKeyEvent? result)
    {
        result = null;
        if (sequence.Length is < 3 or > 4096 || !sequence.StartsWith("\x1b[", StringComparison.Ordinal))
            return false;

        var final = sequence[^1];
        if (final is not ('u' or '~' or 'A' or 'B' or 'C' or 'D' or 'F' or 'H' or 'P' or 'Q' or 'R' or 'S'))
            return false;

        if (sequence == "\x1b[R")
        {
            result = new Hex1bKeyEvent(Hex1bKey.F3, "", Hex1bModifiers.None);
            return true;
        }

        var fields = sequence[2..^1].Split(';');
        // Letter-terminated functional keys default their key number to 1 (e.g. CSI ;1:3B).
        if (fields.Length > 1 && fields[0].Length == 0 && final is not ('u' or '~'))
            fields[0] = "1";
        if (fields.Length is < 1 or > 3 || fields[0].Length == 0)
            return false;
        // A modified F3 press without an event subfield is indistinguishable from a cursor report.
        if (final == 'R' && (fields.Length != 2 || !fields[1].Contains(':')))
            return false;

        var keyParts = fields[0].Split(':');
        if (keyParts.Length is < 1 or > 3 || !TryNumber(keyParts[0], out var code))
            return false;
        if (final != 'u' && keyParts.Length != 1)
            return false;
        if (final == 'u' && !Rune.IsValid(code))
            return false;

        int? shifted = null, baseLayout = null;
        if (keyParts.Length > 1 && keyParts[1].Length > 0)
        {
            if (!TryNumber(keyParts[1], out var value) || !Rune.IsValid(value))
                return false;
            shifted = value;
        }
        if (keyParts.Length > 2 && keyParts[2].Length > 0)
        {
            if (!TryNumber(keyParts[2], out var value) || !Rune.IsValid(value))
                return false;
            baseLayout = value;
        }

        var modifiers = 1;
        var eventType = 1;
        if (fields.Length > 1 && fields[1].Length > 0)
        {
            var parts = fields[1].Split(':');
            if (parts.Length is < 1 or > 2 || !TryNumber(parts[0], out modifiers) ||
                modifiers is < 1 or > 256)
                return false;
            if (parts.Length == 2 && (!TryNumber(parts[1], out eventType) || eventType is < 1 or > 3))
                return false;
        }

        var text = new StringBuilder();
        if (fields.Length == 3)
        {
            if (final != 'u' || fields[2].Length == 0)
                return false;
            foreach (var part in fields[2].Split(':'))
            {
                if (!TryNumber(part, out var value) || !Rune.IsValid(value) ||
                    value < 32 || value is >= 127 and <= 159)
                    return false;
                text.Append(new Rune(value).ToString());
            }
        }

        var key = MapKey(final, code);
        if (final != 'u' && key == Hex1bKey.None)
            return false;
        if (final == 'u' && code == 0 && text.Length == 0)
            return false;

        result = new Hex1bKeyEvent(key, text.ToString(), (Hex1bModifiers)(modifiers - 1))
        {
            EventType = (Hex1bKeyEventType)(eventType - 1),
            UnicodeKeyCode = final == 'u' ? code : null,
            ShiftedKeyCode = shifted,
            BaseLayoutKeyCode = baseLayout,
        };
        return true;
    }

    private static bool TryNumber(string value, out int number)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    internal static Hex1bKey MapKey(char final, int code)
    {
        if (final == '~')
        {
            return code switch
            {
                2 => Hex1bKey.Insert, 3 => Hex1bKey.Delete,
                5 => Hex1bKey.PageUp, 6 => Hex1bKey.PageDown,
                7 => Hex1bKey.Home, 8 => Hex1bKey.End,
                11 => Hex1bKey.F1, 12 => Hex1bKey.F2, 13 => Hex1bKey.F3, 14 => Hex1bKey.F4,
                15 => Hex1bKey.F5, 17 => Hex1bKey.F6, 18 => Hex1bKey.F7, 19 => Hex1bKey.F8,
                20 => Hex1bKey.F9, 21 => Hex1bKey.F10, 23 => Hex1bKey.F11, 24 => Hex1bKey.F12,
                _ => Hex1bKey.None,
            };
        }
        if (final != 'u')
        {
            if (code != 1) return Hex1bKey.None;
            return final switch
            {
                'A' => Hex1bKey.UpArrow, 'B' => Hex1bKey.DownArrow,
                'C' => Hex1bKey.RightArrow, 'D' => Hex1bKey.LeftArrow,
                'H' => Hex1bKey.Home, 'F' => Hex1bKey.End,
                'P' => Hex1bKey.F1, 'Q' => Hex1bKey.F2,
                'R' => Hex1bKey.F3, 'S' => Hex1bKey.F4,
                _ => Hex1bKey.None,
            };
        }
        return code switch
        {
            9 => Hex1bKey.Tab, 13 => Hex1bKey.Enter, 27 => Hex1bKey.Escape,
            32 => Hex1bKey.Spacebar, 127 => Hex1bKey.Backspace,
            >= 'a' and <= 'z' => Hex1bKey.A + (code - 'a'),
            >= 'A' and <= 'Z' => Hex1bKey.A + (code - 'A'),
            >= '0' and <= '9' => Hex1bKey.D0 + (code - '0'),
            ',' or '<' => Hex1bKey.OemComma, '.' or '>' => Hex1bKey.OemPeriod,
            '-' or '_' => Hex1bKey.OemMinus, '=' or '+' => Hex1bKey.OemPlus,
            '/' or '?' => Hex1bKey.OemQuestion, ';' or ':' => Hex1bKey.Oem1,
            '[' or '{' => Hex1bKey.Oem4, '\\' or '|' => Hex1bKey.Oem5,
            ']' or '}' => Hex1bKey.Oem6, '\'' or '"' => Hex1bKey.Oem7,
            '`' or '~' => Hex1bKey.OemTilde,
            >= 57399 and <= 57408 => Hex1bKey.NumPad0 + (code - 57399),
            57409 => Hex1bKey.Decimal, 57410 => Hex1bKey.Divide,
            57411 => Hex1bKey.Multiply, 57412 => Hex1bKey.Subtract, 57413 => Hex1bKey.Add,
            _ => Hex1bKey.None,
        };
    }
}
