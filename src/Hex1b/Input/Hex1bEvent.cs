namespace Hex1b.Input;

/// <summary>
/// Base class for all Hex1b events. Events are categorized into:
/// - Keyboard events: Routed to focused node via InputRouter
/// - System events: Handled by app (resize, etc.)
/// - Terminal events: Handled by app for terminal capability detection
/// </summary>
public abstract record Hex1bEvent;

/// <summary>
/// A keyboard input event that gets routed to the focused node.
/// </summary>
/// <param name="Key">The key that was pressed.</param>
/// <param name="Character">The character produced by the keypress (may be '\0' for non-printable keys).</param>
/// <param name="Modifiers">The modifier keys held during the keypress.</param>
public sealed record Hex1bKeyEvent(Hex1bKey Key, char Character, Hex1bModifiers Modifiers) : Hex1bEvent
{
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
    /// Returns true if the key produces a printable character.
    /// </summary>
    public bool IsPrintable => Character != '\0' && !char.IsControl(Character);

    /// <summary>
    /// Creates an event for a simple key press with no modifiers.
    /// </summary>
    public static Hex1bKeyEvent Plain(Hex1bKey key, char character = '\0') 
        => new(key, character, Hex1bModifiers.None);

    /// <summary>
    /// Creates an event for a key press with Control modifier.
    /// </summary>
    public static Hex1bKeyEvent WithCtrl(Hex1bKey key, char character = '\0') 
        => new(key, character, Hex1bModifiers.Control);

    /// <summary>
    /// Creates an event for a key press with Shift modifier.
    /// </summary>
    public static Hex1bKeyEvent WithShift(Hex1bKey key, char character = '\0') 
        => new(key, character, Hex1bModifiers.Shift);

    /// <summary>
    /// Creates an event for a key press with Alt modifier.
    /// </summary>
    public static Hex1bKeyEvent WithAlt(Hex1bKey key, char character = '\0') 
        => new(key, character, Hex1bModifiers.Alt);
}

/// <summary>
/// A terminal resize event. Handled by the app to trigger re-layout.
/// </summary>
/// <param name="Width">New terminal width in characters.</param>
/// <param name="Height">New terminal height in lines.</param>
public sealed record Hex1bResizeEvent(int Width, int Height) : Hex1bEvent;

/// <summary>
/// A terminal capability response event (e.g., DA1 response for Sixel detection).
/// Handled by the app to update terminal capabilities.
/// </summary>
/// <param name="Response">The raw terminal response string.</param>
public sealed record Hex1bTerminalEvent(string Response) : Hex1bEvent;
