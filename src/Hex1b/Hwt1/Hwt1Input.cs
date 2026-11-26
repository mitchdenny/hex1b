using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Input;

namespace Hex1b;

internal static class Hwt1Input
{
    internal static int MouseTracking(Hex1bTerminalSnapshot snapshot)
        => (int)TerminalInputEncoder.MouseTracking(
            snapshot.MouseProtocolX10Enabled, snapshot.MouseProtocolNormalEnabled,
            snapshot.MouseProtocolButtonEnabled, snapshot.MouseProtocolAnyEnabled);

    internal static string EncodeKey(JsonElement command, Hex1bTerminal terminal)
    {
        var modes = terminal.InputModes;
        var decoded = DecodeKey(command, modes.KittyKeyboardFlags);
        return decoded.Event is null ? "" : TerminalInputEncoder.EncodeKey(decoded.Event, modes);
    }

    /// <summary>A decoded browser key command and the physical key that owns its press.</summary>
    /// <param name="Event">The key event, or null when the key has no encoding in the current mode.</param>
    /// <param name="HeldKey">The DOM code (or key when no code was reported) used to pair releases.</param>
    internal readonly record struct KeyCommand(Hex1bKeyEvent? Event, string HeldKey);

    /// <summary>
    /// Decodes an HWT1 key command. Without Kitty keyboard flags, the legacy fields and
    /// encoding are used unchanged; extended fields are validated but ignored.
    /// Identities are only reported when the browser supplied them: shifted or uppercase
    /// text has no unshifted identity without OS layout metadata, so it is reported
    /// as text without a key code rather than by guessing a case mapping.
    /// </summary>
    internal static KeyCommand DecodeKey(JsonElement command, int kittyFlags)
    {
        var key = command.GetProperty("key").GetString() ?? throw new InvalidDataException("Missing key.");
        var code = "";
        if (command.TryGetProperty("code", out var codeValue))
        {
            code = codeValue.GetString() ?? throw new InvalidDataException("Key code must be a string.");
            if (code.Length > 64)
                throw new InvalidDataException("Key code is too long.");
        }
        var eventType = Hex1bKeyEventType.Press;
        if (command.TryGetProperty("eventType", out var eventTypeValue))
            eventType = eventTypeValue.GetString() switch
            {
                "press" => Hex1bKeyEventType.Press,
                "repeat" => Hex1bKeyEventType.Repeat,
                "release" => Hex1bKeyEventType.Release,
                _ => throw new InvalidDataException("Unsupported keyboard event type.")
            };
        var modifiers = Modifiers(command) |
            (Flag(command, "meta") ? Hex1bModifiers.Super : Hex1bModifiers.None) |
            (Flag(command, "capsLock") ? Hex1bModifiers.CapsLock : Hex1bModifiers.None) |
            (Flag(command, "numLock") ? Hex1bModifiers.NumLock : Hex1bModifiers.None);
        var heldKey = code.Length > 0 ? code : key;
        var namedKey = NamedKey(key);
        var rune = SingleRune(key);
        var extended = ExtendedIdentity(key, code);
        var unsidedModifier = key is "Shift" or "Control" or "Alt" or "Meta" or "OS";
        if ((kittyFlags & 31) == 0)
        {
            // Legacy input keeps its original key set. Kitty-only keys or releases can still arrive
            // after the workload leaves Kitty mode; they have no legacy encoding.
            if (namedKey is null && key.Length != 1)
            {
                if (extended is not null || unsidedModifier)
                    return new(null, heldKey);
                throw new InvalidDataException($"Unsupported key: {key}");
            }
            if (eventType == Hex1bKeyEventType.Release)
                return new(null, heldKey);
            var legacyModifiers = modifiers & (Hex1bModifiers.Shift | Hex1bModifiers.Alt | Hex1bModifiers.Control);
            return new(new Hex1bKeyEvent(namedKey ?? Hex1bKey.None, key.Length == 1 ? key : "", legacyModifiers), heldKey);
        }

        if (namedKey is null && rune is null && extended is null)
        {
            // A modifier without a reported side has no Kitty identity; never guess one.
            if (unsidedModifier)
                return new(null, heldKey);
            throw new InvalidDataException($"Unsupported key: {key}");
        }

        if (extended is { } identity)
        {
            var keypadText = identity is >= 57399 and <= 57416 && rune is { } keypadRune && !Rune.IsControl(keypadRune)
                ? key : "";
            return new(new Hex1bKeyEvent(Hex1bKey.None, keypadText, modifiers)
            {
                EventType = eventType,
                UnicodeKeyCode = identity,
            }, heldKey);
        }
        if (namedKey is { } named)
            return new(new Hex1bKeyEvent(named, "", modifiers) { EventType = eventType }, heldKey);

        var value = rune!.Value;
        var shortcut = (modifiers & (Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Super)) != 0;
        // Report-all mode always uses escape codes, so modified keys must not claim to produce text.
        var text = Rune.IsControl(value) || (shortcut && (kittyFlags & 8) != 0) ? "" : key;
        var knownIdentity = !Rune.IsControl(value) && value.Value is not (>= 57344 and <= 63743) &&
            (modifiers & Hex1bModifiers.Shift) == 0 && !Rune.IsUpper(value);
        return new(new Hex1bKeyEvent(Hex1bKey.None, text, modifiers)
        {
            EventType = eventType,
            UnicodeKeyCode = knownIdentity ? value.Value : null,
            BaseLayoutKeyCode = knownIdentity ? BaseLayoutKey(code) : null,
        }, heldKey);
    }

