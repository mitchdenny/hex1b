using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// A single tracked placement participating in a Sixel reflow pass: its
/// reflow anchor id, the placement geometry to re-derive from, and (when it
/// originated from history) the retained-window descriptor to slice before
/// re-deriving. Mirrors <c>KgpTerminalGraphicsState.ReflowPlacement</c>.
/// </summary>
internal readonly record struct SixelReflowPlacement(
    int Id,
    SixelPlacement Placement,
    SixelHistoryPlacement? History);

/// <summary>
/// The anchors and tracked placements built by <see cref="SixelGraphicsState.PrepareActiveReflow"/>,
/// ready to be merged with another subsystem's anchors for a single combined
/// <c>ReflowHelper.PerformReflowWithAnchors</c> call. Mirrors
/// <c>KgpTerminalGraphicsState.KgpReflowPlan</c>.
/// </summary>
internal sealed class SixelReflowPlan
{
    internal SixelReflowPlan(
        IReadOnlyList<TerminalReflowAnchor> anchors,
        IReadOnlyList<SixelReflowPlacement> placements)
    {
        Anchors = anchors;
        Placements = placements;
    }

    internal IReadOnlyList<TerminalReflowAnchor> Anchors { get; }

    internal IReadOnlyList<SixelReflowPlacement> Placements { get; }
}

/// <summary>
/// Independent terminal-graphics storage and placement state for Sixel,
/// completely decoupled from <see cref="TerminalCell"/>. Stage #451 replaces
/// the previous per-cell ownership model (<c>CellAttributes.Sixel</c> plus
/// origin/continuation cell tracking) with this reachability-based model,
/// modeled after the mature <see cref="KgpTerminalGraphicsState"/> but
/// intentionally much simpler: no public image/placement IDs, no image-number
/// addressing, no explicit delete selectors, no relative-placement graph, no
/// Unicode placeholders, no z-index, and no chunked uploads. Those concepts
/// are genuinely KGP-specific and are deliberately kept out of this shared
/// model. Stage #452 layers scrolling, main-screen scrollback history,
/// viewport clipping/pruning, resize, and anchor-based reflow onto this
/// model, mirroring KGP's scrolling/history/reflow fidelity with the same
/// deliberate simplifications.
/// </summary>
/// <remarks>
/// Main and alternate screen state are fully independent
/// <see cref="SixelScreenGraphicsState"/> instances. Re-entering the
/// alternate screen (for example a program that issues <c>\x1b[?1049h</c>
/// twice without an intervening exit) resets only the alternate state; the
/// main screen's placements, history, and images are untouched. A full
/// terminal reset (RIS) is the only operation that clears both.
/// </remarks>
internal sealed class SixelGraphicsState
{
    private readonly SixelScreenGraphicsState _main = new();
    private SixelScreenGraphicsState? _alternate;
    private bool _alternateActive;

    private SixelScreenGraphicsState Active => _alternateActive ? _alternate! : _main;

    internal bool InAlternateScreen => _alternateActive;

    /// <summary>The active screen's image store (for testing/inspection).</summary>
    internal SixelImageStore ActiveImages => Active.Images;

    /// <summary>The active screen's live placements (for testing/inspection).</summary>
    internal IReadOnlyList<SixelPlacement> ActivePlacements => Active.Placements;

    /// <summary>Total number of placements the main screen has moved into history.</summary>
    internal int MainHistoryPlacementCount
    {
        get
        {
            var count = 0;
            foreach (var list in _main.HistoryPlacements.Values)
                count += list.Count;
            return count;
        }
    }

