namespace Hex1b.Tokens;

/// <summary>
/// Represents an arrow key input with optional modifiers.
/// </summary>
/// <remarks>
/// <para>
/// Arrow key sequences with modifiers use the format ESC [ 1 ; m X where:
/// <list type="bullet">
///   <item>X is A (Up), B (Down), C (Right), or D (Left)</item>
///   <item>m is the modifier code: 1=none, 2=Shift, 3=Alt, 4=Shift+Alt, 5=Ctrl, 6=Shift+Ctrl, 7=Alt+Ctrl, 8=Shift+Alt+Ctrl</item>
/// </list>
/// </para>
/// <para>
/// Plain arrow keys (no modifiers) are still represented as <see cref="CursorMoveToken"/>.
/// This token is specifically for arrow keys with modifiers, to preserve modifier information
/// that would otherwise be lost in <see cref="CursorMoveToken"/>.
/// </para>
/// </remarks>
/// <param name="Direction">The arrow key direction.</param>
/// <param name="Modifiers">The modifier code (1=none, 2=Shift, 3=Alt, 5=Ctrl, etc.).</param>
public sealed record ArrowKeyToken(CursorMoveDirection Direction, int Modifiers = 1) : AnsiToken;
