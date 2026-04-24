using Hex1b.Input;

namespace Hex1b;

/// <summary>
/// Configuration options for standard copy mode key and mouse bindings.
/// All properties have sensible defaults (vi-style keys + mouse).
/// </summary>
public sealed class CopyModeBindingsOptions
{
    // Entry
    /// <summary>Keys that enter copy mode when the terminal is focused.</summary>
    public Hex1bKey[] EnterKeys { get; set; } = [Hex1bKey.F6];

    // Navigation
    /// <summary>Keys that move the copy mode cursor up one row.</summary>
    public Hex1bKey[] CursorUpKeys { get; set; } = [Hex1bKey.UpArrow, Hex1bKey.K];
    /// <summary>Keys that move the copy mode cursor down one row.</summary>
    public Hex1bKey[] CursorDownKeys { get; set; } = [Hex1bKey.DownArrow, Hex1bKey.J];
    /// <summary>Keys that move the copy mode cursor left one column.</summary>
    public Hex1bKey[] CursorLeftKeys { get; set; } = [Hex1bKey.LeftArrow, Hex1bKey.H];
    /// <summary>Keys that move the copy mode cursor right one column.</summary>
    public Hex1bKey[] CursorRightKeys { get; set; } = [Hex1bKey.RightArrow, Hex1bKey.L];
    /// <summary>Keys that move the copy mode cursor forward one word.</summary>
    public Hex1bKey[] WordForwardKeys { get; set; } = [Hex1bKey.W];
    /// <summary>Keys that move the copy mode cursor backward one word.</summary>
    public Hex1bKey[] WordBackwardKeys { get; set; } = [Hex1bKey.B];
    /// <summary>Keys that move the copy mode cursor up one page.</summary>
    public Hex1bKey[] PageUpKeys { get; set; } = [Hex1bKey.PageUp];
    /// <summary>Keys that move the copy mode cursor down one page.</summary>
    public Hex1bKey[] PageDownKeys { get; set; } = [Hex1bKey.PageDown];
    /// <summary>Keys that move the copy mode cursor to the start of the current line.</summary>
    public Hex1bKey[] LineStartKeys { get; set; } = [Hex1bKey.Home, Hex1bKey.D0];
    /// <summary>Keys that move the copy mode cursor to the end of the current line.</summary>
    public Hex1bKey[] LineEndKeys { get; set; } = [Hex1bKey.End];
    /// <summary>Key+modifier combinations that move the cursor to the top of the buffer.</summary>
    public (Hex1bKey Key, Hex1bModifiers Modifiers)[] BufferTopKeys { get; set; } = [(Hex1bKey.G, Hex1bModifiers.None)];
    /// <summary>Key+modifier combinations that move the cursor to the bottom of the buffer.</summary>
    public (Hex1bKey Key, Hex1bModifiers Modifiers)[] BufferBottomKeys { get; set; } = [(Hex1bKey.G, Hex1bModifiers.Shift)];

    // Selection modes
    /// <summary>Keys that start or toggle character selection.</summary>
    public Hex1bKey[] CharacterSelectionKeys { get; set; } = [Hex1bKey.V, Hex1bKey.Spacebar];
    /// <summary>Key+modifier combinations that start or toggle line selection.</summary>
    public (Hex1bKey Key, Hex1bModifiers Modifiers)[] LineSelectionKeys { get; set; } = [(Hex1bKey.V, Hex1bModifiers.Shift)];
    /// <summary>Key+modifier combinations that start or toggle block/rectangular selection.</summary>
    public (Hex1bKey Key, Hex1bModifiers Modifiers)[] BlockSelectionKeys { get; set; } = [(Hex1bKey.V, Hex1bModifiers.Alt)];

    // Actions
    /// <summary>Keys that copy the selection and exit copy mode.</summary>
    public Hex1bKey[] CopyKeys { get; set; } = [Hex1bKey.Y, Hex1bKey.Enter];
    /// <summary>Keys that cancel copy mode without copying.</summary>
    public Hex1bKey[] CancelKeys { get; set; } = [Hex1bKey.Escape, Hex1bKey.Q];

    // Mouse
    /// <summary>Whether mouse drag-to-select is enabled.</summary>
    public bool MouseEnabled { get; set; } = true;
    /// <summary>Mouse modifier for line selection mode during drag.</summary>
    public Hex1bModifiers MouseLineModifier { get; set; } = Hex1bModifiers.Control;
    /// <summary>Mouse modifier for block/rectangular selection mode during drag.</summary>
    public Hex1bModifiers MouseBlockModifier { get; set; } = Hex1bModifiers.Alt;
    /// <summary>Mouse button that copies the selection.</summary>
    public MouseButton MouseCopyButton { get; set; } = MouseButton.Right;
}
