namespace Hex1b.Tokens;

/// <summary>
/// Direction for relative cursor movement.
/// </summary>
public enum CursorMoveDirection
{
    /// <summary>Cursor Up (CUU) - ESC [ n A</summary>
    Up,
    /// <summary>Cursor Down (CUD) - ESC [ n B</summary>
    Down,
    /// <summary>Cursor Forward/Right (CUF) - ESC [ n C</summary>
    Forward,
    /// <summary>Cursor Back/Left (CUB) - ESC [ n D</summary>
    Back,
    /// <summary>Cursor Next Line (CNL) - ESC [ n E - move to beginning of line n lines down</summary>
    NextLine,
    /// <summary>Cursor Previous Line (CPL) - ESC [ n F - move to beginning of line n lines up</summary>
    PreviousLine
}
