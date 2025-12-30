namespace Hex1b.Tokens;

/// <summary>
/// Represents a cursor restore command: ESC 8 (DECRC) or ESC [ u (ANSI.SYS)
/// </summary>
/// <param name="UseDec">
/// If true, use DEC format (ESC 8) which also restores character attributes.
/// If false, use ANSI.SYS format (ESC [ u) which only restores cursor position.
/// </param>
/// <remarks>
/// <para>
/// Restores the cursor position (and optionally attributes) previously saved
/// with <see cref="SaveCursorToken"/>.
/// </para>
/// <para>
/// Should be paired with the matching save format - DEC save with DEC restore,
/// ANSI.SYS save with ANSI.SYS restore.
/// </para>
/// </remarks>
public sealed record RestoreCursorToken(bool UseDec = true) : AnsiToken
{
    /// <summary>Restore cursor using DEC format (ESC 8) - restores position and attributes.</summary>
    public static readonly RestoreCursorToken Dec = new(true);
    
    /// <summary>Restore cursor using ANSI.SYS format (ESC [ u) - restores position only.</summary>
    public static readonly RestoreCursorToken Ansi = new(false);
}
