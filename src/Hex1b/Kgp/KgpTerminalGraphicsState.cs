namespace Hex1b;

internal sealed class KgpTerminalGraphicsState
{
    private sealed class ScreenState
    {
        internal KgpImageStore ImageStore { get; } = new();
        internal List<KgpPlacement> Placements { get; } = [];
        internal Dictionary<uint, int> HistoryReferences { get; } = [];

        internal void Clear()
        {
            Placements.Clear();
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
            active.HistoryReferences.Clear();

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
        Active.Placements.RemoveAll(placement => placement.ImageId == imageId);
        Active.HistoryReferences.Remove(imageId);
    }

    internal void RelocateActiveImage(KgpImageStore.ImageRelocation relocation)
    {
        var placements = Active.Placements;
        for (var i = 0; i < placements.Count; i++)
        {
            if (placements[i].ImageId == relocation.PreviousId)
                placements[i] = placements[i].WithImageId(relocation.CurrentId);
        }

        if (Active.HistoryReferences.Remove(relocation.PreviousId, out var count))
        {
            Active.HistoryReferences.TryGetValue(relocation.CurrentId, out var existing);
            Active.HistoryReferences[relocation.CurrentId] = checked(existing + count);
        }
    }

    internal (
        IReadOnlyList<KgpPlacement> Placements,
        IReadOnlyDictionary<uint, KgpImageData> Images) CaptureActiveSnapshot()
        => Active.ImageStore.CaptureSnapshot(Active.Placements);
}
