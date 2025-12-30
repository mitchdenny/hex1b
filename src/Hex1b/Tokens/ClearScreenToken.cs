namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Erase in Display command (ED): ESC [ mode J
/// </summary>
/// <param name="Mode">What portion of the screen to clear.</param>
public sealed record ClearScreenToken(ClearMode Mode = ClearMode.ToEnd) : AnsiToken;
