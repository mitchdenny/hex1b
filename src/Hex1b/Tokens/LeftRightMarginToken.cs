namespace Hex1b.Tokens;

/// <summary>
/// Represents a CSI Set Left Right Margins (DECSLRM) command: ESC [ left ; right s
/// Sets the left and right margins for the scrolling region.
/// Only active when DECLRMM (mode 69) is enabled.
/// </summary>
/// <param name="Left">Left margin column (1-based). Default is 1.</param>
/// <param name="Right">Right margin column (1-based). 0 means reset to screen width.</param>
public sealed record LeftRightMarginToken(int Left = 1, int Right = 0) : AnsiToken;
