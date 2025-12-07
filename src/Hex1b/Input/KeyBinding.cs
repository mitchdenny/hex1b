namespace Hex1b.Input;

/// <summary>
/// Represents a key combination (key + modifiers) for shortcuts.
/// </summary>
public readonly struct KeyBinding : IEquatable<KeyBinding>
{
    public ConsoleKey Key { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public KeyBinding(ConsoleKey key, bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    /// <summary>
    /// Creates a binding with Ctrl modifier.
    /// </summary>
    public static KeyBinding WithCtrl(ConsoleKey key) => new(key, ctrl: true);

    /// <summary>
    /// Creates a binding with Alt modifier.
    /// </summary>
    public static KeyBinding WithAlt(ConsoleKey key) => new(key, alt: true);

    /// <summary>
    /// Creates a binding with Shift modifier.
    /// </summary>
    public static KeyBinding WithShift(ConsoleKey key) => new(key, shift: true);

    /// <summary>
    /// Creates a binding with Ctrl+Shift modifiers.
    /// </summary>
    public static KeyBinding WithCtrlShift(ConsoleKey key) => new(key, ctrl: true, shift: true);

    /// <summary>
    /// Creates a binding for a plain key (no modifiers).
    /// </summary>
    public static KeyBinding Plain(ConsoleKey key) => new(key);

    /// <summary>
    /// Checks if this binding matches the given input event.
    /// </summary>
    public bool Matches(KeyInputEvent evt) =>
        evt.Key == Key && evt.Control == Ctrl && evt.Alt == Alt && evt.Shift == Shift;

    public bool Equals(KeyBinding other) =>
        Key == other.Key && Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;

    public override bool Equals(object? obj) => obj is KeyBinding other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Key, Ctrl, Alt, Shift);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static bool operator ==(KeyBinding left, KeyBinding right) => left.Equals(right);
    public static bool operator !=(KeyBinding left, KeyBinding right) => !left.Equals(right);
}