    private static Rune? SingleRune(string key)
        => key.Length > 0 && Rune.DecodeFromUtf16(key, out var rune, out var consumed) == OperationStatus.Done &&
            consumed == key.Length ? rune : null;

    private static Hex1bKey? NamedKey(string key) => key switch
    {
        "ArrowUp" => Hex1bKey.UpArrow,
        "ArrowDown" => Hex1bKey.DownArrow,
        "ArrowRight" => Hex1bKey.RightArrow,
        "ArrowLeft" => Hex1bKey.LeftArrow,
        "Home" => Hex1bKey.Home,
        "End" => Hex1bKey.End,
        "Insert" => Hex1bKey.Insert,
        "Delete" => Hex1bKey.Delete,
        "PageUp" => Hex1bKey.PageUp,
        "PageDown" => Hex1bKey.PageDown,
        "F1" => Hex1bKey.F1,
        "F2" => Hex1bKey.F2,
        "F3" => Hex1bKey.F3,
        "F4" => Hex1bKey.F4,
        "F5" => Hex1bKey.F5,
        "F6" => Hex1bKey.F6,
        "F7" => Hex1bKey.F7,
        "F8" => Hex1bKey.F8,
        "F9" => Hex1bKey.F9,
        "F10" => Hex1bKey.F10,
        "F11" => Hex1bKey.F11,
        "F12" => Hex1bKey.F12,
        "Enter" => Hex1bKey.Enter,
        "Backspace" => Hex1bKey.Backspace,
        "Tab" => Hex1bKey.Tab,
        "Escape" => Hex1bKey.Escape,
        _ => null
    };

    // Kitty functional key codes for keys the browser identifies by DOM key and physical code.
    private static int? ExtendedIdentity(string key, string code)
    {
        if (code.StartsWith("Numpad", StringComparison.Ordinal))
        {
            var keypad = Keypad(key, code);
            if (keypad is not null)
                return keypad;
        }
        if (key.Length is 3 or 4 && key[0] == 'F' && int.TryParse(key.AsSpan(1), NumberStyles.None,
                CultureInfo.InvariantCulture, out var function) && function is >= 13 and <= 35)
            return 57376 + function - 13;
        return key switch
        {
            "CapsLock" => 57358,
            "ScrollLock" => 57359,
            "NumLock" => 57360,
            "PrintScreen" => 57361,
            "Pause" => 57362,
            "ContextMenu" => 57363,
            "Shift" => code switch { "ShiftLeft" => 57441, "ShiftRight" => 57447, _ => null },
            "Control" => code switch { "ControlLeft" => 57442, "ControlRight" => 57448, _ => null },
            "Alt" => code switch { "AltLeft" => 57443, "AltRight" => 57449, _ => null },
            "Meta" or "OS" => code switch
            {
                "MetaLeft" or "OSLeft" => 57444,
                "MetaRight" or "OSRight" => 57450,
                _ => null
            },
            _ => null
        };
    }

    private static int? Keypad(string key, string code) => code switch
    {
        _ when code.Length == 7 && code[6] is >= '0' and <= '9' && key.Length == 1 && key[0] == code[6]
            => 57399 + code[6] - '0',
        "NumpadDecimal" when key is "." or "," => 57409,
        "NumpadDivide" when key == "/" => 57410,
        "NumpadMultiply" when key == "*" => 57411,
        "NumpadSubtract" when key == "-" => 57412,
        "NumpadAdd" when key == "+" => 57413,
        "NumpadEnter" when key == "Enter" => 57414,
        "NumpadEqual" when key == "=" => 57415,
        "NumpadComma" when key is "," or "." => 57416,
        _ => key switch
        {
            // Navigation identities only apply when the browser reports the keypad's navigation key.
            "ArrowLeft" when code == "Numpad4" => 57417,
            "ArrowRight" when code == "Numpad6" => 57418,
            "ArrowUp" when code == "Numpad8" => 57419,
            "ArrowDown" when code == "Numpad2" => 57420,
            "PageUp" when code == "Numpad9" => 57421,
            "PageDown" when code == "Numpad3" => 57422,
            "Home" when code == "Numpad7" => 57423,
            "End" when code == "Numpad1" => 57424,
            "Insert" when code == "Numpad0" => 57425,
            "Delete" when code == "NumpadDecimal" => 57426,
            "Clear" when code == "Numpad5" => 57427,
            _ => null
        }
    };

