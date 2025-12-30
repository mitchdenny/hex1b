namespace Hex1b.Tokens;

/// <summary>
/// Represents a cursor save command: ESC 7 (DECSC) or ESC [ s (ANSI.SYS)
/// </summary>
/// <param name="UseDec">
/// If true, use DEC format (ESC 7) which also saves character attributes.
/// If false, use ANSI.SYS format (ESC [ s) which only saves cursor position.
/// </param>
/// <remarks>
/// <para>
/// Saves the current cursor position (and optionally attributes) for later restoration
/// with <see cref="RestoreCursorToken"/>.
/// </para>
/// <para>
/// The DEC format (ESC 7) is more widely supported and also saves:
/// <list type="bullet">
///   <item>Cursor position</item>
///   <item>Character attributes (SGR)</item>
///   <item>Character set</item>
///   <item>Origin mode state</item>
/// </list>
/// </para>
/// </remarks>
public sealed record SaveCursorToken(bool UseDec = true) : AnsiToken
{
    /// <summary>Save cursor using DEC format (ESC 7) - saves position and attributes.</summary>
    public static readonly SaveCursorToken Dec = new(true);
    
    /// <summary>Save cursor using ANSI.SYS format (ESC [ s) - saves position only.</summary>
    public static readonly SaveCursorToken Ansi = new(false);
}
