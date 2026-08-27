using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct KgpScrollRectangle(
    int Top,
    int Bottom,
    int Left,
    int Right);

internal sealed class KgpTerminalGraphicsState
{
    internal readonly record struct HistoryPlacement(
        KgpPlacement Placement,
        uint FirstRow,
        uint RetainedRows);

    internal readonly record struct ReflowPlacement(
        int Id,
        KgpPlacement Placement,
        HistoryPlacement? History);

    internal sealed class KgpReflowPlan
    {
        internal readonly IReadOnlyList<ReflowPlacement> Placements;

        internal KgpReflowPlan(
            IReadOnlyList<TerminalReflowAnchor> anchors,
            IReadOnlyList<ReflowPlacement> placements)
        {
            Anchors = anchors;
            Placements = placements;
        }

        internal IReadOnlyList<TerminalReflowAnchor> Anchors { get; }
    }

    private sealed class ScreenState
    {
        internal KgpImageStore ImageStore { get; } = new();
        internal List<KgpPlacement> Placements { get; } = [];
        internal Dictionary<long, List<HistoryPlacement>> HistoryPlacements { get; } = [];
        internal Dictionary<uint, int> HistoryReferences { get; } = [];

        internal void Clear()
        {
            Placements.Clear();
            HistoryPlacements.Clear();
            HistoryReferences.Clear();
            ImageStore.Clear();
        }
    }

    private readonly ScreenState _main = new();
    private ScreenState? _alternate;
    private bool _alternateActive;

    private ScreenState Active
        => _alternateActive
            ? _alternate ?? throw new InvalidOperationException("Alternate KGP state is not initialized.")
            : _main;

    internal KgpImageStore ActiveImageStore => Active.ImageStore;

    internal List<KgpPlacement> ActivePlacements => Active.Placements;

    internal void ReconcileActiveImageReferences()
        => ReconcileImageReferences(Active);

    internal void EnterAlternateScreen()
    {
        if (_alternateActive)
        {
            _alternate!.Clear();
            _alternate = new ScreenState();
            return;
        }

        _alternate = new ScreenState();
        _alternateActive = true;
    }

    internal void ExitAlternateScreen()
    {
        if (!_alternateActive)
            return;

        _alternate!.Clear();
        _alternate = null;
        _alternateActive = false;
    }

    internal void Reset()
    {
        _main.Clear();
        _alternate?.Clear();
        _alternate = null;
        _alternateActive = false;
    }

    internal void ClearActiveScreen(bool clearHistory)
    {
        var active = Active;
        active.Placements.Clear();
        if (clearHistory)
        {
            active.HistoryPlacements.Clear();
            active.HistoryReferences.Clear();
        }

        active.ImageStore.RemoveUnreferencedImages(active.HistoryReferences.Keys);
    }

    internal void RetainActiveHistoryImage(uint imageId)
    {
        if (imageId == 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));
        if (Active.ImageStore.GetImageById(imageId) is null)
            throw new InvalidOperationException($"Cannot retain missing KGP image {imageId}.");

