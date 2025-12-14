namespace Hex1b.Input;

/// <summary>
/// Maps between System.ConsoleKey and Hex1bKey.
/// This centralizes the dependency on System.Console types.
/// </summary>
public static class KeyMapper
{
    /// <summary>
    /// Converts a ConsoleKey to a Hex1bKey.
    /// </summary>
    public static Hex1bKey ToHex1bKey(ConsoleKey consoleKey)
    {
        return consoleKey switch
        {
            // Letters
            ConsoleKey.A => Hex1bKey.A,
            ConsoleKey.B => Hex1bKey.B,
            ConsoleKey.C => Hex1bKey.C,
            ConsoleKey.D => Hex1bKey.D,
            ConsoleKey.E => Hex1bKey.E,
            ConsoleKey.F => Hex1bKey.F,
            ConsoleKey.G => Hex1bKey.G,
            ConsoleKey.H => Hex1bKey.H,
            ConsoleKey.I => Hex1bKey.I,
            ConsoleKey.J => Hex1bKey.J,
            ConsoleKey.K => Hex1bKey.K,
            ConsoleKey.L => Hex1bKey.L,
            ConsoleKey.M => Hex1bKey.M,
            ConsoleKey.N => Hex1bKey.N,
            ConsoleKey.O => Hex1bKey.O,
            ConsoleKey.P => Hex1bKey.P,
            ConsoleKey.Q => Hex1bKey.Q,
            ConsoleKey.R => Hex1bKey.R,
            ConsoleKey.S => Hex1bKey.S,
            ConsoleKey.T => Hex1bKey.T,
            ConsoleKey.U => Hex1bKey.U,
            ConsoleKey.V => Hex1bKey.V,
            ConsoleKey.W => Hex1bKey.W,
            ConsoleKey.X => Hex1bKey.X,
            ConsoleKey.Y => Hex1bKey.Y,
            ConsoleKey.Z => Hex1bKey.Z,

            // Numbers
            ConsoleKey.D0 => Hex1bKey.D0,
            ConsoleKey.D1 => Hex1bKey.D1,
            ConsoleKey.D2 => Hex1bKey.D2,
            ConsoleKey.D3 => Hex1bKey.D3,
            ConsoleKey.D4 => Hex1bKey.D4,
            ConsoleKey.D5 => Hex1bKey.D5,
            ConsoleKey.D6 => Hex1bKey.D6,
            ConsoleKey.D7 => Hex1bKey.D7,
            ConsoleKey.D8 => Hex1bKey.D8,
            ConsoleKey.D9 => Hex1bKey.D9,

            // Function keys
            ConsoleKey.F1 => Hex1bKey.F1,
            ConsoleKey.F2 => Hex1bKey.F2,
            ConsoleKey.F3 => Hex1bKey.F3,
            ConsoleKey.F4 => Hex1bKey.F4,
            ConsoleKey.F5 => Hex1bKey.F5,
            ConsoleKey.F6 => Hex1bKey.F6,
            ConsoleKey.F7 => Hex1bKey.F7,
            ConsoleKey.F8 => Hex1bKey.F8,
            ConsoleKey.F9 => Hex1bKey.F9,
            ConsoleKey.F10 => Hex1bKey.F10,
            ConsoleKey.F11 => Hex1bKey.F11,
            ConsoleKey.F12 => Hex1bKey.F12,

            // Navigation
            ConsoleKey.UpArrow => Hex1bKey.UpArrow,
            ConsoleKey.DownArrow => Hex1bKey.DownArrow,
            ConsoleKey.LeftArrow => Hex1bKey.LeftArrow,
            ConsoleKey.RightArrow => Hex1bKey.RightArrow,
            ConsoleKey.Home => Hex1bKey.Home,
            ConsoleKey.End => Hex1bKey.End,
            ConsoleKey.PageUp => Hex1bKey.PageUp,
            ConsoleKey.PageDown => Hex1bKey.PageDown,

            // Editing
            ConsoleKey.Backspace => Hex1bKey.Backspace,
            ConsoleKey.Delete => Hex1bKey.Delete,
            ConsoleKey.Insert => Hex1bKey.Insert,

            // Whitespace
            ConsoleKey.Tab => Hex1bKey.Tab,
            ConsoleKey.Enter => Hex1bKey.Enter,
            ConsoleKey.Spacebar => Hex1bKey.Spacebar,

            // Escape
            ConsoleKey.Escape => Hex1bKey.Escape,

            // Punctuation
            ConsoleKey.OemComma => Hex1bKey.OemComma,
            ConsoleKey.OemPeriod => Hex1bKey.OemPeriod,
            ConsoleKey.OemMinus => Hex1bKey.OemMinus,
            ConsoleKey.OemPlus => Hex1bKey.OemPlus,
            ConsoleKey.Oem1 => Hex1bKey.Oem1,
            ConsoleKey.Oem4 => Hex1bKey.Oem4,
            ConsoleKey.Oem5 => Hex1bKey.Oem5,
            ConsoleKey.Oem6 => Hex1bKey.Oem6,
            ConsoleKey.Oem7 => Hex1bKey.Oem7,

            // Numpad
            ConsoleKey.NumPad0 => Hex1bKey.NumPad0,
            ConsoleKey.NumPad1 => Hex1bKey.NumPad1,
            ConsoleKey.NumPad2 => Hex1bKey.NumPad2,
            ConsoleKey.NumPad3 => Hex1bKey.NumPad3,
            ConsoleKey.NumPad4 => Hex1bKey.NumPad4,
            ConsoleKey.NumPad5 => Hex1bKey.NumPad5,
            ConsoleKey.NumPad6 => Hex1bKey.NumPad6,
            ConsoleKey.NumPad7 => Hex1bKey.NumPad7,
            ConsoleKey.NumPad8 => Hex1bKey.NumPad8,
            ConsoleKey.NumPad9 => Hex1bKey.NumPad9,
            ConsoleKey.Multiply => Hex1bKey.Multiply,
            ConsoleKey.Add => Hex1bKey.Add,
            ConsoleKey.Subtract => Hex1bKey.Subtract,
            ConsoleKey.Decimal => Hex1bKey.Decimal,
            ConsoleKey.Divide => Hex1bKey.Divide,

            _ => Hex1bKey.None,
        };
    }

    /// <summary>
    /// Converts ConsoleModifiers to Hex1bModifiers.
    /// </summary>
    public static Hex1bModifiers ToHex1bModifiers(bool shift, bool alt, bool control)
    {
        var modifiers = Hex1bModifiers.None;
        if (shift) modifiers |= Hex1bModifiers.Shift;
        if (alt) modifiers |= Hex1bModifiers.Alt;
        if (control) modifiers |= Hex1bModifiers.Control;
        return modifiers;
    }

    /// <summary>
    /// Creates a Hex1bKeyEvent from console key info.
    /// </summary>
    public static Hex1bKeyEvent ToHex1bKeyEvent(ConsoleKey key, char keyChar, bool shift, bool alt, bool control)
    {
        return new Hex1bKeyEvent(
            ToHex1bKey(key),
            keyChar,
            ToHex1bModifiers(shift, alt, control)
        );
    }
}
