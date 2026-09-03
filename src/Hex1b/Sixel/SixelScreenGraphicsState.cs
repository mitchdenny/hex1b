using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// One screen's (main or alternate) independent Sixel graphics state: its
/// image store, its live placements, and (main screen only) the placements
/// that have scrolled into scrollback history, partitioned by scrollback row
/// identity.
/// </summary>
internal sealed class SixelScreenGraphicsState
{
    internal SixelImageStore Images { get; } = new();

    internal List<SixelPlacement> Placements { get; } = [];

    /// <summary>
    /// Placements that scrolled into history, keyed by the stable scrollback
    /// row identity (<see cref="ScrollbackEntry.RowId"/>) they are anchored
    /// to. Only ever populated for the main screen; the alternate screen has
    /// no history partition.
    /// </summary>
    internal Dictionary<long, List<SixelPlacement>> HistoryPlacements { get; } = [];

    internal void Clear()
    {
        Placements.Clear();
        HistoryPlacements.Clear();
        Images.Clear();
    }

    /// <summary>
    /// Recomputes which images are still reachable from this screen's live
    /// placements and history placements, sweeping everything else from
    /// <see cref="Images"/>.
    /// </summary>
    internal void ReconcileImages()
    {
        var retained = new HashSet<byte[]>(SixelContentHashComparer.Instance);
        foreach (var placement in Placements)
            retained.Add(placement.Image.ContentHash);
        foreach (var list in HistoryPlacements.Values)
        {
            foreach (var placement in list)
                retained.Add(placement.Image.ContentHash);
        }

        Images.RemoveUnreferenced(retained);
    }
}
