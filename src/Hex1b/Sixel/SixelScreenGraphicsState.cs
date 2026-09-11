using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b;

/// <summary>
/// One screen's (main or alternate) independent Sixel graphics state: its
/// image store, its live placements, and (main screen only) the placements
/// that have scrolled into scrollback history, partitioned by scrollback row
/// identity.
/// </summary>
internal sealed class SixelScreenGraphicsState
{
    private sealed class RetainedResourceOwner(
        SixelScreenGraphicsState screen,
        object syncRoot,
        Func<SixelScreenGraphicsState, SixelData, long, bool> tryReserve)
        : ISixelRetainedResourceOwner
    {
        public SixelRasterResult GetRaster(SixelData image)
        {
            if (image.TryGetCachedRaster(out var cached))
                return cached;

            var candidate = image.ComputeRasterCandidate();
            lock (syncRoot)
            {
                if (!screen.Images.Contains(image))
                {
                    image.DetachRetainedResourceOwner(this);
                    return image.PublishRasterUnowned(candidate);
                }

                return image.PublishRasterOwned(
                    candidate,
                    bytes => tryReserve(screen, image, bytes));
            }
        }

        public SixelPixelBuffer? GetPixels(SixelData image)
        {
            if (image.TryGetCachedPixels(out var cached, out var attempted))
                return cached;
            if (attempted)
                return null;

            var raster = GetRaster(image);
            var candidate = image.ComputePixelCandidate(raster);
            lock (syncRoot)
            {
                if (!screen.Images.Contains(image))
                {
                    image.DetachRetainedResourceOwner(this);
                    return image.PublishPixelsUnowned(raster, candidate);
                }

                return image.PublishPixelsOwned(
                    raster,
                    candidate,
                    bytes => tryReserve(screen, image, bytes));
            }
        }
    }

    internal SixelScreenGraphicsState(
        object syncRoot,
        TerminalGraphicsRetainedBudget retainedBudget,
        Func<SixelScreenGraphicsState, SixelData, long, bool> tryReserve)
    {
        RetainedBudget = retainedBudget;
        Images = new SixelImageStore(
            new RetainedResourceOwner(this, syncRoot, tryReserve),
            retainedBudget);
    }

    internal SixelImageStore Images { get; }

    internal TerminalGraphicsRetainedBudget RetainedBudget { get; }

    internal List<SixelPlacement> Placements { get; } = [];

    /// <summary>
    /// Placements that scrolled into history, keyed by the stable scrollback
    /// row identity (<see cref="ScrollbackEntry.RowId"/>) they are anchored
    /// to. Only ever populated for the main screen; the alternate screen has
    /// no history partition.
    /// </summary>
    internal Dictionary<long, List<SixelHistoryPlacement>> HistoryPlacements { get; } = [];

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
    internal int ReconcileImages()
    {
        var retained = new HashSet<byte[]>(SixelContentHashComparer.Instance);
        foreach (var placement in Placements)
            retained.Add(placement.Image.ContentHash);
        foreach (var list in HistoryPlacements.Values)
        {
            foreach (var historyPlacement in list)
                retained.Add(historyPlacement.Placement.Image.ContentHash);
        }

        return Images.RemoveUnreferenced(retained);
    }
}
