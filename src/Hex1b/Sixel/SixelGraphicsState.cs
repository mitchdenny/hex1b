using Hex1b.Sixel;

namespace Hex1b;

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
/// model.
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
    /// Moves every main-screen placement anchored at row 0 into history under
    /// <paramref name="rowId"/> (the scrollback row identity that row was just
    /// captured under), and shifts every other placement's anchor up by one
    /// row. Mirrors <c>KgpTerminalGraphicsState.MoveMainPlacementsIntoHistory</c>.
    /// </summary>
    internal void MoveMainPlacementsIntoHistory(long rowId)
    {
        for (var i = _main.Placements.Count - 1; i >= 0; i--)
        {
            var placement = _main.Placements[i];
            if (placement.Row == 0)
            {
                _main.Placements.RemoveAt(i);
                if (!_main.HistoryPlacements.TryGetValue(rowId, out var list))
                {
                    list = [];
                    _main.HistoryPlacements[rowId] = list;
                }

                list.Add(placement);
            }
            else
            {
                placement.Row -= 1;
            }
        }
    }

    /// <summary>
    /// Releases the history placements anchored to a scrollback row that has
    /// just been evicted (capacity eviction or an explicit scrollback clear).
    /// </summary>
    /// <remarks>
    /// Stage #451 intentionally keeps history eviction simple: the whole
    /// placement is released along with its row. KGP's partial cropping and
    /// transfer-to-successor-row fidelity on capacity eviction
    /// (<c>ClipRows</c>/<c>RetainedRows</c>) is not replicated here; that level
    /// of scrolling/reflow fidelity is deferred to #452.
    /// </remarks>
    internal void PruneMainHistoryRow(ScrollbackPrunedRow pruned)
    {
        if (_main.HistoryPlacements.Remove(pruned.RowId, out var removed) && removed.Count > 0)
        {
            _main.ReconcileImages();
        }
    }

    /// <summary>
    /// Shifts or drops active placements for a scroll of <paramref name="rowDelta"/>
    /// rows within <paramref name="region"/>. A placement wholly contained in
    /// the region shifts with it; one that would land completely outside the
    /// region is no longer reachable and is removed. Placements that are not
    /// wholly contained in the region (partially overlapping it) are left
    /// untouched, mirroring <c>KgpTerminalGraphicsState.AdjustActivePlacementsForScroll</c>.
    /// </summary>
    internal void AdjustActivePlacementsForScroll(int rowDelta, SixelScrollRegion region)
    {
        var active = Active;
        var changed = false;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            var bottom = placement.Row + placement.HeightInCells - 1;
            var right = placement.Column + placement.WidthInCells - 1;
            var whollyContained =
                placement.Row >= region.Top && bottom <= region.Bottom &&
                placement.Column >= region.Left && right <= region.Right;
            if (!whollyContained)
                continue;

            var movedRow = placement.Row + rowDelta;
            var movedBottom = movedRow + placement.HeightInCells - 1;
            if (movedBottom < region.Top || movedRow > region.Bottom)
            {
                active.Placements.RemoveAt(i);
                changed = true;
                continue;
            }

            placement.Row = movedRow;
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
    /// size (used on resize). Placement positions are not reflowed/translated;
    /// full reflow-aware repositioning is deferred to #452, matching the
    /// existing fallback KGP itself uses for reflow providers without row
    /// lineage.
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
    /// Projects the active screen's placements into a unified coordinate space
    /// (history rows first, then the live viewport) for inclusion in a
    /// terminal snapshot, exactly like
    /// <c>KgpTerminalGraphicsState.CaptureActiveSnapshot</c>.
    /// </summary>
    /// <remarks>
    /// Returned placements are independent copies (see
    /// <see cref="SixelPlacement.WithRow"/>): they, and the
    /// <see cref="SixelData"/> images dictionary returned alongside them,
    /// remain valid and reachable for as long as the snapshot itself is kept
    /// alive, even after the live graphics state that produced them mutates
    /// or sweeps its own store.
    /// </remarks>
    internal (IReadOnlyList<SixelPlacement> Placements, IReadOnlyDictionary<byte[], SixelData> Images) CaptureActiveSnapshot(
        IReadOnlyList<ScrollbackEntry> allHistoryRows,
        int selectedHistoryCount,
        int viewportHeight)
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
            AddIfVisible(placement, unifiedRow, snapshotStart, snapshotEnd, placements, images);
        }

        if (isMain && rowIndexByRowId is not null)
        {
            foreach (var (rowId, list) in active.HistoryPlacements)
            {
                if (!rowIndexByRowId.TryGetValue(rowId, out var unifiedRow))
                    continue; // Evicted or outside the selected scrollback window.

                foreach (var placement in list)
                    AddIfVisible(placement, unifiedRow, snapshotStart, snapshotEnd, placements, images);
            }
        }

        return (placements, images);
    }

    private static void AddIfVisible(
        SixelPlacement placement,
        int unifiedRow,
        int snapshotStart,
        int snapshotEnd,
        List<SixelPlacement> placements,
        Dictionary<byte[], SixelData> images)
    {
        if (unifiedRow < snapshotStart || unifiedRow >= snapshotEnd)
            return;

        placements.Add(placement.WithRow(unifiedRow - snapshotStart));
        images[placement.Image.ContentHash] = placement.Image;
    }
}
