using Hex1b.Input;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// Maps between <see cref="Hex1bKeyEvent"/> and <see cref="ConsoleKeyInfo"/>
/// for the Spectre.Console and Spectre.Tui input bridges.
/// </summary>
internal static class SpectreConsoleKeyMapper
{
    /// <summary>
    /// Converts a <see cref="Hex1bKeyEvent"/> into a <see cref="ConsoleKeyInfo"/>
    /// suitable for handing to Spectre's input layer.
    /// </summary>
    /// <returns>
    /// The mapped <see cref="ConsoleKeyInfo"/>, or <c>null</c> when no useful
    /// mapping exists (for example the event carries only multi-grapheme paste
    /// text and no scalar key).
    /// </returns>
    public static ConsoleKeyInfo? ToConsoleKeyInfo(Hex1bKeyEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var consoleKey = ToConsoleKey(evt.Key);
        var keyChar = evt.Character;

        // Skip events that carry neither a recognisable key nor a printable
        // character — Spectre's reader will treat them as a real key press
        // otherwise and surface garbage to the app.
        if (consoleKey == ConsoleKey.NoName && keyChar == '\0')
        {
            return null;
        }

        return new ConsoleKeyInfo(
            keyChar,
            consoleKey,
            shift: evt.Shift,
            alt: evt.Alt,
            control: evt.Control);
    }

    /// <summary>
    /// Maps a <see cref="Hex1bKey"/> back to a <see cref="ConsoleKey"/>. Keys
    /// without a direct equivalent return <see cref="ConsoleKey.NoName"/>.
    /// </summary>
    public static ConsoleKey ToConsoleKey(Hex1bKey key)
    {
        return key switch
        {
            // Letters
            Hex1bKey.A => ConsoleKey.A,
            Hex1bKey.B => ConsoleKey.B,
            Hex1bKey.C => ConsoleKey.C,
            Hex1bKey.D => ConsoleKey.D,
            Hex1bKey.E => ConsoleKey.E,
            Hex1bKey.F => ConsoleKey.F,
            Hex1bKey.G => ConsoleKey.G,
            Hex1bKey.H => ConsoleKey.H,
            Hex1bKey.I => ConsoleKey.I,
            Hex1bKey.J => ConsoleKey.J,
            Hex1bKey.K => ConsoleKey.K,
            Hex1bKey.L => ConsoleKey.L,
            Hex1bKey.M => ConsoleKey.M,
            Hex1bKey.N => ConsoleKey.N,
            Hex1bKey.O => ConsoleKey.O,
            Hex1bKey.P => ConsoleKey.P,
            Hex1bKey.Q => ConsoleKey.Q,
            Hex1bKey.R => ConsoleKey.R,
            Hex1bKey.S => ConsoleKey.S,
            Hex1bKey.T => ConsoleKey.T,
            Hex1bKey.U => ConsoleKey.U,
            Hex1bKey.V => ConsoleKey.V,
            Hex1bKey.W => ConsoleKey.W,
            Hex1bKey.X => ConsoleKey.X,
            Hex1bKey.Y => ConsoleKey.Y,
            Hex1bKey.Z => ConsoleKey.Z,

            // Numbers
            Hex1bKey.D0 => ConsoleKey.D0,
            Hex1bKey.D1 => ConsoleKey.D1,
            Hex1bKey.D2 => ConsoleKey.D2,
            Hex1bKey.D3 => ConsoleKey.D3,
            Hex1bKey.D4 => ConsoleKey.D4,
            Hex1bKey.D5 => ConsoleKey.D5,
            Hex1bKey.D6 => ConsoleKey.D6,
            Hex1bKey.D7 => ConsoleKey.D7,
            Hex1bKey.D8 => ConsoleKey.D8,
            Hex1bKey.D9 => ConsoleKey.D9,

            // Function keys
            Hex1bKey.F1 => ConsoleKey.F1,
            Hex1bKey.F2 => ConsoleKey.F2,
            Hex1bKey.F3 => ConsoleKey.F3,
            Hex1bKey.F4 => ConsoleKey.F4,
            Hex1bKey.F5 => ConsoleKey.F5,
            Hex1bKey.F6 => ConsoleKey.F6,
            Hex1bKey.F7 => ConsoleKey.F7,
            Hex1bKey.F8 => ConsoleKey.F8,
            Hex1bKey.F9 => ConsoleKey.F9,
            Hex1bKey.F10 => ConsoleKey.F10,
            Hex1bKey.F11 => ConsoleKey.F11,
            Hex1bKey.F12 => ConsoleKey.F12,

            // Navigation
            Hex1bKey.UpArrow => ConsoleKey.UpArrow,
            Hex1bKey.DownArrow => ConsoleKey.DownArrow,
            Hex1bKey.LeftArrow => ConsoleKey.LeftArrow,
            Hex1bKey.RightArrow => ConsoleKey.RightArrow,
            Hex1bKey.Home => ConsoleKey.Home,
            Hex1bKey.End => ConsoleKey.End,
            Hex1bKey.PageUp => ConsoleKey.PageUp,
            Hex1bKey.PageDown => ConsoleKey.PageDown,

            // Editing
            Hex1bKey.Backspace => ConsoleKey.Backspace,
            Hex1bKey.Delete => ConsoleKey.Delete,
            Hex1bKey.Insert => ConsoleKey.Insert,

            // Whitespace
            Hex1bKey.Tab => ConsoleKey.Tab,
            Hex1bKey.Enter => ConsoleKey.Enter,
            Hex1bKey.Spacebar => ConsoleKey.Spacebar,

            // Escape
            Hex1bKey.Escape => ConsoleKey.Escape,

            // Punctuation
            Hex1bKey.OemComma => ConsoleKey.OemComma,
            Hex1bKey.OemPeriod => ConsoleKey.OemPeriod,
            Hex1bKey.OemMinus => ConsoleKey.OemMinus,
            Hex1bKey.OemPlus => ConsoleKey.OemPlus,
            Hex1bKey.Oem1 => ConsoleKey.Oem1,
            Hex1bKey.Oem4 => ConsoleKey.Oem4,
            Hex1bKey.Oem5 => ConsoleKey.Oem5,
            Hex1bKey.Oem6 => ConsoleKey.Oem6,
            Hex1bKey.Oem7 => ConsoleKey.Oem7,
            Hex1bKey.OemQuestion => ConsoleKey.Oem2,

            // Numpad
            Hex1bKey.NumPad0 => ConsoleKey.NumPad0,
            Hex1bKey.NumPad1 => ConsoleKey.NumPad1,
            Hex1bKey.NumPad2 => ConsoleKey.NumPad2,
            Hex1bKey.NumPad3 => ConsoleKey.NumPad3,
            Hex1bKey.NumPad4 => ConsoleKey.NumPad4,
            Hex1bKey.NumPad5 => ConsoleKey.NumPad5,
            Hex1bKey.NumPad6 => ConsoleKey.NumPad6,
            Hex1bKey.NumPad7 => ConsoleKey.NumPad7,
            Hex1bKey.NumPad8 => ConsoleKey.NumPad8,
            Hex1bKey.NumPad9 => ConsoleKey.NumPad9,
            Hex1bKey.Multiply => ConsoleKey.Multiply,
            Hex1bKey.Add => ConsoleKey.Add,
            Hex1bKey.Subtract => ConsoleKey.Subtract,
            Hex1bKey.Decimal => ConsoleKey.Decimal,
            Hex1bKey.Divide => ConsoleKey.Divide,

            _ => ConsoleKey.NoName,
        };
    }
}
