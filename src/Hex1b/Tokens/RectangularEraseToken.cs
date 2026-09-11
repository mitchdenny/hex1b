namespace Hex1b.Tokens;

/// <summary>
/// Represents DECERA or DECSERA rectangular erase.
/// </summary>
public sealed record RectangularEraseToken(
    int Top,
    int Left,
    int Bottom,
    int Right,
    bool Selective) : AnsiToken;
