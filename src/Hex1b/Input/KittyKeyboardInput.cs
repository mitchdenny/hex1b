using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hex1b.Input;

internal static class KittyKeyboardInput
{
    internal static bool IsCursorPositionReport(string sequence)
    {
        if (!sequence.StartsWith("\x1b[", StringComparison.Ordinal) || !sequence.EndsWith('R'))
            return false;
        var fields = sequence[2..^1].Split(';');
        return fields.Length == 2 && Number(fields[0], out var row) && row > 0 &&
            Number(fields[1], out var column) && column > 0;
    }

    internal static bool TryDecode(string sequence, [NotNullWhen(true)] out Hex1bKeyEvent? result)
    {
        result = null;
        if (sequence.Length is < 3 or > 4096 ||
            !sequence.StartsWith("\x1b[", StringComparison.Ordinal) || IsCursorPositionReport(sequence))
            return false;
        var final = sequence[^1];
        if (final is not ('u' or '~' or 'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'H' or 'P' or 'Q' or 'R' or 'S'))
            return false;
        var fields = sequence[2..^1].Split(';');
        if (fields.Length > 3 || (final != 'u' && fields.Length > 2))
            return false;
        var identities = fields[0].Split(':');
        if (identities.Length > 3 || (final != 'u' && identities.Length != 1))
            return false;
        var code = 1;
        if (identities[0].Length == 0 ? final is 'u' or '~' : !Number(identities[0], out code))
            return false;
        if (final == 'u' && !Rune.IsValid(code))
            return false;
        int? shifted = null, layout = null;
        if (identities.Length > 1 && identities[1].Length > 0)
        {
            if (!Number(identities[1], out var value) || !Rune.IsValid(value)) return false;
            shifted = value;
        }
        if (identities.Length > 2 && identities[2].Length > 0)
        {
            if (!Number(identities[2], out var value) || !Rune.IsValid(value)) return false;
            layout = value;
        }
        var modifiers = 1;
        var kind = 1;
        if (fields.Length > 1 && fields[1].Length > 0)
        {
            var parts = fields[1].Split(':');
            if (parts.Length > 2 || !Number(parts[0], out modifiers) || modifiers is < 1 or > 256)
                return false;
            if (parts.Length == 2 && (!Number(parts[1], out kind) || kind is < 1 or > 3))
                return false;
        }
        var text = new StringBuilder();
        if (fields.Length == 3)
        {
            foreach (var part in fields[2].Split(':'))
            {
                if (!Number(part, out var value) || !Rune.IsValid(value) || Rune.IsControl(new Rune(value)))
                    return false;
                text.Append(new Rune(value));
            }
        }
        var key = MapKey(final, code);
        if (final != 'u' && key == Hex1bKey.None && !((final == 'E' && code == 1) || (final == '~' && code == 57427)))
            return false;
        result = new(key, text.ToString(), (Hex1bModifiers)(modifiers - 1))
        {
            EventType = (Hex1bKeyEventType)(kind - 1),
            UnicodeKeyCode = final == 'u' ? code : key == Hex1bKey.None ? 57427 : null,
            ShiftedKeyCode = shifted,
            BaseLayoutKeyCode = layout,
        };
        return true;
    }

    private static bool Number(string text, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    internal static Hex1bKey MapKey(char final, int code)
    {
        if (final == '~')
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
        if (final != 'u')
            return code != 1 ? Hex1bKey.None : final switch
            {
                'A' => Hex1bKey.UpArrow, 'B' => Hex1bKey.DownArrow,
                'C' => Hex1bKey.RightArrow, 'D' => Hex1bKey.LeftArrow,
                'H' => Hex1bKey.Home, 'F' => Hex1bKey.End,
                'P' => Hex1bKey.F1, 'Q' => Hex1bKey.F2, 'R' => Hex1bKey.F3, 'S' => Hex1bKey.F4,
                _ => Hex1bKey.None,
            };
        if (code == 0) return Hex1bKey.None;
        if (code is >= 'A' and <= 'Z') return Hex1bKey.A + (code - 'A');
        // Only map identities defined by Hex1bKey; never infer identity from associated text.
        foreach (var key in Enum.GetValues<Hex1bKey>())
            if (key != Hex1bKey.None && KittyKeyboardEncoder.KeyCode(key) == code)
                return key;
        return Hex1bKey.None;
    }
}
