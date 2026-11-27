using System.Globalization;
using System.Text;

namespace Hex1b.Input;

/// <summary>
/// Encodes key events using the Kitty keyboard protocol.
/// </summary>
/// <remarks>
/// This is a port of the encoding rules in Kitty's <c>kitty/key_encoding.c</c>, which is used to
/// resolve cases where the specification is ambiguous.
/// </remarks>
internal static class KittyKeyboardEncoder
{
    private const int Disambiguate = 1;
    private const int ReportEvents = 2;
    private const int ReportAlternates = 4;
    private const int ReportAll = 8;
    private const int EmbedText = 16;

    private const int Shift = 1, Alt = 2, Ctrl = 4;
    private const int LockMask = 64 | 128;
    private const int TextSuppressingMods = (int)(Hex1bModifiers.Alt | Hex1bModifiers.Control |
        Hex1bModifiers.Super | Hex1bModifiers.Hyper | Hex1bModifiers.Meta);

    // Kitty's private-use functional key numbers.
    private const int FunctionalFirst = 57344, FunctionalLast = 63743;
    private const int KeyEscape = 57344, KeyEnter = 57345, KeyTab = 57346, KeyBackspace = 57347;
    private const int KeyInsert = 57348, KeyDelete = 57349, KeyLeft = 57350, KeyRight = 57351;
    private const int KeyUp = 57352, KeyDown = 57353, KeyPageUp = 57354, KeyPageDown = 57355;
    private const int KeyHome = 57356, KeyEnd = 57357, KeyF1 = 57364;
    private const int KeypadFirst = 57399, KeypadLast = 57427;
    private const int ModifierFirst = 57441, ModifierLast = 57454;

    /// <summary>
    /// Encodes <paramref name="input"/> for the negotiated progressive enhancement
    /// <paramref name="flags"/>.
    /// </summary>
    /// <returns><see langword="false"/> when the legacy encoder should handle the event.</returns>
    internal static bool TryEncode(Hex1bKeyEvent input, int flags, out string encoded, bool cursorKeyMode = false)
    {
        encoded = "";
        if (flags == 0)
            return input.EventType == Hex1bKeyEventType.Release;

        var disambiguate = (flags & Disambiguate) != 0;
        var reportEvents = (flags & ReportEvents) != 0;
        var reportAll = (flags & ReportAll) != 0;
        var key = KeyNumber(input);
        if (!reportAll && key is >= ModifierFirst and <= ModifierLast)
            return true;

        var release = input.EventType == Hex1bKeyEventType.Release;
        // Release events carry no text, matching Kitty and Ghostty.
        var hasText = !release && input.Text.Length > 0 && Rune.GetRuneAt(input.Text, 0).Value is >= 32 and not 127;
        if (key == 0 && !hasText)
            return true;
        if (!disambiguate && !reportAll && key is >= KeypadFirst and <= KeypadLast)
            key = KeypadToNormal(key);

        // Text producing keys are sent as plain text unless all keys are reported as escape codes.
        // Hex1b events may carry text alongside Ctrl/Alt/etc, which a terminal would not report as text.
        if (!reportAll && hasText && ((int)input.Modifiers & TextSuppressingMods) == 0)
        {
            encoded = input.Text;
            return true;
        }

        if (!reportEvents && release)
            return true;

        var data = new EncodingData
        {
            Key = key,
            Mods = (int)input.Modifiers & 0xFF,
            AddActions = reportEvents && input.EventType != Hex1bKeyEventType.Press,
            Action = (int)input.EventType + 1,
            Text = (flags & EmbedText) != 0 ? AssociatedText(input.Text, release) : "",
        };
        var shifted = input.ShiftedKeyCode ?? (input.Key is >= Hex1bKey.A and <= Hex1bKey.Z ? 'A' + (input.Key - Hex1bKey.A) : 0);
        var alternate = input.BaseLayoutKeyCode ?? 0;

        encoded = key is >= FunctionalFirst and <= FunctionalLast
            ? EncodeFunctionKey(data, flags, cursorKeyMode)
            : EncodeTextKey(data, flags, shifted, alternate);
        return true;
    }

