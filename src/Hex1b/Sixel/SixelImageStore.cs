using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Identity-keyed store of anonymous Sixel image resources for a single screen
/// (main or alternate).
/// </summary>
/// <remarks>
/// Unlike <see cref="TrackedObjectStore"/>'s manually reference-counted
/// tracked objects, this store follows <see cref="KgpImageStore"/>'s
/// reachability model: an image is retained only while at least one live
/// placement or history placement in the owning screen still references it.
/// <see cref="RemoveUnreferenced"/> recomputes that reachable set and sweeps
/// everything else after any placement mutation. Snapshots are unaffected by
/// this sweep because they hold their own direct <see cref="SixelData"/>
/// references (kept alive by ordinary .NET garbage collection), decoupled
/// from this store's dictionary.
/// </remarks>
internal sealed class SixelImageStore
{
    private sealed record Entry(SixelData Image, long RetainedBytes);

    private readonly Dictionary<byte[], Entry> _byHash = new(SixelContentHashComparer.Instance);
    private readonly ISixelRetainedResourceOwner _owner;
    private readonly TerminalGraphicsRetainedBudget _retainedBudget;
    private long _retainedBytes;

    internal SixelImageStore(
        ISixelRetainedResourceOwner owner,
        TerminalGraphicsRetainedBudget retainedBudget)
    {
        _owner = owner;
        _retainedBudget = retainedBudget;
    }

    /// <summary>Number of distinct images currently retained.</summary>
    internal int Count => _byHash.Count;

    /// <summary>The images currently retained.</summary>
    internal IReadOnlyCollection<SixelData> Images =>
        _byHash.Values.Select(entry => entry.Image).ToArray();

    /// <summary>Deterministic retained bytes for all distinct images.</summary>
    internal long RetainedBytes => _retainedBytes;

    /// <summary>
    /// Aggregate logical pixel area of the distinct retained images.
    /// </summary>
    internal long GetBoundedLogicalPixelCount(long maximumPerImage)
    {
        long total = 0;
        foreach (var entry in _byHash.Values)
        {
            var image = entry.Image;
            var extent = image.ParseResult.UnscaledLogicalCanvasExtent;
            var pixels = Math.Min((long)extent.Width * extent.Height, maximumPerImage);
            total = pixels > long.MaxValue - total ? long.MaxValue : total + pixels;
        }

        return total;
    }

    /// <summary>
    /// Gets the existing image for this exact resource identity (payload,
    /// captured raster state, protocol metrics, and cell span).
    /// </summary>
    internal bool TryGet(byte[] hash, out SixelData image)
    {
        if (_byHash.TryGetValue(hash, out var entry))
        {
            image = entry.Image;
            return true;
        }

        image = null!;
        return false;
    }

    internal void Add(SixelData image)
    {
        var retainedBytes = image.RetainedByteCount;
        image.AttachRetainedResourceOwner(_owner);
        _byHash.Add(image.ContentHash, new Entry(image, retainedBytes));
        _retainedBytes = checked(_retainedBytes + retainedBytes);
        _retainedBudget.SetSixelBytes(_retainedBytes);
    }

    internal bool Contains(SixelData image) =>
        _byHash.TryGetValue(image.ContentHash, out var entry) &&
        ReferenceEquals(entry.Image, image);

    internal bool TryIncreaseRetainedBytes(SixelData image, long addedBytes)
    {
        if (addedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(addedBytes));
        if (!_byHash.TryGetValue(image.ContentHash, out var entry) ||
            !ReferenceEquals(entry.Image, image))
        {
            return false;
        }

        var retainedBytes = checked(entry.RetainedBytes + addedBytes);
        _byHash[image.ContentHash] = entry with { RetainedBytes = retainedBytes };
        _retainedBytes = checked(_retainedBytes + addedBytes);
        _retainedBudget.SetSixelBytes(_retainedBytes);
        return true;
    }

    internal static SixelData Create(
        string payload,
        byte[] contentHash,
        int widthInCells,
        int heightInCells,
        SixelParseResult parseResult,
        SixelRasterPreparation rasterPreparation,
        SixelCellMetrics cellMetrics,
        bool payloadComplete)
    {
        return new SixelData(
            payload,
            widthInCells,
            heightInCells,
            contentHash,
            parseResult.DeclaredExtent.Width,
            parseResult.DeclaredExtent.Height,
            parseResult,
            rasterPreparation: rasterPreparation,
            cellMetrics: cellMetrics,
            payloadComplete: payloadComplete);
    }

    internal void Clear()
    {
        foreach (var entry in _byHash.Values)
            entry.Image.DetachRetainedResourceOwner(_owner);
        _byHash.Clear();
        _retainedBytes = 0;
        _retainedBudget.SetSixelBytes(0);
    }

    /// <summary>
    /// Removes every image whose content hash is not present in
    /// <paramref name="retainedHashes"/> (mark-and-sweep reachability, the
    /// same strategy <see cref="KgpImageStore.RemoveUnreferencedImages"/> uses).
    /// </summary>
    internal int RemoveUnreferenced(HashSet<byte[]> retainedHashes)
    {
        if (_byHash.Count == 0)
            return 0;

        List<byte[]>? toRemove = null;
        foreach (var hash in _byHash.Keys)
        {
            if (!retainedHashes.Contains(hash))
                (toRemove ??= []).Add(hash);
        }

        if (toRemove is null)
            return 0;

        foreach (var hash in toRemove)
        {
            var entry = _byHash[hash];
            entry.Image.DetachRetainedResourceOwner(_owner);
            _retainedBytes = checked(_retainedBytes - entry.RetainedBytes);
            _byHash.Remove(hash);
        }
        _retainedBudget.SetSixelBytes(_retainedBytes);

        return toRemove.Count;
    }
}
