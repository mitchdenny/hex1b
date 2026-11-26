using System.Text;

namespace Hex1b.Input;

internal static class KittyKeyboardEncoder
{
    internal static bool TryEncode(Hex1bKeyEvent input, int flags, out string encoded)
    {
        encoded = "";
        var reportEvents = (flags & 2) != 0;
        var reportAll = (flags & 8) != 0;
        if (input.EventType == Hex1bKeyEventType.Release && !reportEvents)
            return true;
        if (flags == 0)
            return input.EventType == Hex1bKeyEventType.Release;

        var eventType = reportEvents ? input.EventType : Hex1bKeyEventType.Press;
        var modifiers = 1 + (int)input.Modifiers;
        var eventSuffix = eventType switch
        {
            Hex1bKeyEventType.Repeat => ":2",
            Hex1bKeyEventType.Release => ":3",
            _ => "",
        };

        var (final, number) = FunctionalKey(input.Key);
        if (final != '\0')
        {
            if ((flags & (1 | 8)) == 0 && eventSuffix.Length == 0 &&
                ((int)input.Modifiers & ~7) == 0)
                return false;
            if (modifiers == 1 && eventSuffix.Length == 0)
                encoded = final == '~' ? $"\x1b[{number}~" : $"\x1b[{final}";
            else
                encoded = $"\x1b[{number};{modifiers}{eventSuffix}{final}";
            return true;
        }

        var isLegacyControl = input.Key is Hex1bKey.Enter or Hex1bKey.Tab or Hex1bKey.Backspace;
        if (isLegacyControl && !reportAll)
            return input.EventType == Hex1bKeyEventType.Release;

        var code = input.UnicodeKeyCode ?? KeyCode(input.Key);
        var isKeypad = input.Key is >= Hex1bKey.NumPad0 and <= Hex1bKey.Divide;
        if ((flags & 1) != 0 && isKeypad && input.IsPrintable && !reportAll &&
            (input.Modifiers & (Hex1bModifiers.Alt | Hex1bModifiers.Control |
                Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta)) == 0)
        {
            encoded = input.EventType == Hex1bKeyEventType.Release ? "" : input.Text;
            return true;
        }
        var needsDisambiguation = (flags & 1) != 0 &&
            (input.Key == Hex1bKey.Escape || (isKeypad && !input.IsPrintable) ||
                (input.Modifiers & (Hex1bModifiers.Alt | Hex1bModifiers.Control |
                    Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta)) != 0);
        if (!reportAll && !needsDisambiguation)
            return input.EventType == Hex1bKeyEventType.Release;

        if (code is null)
        {
            if (!reportAll || input.Text.Length == 0)
                return true;
            code = 0;
        }

        var key = code.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if ((flags & 4) != 0 && ((input.Shift && input.ShiftedKeyCode.HasValue) || input.BaseLayoutKeyCode.HasValue))
        {
            key += ":";
            if (input.ShiftedKeyCode.HasValue && input.Shift)
                key += input.ShiftedKeyCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (input.BaseLayoutKeyCode.HasValue)
                key += ":" + input.BaseLayoutKeyCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        var associatedText = "";
        if (reportAll && (flags & 16) != 0 && eventType != Hex1bKeyEventType.Release)
        {
            var scalars = input.Text.EnumerateRunes()
                .Where(rune => rune.Value >= 32 && (rune.Value < 127 || rune.Value > 159))
                .Select(rune => rune.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            associatedText = string.Join(':', scalars);
        }
        encoded = associatedText.Length > 0
            ? $"\x1b[{key};{modifiers}{eventSuffix};{associatedText}u"
            : modifiers == 1 && eventSuffix.Length == 0
                ? $"\x1b[{key}u"
                : $"\x1b[{key};{modifiers}{eventSuffix}u";
        return true;
    }

    private static (char Final, int Number) FunctionalKey(Hex1bKey key) => key switch
    {
        Hex1bKey.UpArrow => ('A', 1), Hex1bKey.DownArrow => ('B', 1),
        Hex1bKey.RightArrow => ('C', 1), Hex1bKey.LeftArrow => ('D', 1),
        Hex1bKey.Home => ('H', 1), Hex1bKey.End => ('F', 1),
        Hex1bKey.F1 => ('P', 1), Hex1bKey.F2 => ('Q', 1),
        Hex1bKey.F3 => ('~', 13), Hex1bKey.F4 => ('S', 1),
        Hex1bKey.Insert => ('~', 2), Hex1bKey.Delete => ('~', 3),
        Hex1bKey.PageUp => ('~', 5), Hex1bKey.PageDown => ('~', 6),
        Hex1bKey.F5 => ('~', 15), Hex1bKey.F6 => ('~', 17), Hex1bKey.F7 => ('~', 18),
        Hex1bKey.F8 => ('~', 19), Hex1bKey.F9 => ('~', 20), Hex1bKey.F10 => ('~', 21),
        Hex1bKey.F11 => ('~', 23), Hex1bKey.F12 => ('~', 24),
        _ => ('\0', 0),
    };

    private static int? KeyCode(Hex1bKey key) => key switch
    {
        >= Hex1bKey.A and <= Hex1bKey.Z => 'a' + (key - Hex1bKey.A),
        >= Hex1bKey.D0 and <= Hex1bKey.D9 => '0' + (key - Hex1bKey.D0),
        >= Hex1bKey.NumPad0 and <= Hex1bKey.NumPad9 => 57399 + (key - Hex1bKey.NumPad0),
        Hex1bKey.Enter => 13, Hex1bKey.Tab => 9, Hex1bKey.Escape => 27,
        Hex1bKey.Backspace => 127, Hex1bKey.Spacebar => 32,
        Hex1bKey.OemComma => ',', Hex1bKey.OemPeriod => '.', Hex1bKey.OemMinus => '-',
        Hex1bKey.OemPlus => '=', Hex1bKey.OemQuestion => '/', Hex1bKey.Oem1 => ';',
        Hex1bKey.Oem4 => '[', Hex1bKey.Oem5 => '\\', Hex1bKey.Oem6 => ']',
        Hex1bKey.Oem7 => '\'', Hex1bKey.OemTilde => '`',
        Hex1bKey.Decimal => 57409, Hex1bKey.Divide => 57410, Hex1bKey.Multiply => 57411,
        Hex1bKey.Subtract => 57412, Hex1bKey.Add => 57413,
        _ => null,
    };
}
