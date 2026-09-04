namespace Hex1b.Sixel;

/// <summary>
/// A Sixel placement that has scrolled into main-screen scrollback history,
/// tracking how much of its painted window is still retained by the
/// scrollback row identity it is currently anchored to.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>KgpTerminalGraphicsState.HistoryPlacement</c>: <see cref="Placement"/>
/// keeps describing the geometry it had at the moment it (or its predecessor
/// transfer) last entered history, and never has its own painted window
/// mutated in place. <see cref="FirstRow"/>/<see cref="RetainedRows"/> instead
/// describe, non-destructively, which sub-window of
/// <see cref="Placement"/>'s own <c>PaintedRowOffset</c>/<c>PaintedRowCount</c>
/// range is still owned by this row identity — both are 0-based offsets
/// within that painted window, not within the placement's declared footprint
/// and not absolute terminal rows.
/// </para>
/// <para>
/// This indirection is what lets a multi-row placement "spill over" from
/// history back into the still-visible viewport (its <see cref="RetainedRows"/>
/// can extend past the true scrollback row count into unified snapshot space)
/// and lets capacity eviction transfer a shrinking remainder to the successor
/// row (<see cref="FirstRow"/> creeps forward, <see cref="RetainedRows"/>
/// shrinks) without ever re-deriving cropped geometry from the full,
/// original footprint — the same "never resurrect a previously discarded
/// row" invariant <see cref="SixelPlacement.ClipToCellRectangle"/> relies on.
/// </para>
/// </remarks>
/// <param name="Placement">
/// The placement as it was recorded when it (or its predecessor transfer)
/// entered history. Never mutated in place; slicing always happens on demand
/// via <see cref="SixelPlacement.SliceHistoryRows"/>.
/// </param>
/// <param name="FirstRow">
/// Offset, relative to <see cref="Placement"/>'s own painted-row window, of
/// the first still-retained painted row.
/// </param>
/// <param name="RetainedRows">
/// How many painted rows starting at <paramref name="FirstRow"/> are still
/// retained by this row identity's chain.
/// </param>
internal readonly record struct SixelHistoryPlacement(
    SixelPlacement Placement,
    int FirstRow,
    int RetainedRows);