    private static string EncodeFunctionKey(EncodingData data, int flags, bool cursorKeyMode)
    {
        var key = data.Key;
        var mods = data.Mods;
        var legacyMode = (flags & (Disambiguate | ReportEvents | ReportAll)) == 0;
        var reportAll = (flags & ReportAll) != 0;
        var release = data.Action == 3;

        if (cursorKeyMode && legacyMode && mods == 0)
        {
            var ss3 = key switch
            {
                KeyUp => "\x1bOA", KeyDown => "\x1bOB", KeyRight => "\x1bOC", KeyLeft => "\x1bOD",
                KeyEnd => "\x1bOF", KeyHome => "\x1bOH", _ => null,
            };
            if (ss3 is not null)
                return ss3;
        }

        if (mods == 0)
        {
            if ((flags & (Disambiguate | ReportAll)) == 0 && key == KeyEscape)
                return "\x1b";
            if (legacyMode && key is >= KeyF1 and <= KeyF1 + 3)
                return "\x1bO" + (char)('P' + (key - KeyF1));
        }
        else if (legacyMode)
        {
            var legacy = LegacyFunctionalKey(key, mods);
            if (legacy is not null)
                return legacy;
        }

        if ((mods & ~LockMask) == 0 && !reportAll && key is KeyEnter or KeyBackspace or KeyTab)
        {
            if (release)
                return "";
            return key switch { KeyEnter => "\r", KeyBackspace => "\x7f", _ => "\t" };
        }

        var (number, trailer) = key switch
        {
            KeyEscape => (27, 'u'), KeyEnter => (13, 'u'), KeyTab => (9, 'u'), KeyBackspace => (127, 'u'),
            KeyInsert => (2, '~'), KeyDelete => (3, '~'),
            KeyLeft => (1, 'D'), KeyRight => (1, 'C'), KeyUp => (1, 'A'), KeyDown => (1, 'B'),
            KeyPageUp => (5, '~'), KeyPageDown => (6, '~'), KeyHome => (1, 'H'), KeyEnd => (1, 'F'),
            KeyF1 => (1, 'P'), KeyF1 + 1 => (1, 'Q'), KeyF1 + 2 => (13, '~'), KeyF1 + 3 => (1, 'S'),
            KeyF1 + 4 => (15, '~'), KeyF1 + 5 => (17, '~'), KeyF1 + 6 => (18, '~'), KeyF1 + 7 => (19, '~'),
            KeyF1 + 8 => (20, '~'), KeyF1 + 9 => (21, '~'), KeyF1 + 10 => (23, '~'), KeyF1 + 11 => (24, '~'),
            _ => (key, 'u'),
        };
        return Serialize(data with { Key = number }, 0, 0, false, trailer);
    }

    private static string? LegacyFunctionalKey(int key, int mods)
    {
        var prefix = (mods & Alt) != 0 ? "\x1b" : "";
        switch (key)
        {
            case KeyEnter: return prefix + "\r";
            case KeyEscape: return prefix + "\x1b";
            case KeyBackspace: return prefix + ((mods & Ctrl) != 0 ? "\b" : "\x7f");
            case KeyTab:
                if ((mods & Shift) != 0)
                    return ((mods & Alt) != 0 ? "\x1b\x1b" : "\x1b") + "[Z";
                return prefix + "\t";
            default: return null;
        }
    }

    private static string EncodeTextKey(EncodingData data, int flags, int shifted, int alternate)
    {
        var addAlternates = (flags & ReportAlternates) != 0 &&
            ((shifted > 0 && (data.Mods & Shift) != 0) || alternate > 0);
        var simple = !data.AddActions && !addAlternates && data.Text.Length == 0;
        if (simple)
        {
            if (data.Mods == 0)
                return (flags & ReportAll) != 0 ? Serialize(data, 0, 0, false, 'u') : char.ConvertFromUtf32(data.Key);
            if ((flags & (Disambiguate | ReportAll)) == 0)
            {
                if (IsLegacyAscii(data.Key) || (shifted > 0 && IsLegacyAscii(shifted)))
                {
                    var legacy = EncodePrintableAsciiLegacy(data.Key, shifted, data.Mods);
                    if (legacy.Length > 0)
                        return legacy;
                }
                if (data.Mods is Ctrl or Alt or (Ctrl | Alt) && alternate > 0 &&
                    !IsLegacyAscii(data.Key) && IsLegacyAscii(alternate))
                {
                    var legacy = EncodePrintableAsciiLegacy(alternate, 0, data.Mods);
                    if (legacy.Length > 0)
                        return legacy;
                }
            }
        }

        return addAlternates
            ? Serialize(data, (data.Mods & Shift) != 0 ? shifted : 0, alternate, true, 'u')
            : Serialize(data, 0, 0, false, 'u');
    }

    private static string EncodePrintableAsciiLegacy(int keyNumber, int shifted, int mods)
    {
        var key = (char)keyNumber;
        if (mods == 0)
            return key.ToString();
        var remaining = mods;
        if ((remaining & Shift) != 0 && shifted > 0 && shifted < 128 && shifted != key &&
            ((remaining & Ctrl) == 0 || key is < 'a' or > 'z'))
        {
            key = (char)shifted;
            remaining &= ~Shift;
        }

        if (mods == Shift) return key.ToString();
        if (remaining == Alt) return "\x1b" + key;
        if (remaining == Ctrl) return CtrledKey(key).ToString();
        if (remaining == (Ctrl | Alt)) return "\x1b" + CtrledKey(key);
        if (key == ' ')
        {
            if (remaining == (Ctrl | Shift)) return CtrledKey(key).ToString();
            if (remaining == (Alt | Shift)) return "\x1b" + key;
        }
        return "";
    }

    private static char CtrledKey(char key) => key switch
    {
        ' ' or '2' or '@' => '\0',
        '/' or '7' or '_' => (char)31,
        '3' or '[' => (char)27,
        '4' or '\\' => (char)28,
        '5' or ']' => (char)29,
        '6' or '^' or '~' => (char)30,
        '8' or '?' => (char)127,
        >= 'a' and <= 'z' => (char)(key - 'a' + 1),
        _ => key,
    };

