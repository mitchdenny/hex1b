namespace Hex1b.Widgets;

/// <summary>
/// Identifies which subset of the rendered content a
/// <see cref="SelectionPanelWidget"/> snapshot should return. Until the
/// interactive copy mode is built out, the panel hard-codes a fixed
/// selection geometry per mode (the middle ~50% of the panel's bounds)
/// so that each mode produces a visibly distinct slice of the content
/// for proof-of-concept wiring.
/// </summary>
public enum SelectionPanelSnapshotMode
{
    /// <summary>
    /// The entire rendered content of the wrapped subtree, row-by-row,
    /// with trailing whitespace trimmed per line. This is the same
    /// behaviour as the parameterless <c>SnapshotText()</c> overload.
    /// </summary>
    Full,

    /// <summary>
    /// A character-stream selection — the same shape that a terminal
    /// emulator's mouse drag produces. Cells are taken as if the user had
    /// dragged from a start position to an end position: the start row
    /// emits cells from the start column to the end of the row, every
    /// fully-spanned row emits all its cells, and the end row emits cells
    /// from column zero up to the end column.
    /// </summary>
    Cells,

    /// <summary>
    /// A rectangular block selection — only cells whose row falls within
    /// the row range and whose column falls within the column range are
    /// emitted. Rows are emitted in order; each row contributes the same
    /// fixed-width slice.
    /// </summary>
    Block,

    /// <summary>
    /// A whole-line selection — every cell in every selected row is
    /// emitted regardless of column, producing complete lines of the
    /// rendered output (including any borders that lie within the row
    /// range).
    /// </summary>
    Lines,
}
