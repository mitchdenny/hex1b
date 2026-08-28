using Hex1b.Reflow;

namespace Hex1b;

internal readonly record struct KgpScrollRectangle(
    int Top,
    int Bottom,
    int Left,
    int Right);

internal sealed class KgpTerminalGraphicsState
{
    internal enum PlacementError
    {
        None,
        ParentImageNotFound,
        ParentPlacementNotFound,
        SelfParent,
        Cycle,
        TooDeep,
    }

    private const int MaximumParentDepth = 8;

    internal readonly record struct HistoryPlacement(
        KgpPlacement Placement,
        uint FirstRow,
        uint RetainedRows);

    internal readonly record struct ReflowPlacement(
        int Id,
        KgpPlacement Placement,
        HistoryPlacement? History);

    private readonly record struct EffectiveOrigin(int Row, int Column);

    private readonly record struct PlacementIdentity(
        long GraphId,
        uint ImageId,
        uint PlacementId);

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
        internal List<KgpVirtualPlacement> VirtualPlacements { get; } = [];
        internal Dictionary<long, List<HistoryPlacement>> HistoryPlacements { get; } = [];
        internal Dictionary<uint, int> HistoryReferences { get; } = [];
        internal Dictionary<uint, int> VirtualReferences { get; } = [];
        internal long NextPlacementGraphId { get; set; } = 1;