    private static bool IsLegacyAscii(int key)
        => key is >= 'a' and <= 'z' or >= '0' and <= '9' or ' ' ||
            (key < 128 && "!@#$%^&*()`~-_=+[{]}\\|;:'\",<.>/?".Contains((char)key));

    private static string Serialize(EncodingData data, int shifted, int alternate, bool addAlternates, char trailer)
    {
        var secondField = data.Mods != 0 || data.AddActions;
        var thirdField = data.Text.Length > 0;
        var builder = new StringBuilder("\x1b[");
        if (data.Key != 1 || addAlternates || secondField || thirdField)
            builder.Append(data.Key.ToString(CultureInfo.InvariantCulture));
        if (addAlternates)
        {
            builder.Append(':');
            if (shifted > 0)
                builder.Append(shifted.ToString(CultureInfo.InvariantCulture));
            if (alternate > 0)
                builder.Append(':').Append(alternate.ToString(CultureInfo.InvariantCulture));
        }
        if (secondField || thirdField)
        {
            builder.Append(';');
            if (secondField)
                builder.Append((data.Mods + 1).ToString(CultureInfo.InvariantCulture));
            if (data.AddActions)
                builder.Append(':').Append(data.Action.ToString(CultureInfo.InvariantCulture));
        }
        if (thirdField)
            builder.Append(';').Append(data.Text);
        return builder.Append(trailer).ToString();
    }

    private static string AssociatedText(string text, bool release)
    {
        if (release)
            return "";
        // The specification forbids C0/C1 controls in associated text.
        return string.Join(':', text.EnumerateRunes()
            .Where(rune => rune.Value >= 32 && rune.Value is < 127 or > 159)
            .Select(rune => rune.Value.ToString(CultureInfo.InvariantCulture)));
    }

    private static int KeyNumber(Hex1bKeyEvent input)
    {
        var functional = input.Key switch
        {
            Hex1bKey.Escape => KeyEscape, Hex1bKey.Enter => KeyEnter,
            Hex1bKey.Tab => KeyTab, Hex1bKey.Backspace => KeyBackspace,
            Hex1bKey.Insert => KeyInsert, Hex1bKey.Delete => KeyDelete,
            Hex1bKey.LeftArrow => KeyLeft, Hex1bKey.RightArrow => KeyRight,
            Hex1bKey.UpArrow => KeyUp, Hex1bKey.DownArrow => KeyDown,
            Hex1bKey.PageUp => KeyPageUp, Hex1bKey.PageDown => KeyPageDown,
            Hex1bKey.Home => KeyHome, Hex1bKey.End => KeyEnd,
            >= Hex1bKey.F1 and <= Hex1bKey.F12 => KeyF1 + (input.Key - Hex1bKey.F1),
            >= Hex1bKey.NumPad0 and <= Hex1bKey.NumPad9 => KeypadFirst + (input.Key - Hex1bKey.NumPad0),
            Hex1bKey.Decimal => 57409, Hex1bKey.Divide => 57410, Hex1bKey.Multiply => 57411,
            Hex1bKey.Subtract => 57412, Hex1bKey.Add => 57413,
            _ => 0,
        };
        if (functional != 0)
            return functional;
        if (input.UnicodeKeyCode is { } code)
            return code switch { 27 => KeyEscape, 13 => KeyEnter, 9 => KeyTab, 127 => KeyBackspace, _ => code };
        return input.Key switch
        {
            >= Hex1bKey.A and <= Hex1bKey.Z => 'a' + (input.Key - Hex1bKey.A),
            >= Hex1bKey.D0 and <= Hex1bKey.D9 => '0' + (input.Key - Hex1bKey.D0),
            Hex1bKey.Spacebar => ' ',
            Hex1bKey.OemComma => ',', Hex1bKey.OemPeriod => '.', Hex1bKey.OemMinus => '-',
            Hex1bKey.OemPlus => '=', Hex1bKey.OemQuestion => '/', Hex1bKey.Oem1 => ';',
            Hex1bKey.Oem4 => '[', Hex1bKey.Oem5 => '\\', Hex1bKey.Oem6 => ']',
            Hex1bKey.Oem7 => '\'', Hex1bKey.OemTilde => '`',
            _ => 0,
        };
    }

    private static int KeypadToNormal(int key) => key switch
    {
        >= KeypadFirst and <= KeypadFirst + 9 => '0' + (key - KeypadFirst),
        57409 => '.', 57410 => '/', 57411 => '*', 57412 => '-', 57413 => '+', 57415 => '=',
        57414 => KeyEnter, 57417 => KeyLeft, 57418 => KeyRight, 57419 => KeyUp, 57420 => KeyDown,
        57421 => KeyPageUp, 57422 => KeyPageDown, 57423 => KeyHome, 57424 => KeyEnd,
        57425 => KeyInsert, 57426 => KeyDelete,
        _ => key,
    };

    private readonly record struct EncodingData
    {
        public int Key { get; init; }
        public int Mods { get; init; }
        public bool AddActions { get; init; }
        public int Action { get; init; }
        public string Text { get; init; }
    }
}