using Hex1b.Input;

namespace Hex1b.Tokens;

/// <summary>
/// Represents an SGR (1006) mouse protocol sequence.
/// Format: ESC [ &lt; Cb ; Cx ; Cy M (press) or ESC [ &lt; Cb ; Cx ; Cy m (release)
/// </summary>
/// <remarks>
/// <para>
/// The SGR mouse protocol (mode 1006) uses the following button encoding:
/// <list type="bullet">
///   <item>0 = Left button</item>
///   <item>1 = Middle button</item>
///   <item>2 = Right button</item>
///   <item>32 = Motion with button held (add to button code)</item>
///   <item>64 = Scroll up</item>
///   <item>65 = Scroll down</item>
/// </list>
/// </para>
/// <para>
/// Modifier keys are encoded as bit flags added to the button code:
/// <list type="bullet">
///   <item>+4 = Shift</item>
///   <item>+8 = Alt/Meta</item>
///   <item>+16 = Control</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Button">The decoded mouse button.</param>
/// <param name="Action">The mouse action (press, release, move, drag).</param>
/// <param name="X">The X coordinate (0-based).</param>
/// <param name="Y">The Y coordinate (0-based).</param>
/// <param name="Modifiers">The modifier keys held during the event.</param>
/// <param name="RawButtonCode">The raw button code from the sequence for serialization.</param>
public sealed record SgrMouseToken(
    MouseButton Button,
    MouseAction Action,
    int X,
    int Y,
    Hex1bModifiers Modifiers,
    int RawButtonCode) : AnsiToken;