    // PC-101 physical positions; the base layout key is defined by position, not by the active layout.
    private static int? BaseLayoutKey(string code)
    {
        if (code.Length == 4 && code.StartsWith("Key", StringComparison.Ordinal) && code[3] is >= 'A' and <= 'Z')
            return char.ToLowerInvariant(code[3]);
        if (code.Length == 6 && code.StartsWith("Digit", StringComparison.Ordinal) && code[5] is >= '0' and <= '9')
            return code[5];
        return code switch
        {
            "Minus" => '-', "Equal" => '=', "BracketLeft" => '[', "BracketRight" => ']',
            "Backslash" => '\\', "Semicolon" => ';', "Quote" => '\'', "Backquote" => '`',
            "Comma" => ',', "Period" => '.', "Slash" => '/', "Space" => ' ',
            _ => null
        };
    }

    internal static string EncodePaste(string text, Hex1bTerminal terminal)
        => TerminalInputEncoder.EncodePaste(text, terminal.InputModes);

    /// <summary>
    /// Encodes committed browser text. Kitty report-all mode reports text as key events
    /// (with code points only when text reporting is enabled); other modes send text unchanged.
    /// Text containing control characters is sent unchanged because it has no key identity.
    /// </summary>
    internal static string EncodeText(string text, Hex1bTerminal terminal)
    {
        var modes = terminal.InputModes;
        if ((modes.KittyKeyboardFlags & 8) == 0 || text.Length == 0)
            return text;
        foreach (var rune in text.EnumerateRunes())
            if (Rune.IsControl(rune))
                return text;
        return TerminalInputEncoder.EncodeKey(Hex1bKeyEvent.FromText(text), modes);
    }

    internal static byte[] EncodeMouse(JsonElement command, Hex1bTerminal terminal)
    {
        var action = command.GetProperty("action").GetString();
        if (action is not ("down" or "up" or "move" or "wheel"))
            throw new InvalidDataException("Unsupported mouse action.");
        var button = command.GetProperty("button").GetString() switch
        {
            "left" => TerminalMouseButton.Left,
            "middle" => TerminalMouseButton.Middle,
            "right" => TerminalMouseButton.Right,
            "none" => TerminalMouseButton.None,
            "wheelUp" => TerminalMouseButton.WheelUp,
            "wheelDown" => TerminalMouseButton.WheelDown,
            "wheelLeft" => TerminalMouseButton.WheelLeft,
            "wheelRight" => TerminalMouseButton.WheelRight,
            _ => throw new InvalidDataException("Unsupported mouse button.")
        };
        if ((action == "wheel") != (button >= TerminalMouseButton.WheelUp) ||
            (button == TerminalMouseButton.None && action != "move"))
            throw new InvalidDataException("Mouse button does not match its action.");
        var x = command.GetProperty("x").GetInt32();
        var y = command.GetProperty("y").GetInt32();
        if (x is < 0 or > 1023 || y is < 0 or > 511)
            throw new InvalidDataException("Mouse coordinates are outside protocol bounds.");
        var count = command.TryGetProperty("count", out var value) ? value.GetInt32() : 1;
        if (count is < 1 or > 32 || (action != "wheel" && count != 1))
            throw new InvalidDataException("Only wheel events can repeat, at most 32 times.");
        var typedAction = action switch
        {
            "up" => MouseAction.Up,
            "move" => MouseAction.Move,
            _ => MouseAction.Down
        };
        return TerminalInputEncoder.EncodeMouse(
            new TerminalMouseInput(button, typedAction, x, y, Modifiers(command), count), terminal.InputModes);
    }

    private static Hex1bModifiers Modifiers(JsonElement command)
        => (Flag(command, "shift") ? Hex1bModifiers.Shift : Hex1bModifiers.None) |
            (Flag(command, "alt") ? Hex1bModifiers.Alt : Hex1bModifiers.None) |
            (Flag(command, "ctrl") ? Hex1bModifiers.Control : Hex1bModifiers.None);

    private static bool Flag(JsonElement command, string name)
    {
        if (!command.TryGetProperty(name, out var value))
            return false;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"Input flag '{name}' must be a boolean.")
        };
    }
}
