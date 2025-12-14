namespace Hex1b.Input;

/// <summary>
/// Represents a key binding that matches a specific key combination and executes an action.
/// </summary>
public sealed class InputBinding
{
    /// <summary>
    /// The key that triggers this binding.
    /// </summary>
    public Hex1bKey Key { get; }

    /// <summary>
    /// The modifiers required for this binding.
    /// </summary>
    public Hex1bModifiers Modifiers { get; }

    /// <summary>
    /// The action to execute when the binding matches.
    /// </summary>
    public Action Handler { get; }

    /// <summary>
    /// Optional description for this binding (for help/documentation).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Creates an input binding for a specific key combination.
    /// </summary>
    public InputBinding(Hex1bKey key, Hex1bModifiers modifiers, Action handler, string? description = null)
    {
        Key = key;
        Modifiers = modifiers;
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = description;
    }

    /// <summary>
    /// Checks if this binding matches the given key event.
    /// </summary>
    public bool Matches(Hex1bKeyEvent evt) => evt.Key == Key && evt.Modifiers == Modifiers;

    /// <summary>
    /// Executes the binding's handler.
    /// </summary>
    public void Execute() => Handler();

    // Factory methods for common patterns
    
    /// <summary>
    /// Creates a binding for a plain key (no modifiers).
    /// </summary>
    public static InputBinding Plain(Hex1bKey key, Action handler, string? description = null)
        => new(key, Hex1bModifiers.None, handler, description);

    /// <summary>
    /// Creates a binding for Ctrl+Key.
    /// </summary>
    public static InputBinding Ctrl(Hex1bKey key, Action handler, string? description = null)
        => new(key, Hex1bModifiers.Control, handler, description);

    /// <summary>
    /// Creates a binding for Alt+Key.
    /// </summary>
    public static InputBinding Alt(Hex1bKey key, Action handler, string? description = null)
        => new(key, Hex1bModifiers.Alt, handler, description);

    /// <summary>
    /// Creates a binding for Shift+Key.
    /// </summary>
    public static InputBinding Shift(Hex1bKey key, Action handler, string? description = null)
        => new(key, Hex1bModifiers.Shift, handler, description);

    /// <summary>
    /// Creates a binding for Ctrl+Shift+Key.
    /// </summary>
    public static InputBinding CtrlShift(Hex1bKey key, Action handler, string? description = null)
        => new(key, Hex1bModifiers.Control | Hex1bModifiers.Shift, handler, description);

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & Hex1bModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & Hex1bModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & Hex1bModifiers.Shift) != 0) parts.Add("Shift");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