    /// <summary>
    /// Creates an anonymous raster resource (or reuses a content-identical
    /// existing one) in the active screen's image store, and adds a placement
    /// anchored at the given position that references it.
    /// </summary>
    internal SixelPlacement CreatePlacement(
        string payload,
        SixelParseResult parseResult,
        SixelRasterPreparation rasterPreparation,
        SixelCellMetrics cellMetrics,
        int row,
        int column,
        int widthInCells,
        int heightInCells,
        int paintedRowOffset,
        int paintedRowCount,
        int paintedColumnOffset,
        int paintedColumnCount,
        long sequence,
        DateTimeOffset createdAt)
    {
        var active = Active;
        var image = active.Images.GetOrCreate(
            payload, widthInCells, heightInCells, parseResult, rasterPreparation, cellMetrics);
        var placement = new SixelPlacement(
            image,
            row,
            column,
            widthInCells,
            heightInCells,
            paintedRowOffset,
            paintedRowCount,
            paintedColumnOffset,
            paintedColumnCount,
            sequence,
            createdAt);
        active.Placements.Add(placement);
        return placement;
    }

    /// <summary>
    /// Enters the alternate screen. Repeated entry (already active) resets
    /// only the alternate state; the main screen is never touched here.
    /// </summary>
    internal void EnterAlternateScreen()
    {
        _alternate?.Clear();
        _alternate = new SixelScreenGraphicsState();
        _alternateActive = true;
    }

    /// <summary>Exits the alternate screen, discarding its graphics state entirely.</summary>
    internal void ExitAlternateScreen()
    {
        if (!_alternateActive)
            return;

        _alternate?.Clear();
        _alternate = null;
        _alternateActive = false;
    }

    /// <summary>RIS (full terminal reset): clears both the main and alternate graphics state.</summary>
    internal void Reset()
    {
        _main.Clear();
        _alternate?.Clear();
        _alternate = null;
        _alternateActive = false;
    }

    /// <summary>
    /// Clears the active screen's live placements (ED/DECSED-style clear), and
    /// optionally its history partition too (used when scrollback itself is
    /// also being cleared, or when clearing the alternate screen which has no
    /// separate history concept).
    /// </summary>
    internal void ClearActiveScreen(bool clearHistory)
    {
        var active = Active;
        active.Placements.Clear();
        if (clearHistory)
            active.HistoryPlacements.Clear();
        active.ReconcileImages();
    }

    /// <summary>
    /// Destructively damages Sixel pixels projected into one active-screen cell.
    /// </summary>
    /// <returns><see langword="true"/> when at least one visible placement was damaged.</returns>
    internal bool DamageActiveCell(int row, int column)
    {
        var active = Active;
        var changed = false;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            if (!placement.DamageCell(row, column))
                continue;

            changed = true;
            if (!placement.HasVisiblePaintedCells)
                active.Placements.RemoveAt(i);
        }

