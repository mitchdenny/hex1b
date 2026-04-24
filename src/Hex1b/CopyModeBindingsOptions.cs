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

/// <summary>
/// Configuration options for standard copy mode key and mouse bindings.
/// All properties have sensible defaults (vi-style keys + mouse).
/// </summary>
public sealed class CopyModeBindingsOptions
{
    // Entry
    /// <summary>Key bindings that enter copy mode when the terminal is focused.</summary>
    public KeyBinding[] EnterKeys { get; set; } = [Hex1bKey.F6];

    // Navigation
    /// <summary>Key bindings that move the copy mode cursor up one row.</summary>
    public KeyBinding[] CursorUpKeys { get; set; } = [Hex1bKey.UpArrow, Hex1bKey.K];
    /// <summary>Key bindings that move the copy mode cursor down one row.</summary>
    public KeyBinding[] CursorDownKeys { get; set; } = [Hex1bKey.DownArrow, Hex1bKey.J];
    /// <summary>Key bindings that move the copy mode cursor left one column.</summary>
    public KeyBinding[] CursorLeftKeys { get; set; } = [Hex1bKey.LeftArrow, Hex1bKey.H];
    /// <summary>Key bindings that move the copy mode cursor right one column.</summary>
    public KeyBinding[] CursorRightKeys { get; set; } = [Hex1bKey.RightArrow, Hex1bKey.L];
    /// <summary>Key bindings that move the copy mode cursor forward one word.</summary>
    public KeyBinding[] WordForwardKeys { get; set; } = [Hex1bKey.W];
    /// <summary>Key bindings that move the copy mode cursor backward one word.</summary>
    public KeyBinding[] WordBackwardKeys { get; set; } = [Hex1bKey.B];
    /// <summary>Key bindings that move the copy mode cursor up one page.</summary>
    public KeyBinding[] PageUpKeys { get; set; } = [Hex1bKey.PageUp];
    /// <summary>Key bindings that move the copy mode cursor down one page.</summary>
    public KeyBinding[] PageDownKeys { get; set; } = [Hex1bKey.PageDown];
    /// <summary>Key bindings that move the copy mode cursor to the start of the current line.</summary>
    public KeyBinding[] LineStartKeys { get; set; } = [Hex1bKey.Home, Hex1bKey.D0];
    /// <summary>Key bindings that move the copy mode cursor to the end of the current line.</summary>
    public KeyBinding[] LineEndKeys { get; set; } = [Hex1bKey.End];
    /// <summary>Key bindings that move the cursor to the top of the buffer.</summary>
    public KeyBinding[] BufferTopKeys { get; set; } = [new(Hex1bKey.G)];
    /// <summary>Key bindings that move the cursor to the bottom of the buffer.</summary>
    public KeyBinding[] BufferBottomKeys { get; set; } = [new(Hex1bKey.G, Hex1bModifiers.Shift)];

    // Selection modes
    /// <summary>Key bindings that start or toggle character selection.</summary>
    public KeyBinding[] CharacterSelectionKeys { get; set; } = [Hex1bKey.V, Hex1bKey.Spacebar];
    /// <summary>Key bindings that start or toggle line selection.</summary>
    public KeyBinding[] LineSelectionKeys { get; set; } = [new(Hex1bKey.V, Hex1bModifiers.Shift)];
    /// <summary>Key bindings that start or toggle block/rectangular selection.</summary>
    public KeyBinding[] BlockSelectionKeys { get; set; } = [new(Hex1bKey.V, Hex1bModifiers.Alt)];

    // Actions
    /// <summary>Key bindings that copy the selection and exit copy mode.</summary>
    public KeyBinding[] CopyKeys { get; set; } = [Hex1bKey.Y, Hex1bKey.Enter];
    /// <summary>Key bindings that cancel copy mode without copying.</summary>
    public KeyBinding[] CancelKeys { get; set; } = [Hex1bKey.Escape, Hex1bKey.Q];

    // Mouse
    /// <summary>Whether mouse drag-to-select is enabled.</summary>
    public bool MouseEnabled { get; set; } = true;
    /// <summary>Mouse modifier for line selection mode during drag.</summary>
    public Hex1bModifiers MouseLineModifier { get; set; } = Hex1bModifiers.Control;
    /// <summary>Mouse modifier for block/rectangular selection mode during drag.</summary>
    public Hex1bModifiers MouseBlockModifier { get; set; } = Hex1bModifiers.Alt;
    /// <summary>Mouse button that copies the selection.</summary>
    public MouseButton MouseCopyButton { get; set; } = MouseButton.Right;

    // Clipboard
    /// <summary>
    /// Callback to copy text to the system clipboard. When null (default), the framework
    /// automatically uses the Hex1bApp's OSC 52 clipboard support.
    /// Set to a custom callback to override clipboard behavior, or set to <c>_ => { }</c> to disable.
    /// </summary>
    public Action<string>? CopyToClipboard { get; set; }
}
