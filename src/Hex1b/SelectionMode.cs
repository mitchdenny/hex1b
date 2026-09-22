namespace Hex1b;

/// <summary>
/// Specifies how text is selected within a terminal buffer.
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// Character-level selection: selects contiguous characters from anchor to cursor,
    /// wrapping across rows.
    /// </summary>
    Character,

    /// <summary>
    /// Line-level selection: selects entire rows from the anchor row to the cursor row.
    /// </summary>
    Line,

    /// <summary>
    /// Block/rectangular selection: selects a rectangle defined by the anchor and cursor
    /// columns across all rows between them.
    /// </summary>
    Block
}
