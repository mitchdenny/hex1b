namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Erase in Line command (EL): ESC [ mode K
/// </summary>
/// <param name="Mode">What portion of the line to clear.</param>
/// <remarks>
/// <para>
/// Clears part or all of the current line without affecting other lines.
/// Uses the same <see cref="ClearMode"/> enum as <see cref="ClearScreenToken"/>,
/// but <see cref="ClearMode.AllAndScrollback"/> is not applicable here.
/// </para>
/// </remarks>
public sealed record ClearLineToken(ClearMode Mode = ClearMode.ToEnd) : AnsiToken;
