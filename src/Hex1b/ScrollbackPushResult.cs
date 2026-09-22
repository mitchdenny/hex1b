using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct ScrollbackPushResult(
    long RowId,
    ScrollbackRow? EvictedRow,
    long? EvictedRowId,
    long? SuccessorRowId);