        Active.HistoryReferences.TryGetValue(imageId, out var count);
        Active.HistoryReferences[imageId] = checked(count + 1);
    }

    internal void ReleaseActiveHistoryImage(uint imageId)
    {
        if (!Active.HistoryReferences.TryGetValue(imageId, out var count))
            throw new InvalidOperationException($"KGP image {imageId} has no history reference.");

        if (count == 1)
        {
            Active.HistoryReferences.Remove(imageId);
            if (!Active.Placements.Any(placement => placement.ImageId == imageId))
                Active.ImageStore.RemoveImage(imageId);
        }
        else
        {
            Active.HistoryReferences[imageId] = count - 1;
        }
    }

    internal void RemoveActiveImageReferences(uint imageId)
    {
        var active = Active;
        active.Placements.RemoveAll(placement => placement.ImageId == imageId);
        foreach (var rowId in active.HistoryPlacements.Keys.ToArray())
        {
            var placements = active.HistoryPlacements[rowId];
            placements.RemoveAll(placement => placement.Placement.ImageId == imageId);
            if (placements.Count == 0)
                active.HistoryPlacements.Remove(rowId);
        }

        active.HistoryReferences.Remove(imageId);
    }

    internal void ReplaceActivePlacement(
        uint imageId,
        uint placementId,
        KgpPlacement? replacement)
    {
        var active = Active;
        if (placementId == 0)
        {
            if (replacement is not null)
                active.Placements.Add(replacement);
            return;
        }

        active.Placements.RemoveAll(
            placement => placement.ImageId == imageId &&
                placement.PlacementId == placementId);

        // Add first so releasing the last history owner cannot remove image
        // data needed by the replacement placement.
        if (replacement is not null)
            active.Placements.Add(replacement);

        foreach (var rowId in active.HistoryPlacements.Keys.ToArray())
        {
            var placements = active.HistoryPlacements[rowId];
            for (var i = placements.Count - 1; i >= 0; i--)
            {
                var placement = placements[i].Placement;
                if (placement.ImageId != imageId ||
                    placement.PlacementId != placementId)
                {
                    continue;
                }

                placements.RemoveAt(i);
                ReleaseHistoryImage(active, imageId);
            }

            if (placements.Count == 0)
                active.HistoryPlacements.Remove(rowId);
        }
    }

    internal void RelocateActiveImage(KgpImageStore.ImageRelocation relocation)
    {
        var placements = Active.Placements;
        for (var i = 0; i < placements.Count; i++)
        {
            if (placements[i].ImageId == relocation.PreviousId)
                placements[i] = placements[i].WithImageId(relocation.CurrentId);
        }

        foreach (var historyPlacements in Active.HistoryPlacements.Values)
        {
            for (var i = 0; i < historyPlacements.Count; i++)
            {
                var historyPlacement = historyPlacements[i];
                if (historyPlacement.Placement.ImageId == relocation.PreviousId)
                {
                    historyPlacements[i] = historyPlacement with
                    {
                        Placement = historyPlacement.Placement.WithImageId(relocation.CurrentId)
                    };
                }
            }
        }

        if (Active.HistoryReferences.Remove(relocation.PreviousId, out var count))
        {
            Active.HistoryReferences.TryGetValue(relocation.CurrentId, out var existing);
            Active.HistoryReferences[relocation.CurrentId] = checked(existing + count);
        }
    }

    internal void MoveMainPlacementsIntoHistory(long rowId)
    {
        if (_alternateActive)
            throw new InvalidOperationException("Alternate-screen placements cannot enter main-screen history.");

        ReconcileImageReferences(_main);
        for (var i = _main.Placements.Count - 1; i >= 0; i--)
        {
            var placement = _main.Placements[i];
            if (placement.Row == 0)
            {
                _main.Placements.RemoveAt(i);
                AddHistoryPlacement(
                    _main,
                    rowId,
                    new HistoryPlacement(
                        placement.WithPosition(0, placement.Column),
                        FirstRow: 0,
                        placement.DisplayRows),
                    retainImage: true);
            }
            else
            {
                _main.Placements[i] = placement.WithPosition(
                    checked(placement.Row - 1),
                    placement.Column);
            }
        }
    }

    internal void PruneMainHistoryRow(
        ScrollbackPrunedRow pruned,
        int cellPixelHeight)
    {
        if (pruned.Reason == ScrollbackPruneReason.Capacity)
            ReconcileImageReferences(_main);
        if (!_main.HistoryPlacements.Remove(pruned.RowId, out var placements))
            return;

        foreach (var historyPlacement in placements)
        {
            if (pruned.Reason == ScrollbackPruneReason.Capacity &&
                pruned.SuccessorRowId is { } successorRowId &&
                historyPlacement.RetainedRows > 1)
            {
                // The anchor row is only the first retained destination row.
                // Transfer ownership to its successor and crop from the original
                // geometry so repeated capacity eviction cannot accumulate error.
                var placement = historyPlacement.Placement;
                var image = _main.ImageStore.GetImageById(placement.ImageId)
                    ?? throw new InvalidOperationException(
                        $"History placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
                var transferred = historyPlacement with
                {
                    FirstRow = checked(historyPlacement.FirstRow + 1),
                    RetainedRows = historyPlacement.RetainedRows - 1,
                };
                var clipped = transferred.Placement.ClipRows(
                    image,
                    transferred.FirstRow,
                    transferred.RetainedRows,
                    resultRow: 0,
                    cellPixelHeight);
                if (clipped is not null)
                {
                    AddHistoryPlacement(
                        _main,
                        successorRowId,
                        transferred,
                        retainImage: false);
                    continue;
                }
            }

            ReleaseHistoryImage(_main, historyPlacement.Placement.ImageId);
        }
    }

    internal void AdjustActivePlacementsForScroll(
        int rowDelta,
        KgpScrollRectangle rectangle,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            if (!IsWhollyContained(placement, rectangle))
                continue;

            var image = active.ImageStore.GetImageById(placement.ImageId)
                ?? throw new InvalidOperationException(
                    $"Active placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
            var moved = placement.WithPosition(
                checked(placement.Row + rowDelta),
                placement.Column);
            var clipped = moved.ClipToCellRectangle(
                image,
                rectangle.Top,
                checked(rectangle.Bottom + 1),
                rectangle.Left,
                checked(rectangle.Right + 1),
                cellPixelWidth,
                cellPixelHeight);
            if (clipped is null)
                active.Placements.RemoveAt(i);
            else
                active.Placements[i] = clipped;
        }
    }

    internal void ClipActivePlacementsToViewport(
        int width,
        int height,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            var image = active.ImageStore.GetImageById(placement.ImageId)
                ?? throw new InvalidOperationException(
                    $"Active placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
            var clipped = placement.ClipToCellRectangle(
                image,
                0,
                height,
                0,
                width,
                cellPixelWidth,
                cellPixelHeight);
            if (clipped is null)
                active.Placements.RemoveAt(i);
            else
                active.Placements[i] = clipped;
        }
    }

    internal void ClearActiveViewport(
        IReadOnlyList<ScrollbackEntry> historyRows,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        active.Placements.Clear();
        if (!_alternateActive)
            ClipMainHistoryToRetainedRows(
                historyRows,
                additionalRows: 0,
                cellPixelHeight);
        active.ImageStore.RemoveUnreferencedImages(active.HistoryReferences.Keys);
    }

    internal void ClipActiveScreenToViewport(
        IReadOnlyList<ScrollbackEntry> historyRows,
        int width,
        int height,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        // This first step reconciles both active and history references before
        // the history geometry below reads image data.
        ClipActivePlacementsToViewport(
            width,
            height,
            cellPixelWidth,
            cellPixelHeight);
        if (!_alternateActive)
        {
            ClipMainHistoryToRetainedRows(
                historyRows,
                height,
                cellPixelHeight);
        }
    }

    internal KgpReflowPlan PrepareActiveReflow(
        IReadOnlyList<ScrollbackEntry> historyRows)
    {
        var active = Active;
        ReconcileImageReferences(active);
        var historyCount = _alternateActive ? 0 : historyRows.Count;
        var anchors = new List<TerminalReflowAnchor>(
            active.Placements.Count + active.HistoryPlacements.Count);
        var placements = new List<ReflowPlacement>(anchors.Capacity);
        var nextId = 1;

        foreach (var placement in active.Placements)
        {
            var id = nextId++;
            anchors.Add(new TerminalReflowAnchor(
                id,
                checked(historyCount + placement.Row),
                placement.Column));
            placements.Add(new ReflowPlacement(id, placement, History: null));
        }

        if (!_alternateActive && _main.HistoryPlacements.Count > 0)
        {
            var rowIndices = new Dictionary<long, int>(historyRows.Count);
            for (var i = 0; i < historyRows.Count; i++)
                rowIndices.Add(historyRows[i].RowId, i);

            foreach (var (rowId, rowPlacements) in _main.HistoryPlacements)
            {
                if (!rowIndices.TryGetValue(rowId, out var rowIndex))
                {
                    throw new InvalidOperationException(
                        $"KGP history placement anchor {rowId} is not present in scrollback.");
                }

                foreach (var historyPlacement in rowPlacements)
                {
                    var id = nextId++;
                    anchors.Add(new TerminalReflowAnchor(
                        id,
                        rowIndex,
                        historyPlacement.Placement.Column));
                    placements.Add(new ReflowPlacement(
                        id,
                        historyPlacement.Placement,
                        historyPlacement));
                }
            }
        }

        return new KgpReflowPlan(anchors, placements);
    }

    internal void ApplyActiveReflow(
        KgpReflowPlan plan,
        IReadOnlyList<TerminalReflowAnchor> mappedAnchors,
        int resultHistoryCount,
        ScrollbackReplacementResult replacement,
        int width,
        int height,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        var mappedById = mappedAnchors.ToDictionary(anchor => anchor.Id);
        active.Placements.Clear();
        active.HistoryPlacements.Clear();

        foreach (var tracked in plan.Placements)
        {
            var wasHistory = tracked.History.HasValue;
            var placement = tracked.Placement;
            if (tracked.History is { } history)
            {
                var image = active.ImageStore.GetImageById(placement.ImageId)
                    ?? throw new InvalidOperationException(
                        $"History placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
                var clippedHistoryPlacement = placement.ClipRows(
                    image,
                    history.FirstRow,
                    history.RetainedRows,
                    resultRow: 0,
                    cellPixelHeight);
                if (clippedHistoryPlacement is null)
                {
                    ReleaseHistoryImage(active, tracked.Placement.ImageId);
                    continue;
                }

                placement = clippedHistoryPlacement;
            }

            if (!mappedById.TryGetValue(tracked.Id, out var mapped))
            {
                if (wasHistory)
                    ReleaseHistoryImage(active, placement.ImageId);
                continue;
            }

            if (mapped.Row < replacement.DiscardedRowCount)
            {
                var discardedRows = replacement.DiscardedRowCount - mapped.Row;
                if ((uint)discardedRows >= placement.DisplayRows)
                {
                    if (wasHistory)
                        ReleaseHistoryImage(active, placement.ImageId);
                    continue;
                }

                var image = active.ImageStore.GetImageById(placement.ImageId)
                    ?? throw new InvalidOperationException(
                        $"Placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
                var clipped = placement.ClipRows(
                    image,
                    checked((uint)discardedRows),
                    placement.DisplayRows - checked((uint)discardedRows),
                    resultRow: 0,
                    cellPixelHeight);
                if (clipped is null)
                {
                    if (wasHistory)
                        ReleaseHistoryImage(active, placement.ImageId);
                    continue;
                }

                placement = clipped;
                mapped = mapped with { Row = replacement.DiscardedRowCount };
            }

            if (mapped.Row < resultHistoryCount)
            {
                var retainedIndex = mapped.Row - replacement.DiscardedRowCount;
                if (retainedIndex < 0 || retainedIndex >= replacement.Entries.Length)
                {
                    if (wasHistory)
                        ReleaseHistoryImage(active, placement.ImageId);
                    continue;
                }

                var historyPlacement = new HistoryPlacement(
                    placement.WithPosition(0, mapped.Column),
                    FirstRow: 0,
                    placement.DisplayRows);
                AddHistoryPlacement(
                    active,
                    replacement.Entries[retainedIndex].RowId,
                    historyPlacement,
                    retainImage: !wasHistory);
                continue;
            }

            var screenRow = mapped.Row - resultHistoryCount;
            var imageData = active.ImageStore.GetImageById(placement.ImageId)
                ?? throw new InvalidOperationException(
                    $"Placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
            var activePlacement = placement
                .WithPosition(screenRow, mapped.Column)
                .ClipToCellRectangle(
                    imageData,
                    0,
                    height,
                    0,
                    width,
                    cellPixelWidth,
                    cellPixelHeight);
            if (activePlacement is not null)
                active.Placements.Add(activePlacement);
            if (wasHistory)
                ReleaseHistoryImage(active, placement.ImageId);
        }
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureActiveSnapshot()
    {
        var active = Active;
        ReconcileImageReferences(active);
        return active.ImageStore.CaptureSnapshot(active.Placements);
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureActiveSnapshot(
            IReadOnlyList<ScrollbackEntry> historyRows,
            int selectedHistoryCount,
            int width,
            int height,
            int cellPixelWidth,
            int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        var historyCount = _alternateActive ? 0 : historyRows.Count;
        var selectedCount = Math.Clamp(selectedHistoryCount, 0, historyCount);
        var snapshotStart = historyCount - selectedCount;
        var snapshotEnd = checked(historyCount + height);
        var materialized = new List<KgpPlacement>(
            active.Placements.Count + active.HistoryPlacements.Count);

        foreach (var placement in active.Placements)
        {
            AddMaterializedPlacement(
                active,
                materialized,
                placement.WithPosition(
                    checked(historyCount + placement.Row),
                    placement.Column),
                snapshotStart,
                snapshotEnd,
                width,
                cellPixelWidth,
                cellPixelHeight);
        }

        if (!_alternateActive && _main.HistoryPlacements.Count > 0)
        {
            var rowIndices = new Dictionary<long, int>(historyRows.Count);
            for (var i = 0; i < historyRows.Count; i++)
                rowIndices.Add(historyRows[i].RowId, i);

            foreach (var (rowId, placements) in _main.HistoryPlacements)
            {
                if (!rowIndices.TryGetValue(rowId, out var rowIndex))
                {
                    throw new InvalidOperationException(
                        $"KGP history placement anchor {rowId} is not present in scrollback.");
                }

                foreach (var placement in placements)
                {
                    AddMaterializedHistoryPlacement(
                        _main,
                        materialized,
                        placement,
                        rowIndex,
                        snapshotStart,
                        snapshotEnd,
                        width,
                        cellPixelWidth,
                        cellPixelHeight);
                }
            }
        }

        return active.ImageStore.CaptureSnapshot(materialized);
    }

    internal int ActiveHistoryReferenceCount(uint imageId)
        => Active.HistoryReferences.TryGetValue(imageId, out var count) ? count : 0;

    internal int MainHistoryPlacementCount
        => _main.HistoryPlacements.Values.Sum(placements => placements.Count);

    private static bool IsWhollyContained(
        KgpPlacement placement,
        KgpScrollRectangle rectangle)
    {
        var bottom = (long)placement.Row + placement.DisplayRows - 1;
        var right = (long)placement.Column + placement.DisplayColumns - 1;
        return placement.Row >= rectangle.Top &&
            bottom <= rectangle.Bottom &&
            placement.Column >= rectangle.Left &&
            right <= rectangle.Right;
    }

    private static void ReconcileImageReferences(ScreenState screen)
    {
        // Deletion and quota policy live in the image store. If either removes
        // data, discard only the now-invalid placement ownership here.
        screen.Placements.RemoveAll(
            placement => screen.ImageStore.GetImageById(placement.ImageId) is null);

        var historyReferences = new Dictionary<uint, int>();
        foreach (var rowId in screen.HistoryPlacements.Keys.ToArray())
        {
            var placements = screen.HistoryPlacements[rowId];
            placements.RemoveAll(
                placement => screen.ImageStore.GetImageById(
                    placement.Placement.ImageId) is null);
            foreach (var placement in placements)
            {
                var imageId = placement.Placement.ImageId;
                historyReferences.TryGetValue(imageId, out var count);
                historyReferences[imageId] = checked(count + 1);
            }

            if (placements.Count == 0)
                screen.HistoryPlacements.Remove(rowId);
        }

        screen.HistoryReferences.Clear();
        foreach (var (imageId, count) in historyReferences)
            screen.HistoryReferences.Add(imageId, count);
    }

    private static void AddHistoryPlacement(
        ScreenState screen,
        long rowId,
        HistoryPlacement placement,
        bool retainImage)
    {
        if (!screen.HistoryPlacements.TryGetValue(rowId, out var placements))
        {
            placements = [];
            screen.HistoryPlacements.Add(rowId, placements);
        }

        placements.Add(placement);
        if (retainImage)
            RetainHistoryImage(screen, placement.Placement.ImageId);
    }

    private static void RetainHistoryImage(ScreenState screen, uint imageId)
    {
        if (imageId == 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));
        if (screen.ImageStore.GetImageById(imageId) is null)
            throw new InvalidOperationException($"Cannot retain missing KGP image {imageId}.");

        screen.HistoryReferences.TryGetValue(imageId, out var count);
        screen.HistoryReferences[imageId] = checked(count + 1);
    }

    private static void ReleaseHistoryImage(ScreenState screen, uint imageId)
    {
        if (!screen.HistoryReferences.TryGetValue(imageId, out var count))
            throw new InvalidOperationException($"KGP image {imageId} has no history reference.");

        if (count == 1)
        {
            screen.HistoryReferences.Remove(imageId);
            if (!screen.Placements.Any(placement => placement.ImageId == imageId))
                screen.ImageStore.RemoveImage(imageId);
        }
        else
        {
            screen.HistoryReferences[imageId] = count - 1;
        }
    }

    private void ClipMainHistoryToRetainedRows(
        IReadOnlyList<ScrollbackEntry> historyRows,
        int additionalRows,
        int cellPixelHeight)
    {
        if (_main.HistoryPlacements.Count == 0)
            return;

        var rowIndices = new Dictionary<long, int>(historyRows.Count);
        for (var i = 0; i < historyRows.Count; i++)
            rowIndices.Add(historyRows[i].RowId, i);

        foreach (var rowId in _main.HistoryPlacements.Keys.ToArray())
        {
            if (!rowIndices.TryGetValue(rowId, out var rowIndex))
            {
                var orphaned = _main.HistoryPlacements[rowId];
                _main.HistoryPlacements.Remove(rowId);
                foreach (var historyPlacement in orphaned)
                    ReleaseHistoryImage(_main, historyPlacement.Placement.ImageId);
                continue;
            }

            var retainedRowCount = checked(
                (uint)(historyRows.Count - rowIndex + additionalRows));
            var placements = _main.HistoryPlacements[rowId];
            for (var i = placements.Count - 1; i >= 0; i--)
            {
                var historyPlacement = placements[i];
                if (historyPlacement.RetainedRows <= retainedRowCount)
                    continue;

                var placement = historyPlacement.Placement;
                var retained = historyPlacement with { RetainedRows = retainedRowCount };
                var image = _main.ImageStore.GetImageById(placement.ImageId)
                    ?? throw new InvalidOperationException(
                        $"History placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
                var clipped = placement.ClipRows(
                    image,
                    retained.FirstRow,
                    retained.RetainedRows,
                    resultRow: 0,
                    cellPixelHeight);
                if (clipped is null)
                {
                    placements.RemoveAt(i);
                    ReleaseHistoryImage(_main, placement.ImageId);
                }
                else
                {
                    placements[i] = retained;
                }
            }

            if (placements.Count == 0)
                _main.HistoryPlacements.Remove(rowId);
        }
    }

    private static void AddMaterializedHistoryPlacement(
        ScreenState screen,
        List<KgpPlacement> destination,
        HistoryPlacement historyPlacement,
        int anchorRow,
        int snapshotStart,
        int snapshotEnd,
        int width,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var placement = historyPlacement.Placement;
        var placementEnd = checked(anchorRow + (int)historyPlacement.RetainedRows);
        var retainedStart = Math.Max(anchorRow, snapshotStart);
        var retainedEnd = Math.Min(placementEnd, snapshotEnd);
        if (retainedStart >= retainedEnd)
            return;

        var image = screen.ImageStore.GetImageById(placement.ImageId)
            ?? throw new InvalidOperationException(
                $"History placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
        var firstRow = checked(
            historyPlacement.FirstRow + (uint)(retainedStart - anchorRow));
        var retainedRows = checked((uint)(retainedEnd - retainedStart));
        var sliced = placement.ClipRows(
            image,
            firstRow,
            retainedRows,
            retainedStart,
            cellPixelHeight);
        if (sliced is null)
            return;

        AddMaterializedPlacement(
            screen,
            destination,
            sliced,
            snapshotStart,
            snapshotEnd,
            width,
            cellPixelWidth,
            cellPixelHeight);
    }

    private static void AddMaterializedPlacement(
        ScreenState screen,
        List<KgpPlacement> destination,
        KgpPlacement placement,
        int snapshotStart,
        int snapshotEnd,
        int width,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var image = screen.ImageStore.GetImageById(placement.ImageId)
            ?? throw new InvalidOperationException(
                $"Placement {placement.PlacementId} references missing KGP image {placement.ImageId}.");
        var clipped = placement.ClipToCellRectangle(
            image,
            snapshotStart,
            snapshotEnd,
            0,
            width,
            cellPixelWidth,
            cellPixelHeight);
        if (clipped is not null)
        {
            destination.Add(clipped.WithPosition(
                checked(clipped.Row - snapshotStart),
                clipped.Column));
        }
    }
}
