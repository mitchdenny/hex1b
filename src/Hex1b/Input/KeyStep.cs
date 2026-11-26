namespace Hex1b.Input;

/// <summary>
/// Represents a single key press with modifiers in a key binding sequence.
/// </summary>
public readonly record struct KeyStep(Hex1bKey Key, Hex1bModifiers Modifiers = Hex1bModifiers.None)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & Hex1bModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & Hex1bModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & Hex1bModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & Hex1bModifiers.Super) != 0) parts.Add("Super");
        if ((Modifiers & Hex1bModifiers.Hyper) != 0) parts.Add("Hyper");
        if ((Modifiers & Hex1bModifiers.Meta) != 0) parts.Add("Meta");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    /// <summary>
    /// Checks if this step matches the given key event. Lock states do not affect matching.
    /// </summary>
    public bool Matches(Hex1bKeyEvent evt) => evt.Key == Key && WithoutLocks(evt.Modifiers) == WithoutLocks(Modifiers);

    internal static Hex1bModifiers WithoutLocks(Hex1bModifiers modifiers)
        => modifiers & ~(Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock);
}
