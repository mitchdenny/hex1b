namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI standard (non-private) mode set/reset command: ESC [ mode h/l
/// </summary>
/// <param name="Mode">The standard mode number.</param>
/// <param name="Enable">True for 'h' (set/enable), false for 'l' (reset/disable).</param>
/// <remarks>
/// <para>
/// Standard modes defined in ECMA-48:
/// <list type="bullet">
///   <item>4 = IRM (Insert/Replace Mode) — insert mode when set, replace when reset</item>
///   <item>20 = LNM (Automatic Newline) — LF implies CR when set</item>
/// </list>
/// </para>
/// <para>
/// These differ from private modes (CSI ? n h/l) which use the '?' prefix.
/// </para>
/// </remarks>
public sealed record StandardModeToken(int Mode, bool Enable) : AnsiToken;