        internal void Clear()
        {
            Placements.Clear();
            VirtualPlacements.Clear();
            HistoryPlacements.Clear();
            HistoryReferences.Clear();
            VirtualReferences.Clear();
            NextPlacementGraphId = 1;
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

    internal int ActiveVirtualPlacementCount => Active.VirtualPlacements.Count;

    internal int ActiveVirtualReferenceCount(uint imageId)
        => Active.VirtualReferences.TryGetValue(imageId, out var count) ? count : 0;

    internal bool HasActiveVirtualPlacement(
        uint imageId,
        uint placementId = 0)
        => Active.VirtualPlacements.Any(
            placement => placement.ImageId == imageId &&
                (placementId == 0 || placement.PlacementId == placementId));

    internal bool HasActivePlacementOwner(uint imageId)
        => HasPlacementReference(Active, imageId);

    internal void ReconcileActiveImageReferences()
        => ReconcileImageReferences(Active);

    internal void ReplaceActiveVirtualPlacement(
        uint imageId,
        uint placementId,
        uint columns,
        uint rows)
    {
        if (imageId == 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        var active = Active;
        ReconcileImageReferences(active);
        var graphId = placementId > 0 &&
            TryFindGraphId(active, imageId, placementId, out var existingGraphId)
                ? existingGraphId
                : AllocateGraphId(active);
        DetachPlacement(active, graphId);
        active.VirtualPlacements.Add(new KgpVirtualPlacement(
            graphId,
            imageId,
            placementId,
            columns,
            rows));
        RebuildHistoryReferences(active);
        RebuildVirtualReferences(active);
    }

    internal int RemoveActiveVirtualPlacements(
        uint imageId,
        uint placementId = 0)
    {
        if (imageId == 0)
            return 0;

        var active = Active;
        var selected = active.VirtualPlacements
            .Where(placement => placement.ImageId == imageId &&
                (placementId == 0 || placement.PlacementId == placementId))
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
        return selected.Length;
    }

    internal int RemoveActiveVirtualPlacementsInRange(
        uint firstImageId,
        uint lastImageId)
    {
        if (firstImageId == 0 ||
            lastImageId == 0 ||
            firstImageId > lastImageId)
        {
            return 0;
        }

        var active = Active;
        var selected = active.VirtualPlacements
            .Where(placement => placement.ImageId >= firstImageId &&
                placement.ImageId <= lastImageId)
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
        return selected.Length;
    }

    internal void DeleteAllActiveOrdinaryPlacements(bool freeData)
    {
        var active = Active;
        var selected = active.Placements
            .Where(placement => !placement.IsRelative)
            .Select(placement => placement.GraphId)
            .ToList();
        if (freeData)
        {
            selected.AddRange(active.HistoryPlacements.Values
                .SelectMany(placements => placements)
                .Select(placement => placement.Placement.GraphId));
        }

        RemovePlacementSubtrees(active, selected);
        if (freeData)
            active.ImageStore.RemoveUnreferencedImages(GetRetainedImageIds(active));
    }

    internal void RemoveActiveOrdinaryPlacements(
        uint imageId,
        uint placementId = 0)
    {
        if (imageId == 0)
            return;

        var active = Active;
        var selected = active.Placements
            .Where(placement => placement.ImageId == imageId &&
                (placementId == 0 || placement.PlacementId == placementId))
            .Select(placement => placement.GraphId)
            .Concat(active.HistoryPlacements.Values
                .SelectMany(placements => placements)
                .Where(placement => placement.Placement.ImageId == imageId &&
                    (placementId == 0 ||
                     placement.Placement.PlacementId == placementId))
                .Select(placement => placement.Placement.GraphId))
            .ToArray();
        RemovePlacementSubtrees(active, selected);
    }

    internal void RemoveActivePlacements(
        uint imageId,
        uint placementId = 0)
    {
        if (imageId == 0)
            return;

        var active = Active;
        var selected = EnumeratePlacementIdentities(active)
            .Where(placement => placement.ImageId == imageId &&
                (placementId == 0 || placement.PlacementId == placementId))
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
    }

    internal void RemoveActivePlacementsByGraphId(
        IEnumerable<long> graphIds)
        => RemovePlacementSubtrees(Active, graphIds);

    internal void RemoveActiveRelativePlacementsByGraphId(
        IEnumerable<long> graphIds)
    {
        var selected = graphIds.ToHashSet();
        RemovePlacementSubtrees(
            Active,
            Active.Placements
                .Where(placement => placement.IsRelative &&
                    selected.Contains(placement.GraphId))
                .Select(placement => placement.GraphId)
                .ToArray());
    }

    internal void RemoveActivePlacementsInImageSet(
        IReadOnlySet<uint> imageIds)
    {
        ArgumentNullException.ThrowIfNull(imageIds);
        var active = Active;
        var selected = EnumeratePlacementIdentities(active)
            .Where(placement => imageIds.Contains(placement.ImageId))
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
    }

    internal void RemoveActivePlacementsByZIndex(int zIndex)
    {
        var active = Active;
        var selected = active.Placements
            .Where(placement => placement.ZIndex == zIndex)
            .Select(placement => placement.GraphId)
            .Concat(active.HistoryPlacements.Values
                .SelectMany(placements => placements)
                .Where(placement => placement.Placement.ZIndex == zIndex)
                .Select(placement => placement.Placement.GraphId))
            .ToArray();
        RemovePlacementSubtrees(active, selected);
    }

    internal PlacementError TryReplaceActivePlacement(
        uint imageId,
        uint placementId,
        KgpPlacement? replacement,
        uint parentImageId,
        uint parentPlacementId,
        int parentOffsetHorizontal,
        int parentOffsetVertical)
    {
        var active = Active;
        ReconcileImageReferences(active);

        var existingGraphId = 0L;
        var hasExisting = placementId > 0 &&
            TryFindGraphId(active, imageId, placementId, out existingGraphId);
        long? parentGraphId = null;
        if (parentImageId > 0)
        {
            var parentError = TryResolveParent(
                active,
                hasExisting ? existingGraphId : null,
                parentImageId,
                parentPlacementId,
                out var resolvedParentGraphId);
            if (parentError != PlacementError.None)
                return parentError;

            parentGraphId = resolvedParentGraphId;
        }

        var graphId = hasExisting
            ? existingGraphId
            : AllocateGraphId(active);
        if (replacement is null)
        {
            if (hasExisting)
                RemovePlacementSubtrees(active, [graphId]);
            return PlacementError.None;
        }

        if (hasExisting)
            DetachPlacement(active, graphId);
        var stored = replacement
            .WithPosition(parentGraphId.HasValue ? 0 : replacement.Row,
                parentGraphId.HasValue ? 0 : replacement.Column)
            .WithGraphIdentity(
                graphId,
                parentGraphId,
                parentOffsetHorizontal,
                parentOffsetVertical);
        active.Placements.Add(stored);
        RebuildHistoryReferences(active);
        RebuildVirtualReferences(active);
        return PlacementError.None;
    }

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
        var selected = active.Placements
            .Where(placement => !placement.IsRelative)
            .Select(placement => placement.GraphId)
            .ToList();
        if (clearHistory)
        {
            selected.AddRange(active.HistoryPlacements.Values
                .SelectMany(placements => placements)
                .Select(placement => placement.Placement.GraphId));
        }

        RemovePlacementSubtrees(active, selected);
        if (clearHistory)
            active.HistoryReferences.Clear();
        active.ImageStore.RemoveUnreferencedImages(GetRetainedImageIds(active));
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
            if (!HasPlacementReference(Active, imageId))
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
        var selected = EnumeratePlacementIdentities(active)
            .Where(placement => placement.ImageId == imageId)
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
    }

    internal void ReplaceActivePlacement(
        uint imageId,
        uint placementId,
        KgpPlacement? replacement)
    {
        _ = TryReplaceActivePlacement(
            imageId,
            placementId,
            replacement,
            parentImageId: 0,
            parentPlacementId: 0,
            parentOffsetHorizontal: 0,
            parentOffsetVertical: 0);
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

        var virtualPlacements = Active.VirtualPlacements;
        for (var i = 0; i < virtualPlacements.Count; i++)
        {
            if (virtualPlacements[i].ImageId == relocation.PreviousId)
            {
                virtualPlacements[i] = virtualPlacements[i] with
                {
                    ImageId = relocation.CurrentId,
                };
            }
        }
        RebuildVirtualReferences(Active);
        RebuildHistoryReferences(Active);
    }

    internal void MoveMainPlacementsIntoHistory(long rowId)
    {
        if (_alternateActive)
            throw new InvalidOperationException("Alternate-screen placements cannot enter main-screen history.");

        ReconcileImageReferences(_main);
        for (var i = _main.Placements.Count - 1; i >= 0; i--)
        {
            var placement = _main.Placements[i];
            if (placement.IsRelative)
                continue;

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
        if (!_main.HistoryPlacements.TryGetValue(pruned.RowId, out var placements))
            return;

        var transfers = new List<(long RowId, HistoryPlacement Placement)>();
        var removedGraphIds = new List<long>();
        var removedImageIds = new HashSet<uint>();
        foreach (var historyPlacement in placements.ToArray())
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
                    transfers.Add((successorRowId, transferred));
                    continue;
                }
            }

            removedGraphIds.Add(historyPlacement.Placement.GraphId);
            removedImageIds.Add(historyPlacement.Placement.ImageId);
        }

