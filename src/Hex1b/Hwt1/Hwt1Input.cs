using System.Text.Json;
using System.Text;
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
        => TerminalInputEncoder.EncodeKey(
            DecodeKey(command, (terminal.InputModes.KittyKeyboardFlags & 8) != 0), terminal.InputModes);

    internal static Hex1bKeyEvent DecodeKey(JsonElement command, bool reportAllKeys = false)
    {
        var key = command.GetProperty("key").GetString() ?? throw new InvalidDataException("Missing key.");
        var functionalCode = key switch
        {
            "CapsLock" => 57358, "ScrollLock" => 57359, "NumLock" => 57360,
            "ShiftLeft" => 57441, "ControlLeft" => 57442, "AltLeft" => 57443,
            "MetaLeft" => 57444, "ShiftRight" => 57447, "ControlRight" => 57448,
            "AltRight" => 57449, "MetaRight" => 57450,
            _ => (int?)null,
        };
        var typedKey = key switch
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
            _ when functionalCode.HasValue => Hex1bKey.None,
            _ when key.Length > 0 && Rune.TryGetRuneAt(key, 0, out var rune) && rune.Utf16SequenceLength == key.Length => Hex1bKey.None,
            _ => throw new InvalidDataException($"Unsupported key: {key}")
        };
        var type = command.TryGetProperty("eventType", out var eventType) ? eventType.GetString() : "press";
        var kind = type switch
        {
            "press" => Hex1bKeyEventType.Press,
            "repeat" => Hex1bKeyEventType.Repeat,
            "release" => Hex1bKeyEventType.Release,
            _ => throw new InvalidDataException("Unsupported keyboard event type.")
        };
        var modifiers = Modifiers(command);
        var text = typedKey == Hex1bKey.None && !functionalCode.HasValue &&
            kind != Hex1bKeyEventType.Release &&
            (!reportAllKeys || (modifiers & (Hex1bModifiers.Control | Hex1bModifiers.Alt | Hex1bModifiers.Super)) == 0)
            ? key : "";
        var unicodeKey = functionalCode ?? (typedKey == Hex1bKey.None && Rune.TryGetRuneAt(key, 0, out var keyRune)
            ? Rune.ToLowerInvariant(keyRune).Value : (int?)null);
        if (unicodeKey is int code)
            typedKey = KittyKeyboardInput.MapKey('u', code);
        return new Hex1bKeyEvent(typedKey, text, modifiers)
        {
            EventType = kind,
            UnicodeKeyCode = unicodeKey,
        };
    }

    internal static string EncodePaste(string text, Hex1bTerminal terminal)
        => TerminalInputEncoder.EncodePaste(text, terminal.InputModes);

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
            (Flag(command, "ctrl") ? Hex1bModifiers.Control : Hex1bModifiers.None) |
            (Flag(command, "super") ? Hex1bModifiers.Super : Hex1bModifiers.None) |
            (Flag(command, "capsLock") ? Hex1bModifiers.CapsLock : Hex1bModifiers.None) |
            (Flag(command, "numLock") ? Hex1bModifiers.NumLock : Hex1bModifiers.None);

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
