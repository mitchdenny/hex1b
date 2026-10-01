using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// A key binding: a key combined with optional modifier keys.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">Required modifier keys. Defaults to <see cref="Hex1bModifiers.None"/>.</param>
public readonly record struct KeyBinding(Hex1bKey Key, Hex1bModifiers Modifiers = Hex1bModifiers.None)
{
    /// <summary>Creates a key binding with no modifiers.</summary>
    public static implicit operator KeyBinding(Hex1bKey key) => new(key);
    
    /// <summary>Checks if this binding matches the given key event.</summary>
    public bool Matches(Hex1bKeyEvent keyEvent) => keyEvent.Key == Key && keyEvent.Modifiers == Modifiers;
}
