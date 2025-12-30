namespace Hex1b.Tokens;

/// <summary>
/// Represents a token that has been applied to the terminal buffer,
/// bundled with metadata about what changed.
/// </summary>
/// <param name="Token">The original ANSI token that was applied.</param>
/// <param name="CellImpacts">The cells that were modified by this token.</param>
/// <param name="CursorXBefore">The cursor column position before the token was applied.</param>
/// <param name="CursorYBefore">The cursor row position before the token was applied.</param>
/// <param name="CursorXAfter">The cursor column position after the token was applied.</param>
/// <param name="CursorYAfter">The cursor row position after the token was applied.</param>
public record AppliedToken(
    AnsiToken Token,
    IReadOnlyList<CellImpact> CellImpacts,
    int CursorXBefore,
    int CursorYBefore,
    int CursorXAfter,
    int CursorYAfter)
{
    /// <summary>
    /// Gets whether this token modified any cells.
    /// </summary>
    public bool HasCellImpacts => CellImpacts.Count > 0;

    /// <summary>
    /// Gets whether the cursor position changed.
    /// </summary>
    public bool CursorMoved => CursorXBefore != CursorXAfter || CursorYBefore != CursorYAfter;

    /// <summary>
    /// Creates an AppliedToken with no cell impacts (for tokens that only affect cursor or state).
    /// </summary>
    public static AppliedToken WithNoCellImpacts(
        AnsiToken token,
        int cursorXBefore, int cursorYBefore,
        int cursorXAfter, int cursorYAfter)
        => new(token, Array.Empty<CellImpact>(), cursorXBefore, cursorYBefore, cursorXAfter, cursorYAfter);
}