        if (changed)
            active.ReconcileImages();
        return changed;
    }

    /// <summary>
    /// Shifts every main-screen placement's anchor up by one row (the row
    /// that just scrolled off the top of the screen and was captured into
    /// scrollback under <paramref name="rowId"/>), and, for any placement
    /// whose painted window still touches that departing row afterwards,
    /// splits it: exactly the departing painted row moves into history, while
    /// any painted rows beneath it stay active, re-anchored so the new
    /// topmost row lands at absolute row 0.
    /// </summary>
    /// <remarks>
    /// Unlike a single-row placement (the common case, where the whole thing
    /// simply transfers), a placement whose declared footprint spans more
    /// than one row can have only its first painted row depart while the
    /// rest remains visible on the newly-shifted screen — this is the
    /// "history/viewport split" the issue requires (a graphic straddling the
    /// scrollback boundary keeps a live, independently-clippable copy on each
    /// side). Both the departing slice and the remaining slice are cut from
    /// the placement's *current* painted window via
    /// <see cref="SixelPlacement.SliceHistoryRows"/>, never from its original
    /// declared geometry, preserving the "no resurrection" invariant across
    /// repeated scrolling. Geometry-only placements (nothing ever painted)
    /// have no window to split, so the entire placement transfers as soon as
    /// its anchor departs, purely to preserve reachability — mirroring
    /// <c>KgpTerminalGraphicsState.MoveMainPlacementsIntoHistory</c> for that
    /// case.
    /// <para>
    /// Only placements wholly contained in <paramref name="region"/> (the
    /// same gate <see cref="AdjustActivePlacementsForScroll"/> uses) shift at
    /// all: a full-height scroll region contains everything on the main
    /// screen, but under a partial vertical margin (DECSTBM's bottom smaller
    /// than the physical screen height) only rows inside the margin actually
    /// move — a placement anchored below the margin, or straddling its
    /// bottom edge, is untouched by this scroll and must not be shifted or
    /// have any of its rows transferred, exactly like the ordinary
    /// non-history scroll path.
    /// </para>
    /// </remarks>
    internal void MoveMainPlacementsIntoHistory(long rowId, SixelScrollRegion region)
    {
        for (var i = _main.Placements.Count - 1; i >= 0; i--)
        {
            var placement = _main.Placements[i];
            if (!IsWhollyContained(placement, region))
                continue;

            placement.Row -= 1;

            if (!placement.HasPaintedExtent)
            {
                if (placement.PaintedTop > -1)
                    continue;

                _main.Placements.RemoveAt(i);
                AddHistoryPlacement(
                    _main,
                    rowId,
                    new SixelHistoryPlacement(placement, FirstRow: 0, RetainedRows: 0));
                continue;
            }

            if (placement.PaintedTop > -1)
                continue;

            var departing = placement.SliceHistoryRows(firstRow: 0, retainedRows: 1, resultRow: 0);
            var remainingRows = placement.PaintedRowCount - 1;
            var remainder = remainingRows > 0
                ? placement.SliceHistoryRows(firstRow: 1, retainedRows: remainingRows, resultRow: 0)
                : null;

            _main.Placements.RemoveAt(i);
            if (remainder is not null)
                _main.Placements.Insert(i, remainder);

            if (departing is not null)
            {
                AddHistoryPlacement(
                    _main,
                    rowId,
                    new SixelHistoryPlacement(departing, FirstRow: 0, RetainedRows: departing.PaintedRowCount));
            }
        }
    }

    /// <summary>
    /// Releases or transfers the history placements anchored to a scrollback
    /// row that has just been evicted (capacity eviction or an explicit
    /// scrollback clear).
    /// </summary>
    /// <remarks>
    /// On capacity eviction with a successor row, a placement whose retained
    /// window still spans more than one row transfers its remaining window to
    /// the successor — cropped from the same original geometry every time
    /// (never from an already-cropped copy), so repeated capacity eviction
    /// cannot accumulate error. Mirrors
    /// <c>KgpTerminalGraphicsState.PruneMainHistoryRow</c>'s transfer-to-successor
    /// behavior, minus KGP's pixel-precise <c>cellPixelHeight</c> rounding
    /// (Sixel's window arithmetic is exact cell-integer math, so the
    /// transferred window is always representable when the source invariant
    /// holds).
    /// </remarks>
    internal void PruneMainHistoryRow(ScrollbackPrunedRow pruned)
    {
        if (!_main.HistoryPlacements.TryGetValue(pruned.RowId, out var placements))
            return;

        var transfers = new List<(long RowId, SixelHistoryPlacement Placement)>();
        var anyDropped = false;

        foreach (var historyPlacement in placements)
        {
            if (pruned.Reason == ScrollbackPruneReason.Capacity &&
                pruned.SuccessorRowId is { } successorRowId &&
                historyPlacement.RetainedRows > 1)
            {
                var transferred = historyPlacement with
                {
                    FirstRow = historyPlacement.FirstRow + 1,
                    RetainedRows = historyPlacement.RetainedRows - 1,
                };
                if (transferred.Placement.SliceHistoryRows(
                        transferred.FirstRow, transferred.RetainedRows, resultRow: 0) is not null)
                {
                    transfers.Add((successorRowId, transferred));
                    continue;
                }
            }

            anyDropped = true;
        }

        _main.HistoryPlacements.Remove(pruned.RowId);
        foreach (var (rowId, historyPlacement) in transfers)
            AddHistoryPlacement(_main, rowId, historyPlacement);

        if (anyDropped || transfers.Count > 0)
            _main.ReconcileImages();
    }

    /// <summary>
    /// Shifts or crops active placements for a scroll of <paramref name="rowDelta"/>
    /// rows within <paramref name="region"/>. A placement whose painted
    /// rectangle is wholly contained in the region shifts with it and is
    /// re-cropped to the region's bounds; one that would land completely
    /// outside is no longer reachable and is removed. Placements not wholly
    /// contained in the region (partially overlapping it) are left untouched.
    /// Mirrors <c>KgpTerminalGraphicsState.AdjustActivePlacementsForScroll</c>.
    /// </summary>
    internal void AdjustActivePlacementsForScroll(int rowDelta, SixelScrollRegion region)
    {
        var active = Active;
        var changed = false;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            if (!IsWhollyContained(placement, region))
                continue;

            placement.Row += rowDelta;
            var clipped = placement.ClipToCellRectangle(
                region.Top, region.Bottom + 1, region.Left, region.Right + 1);
            if (clipped is null)
            {
                active.Placements.RemoveAt(i);
                changed = true;
            }
            else if (!ReferenceEquals(clipped, placement))
            {
                active.Placements[i] = clipped;
                changed = true;
            }
        }

        if (changed)
            active.ReconcileImages();
    }

    /// <summary>
    /// Releases any active placement anchored exactly at <paramref name="row"/>.
    /// Used by IL/DL (insert/delete lines), which — unlike ordinary scrolling —
    /// discard a single row's content outright. KGP intentionally leaves its
    /// ordinary placements fixed for IL/DL (matching Kitty/Ghostty); the prior
    /// per-cell Sixel model released the origin cell's tracked reference in
    /// this situation, so this preserves that same observable behavior under
    /// the new placement-based model.
    /// </summary>
    internal void ReleasePlacementsAnchoredAtRow(int row)
    {
        var active = Active;
        if (active.Placements.RemoveAll(p => p.Row == row) > 0)
            active.ReconcileImages();
    }

    /// <summary>
    /// Removes active placements that fall completely outside a new viewport
    /// size (used on resize without reflow). Unlike scroll-margin cropping or
    /// history/snapshot slicing, a viewport-only resize never permanently
    /// narrows a placement's painted window: on a real terminal, columns or
    /// rows that are merely off-screen because the current window is smaller
    /// than the placement's true extent are not "destroyed" content, and
    /// widening the viewport back out must be able to reveal them again. Only
    /// placements whose bounding box no longer intersects the new viewport at
    /// all are dropped (their content genuinely can no longer be observed by
    /// any resize back to a larger size, since their row/column anchor itself
    /// is untouched by a plain crop resize).
    /// </summary>
    internal void ClipActivePlacementsToViewport(int width, int height)
    {
        var active = Active;
        var changed = false;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            var bottom = placement.Row + placement.HeightInCells - 1;
            var right = placement.Column + placement.WidthInCells - 1;
            if (bottom < 0 || placement.Row >= height || right < 0 || placement.Column >= width)
            {
                active.Placements.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            active.ReconcileImages();
    }

    /// <summary>
    /// Clips the active screen's live placements to a new viewport size and,
    /// for the main screen, also drops any history placement whose scrollback
    /// row is no longer retained.
    /// </summary>
    /// <remarks>
    /// Unlike KGP, no eager per-row retained-window shrink is needed here:
    /// Sixel's history slicing is always non-destructive (the stored
    /// <see cref="SixelHistoryPlacement.Placement"/> is never mutated), so
    /// <see cref="CaptureActiveSnapshot"/>'s own spillover intersection
    /// against the current selected scrollback window is sufficient to keep
    /// materialized geometry correct without any eager re-bound here.
    /// </remarks>
    internal void ClipActiveScreenToViewport(
        IReadOnlyList<ScrollbackEntry> historyRows,
        int width,
        int height)
    {
        ClipActivePlacementsToViewport(width, height);

        if (Active != _main)
            return;

        if (_main.HistoryPlacements.Count == 0)
            return;

        var retainedRowIds = new HashSet<long>(historyRows.Count);
        foreach (var entry in historyRows)
            retainedRowIds.Add(entry.RowId);

        List<long>? toRemove = null;
        foreach (var rowId in _main.HistoryPlacements.Keys)
        {
            if (!retainedRowIds.Contains(rowId))
                (toRemove ??= []).Add(rowId);
        }

        if (toRemove is null)
            return;

        foreach (var rowId in toRemove)
            _main.HistoryPlacements.Remove(rowId);

        _main.ReconcileImages();
    }

    /// <summary>
    /// Builds the reflow anchors and tracked placements for the active
    /// screen, for merging into a single combined
    /// <c>ReflowHelper.PerformReflowWithAnchors</c> call alongside KGP's own
    /// anchors. Mirrors <c>KgpTerminalGraphicsState.PrepareActiveReflow</c>.
    /// </summary>
    /// <remarks>
    /// Anchor ids are negative (starting at -1, decrementing), guaranteeing no
    /// collision with KGP's positive ids (which start at 1) when both
    /// subsystems' anchors are merged into one list — required because the
    /// reflow strategy needs a single, global view of every anchor to
    /// redistribute logical lines consistently.
    /// </remarks>
    internal SixelReflowPlan PrepareActiveReflow(IReadOnlyList<ScrollbackEntry> historyRows)
    {
        var active = Active;
        var isMain = active == _main;
        var historyCount = isMain ? historyRows.Count : 0;
        var anchors = new List<TerminalReflowAnchor>(
            active.Placements.Count + active.HistoryPlacements.Count);
        var placements = new List<SixelReflowPlacement>(anchors.Capacity);
        var nextId = -1;

        foreach (var placement in active.Placements)
        {
            var id = nextId--;
            anchors.Add(new TerminalReflowAnchor(id, historyCount + placement.Row, placement.Column));
            placements.Add(new SixelReflowPlacement(id, placement, History: null));
        }

        if (isMain && active.HistoryPlacements.Count > 0)
        {
            var rowIndices = new Dictionary<long, int>(historyRows.Count);
            for (var i = 0; i < historyRows.Count; i++)
                rowIndices[historyRows[i].RowId] = i;

            foreach (var (rowId, rowPlacements) in active.HistoryPlacements)
            {
                if (!rowIndices.TryGetValue(rowId, out var rowIndex))
                    continue; // Evicted or outside the selected scrollback window.

                foreach (var historyPlacement in rowPlacements)
                {
                    var id = nextId--;
                    anchors.Add(new TerminalReflowAnchor(id, rowIndex, historyPlacement.Placement.Column));
                    placements.Add(new SixelReflowPlacement(id, historyPlacement.Placement, historyPlacement));
                }
            }
        }

        return new SixelReflowPlan(anchors, placements);
    }

    /// <summary>
    /// Applies a completed reflow: each tracked placement moves atomically to
    /// wherever its single anchor point was mapped (no per-row splitting
    /// during reflow itself), then is re-partitioned into history vs. live
    /// viewport based on the mapped row. A placement whose anchor could not
    /// be represented in the reflowed layout (row consumed elsewhere, or a
    /// history window that no longer fits) is dropped — the explicit safe
    /// behavior the issue requires for genuinely unrepresentable placements.
    /// Mirrors <c>KgpTerminalGraphicsState.ApplyActiveReflow</c>.
    /// </summary>
    internal void ApplyActiveReflow(
        SixelReflowPlan plan,
        IReadOnlyList<TerminalReflowAnchor> mappedAnchors,
        int resultHistoryCount,
        ScrollbackReplacementResult replacement,
        int width,
        int height)
    {
        var active = Active;
        var mappedById = new Dictionary<int, TerminalReflowAnchor>(mappedAnchors.Count);
        foreach (var anchor in mappedAnchors)
            mappedById[anchor.Id] = anchor;

        active.Placements.Clear();
        active.HistoryPlacements.Clear();

        foreach (var tracked in plan.Placements)
        {
            var placement = tracked.Placement;
            if (tracked.History is { } history)
            {
                var sliced = placement.SliceHistoryRows(history.FirstRow, history.RetainedRows, resultRow: 0);
                if (sliced is null)
                    continue;

                placement = sliced;
            }

            if (!mappedById.TryGetValue(tracked.Id, out var mapped))
                continue;

            if (mapped.Row < replacement.DiscardedRowCount)
            {
                var discardedRows = replacement.DiscardedRowCount - mapped.Row;
                if (discardedRows >= placement.PaintedRowCount)
                    continue;

                var clipped = placement.SliceHistoryRows(
                    discardedRows, placement.PaintedRowCount - discardedRows, resultRow: 0);
                if (clipped is null)
                    continue;

                placement = clipped;
                mapped = mapped with { Row = replacement.DiscardedRowCount };
            }

            if (mapped.Row < resultHistoryCount)
            {
                var retainedIndex = mapped.Row - replacement.DiscardedRowCount;
                if (retainedIndex < 0 || retainedIndex >= replacement.Entries.Length)
                    continue;

                var repositioned = placement.WithPosition(0, mapped.Column);
                AddHistoryPlacement(
                    active,
                    replacement.Entries[retainedIndex].RowId,
                    new SixelHistoryPlacement(repositioned, FirstRow: 0, repositioned.PaintedRowCount));
                continue;
            }

            var screenRow = mapped.Row - resultHistoryCount;
            var activePlacement = placement
                .WithPosition(screenRow, mapped.Column)
                .ClipToCellRectangle(0, height, 0, width);
            if (activePlacement is not null)
                active.Placements.Add(activePlacement);
        }

        active.ReconcileImages();
    }

    /// <summary>
    /// Projects the active screen's placements into a unified coordinate space
    /// (history rows first, then the live viewport) for inclusion in a
    /// terminal snapshot, exactly like
    /// <c>KgpTerminalGraphicsState.CaptureActiveSnapshot</c>. A placement that
    /// spans the scrollback/viewport boundary is split into independent
    /// projections on each side (never duplicated whole).
    /// </summary>
    /// <remarks>
    /// Returned placements are independent copies (see
    /// <see cref="SixelPlacement.WithRow"/>): they, and the
    /// <see cref="SixelData"/> images dictionary returned alongside them,
    /// remain valid and reachable for as long as the snapshot itself is kept
    /// alive, even after the live graphics state that produced them mutates
    /// or sweeps its own store. <paramref name="width"/> bounds both the live
    /// viewport and history rows uniformly, matching the snapshot's single
    /// unified width (which may exceed the terminal's current width when
    /// projecting scrollback under <c>ScrollbackWidth.Original</c>).
    /// </remarks>
    internal (IReadOnlyList<SixelPlacement> Placements, IReadOnlyDictionary<byte[], SixelData> Images) CaptureActiveSnapshot(
        IReadOnlyList<ScrollbackEntry> allHistoryRows,
        int selectedHistoryCount,
        int viewportHeight,
        int width)
    {
        var active = Active;
        var isMain = active == _main;
        var historyCount = isMain ? allHistoryRows.Count : 0;
        var selectedCount = Math.Clamp(selectedHistoryCount, 0, historyCount);
        var snapshotStart = historyCount - selectedCount;
        var snapshotEnd = historyCount + viewportHeight;

        Dictionary<long, int>? rowIndexByRowId = null;
        if (isMain && active.HistoryPlacements.Count > 0)
        {
            rowIndexByRowId = new Dictionary<long, int>(allHistoryRows.Count);
            for (var i = 0; i < allHistoryRows.Count; i++)
                rowIndexByRowId[allHistoryRows[i].RowId] = i;
        }

        var placements = new List<SixelPlacement>();
        var images = new Dictionary<byte[], SixelData>(SixelContentHashComparer.Instance);

        foreach (var placement in active.Placements)
        {
            var unifiedRow = historyCount + placement.Row;
            AddMaterializedPlacement(
                placement.WithRow(unifiedRow), snapshotStart, snapshotEnd, width, placements, images);
        }

        if (isMain && rowIndexByRowId is not null)
        {
            foreach (var (rowId, list) in active.HistoryPlacements)
            {
                if (!rowIndexByRowId.TryGetValue(rowId, out var anchorRow))
                    continue; // Evicted or outside the selected scrollback window.

                foreach (var historyPlacement in list)
                {
                    AddMaterializedHistoryPlacement(
                        historyPlacement, anchorRow, snapshotStart, snapshotEnd, width, placements, images);
                }
            }
        }

        return (placements, images);
    }

    private static void AddHistoryPlacement(
        SixelScreenGraphicsState screen,
        long rowId,
        SixelHistoryPlacement historyPlacement)
    {
        if (!screen.HistoryPlacements.TryGetValue(rowId, out var list))
        {
            list = [];
            screen.HistoryPlacements[rowId] = list;
        }

        list.Add(historyPlacement);
    }

    /// <summary>
    /// A placement's painted rectangle (or, for a geometry-only placement, its
    /// declared footprint) is wholly contained in <paramref name="region"/>.
    /// Gating on the painted rectangle — not the raw declared footprint —
    /// means an oversized/cross-margin placement that was already
    /// creation-time-clipped into the scroll region (see
    /// <c>Hex1bTerminal.ResolveSixelPlacement</c>) is correctly eligible to
    /// keep scrolling/cropping with the region, rather than being permanently
    /// excluded because its full declared footprint extends outside it.
    /// </summary>
    private static bool IsWhollyContained(SixelPlacement placement, SixelScrollRegion region)
    {
        if (placement.HasPaintedExtent)
        {
            return placement.PaintedTop >= region.Top && placement.PaintedBottom <= region.Bottom &&
                placement.PaintedLeft >= region.Left && placement.PaintedRight <= region.Right;
        }

        var bottom = placement.Row + placement.HeightInCells - 1;
        var right = placement.Column + placement.WidthInCells - 1;
        return placement.Row >= region.Top && bottom <= region.Bottom &&
            placement.Column >= region.Left && right <= region.Right;
    }

    private static void AddMaterializedHistoryPlacement(
        SixelHistoryPlacement historyPlacement,
        int anchorRow,
        int snapshotStart,
        int snapshotEnd,
        int width,
        List<SixelPlacement> destination,
        Dictionary<byte[], SixelData> images)
    {
        var placementEnd = anchorRow + historyPlacement.RetainedRows;
        var retainedStart = Math.Max(anchorRow, snapshotStart);
        var retainedEnd = Math.Min(placementEnd, snapshotEnd);
        if (retainedStart >= retainedEnd)
            return;

        var firstRow = historyPlacement.FirstRow + (retainedStart - anchorRow);
        var retainedRows = retainedEnd - retainedStart;
        var sliced = historyPlacement.Placement.SliceHistoryRows(firstRow, retainedRows, resultRow: retainedStart);
        if (sliced is null)
            return;

        AddMaterializedPlacement(sliced, snapshotStart, snapshotEnd, width, destination, images);
    }

    private static void AddMaterializedPlacement(
        SixelPlacement placement,
        int snapshotStart,
        int snapshotEnd,
        int width,
        List<SixelPlacement> destination,
        Dictionary<byte[], SixelData> images)
    {
        var clipped = placement.ClipToCellRectangle(snapshotStart, snapshotEnd, 0, width);
        if (clipped is null)
            return;

        destination.Add(clipped.WithRow(clipped.Row - snapshotStart));
        images[clipped.Image.ContentHash] = clipped.Image;
    }
}
