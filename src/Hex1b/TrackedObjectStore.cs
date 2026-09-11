using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// Manages reference-counted tracked objects associated with terminal cells.
/// </summary>
/// <remarks>
/// <para>
/// When cells hold references to objects (like Sixel graphics or hyperlinks),
/// this store provides content-addressable deduplication and lifecycle management.
/// Objects are automatically removed when their reference count reaches zero.
/// </para>
/// <para>
/// This is internal infrastructure - not exposed to API consumers.
/// Type-specific APIs (e.g., <see cref="GetOrCreateSixel(string, int, int)"/>) handle deduplication
/// by complete immutable resource identity.
/// </para>
/// </remarks>
internal sealed class TrackedObjectStore
{
    // Sixel resource identity includes raster content/state, placement span,
    // and captured protocol metrics.
    private readonly Dictionary<string, TrackedObject<SixelData>> _sixelByIdentity = [];
    
    // Content-addressable storage for hyperlink data, keyed by content hash
    private readonly Dictionary<byte[], TrackedObject<HyperlinkData>> _hyperlinkByHash = new(ByteArrayComparer.Instance);
    
    // Content-addressable storage for KGP data, keyed by content hash
    private readonly Dictionary<byte[], TrackedObject<KgpCellData>> _kgpByHash = new(ByteArrayComparer.Instance);
    
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of tracked Sixel objects currently in the store.
    /// </summary>
    public int SixelCount
    {
        get
        {
            lock (_lock)
            {
                return _sixelByIdentity.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of tracked hyperlink objects currently in the store.
    /// </summary>
    public int HyperlinkCount
    {
        get
        {
            lock (_lock)
            {
                return _hyperlinkByHash.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of tracked KGP objects currently in the store.
    /// </summary>
    public int KgpCount
    {
        get
        {
            lock (_lock)
            {
                return _kgpByHash.Count;
            }
        }
    }

    /// <summary>
    /// Gets or creates a tracked Sixel object for the given payload and cell span.
    /// If an identical compatible resource already exists, adds a reference and
    /// returns it. Otherwise, creates a new tracked object with refcount 1.
    /// </summary>
    /// <param name="payload">The raw Sixel DCS sequence.</param>
    /// <param name="widthInCells">Width of the image in terminal cells.</param>
    /// <param name="heightInCells">Height of the image in terminal cells.</param>
    /// <returns>A tracked Sixel object (new or existing with added ref).</returns>
    public TrackedObject<SixelData> GetOrCreateSixel(string payload, int widthInCells, int heightInCells)
        => GetOrCreateSixel(
            payload,
            widthInCells,
            heightInCells,
            SixelParser.ParsePayload(payload));

    internal TrackedObject<SixelData> GetOrCreateSixel(
        string payload,
        int widthInCells,
        int heightInCells,
        SixelParseResult parseResult,
        SixelRasterPreparation? rasterPreparation = null,
        SixelCellMetrics? cellMetrics = null)
    {
        var capturedMetrics = cellMetrics ?? SixelCellMetrics.Unknown;
        var hash = SixelData.ComputeHash(
            payload,
            rasterPreparation?.Identity,
            widthInCells,
            heightInCells,
            capturedMetrics);
        var identity = Convert.ToHexString(hash);

        lock (_lock)
        {
            if (_sixelByIdentity.TryGetValue(identity, out var existing))
            {
                // Found existing - add a reference and return it
                existing.AddRef();
                return existing;
            }

            // Create the data
            var sixelData = new SixelData(
                payload,
                widthInCells,
                heightInCells,
                hash,
                parseResult.DeclaredExtent.Width,
                parseResult.DeclaredExtent.Height,
                parseResult,
                rasterPreparation: rasterPreparation,
                cellMetrics: capturedMetrics);
            
            // Create new tracked wrapper with removal callback
            var tracked = new TrackedObject<SixelData>(
                sixelData,
                onZeroRefs: obj => RemoveSixel(identity, obj));

            _sixelByIdentity[identity] = tracked;
            return tracked;
        }
    }

    /// <summary>
    /// Gets or creates a tracked hyperlink object for the given URI and parameters.
    /// If an identical hyperlink already exists, adds a reference and returns it.
    /// Otherwise, creates a new tracked object with refcount 1.
    /// </summary>
    /// <param name="uri">The hyperlink URI.</param>
    /// <param name="parameters">Optional parameters from the OSC 8 sequence.</param>
    /// <returns>A tracked hyperlink object (new or existing with added ref).</returns>
    public TrackedObject<HyperlinkData> GetOrCreateHyperlink(string uri, string parameters)
    {
        var hash = HyperlinkData.ComputeHash(uri, parameters);

        lock (_lock)
        {
            if (_hyperlinkByHash.TryGetValue(hash, out var existing))
            {
                // Found existing - add a reference and return it
                existing.AddRef();
                return existing;
            }

            // Create the data
            var hyperlinkData = new HyperlinkData(uri, parameters, hash);
            
            // Create new tracked wrapper with removal callback
            var tracked = new TrackedObject<HyperlinkData>(
                hyperlinkData,
                onZeroRefs: obj => RemoveHyperlink(obj.Data));

            _hyperlinkByHash[hash] = tracked;
            return tracked;
        }
    }

    /// <summary>
    /// Gets or creates a tracked KGP object for the given cell data.
    /// If an identical KGP image (by content hash) already exists, adds a reference and returns it.
    /// Otherwise, creates a new tracked object with refcount 1.
    /// </summary>
    /// <param name="kgpData">The KGP cell data to track.</param>
    /// <returns>A tracked KGP object (new or existing with added ref).</returns>
    public TrackedObject<KgpCellData> GetOrCreateKgp(KgpCellData kgpData)
    {
        var hash = kgpData.TrackingHash;

        lock (_lock)
        {
            if (_kgpByHash.TryGetValue(hash, out var existing))
            {
                existing.AddRef();
                return existing;
            }

            var tracked = new TrackedObject<KgpCellData>(
                kgpData,
                onZeroRefs: obj => RemoveKgp(obj.Data));

            _kgpByHash[hash] = tracked;
            return tracked;
        }
    }

    /// <summary>
    /// Clears all tracked objects, resetting the store.
    /// </summary>
    /// <remarks>
    /// This does not decrement reference counts - it's a hard reset.
    /// Use only when disposing the terminal or in tests.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _sixelByIdentity.Clear();
            _hyperlinkByHash.Clear();
            _kgpByHash.Clear();
        }
    }

    private void RemoveSixel(string identity, TrackedObject<SixelData> tracked)
    {
        lock (_lock)
        {
            if (_sixelByIdentity.TryGetValue(identity, out var current) &&
                ReferenceEquals(current, tracked))
            {
                _sixelByIdentity.Remove(identity);
            }
        }
    }

    private void RemoveHyperlink(HyperlinkData hyperlink)
    {
        lock (_lock)
        {
            _hyperlinkByHash.Remove(hyperlink.ContentHash);
        }
    }

    private void RemoveKgp(KgpCellData kgp)
    {
        lock (_lock)
        {
            _kgpByHash.Remove(kgp.TrackingHash);
        }
    }

    /// <summary>
    /// Comparer for byte arrays used as dictionary keys.
    /// </summary>
    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.AsSpan().SequenceEqual(y.AsSpan());
        }

        public int GetHashCode(byte[] obj)
        {
            // Use first 4 bytes of hash as the hashcode
            if (obj.Length >= 4)
            {
                return BitConverter.ToInt32(obj, 0);
            }
            return obj.Length > 0 ? obj[0] : 0;
        }
    }
}
