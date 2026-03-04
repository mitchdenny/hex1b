namespace Hex1b;

/// <summary>
/// Tracks the character protection mode set by DECSCA (CSI Ps " q).
/// </summary>
/// <remarks>
/// Ghostty and xterm distinguish between ISO and DEC protection modes:
/// <list type="bullet">
/// <item><description><see cref="Off"/>: No protection (default). Characters are unprotected.</description></item>
/// <item><description><see cref="Iso"/>: ISO protection. Normal erase (ED/EL) respects protection;
/// selective erase (DECSED/DECSEL) also respects protection.</description></item>
/// <item><description><see cref="Dec"/>: DEC protection. Only selective erase (DECSED/DECSEL)
/// respects protection; normal erase (ED/EL) ignores it.</description></item>
/// </list>
/// </remarks>
internal enum ProtectedMode
{
    /// <summary>No protection mode has been set.</summary>
    Off,
    
    /// <summary>ISO protection mode (set via DECSCA with SCS 0 sequence).</summary>
    Iso,
    
    /// <summary>DEC protection mode (set via DECSCA).</summary>
    Dec
}
