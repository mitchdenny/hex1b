namespace Hex1b.Input;

/// <summary>
/// Platform-independent key identifiers for Hex1b input handling.
/// This abstraction removes the dependency on System.ConsoleKey.
/// </summary>
public enum Hex1bKey
{
    None = 0,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Numbers
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Navigation
    UpArrow,
    DownArrow,
    LeftArrow,
    RightArrow,
    Home,
    End,
    PageUp,
    PageDown,

    // Editing
    Backspace,
    Delete,
    Insert,

    // Whitespace
    Tab,
    Enter,
    Spacebar,

    // Escape
    Escape,

    // Punctuation and symbols
    OemComma,      // ,
    OemPeriod,     // .
    OemMinus,      // -
    OemPlus,       // +/=
    OemQuestion,   // /?
    Oem1,          // ;:
    Oem4,          // [{
    Oem5,          // \|
    Oem6,          // ]}
    Oem7,          // '"
    OemTilde,      // `~

    // Numpad
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Multiply,
    Add,
    Subtract,
    Decimal,
    Divide,
}
