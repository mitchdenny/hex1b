using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct ScrollbackPrunedRow(
    long RowId,
    long? SuccessorRowId,
    ScrollbackPruneReason Reason);