        RemovePlacementSubtrees(_main, removedGraphIds);
        _main.HistoryPlacements.Remove(pruned.RowId);
        foreach (var transfer in transfers)
        {
            AddHistoryPlacement(
                _main,
                transfer.RowId,
                transfer.Placement,
                retainImage: false);
        }

        RebuildHistoryReferences(_main);
        foreach (var imageId in removedImageIds)
        {
            if (!HasPlacementReference(_main, imageId))
                _main.ImageStore.RemoveImage(imageId);
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
        List<long>? removedGraphIds = null;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            if (placement.IsRelative)
                continue;

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
            {
                removedGraphIds ??= [];
                removedGraphIds.Add(placement.GraphId);
            }
            else
                active.Placements[i] = clipped;
        }

        if (removedGraphIds is not null)
            RemovePlacementSubtrees(active, removedGraphIds);
    }

    internal void ClipActivePlacementsToViewport(
        int width,
        int height,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        List<long>? removedGraphIds = null;
        for (var i = active.Placements.Count - 1; i >= 0; i--)
        {
            var placement = active.Placements[i];
            if (placement.IsRelative)
                continue;

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
            {
                removedGraphIds ??= [];
                removedGraphIds.Add(placement.GraphId);
            }
            else
                active.Placements[i] = clipped;
        }

        if (removedGraphIds is not null)
            RemovePlacementSubtrees(active, removedGraphIds);
    }

    internal void ClearActiveViewport(
        IReadOnlyList<ScrollbackEntry> historyRows,
        int cellPixelHeight)
    {
        var active = Active;
        ReconcileImageReferences(active);
        var selected = active.Placements
            .Where(placement => !placement.IsRelative)
            .Select(placement => placement.GraphId)
            .ToArray();
        RemovePlacementSubtrees(active, selected);
        if (!_alternateActive)
            ClipMainHistoryToRetainedRows(
                historyRows,
                additionalRows: 0,
                cellPixelHeight);
        active.ImageStore.RemoveUnreferencedImages(GetRetainedImageIds(active));
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
            if (placement.IsRelative)
                continue;

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
        active.Placements.RemoveAll(placement => !placement.IsRelative);
        active.HistoryPlacements.Clear();
        var removedGraphIds = new HashSet<long>();
        var removedHistoryImageIds = new HashSet<uint>();

        void MarkRemoved(KgpPlacement placement, bool wasHistory)
        {
            removedGraphIds.Add(placement.GraphId);
            if (wasHistory)
                removedHistoryImageIds.Add(placement.ImageId);
        }

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
                    MarkRemoved(tracked.Placement, wasHistory);
                    continue;
                }

                placement = clippedHistoryPlacement;
            }

            if (!mappedById.TryGetValue(tracked.Id, out var mapped))
            {
                MarkRemoved(placement, wasHistory);
                continue;
            }

            if (mapped.Row < replacement.DiscardedRowCount)
            {
                var discardedRows = replacement.DiscardedRowCount - mapped.Row;
                if ((uint)discardedRows >= placement.DisplayRows)
                {
                    MarkRemoved(placement, wasHistory);
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
                    MarkRemoved(placement, wasHistory);
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
                    MarkRemoved(placement, wasHistory);
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
                    retainImage: false);
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
            else
                MarkRemoved(placement, wasHistory);
        }

        RemovePlacementSubtrees(active, removedGraphIds);
        RebuildHistoryReferences(active);
        foreach (var imageId in removedHistoryImageIds)
        {
            if (!HasPlacementReference(active, imageId))
                active.ImageStore.RemoveImage(imageId);
        }
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureActiveSnapshot()
    {
        var active = Active;
        ReconcileImageReferences(active);
        return active.ImageStore.CaptureSnapshot(
            active.Placements
                .Where(placement => !placement.IsRelative)
                .ToArray());
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureActiveSnapshot(
            IReadOnlyList<ScrollbackEntry> historyRows,
            int selectedHistoryCount,
            TerminalCell[,] screenBuffer,
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
        var captured = active.ImageStore.CaptureSnapshot(
            [],
            GetRetainedImageIds(active));
        var rowIndices = new Dictionary<long, int>(historyRows.Count);
        for (var i = 0; i < historyRows.Count; i++)
            rowIndices.Add(historyRows[i].RowId, i);
        var virtualOrigins = new Dictionary<long, (int Row, int Column)>();
        foreach (var (rowId, _) in active.HistoryPlacements)
        {
            if (!rowIndices.ContainsKey(rowId))
            {
                throw new InvalidOperationException(
                    $"KGP history placement anchor {rowId} is not present in scrollback.");
            }
        }

        var screenWidth = Math.Min(width, screenBuffer.GetLength(1));
        var screenHeight = Math.Min(height, screenBuffer.GetLength(0));
        var screenRow = new TerminalCell[screenWidth];
        if (active.VirtualPlacements.Count > 0)
        {
            for (var historyIndex = 0;
                 historyIndex < historyCount;
                 historyIndex++)
            {
                var cells = historyRows[historyIndex].Row.Cells;
                KgpUnicodePlaceholder.CollectOrigins(
                    cells,
                    historyIndex,
                    active.VirtualPlacements,
                    captured.Images,
                    cellPixelWidth,
                    cellPixelHeight,
                    virtualOrigins);
            }

            for (var row = 0; row < screenHeight; row++)
            {
                for (var column = 0; column < screenWidth; column++)
                    screenRow[column] = screenBuffer[row, column];
                KgpUnicodePlaceholder.CollectOrigins(
                    screenRow,
                    SaturatingAdd(historyCount, row),
                    active.VirtualPlacements,
                    captured.Images,
                    cellPixelWidth,
                    cellPixelHeight,
                    virtualOrigins);
            }
        }

        var rootOrigins = new Dictionary<long, EffectiveOrigin>();
        foreach (var placement in active.Placements)
        {
            if (!placement.IsRelative)
            {
                rootOrigins[placement.GraphId] = new EffectiveOrigin(
                    SaturatingAdd(historyCount, placement.Row),
                    placement.Column);
            }
        }

        foreach (var (rowId, placements) in active.HistoryPlacements)
        {
            var rowIndex = rowIndices[rowId];
            foreach (var placement in placements)
            {
                rootOrigins[placement.Placement.GraphId] =
                    new EffectiveOrigin(
                        SaturatingSubtract(rowIndex, placement.FirstRow),
                        placement.Placement.Column);
            }
        }

        foreach (var (graphId, origin) in virtualOrigins)
        {
            rootOrigins[graphId] = new EffectiveOrigin(
                origin.Row,
                origin.Column);
        }

        var materialized = new List<KgpPlacement>(
            active.Placements.Count +
            active.HistoryPlacements.Values.Sum(placements => placements.Count) +
            active.VirtualPlacements.Count);

        foreach (var placement in active.Placements)
        {
            if (placement.IsRelative)
                continue;

            AddMaterializedPlacement(
                captured.Images,
                materialized,
                placement.WithPosition(
                    SaturatingAdd(historyCount, placement.Row),
                    placement.Column),
                snapshotStart,
                snapshotEnd,
                width,
                cellPixelWidth,
                cellPixelHeight);
        }

        if (!_alternateActive && active.HistoryPlacements.Count > 0)
        {
            foreach (var (rowId, placements) in active.HistoryPlacements)
            {
                var rowIndex = rowIndices[rowId];

                foreach (var placement in placements)
                {
                    AddMaterializedHistoryPlacement(
                        captured.Images,
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

        var resolvedOrigins = new Dictionary<long, EffectiveOrigin?>();
        foreach (var placement in active.Placements
            .Where(placement => placement.IsRelative)
            .OrderBy(placement => placement.GraphId))
        {
            if (!TryResolveEffectiveOrigin(
                    active,
                    placement.GraphId,
                    rootOrigins,
                    resolvedOrigins,
                    out var origin))
            {
                continue;
            }

            AddMaterializedPlacement(
                captured.Images,
                materialized,
                placement.WithPosition(origin.Row, origin.Column),
                snapshotStart,
                snapshotEnd,
                width,
                cellPixelWidth,
                cellPixelHeight);
        }

        var snapshotPlacements = materialized;
        if (active.VirtualPlacements.Count > 0)
        {
            for (var historyIndex = snapshotStart;
                 historyIndex < historyCount;
                 historyIndex++)
            {
                var cells = historyRows[historyIndex].Row.Cells;
                KgpUnicodePlaceholder.MaterializeRow(
                    cells.AsSpan(0, Math.Min(cells.Length, width)),
                    historyIndex - snapshotStart,
                    active.VirtualPlacements,
                    captured.Images,
                    cellPixelWidth,
                    cellPixelHeight,
                    snapshotPlacements);
            }

            for (var row = 0; row < screenHeight; row++)
            {
                for (var column = 0; column < screenWidth; column++)
                    screenRow[column] = screenBuffer[row, column];

                KgpUnicodePlaceholder.MaterializeRow(
                    screenRow,
                    checked(selectedCount + row),
                    active.VirtualPlacements,
                    captured.Images,
                    cellPixelWidth,
                    cellPixelHeight,
                    snapshotPlacements);
            }
        }

        snapshotPlacements.Sort(static (left, right) =>
        {
            var result = left.GraphId.CompareTo(right.GraphId);
            if (result != 0)
                return result;
            result = left.Row.CompareTo(right.Row);
            if (result != 0)
                return result;
            result = left.Column.CompareTo(right.Column);
            if (result != 0)
                return result;
            result = left.SourceY.CompareTo(right.SourceY);
            return result != 0
                ? result
                : left.SourceX.CompareTo(right.SourceX);
        });

        var snapshotImages = new Dictionary<uint, KgpImageData>();
        foreach (var placement in snapshotPlacements)
        {
            if (!snapshotImages.ContainsKey(placement.ImageId) &&
                captured.Images.TryGetValue(placement.ImageId, out var image))
            {
                snapshotImages.Add(placement.ImageId, image);
            }
        }

        return (snapshotPlacements, snapshotImages);
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
        // data, discard the now-invalid placement ownership and its descendants.
        // Virtual prototypes intentionally survive missing data so a later
        // transmission with the same client ID can realize existing text cells.
        var missingImagePlacements = screen.Placements
           .Where(placement => screen.ImageStore.GetImageById(
               placement.ImageId) is null)
           .Select(placement => placement.GraphId)
           .Concat(screen.HistoryPlacements.Values
               .SelectMany(placements => placements)
               .Where(placement => screen.ImageStore.GetImageById(
                   placement.Placement.ImageId) is null)
               .Select(placement => placement.Placement.GraphId))
           .ToArray();
        RemovePlacementSubtrees(screen, missingImagePlacements);

        var orphaned = screen.Placements
           .Where(placement => placement.ParentGraphId is { } parentGraphId &&
               !ContainsGraphId(screen, parentGraphId))
           .Select(placement => placement.GraphId)
           .ToArray();
        RemovePlacementSubtrees(
           screen,
           orphaned,
           reclaimSelectedImages: true);
        RebuildHistoryReferences(screen);
        RebuildVirtualReferences(screen);
    }

    private static HashSet<uint> GetRetainedImageIds(ScreenState screen)
    {
        var retained = EnumeratePlacementIdentities(screen)
           .Select(placement => placement.ImageId)
           .ToHashSet();
        retained.UnionWith(screen.HistoryReferences.Keys);
        retained.UnionWith(screen.VirtualReferences.Keys);
        return retained;
    }

    private static bool HasPlacementReference(ScreenState screen, uint imageId)
        => EnumeratePlacementIdentities(screen)
           .Any(placement => placement.ImageId == imageId);

    private static void RebuildHistoryReferences(ScreenState screen)
    {
        screen.HistoryReferences.Clear();
        foreach (var placement in screen.HistoryPlacements.Values
           .SelectMany(placements => placements))
        {
           var imageId = placement.Placement.ImageId;
           screen.HistoryReferences.TryGetValue(imageId, out var count);
           screen.HistoryReferences[imageId] = checked(count + 1);
        }
    }

    private static void RebuildVirtualReferences(ScreenState screen)
    {
        screen.VirtualReferences.Clear();
        foreach (var placement in screen.VirtualPlacements)
        {
            screen.VirtualReferences.TryGetValue(placement.ImageId, out var count);
            screen.VirtualReferences[placement.ImageId] = checked(count + 1);
        }
    }

    private static long AllocateGraphId(ScreenState screen)
    {
        if (screen.NextPlacementGraphId == long.MaxValue)
            throw new InvalidOperationException("No KGP placement graph IDs are available.");

        return screen.NextPlacementGraphId++;
    }

    private static IEnumerable<PlacementIdentity> EnumeratePlacementIdentities(
        ScreenState screen)
    {
        foreach (var placement in screen.Placements)
        {
            yield return new PlacementIdentity(
                placement.GraphId,
                placement.ImageId,
                placement.PlacementId);
        }

        foreach (var placement in screen.VirtualPlacements)
        {
            yield return new PlacementIdentity(
                placement.GraphId,
                placement.ImageId,
                placement.PlacementId);
        }

        foreach (var placement in screen.HistoryPlacements.Values
            .SelectMany(placements => placements))
        {
            yield return new PlacementIdentity(
                placement.Placement.GraphId,
                placement.Placement.ImageId,
                placement.Placement.PlacementId);
        }
    }

    private static bool TryFindGraphId(
        ScreenState screen,
        uint imageId,
        uint placementId,
        out long graphId)
    {
        graphId = 0;
        if (placementId == 0)
            return false;

        foreach (var placement in EnumeratePlacementIdentities(screen))
        {
            if (placement.ImageId == imageId &&
                placement.PlacementId == placementId)
            {
                graphId = placement.GraphId;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindIdentity(
        ScreenState screen,
        long graphId,
        out PlacementIdentity identity)
    {
        foreach (var placement in EnumeratePlacementIdentities(screen))
        {
            if (placement.GraphId == graphId)
            {
                identity = placement;
                return true;
            }
        }

        identity = default;
        return false;
    }

    private static bool ContainsGraphId(ScreenState screen, long graphId)
        => TryFindIdentity(screen, graphId, out _);

    private static bool TryGetRelativePlacement(
        ScreenState screen,
        long graphId,
        out KgpPlacement placement)
    {
        foreach (var candidate in screen.Placements)
        {
            if (candidate.GraphId == graphId && candidate.IsRelative)
            {
                placement = candidate;
                return true;
            }
        }

        placement = null!;
        return false;
    }

    private static void DetachPlacement(ScreenState screen, long graphId)
    {
        screen.Placements.RemoveAll(placement => placement.GraphId == graphId);
        screen.VirtualPlacements.RemoveAll(
            placement => placement.GraphId == graphId);
        foreach (var rowId in screen.HistoryPlacements.Keys.ToArray())
        {
            var placements = screen.HistoryPlacements[rowId];
            placements.RemoveAll(
                placement => placement.Placement.GraphId == graphId);
            if (placements.Count == 0)
                screen.HistoryPlacements.Remove(rowId);
        }
    }

    private static void RemovePlacementSubtrees(
        ScreenState screen,
        IEnumerable<long> selectedGraphIds,
        bool reclaimSelectedImages = false)
    {
        var selected = selectedGraphIds
            .Where(graphId => graphId >= 0)
            .ToHashSet();
        if (selected.Count == 0)
            return;

        var removal = new HashSet<long>(selected);
        var descendants = new HashSet<long>();
        var queue = new Queue<long>(selected);
        while (queue.TryDequeue(out var parentGraphId))
        {
            foreach (var child in screen.Placements)
            {
                if (child.ParentGraphId != parentGraphId)
                    continue;

                descendants.Add(child.GraphId);
                if (removal.Add(child.GraphId))
                    queue.Enqueue(child.GraphId);
            }
        }

        var reclaimedImageIds = new HashSet<uint>();
        foreach (var graphId in descendants)
        {
            if (TryFindIdentity(screen, graphId, out var identity))
                reclaimedImageIds.Add(identity.ImageId);
        }
        if (reclaimSelectedImages)
        {
            foreach (var graphId in selected)
            {
                if (TryFindIdentity(screen, graphId, out var identity))
                    reclaimedImageIds.Add(identity.ImageId);
            }
        }

        screen.Placements.RemoveAll(
            placement => removal.Contains(placement.GraphId));
        var removedVirtual = screen.VirtualPlacements.RemoveAll(
            placement => removal.Contains(placement.GraphId));
        var removedHistory = false;
        foreach (var rowId in screen.HistoryPlacements.Keys.ToArray())
        {
            var placements = screen.HistoryPlacements[rowId];
            removedHistory |= placements.RemoveAll(
                placement => removal.Contains(placement.Placement.GraphId)) > 0;
            if (placements.Count == 0)
                screen.HistoryPlacements.Remove(rowId);
        }

        if (removedHistory)
            RebuildHistoryReferences(screen);
        if (removedVirtual > 0)
            RebuildVirtualReferences(screen);
        foreach (var imageId in reclaimedImageIds)
        {
            if (!HasPlacementReference(screen, imageId))
                screen.ImageStore.RemoveImage(imageId);
        }
    }

    private static PlacementError TryResolveParent(
        ScreenState screen,
        long? childGraphId,
        uint parentImageId,
        uint parentPlacementId,
        out long parentGraphId)
    {
        parentGraphId = 0;
        var parentImage = screen.ImageStore.GetImageByClientId(parentImageId);
        if (parentImage is null)
            return PlacementError.ParentImageNotFound;

        var candidates = EnumeratePlacementIdentities(screen)
            .Where(placement => placement.ImageId == parentImage.ImageId);
        PlacementIdentity? parent = parentPlacementId > 0
            ? candidates.FirstOrDefault(
                placement => placement.PlacementId == parentPlacementId)
            : candidates.OrderBy(placement => placement.GraphId).FirstOrDefault();
        if (parent is null || parent.Value.GraphId == 0)
            return PlacementError.ParentPlacementNotFound;

        parentGraphId = parent.Value.GraphId;
        if (childGraphId == parentGraphId)
            return PlacementError.SelfParent;

        var depth = 1;
        var ancestorGraphId = parentGraphId;
        var visited = new HashSet<long>();
        while (true)
        {
            if (childGraphId == ancestorGraphId)
                return PlacementError.Cycle;
            if (!visited.Add(ancestorGraphId))
                return PlacementError.Cycle;
            if (!TryGetRelativePlacement(
                    screen,
                    ancestorGraphId,
                    out var ancestor))
            {
                if (!ContainsGraphId(screen, ancestorGraphId))
                    return PlacementError.ParentPlacementNotFound;
                break;
            }

            depth++;
            ancestorGraphId = ancestor.ParentGraphId!.Value;
        }

        if (depth > MaximumParentDepth)
            return PlacementError.TooDeep;
        if (childGraphId is { } existingChildGraphId &&
            depth + GetMaximumDescendantDepth(screen, existingChildGraphId) >
                MaximumParentDepth)
        {
            return PlacementError.TooDeep;
        }

        return PlacementError.None;
    }

    private static int GetMaximumDescendantDepth(
        ScreenState screen,
        long graphId)
    {
        var maximum = 0;
        var queue = new Queue<(long GraphId, int Depth)>();
        queue.Enqueue((graphId, 0));
        while (queue.TryDequeue(out var current))
        {
            foreach (var child in screen.Placements)
            {
                if (child.ParentGraphId != current.GraphId)
                    continue;

                var depth = current.Depth + 1;
                maximum = Math.Max(maximum, depth);
                queue.Enqueue((child.GraphId, depth));
            }
        }

        return maximum;
    }

    private static bool TryResolveEffectiveOrigin(
        ScreenState screen,
        long graphId,
        IReadOnlyDictionary<long, EffectiveOrigin> rootOrigins,
        Dictionary<long, EffectiveOrigin?> resolvedOrigins,
        out EffectiveOrigin origin)
    {
        return TryResolveEffectiveOrigin(
            screen,
            graphId,
            rootOrigins,
            resolvedOrigins,
            new HashSet<long>(),
            out origin);
    }

    private static bool TryResolveEffectiveOrigin(
        ScreenState screen,
        long graphId,
        IReadOnlyDictionary<long, EffectiveOrigin> rootOrigins,
        Dictionary<long, EffectiveOrigin?> resolvedOrigins,
        HashSet<long> visiting,
        out EffectiveOrigin origin)
    {
        if (resolvedOrigins.TryGetValue(graphId, out var cached))
        {
            origin = cached.GetValueOrDefault();
            return cached.HasValue;
        }
        if (rootOrigins.TryGetValue(graphId, out origin))
        {
            resolvedOrigins[graphId] = origin;
            return true;
        }
        if (!visiting.Add(graphId) ||
            !TryGetRelativePlacement(screen, graphId, out var placement) ||
            !TryResolveEffectiveOrigin(
                screen,
                placement.ParentGraphId!.Value,
                rootOrigins,
                resolvedOrigins,
                visiting,
                out var parentOrigin))
        {
            resolvedOrigins[graphId] = null;
            origin = default;
            visiting.Remove(graphId);
            return false;
        }

        origin = new EffectiveOrigin(
            SaturatingAdd(parentOrigin.Row, placement.ParentOffsetVertical),
            SaturatingAdd(parentOrigin.Column, placement.ParentOffsetHorizontal));
        visiting.Remove(graphId);
        resolvedOrigins[graphId] = origin;
        return true;
    }

    private static int SaturatingAdd(int left, int right)
        => (int)Math.Clamp((long)left + right, int.MinValue, int.MaxValue);

    private static int SaturatingSubtract(int value, uint offset)
        => (int)Math.Clamp((long)value - offset, int.MinValue, int.MaxValue);

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
        var removedGraphIds = new List<long>();
        var removedImageIds = new HashSet<uint>();

        foreach (var rowId in _main.HistoryPlacements.Keys.ToArray())
        {
            if (!rowIndices.TryGetValue(rowId, out var rowIndex))
            {
                var orphaned = _main.HistoryPlacements[rowId];
                _main.HistoryPlacements.Remove(rowId);
                foreach (var historyPlacement in orphaned)
                {
                    removedGraphIds.Add(historyPlacement.Placement.GraphId);
                    removedImageIds.Add(historyPlacement.Placement.ImageId);
                }
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
                    removedGraphIds.Add(placement.GraphId);
                    removedImageIds.Add(placement.ImageId);
                }
                else
                {
                    placements[i] = retained;
                }
            }

            if (placements.Count == 0)
                _main.HistoryPlacements.Remove(rowId);
        }

        RemovePlacementSubtrees(_main, removedGraphIds);
        RebuildHistoryReferences(_main);
        foreach (var imageId in removedImageIds)
        {
            if (!HasPlacementReference(_main, imageId))
                _main.ImageStore.RemoveImage(imageId);
        }
    }

    private static void AddMaterializedHistoryPlacement(
        IReadOnlyDictionary<uint, KgpImageData> images,
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

        if (!images.TryGetValue(placement.ImageId, out var image))
            return;
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
            images,
            destination,
            sliced,
            snapshotStart,
            snapshotEnd,
            width,
            cellPixelWidth,
            cellPixelHeight);
    }

    private static void AddMaterializedPlacement(
        IReadOnlyDictionary<uint, KgpImageData> images,
        List<KgpPlacement> destination,
        KgpPlacement placement,
        int snapshotStart,
        int snapshotEnd,
        int width,
        int cellPixelWidth,
        int cellPixelHeight)
    {
        if (!images.TryGetValue(placement.ImageId, out var image))
            return;
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
