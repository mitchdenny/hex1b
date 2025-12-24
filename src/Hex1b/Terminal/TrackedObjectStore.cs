namespace Hex1b.Terminal;

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
/// Type-specific APIs (e.g., <see cref="GetOrCreateSixel"/>) handle deduplication
/// by content hash.
/// </para>
/// </remarks>
internal sealed class TrackedObjectStore
{
    // Content-addressable storage for Sixel data, keyed by content hash
    private readonly Dictionary<byte[], TrackedObject<SixelData>> _sixelByHash = new(ByteArrayComparer.Instance);
    
    // Content-addressable storage for hyperlink data, keyed by content hash
    private readonly Dictionary<byte[], TrackedObject<HyperlinkData>> _hyperlinkByHash = new(ByteArrayComparer.Instance);
    
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
                return _sixelByHash.Count;
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
    /// Gets or creates a tracked Sixel object for the given payload.
    /// If an identical payload already exists, adds a reference and returns it.
    /// Otherwise, creates a new tracked object with refcount 1.
    /// </summary>
    /// <param name="payload">The raw Sixel DCS sequence.</param>
    /// <param name="widthInCells">Width of the image in terminal cells.</param>
    /// <param name="heightInCells">Height of the image in terminal cells.</param>
    /// <returns>A tracked Sixel object (new or existing with added ref).</returns>
    public TrackedObject<SixelData> GetOrCreateSixel(string payload, int widthInCells, int heightInCells)
    {
        var hash = SixelData.ComputeHash(payload);

        lock (_lock)
        {
            if (_sixelByHash.TryGetValue(hash, out var existing))
            {
                // Found existing - add a reference and return it
                existing.AddRef();
                return existing;
            }

            // Create the data
            var sixelData = new SixelData(payload, widthInCells, heightInCells, hash);
            
            // Create new tracked wrapper with removal callback
            var tracked = new TrackedObject<SixelData>(
                sixelData,
                onZeroRefs: obj => RemoveSixel(obj.Data));

            _sixelByHash[hash] = tracked;
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
            _sixelByHash.Clear();
            _hyperlinkByHash.Clear();
        }
    }

    private void RemoveSixel(SixelData sixel)
    {
        lock (_lock)
        {
            _sixelByHash.Remove(sixel.ContentHash);
        }
    }

    private void RemoveHyperlink(HyperlinkData hyperlink)
    {
        lock (_lock)
        {
            _hyperlinkByHash.Remove(hyperlink.ContentHash);
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
