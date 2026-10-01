namespace Hex1b;

// A position with leading-cell affinity. In-row positions follow shifted cells;
// end-of-row positions remain boundaries until wrapping binds them to a glyph.
// Identity and ownership survive coordinate generation changes.
internal sealed class TerminalTextAnchor(string id, bool alternate, long rowId, int column)
{
    internal string Id { get; } = id;
    internal bool Alternate { get; } = alternate;
    internal long? RowId { get; set; } = rowId;
    internal int Column { get; set; } = column;
}
