namespace Hex1b.Tokens;

/// <summary>
/// Represents a function key or special key sequence in the format ESC [ n ~.
/// </summary>
/// <remarks>
/// Common sequences:
/// <list type="bullet">
///   <item>ESC [ 1 ~ = Home (vt)</item>
///   <item>ESC [ 2 ~ = Insert</item>
///   <item>ESC [ 3 ~ = Delete</item>
///   <item>ESC [ 4 ~ = End (vt)</item>
///   <item>ESC [ 5 ~ = Page Up</item>
///   <item>ESC [ 6 ~ = Page Down</item>
///   <item>ESC [ 7 ~ = Home (rxvt)</item>
///   <item>ESC [ 8 ~ = End (rxvt)</item>
///   <item>ESC [ 11 ~ = F1 (vt)</item>
///   <item>ESC [ 12 ~ = F2 (vt)</item>
///   <item>ESC [ 13 ~ = F3 (vt)</item>
///   <item>ESC [ 14 ~ = F4 (vt)</item>
///   <item>ESC [ 15 ~ = F5</item>
///   <item>ESC [ 17 ~ = F6</item>
///   <item>ESC [ 18 ~ = F7</item>
///   <item>ESC [ 19 ~ = F8</item>
///   <item>ESC [ 20 ~ = F9</item>
///   <item>ESC [ 21 ~ = F10</item>
///   <item>ESC [ 23 ~ = F11</item>
///   <item>ESC [ 24 ~ = F12</item>
/// </list>
/// With modifiers, the format is ESC [ n ; m ~ where m is the modifier code.
/// </remarks>
/// <param name="KeyCode">The key code number before the tilde.</param>
/// <param name="Modifiers">The modifier code (1=none, 2=Shift, 3=Alt, 5=Ctrl, etc.).</param>
public sealed record SpecialKeyToken(int KeyCode, int Modifiers = 1) : AnsiToken;
