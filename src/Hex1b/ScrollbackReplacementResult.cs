using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct ScrollbackReplacementResult(
    ScrollbackEntry[] Entries,
    int DiscardedRowCount);
