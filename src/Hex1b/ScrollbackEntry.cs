using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct ScrollbackEntry(
    long RowId,
    ScrollbackRow Row);
