namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Erase in Display command (ED): ESC [ mode J
/// or Selective Erase in Display (DECSED): ESC [ ? mode J
/// </summary>
/// <param name="Mode">What portion of the screen to clear.</param>
/// <param name="Selective">When true, this is a DECSED (selective erase) that only erases unprotected cells.</param>
public sealed record ClearScreenToken(ClearMode Mode = ClearMode.ToEnd, bool Selective = false) : AnsiToken;
