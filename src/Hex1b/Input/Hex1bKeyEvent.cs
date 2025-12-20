namespace Hex1b.Input;

/// <summary>
/// A keyboard input event that gets routed to the focused node.
/// </summary>
/// <param name="Key">The key that was pressed.</param>
/// <param name="Text">The text produced by the keypress (may be empty for non-printable keys). Supports multi-char input like emojis.</param>
/// <param name="Modifiers">The modifier keys held during the keypress.</param>
public sealed record Hex1bKeyEvent(Hex1bKey Key, string Text, Hex1bModifiers Modifiers) : Hex1bEvent
{
    /// <summary>
    /// Legacy constructor for single character input.
    /// </summary>
    public Hex1bKeyEvent(Hex1bKey key, char character, Hex1bModifiers modifiers)
        : this(key, character == '\0' ? "" : character.ToString(), modifiers)
    {
    }

    /// <summary>
    /// Returns true if the Shift modifier is active.
    /// </summary>
    public bool Shift => (Modifiers & Hex1bModifiers.Shift) != 0;

    /// <summary>
    /// Returns true if the Alt modifier is active.
    /// </summary>
    public bool Alt => (Modifiers & Hex1bModifiers.Alt) != 0;

    /// <summary>
    /// Returns true if the Control modifier is active.
    /// </summary>
    public bool Control => (Modifiers & Hex1bModifiers.Control) != 0;

    /// <summary>
    /// Gets the first character of the text, or '\0' if empty.
    /// For compatibility with code expecting a single char.
    /// </summary>
    public char Character => Text.Length > 0 ? Text[0] : '\0';

    /// <summary>
    /// Returns true if the key produces printable text.
    /// </summary>
    public bool IsPrintable => Text.Length > 0 && (Text.Length > 1 || !char.IsControl(Text[0]));

    /// <summary>
    /// Creates an event for a simple key press with no modifiers.
    /// </summary>
    public static Hex1bKeyEvent Plain(Hex1bKey key, char character = '\0') 
        => new(key, character == '\0' ? "" : character.ToString(), Hex1bModifiers.None);

    /// <summary>
    /// Creates an event for text input with no modifiers (e.g., paste, emoji).
    /// </summary>
    public static Hex1bKeyEvent FromText(string text) 
        => new(Hex1bKey.None, text, Hex1bModifiers.None);

    /// <summary>
    /// Creates an event for a key press with Control modifier.
    /// </summary>
    public static Hex1bKeyEvent WithCtrl(Hex1bKey key, char character = '\0') 
        => new(key, character == '\0' ? "" : character.ToString(), Hex1bModifiers.Control);

    /// <summary>
    /// Creates an event for a key press with Shift modifier.
    /// </summary>
    public static Hex1bKeyEvent WithShift(Hex1bKey key, char character = '\0') 
        => new(key, character == '\0' ? "" : character.ToString(), Hex1bModifiers.Shift);

    /// <summary>
    /// Creates an event for a key press with Alt modifier.
    /// </summary>
    public static Hex1bKeyEvent WithAlt(Hex1bKey key, char character = '\0') 
        => new(key, character == '\0' ? "" : character.ToString(), Hex1bModifiers.Alt);
}
