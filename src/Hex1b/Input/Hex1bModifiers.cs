namespace Hex1b.Input;

/// <summary>
/// Modifier keys for input events.
/// </summary>
[Flags]
public enum Hex1bModifiers
{
    None = 0,
    Shift = 1,
    Alt = 2,
    Control = 4,
    /// <summary>The Super or Windows/Command key.</summary>
    Super = 8,
    /// <summary>The Hyper modifier.</summary>
    Hyper = 16,
    /// <summary>The Meta modifier.</summary>
    Meta = 32,
    /// <summary>Caps Lock is active.</summary>
    CapsLock = 64,
    /// <summary>Num Lock is active.</summary>
    NumLock = 128,
}
