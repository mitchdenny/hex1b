using System.Globalization;
using System.Text;

namespace Hex1b.Input;

// Independently implemented from https://sw.kovidgoyal.net/kitty/keyboard-protocol/.
internal static class KittyKeyboardEncoder
{
    internal static string Encode(Hex1bKeyEvent input, int flags, TerminalInputModes modes)
    {
        flags &= 31;
        if (!Enum.IsDefined(input.EventType))
            throw new ArgumentOutOfRangeException(nameof(input), "Invalid keyboard event type.");
        if (flags == 0)
            return input.EventType == Hex1bKeyEventType.Release ? "" :
                TerminalInputEncoder.EncodeLegacyKey(input, modes);
        var all = (flags & 8) != 0;
        var events = (flags & 2) != 0;
        var disambiguate = all || (flags & 1) != 0;
        var code = input.UnicodeKeyCode ?? KeyCode(input.Key);
        ValidateCode(code);
        if (input.ShiftedKeyCode is { } shiftedCode) ValidateCode(shiftedCode);
        if (input.BaseLayoutKeyCode is { } baseCode) ValidateCode(baseCode);
        if (((int)input.Modifiers & ~255) != 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Invalid keyboard modifiers.");

        var release = input.EventType == Hex1bKeyEventType.Release;
        var keypad = code is >= 57399 and <= 57427;
        var wireKey = input.Key;
        var wireCode = code;
        // Keypad keys report their non-keypad equivalents unless disambiguation is enabled.
        if (keypad && !disambiguate)
            (wireKey, wireCode) = KeypadEquivalent(code);
        var recoveryKey = wireCode is 9 or 13 or 127;
        if (release && (!events || (recoveryKey && !all)))
            return "";
        // Modifier key events are opt-in independently of event-type reporting.
        if (code is >= 57441 and <= 57454 && !all)
            return "";

        var modifiers = input.Modifiers;
        var functional = code is >= 57344 and <= 63743 ||
            input.Key is >= Hex1bKey.F1 and <= Hex1bKey.Insert;
        if (!all && (!functional || keypad))
            modifiers &= ~(Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock);
        var shortcut = (modifiers & ~Hex1bModifiers.Shift) != 0;
        if (keypad && !disambiguate)
            functional = wireKey is >= Hex1bKey.F1 and <= Hex1bKey.Insert || wireCode == 57427;
        var legacyInput = input with
        {
            Key = keypad && !disambiguate ? wireKey : input.Key,
            Modifiers = modifiers,
        };
        var escape = all || release || functional ||
            (disambiguate && (code == 27 || shortcut)) ||
            (input.Control && input.Shift) ||
            (events && input.EventType == Hex1bKeyEventType.Repeat && code == 27) ||
            (modifiers & (Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta)) != 0;
        if (recoveryKey && !all && !release)
            escape = (modifiers != Hex1bModifiers.None && disambiguate) ||
                (modifiers & (Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta)) != 0;

        var unavailableText = false;
        if (!escape)
        {
            var legacy = TerminalInputEncoder.EncodeLegacyKey(legacyInput, modes);
            // Keep known keys whose text is unavailable as key events rather than inventing layout text.
            if (legacy.Length > 0 || wireCode == 0)
                return legacy;
            escape = unavailableText = true;
        }

        var (number, final) = WireKey(wireKey, wireCode, disambiguate);
        var keyField = number.ToString(CultureInfo.InvariantCulture);
        if (final == 'u' && (flags & 4) != 0 && code != 0 && code is not (>= 57344 and <= 63743))
        {
            var shifted = input.Shift ? PrintableAlternate(input.ShiftedKeyCode) : null;
            var layout = PrintableAlternate(input.BaseLayoutKeyCode);
            if (shifted.HasValue || layout.HasValue)
                keyField += ":" + (shifted?.ToString(CultureInfo.InvariantCulture) ?? "");
            if (layout.HasValue)
                keyField += ":" + layout.Value.ToString(CultureInfo.InvariantCulture);
        }

        var textCodes = new List<string>();
        if (all && (flags & 16) != 0 && !release && final == 'u' &&
            !recoveryKey && code != 27 && (!functional || keypad))
            foreach (var rune in input.Text.EnumerateRunes())
                if (!Rune.IsControl(rune))
                    textCodes.Add(rune.Value.ToString(CultureInfo.InvariantCulture));

        var eventField = events && input.EventType != Hex1bKeyEventType.Press
            ? ":" + ((int)input.EventType + 1).ToString(CultureInfo.InvariantCulture) : "";
        var modifierField = modifiers != Hex1bModifiers.None || eventField.Length > 0
            ? ((int)modifiers + 1).ToString(CultureInfo.InvariantCulture) + eventField : "";
        // Disambiguated non-text keys always use CSI forms; SS3 cursor forms are legacy-only.
        if (!disambiguate && number == 1 && final is 'A' or 'B' or 'C' or 'D' or 'H' or 'F' &&
            modifierField.Length == 0 && modes.ApplicationCursorKeys)
            return "\x1bO" + final;
        if (final != 'u' && keyField == "1" && modifierField.Length == 0)
            keyField = "";
        if (!unavailableText && !all && !disambiguate && !release && modifierField.Length == 0 && eventField.Length == 0)
        {
            var legacy = TerminalInputEncoder.EncodeLegacyKey(legacyInput, modes);
            if (legacy.Length > 0)
                return legacy;
        }
        var suffix = textCodes.Count > 0 ? ";" + string.Join(':', textCodes) : "";
        return "\x1b[" + keyField +
            (modifierField.Length > 0 || suffix.Length > 0 ? ";" + modifierField : "") + suffix + final;
    }

    private static void ValidateCode(int code)
    {
        if (!Rune.IsValid(code))
            throw new ArgumentOutOfRangeException(nameof(code), "Key identities must be Unicode scalar values.");
    }

    private static int? PrintableAlternate(int? code)
        => code is { } value && !Rune.IsControl(new Rune(value)) ? code : null;

    private static (Hex1bKey Key, int Code) KeypadEquivalent(int code) => code switch
    {
        >= 57399 and <= 57408 => (Hex1bKey.None, '0' + code - 57399),
        57409 => (Hex1bKey.None, '.'), 57410 => (Hex1bKey.None, '/'),
        57411 => (Hex1bKey.None, '*'), 57412 => (Hex1bKey.None, '-'),
        57413 => (Hex1bKey.None, '+'), 57414 => (Hex1bKey.Enter, 13),
        57415 => (Hex1bKey.None, '='), 57416 => (Hex1bKey.None, ','),
        57417 => (Hex1bKey.LeftArrow, 0), 57418 => (Hex1bKey.RightArrow, 0),
        57419 => (Hex1bKey.UpArrow, 0), 57420 => (Hex1bKey.DownArrow, 0),
        57421 => (Hex1bKey.PageUp, 0), 57422 => (Hex1bKey.PageDown, 0),
        57423 => (Hex1bKey.Home, 0), 57424 => (Hex1bKey.End, 0),
        57425 => (Hex1bKey.Insert, 0), 57426 => (Hex1bKey.Delete, 0),
        _ => (Hex1bKey.None, 57427),
    };

    private static (int Number, char Final) WireKey(Hex1bKey key, int code, bool disambiguate)
    {
        if (code == 57427) return disambiguate ? (57427, '~') : (1, 'E');
        if (disambiguate && key is >= Hex1bKey.NumPad0 and <= Hex1bKey.Divide)
            return (code, 'u');
        return key switch
        {
            Hex1bKey.UpArrow => (1, 'A'), Hex1bKey.DownArrow => (1, 'B'),
            Hex1bKey.RightArrow => (1, 'C'), Hex1bKey.LeftArrow => (1, 'D'),
            Hex1bKey.Home => (1, 'H'), Hex1bKey.End => (1, 'F'),
            Hex1bKey.Insert => (2, '~'), Hex1bKey.Delete => (3, '~'),
            Hex1bKey.PageUp => (5, '~'), Hex1bKey.PageDown => (6, '~'),
            Hex1bKey.F1 => (1, 'P'), Hex1bKey.F2 => (1, 'Q'),
            Hex1bKey.F3 => (13, '~'), Hex1bKey.F4 => (1, 'S'),
            Hex1bKey.F5 => (15, '~'), Hex1bKey.F6 => (17, '~'),
            Hex1bKey.F7 => (18, '~'), Hex1bKey.F8 => (19, '~'),
            Hex1bKey.F9 => (20, '~'), Hex1bKey.F10 => (21, '~'),
            Hex1bKey.F11 => (23, '~'), Hex1bKey.F12 => (24, '~'),
            _ => (code, 'u'),
        };
    }

    internal static int KeyCode(Hex1bKey key) => key switch
    {
        >= Hex1bKey.A and <= Hex1bKey.Z => 'a' + key - Hex1bKey.A,
        >= Hex1bKey.D0 and <= Hex1bKey.D9 => '0' + key - Hex1bKey.D0,
        Hex1bKey.Escape => 27, Hex1bKey.Enter => 13, Hex1bKey.Tab => 9,
        Hex1bKey.Backspace => 127, Hex1bKey.Spacebar => 32,
        Hex1bKey.OemComma => ',', Hex1bKey.OemPeriod => '.',
        Hex1bKey.OemMinus => '-', Hex1bKey.OemPlus => '=',
        Hex1bKey.OemQuestion => '/', Hex1bKey.Oem1 => ';',
        Hex1bKey.Oem4 => '[', Hex1bKey.Oem5 => '\\', Hex1bKey.Oem6 => ']',
        Hex1bKey.Oem7 => '\'', Hex1bKey.OemTilde => '`',
        >= Hex1bKey.NumPad0 and <= Hex1bKey.NumPad9 => 57399 + key - Hex1bKey.NumPad0,
        Hex1bKey.Decimal => 57409, Hex1bKey.Divide => 57410,
        Hex1bKey.Multiply => 57411, Hex1bKey.Subtract => 57412, Hex1bKey.Add => 57413,
        _ => 0,
    };
}
