using System.Collections.Concurrent;

namespace Hex1b.Data;

/// <summary>
/// Configuration options for <see cref="TileCache"/>.
/// </summary>
internal record TileCacheOptions
{
    /// <summary>
    /// Maximum number of tiles to keep in cache. When exceeded, least-recently-used
    /// tiles are evicted. Default: 10,000.
    /// </summary>
    public int MaxCachedTiles { get; init; } = 10_000;
}

/// <summary>
/// Wraps an <see cref="ITileDataSource"/> with per-tile caching and background loading.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetTiles"/> returns cached tiles immediately and never blocks.
/// Uncached tile positions are returned as <c>default(TileData)</c> and automatically
/// queued for background fetching via the wrapped data source.
/// </para>
/// <para>
/// When background-fetched tiles become available, <see cref="TilesAvailable"/> is raised
/// immediately. The consumer is expected to coalesce rapid notifications (e.g. via a
/// bounded channel) to avoid excessive re-renders.
/// </para>
/// </remarks>
internal class TileCache : IDisposable
{
    private readonly ITileDataSource _dataSource;
    private readonly TileCacheOptions _options;
    private readonly ConcurrentDictionary<(int x, int y), TileData> _cache = new();

    // LRU tracking: tiles ordered by last access time
    private readonly ConcurrentDictionary<(int x, int y), long> _accessOrder = new();
    private long _accessCounter;

    // Background fetch state
    private CancellationTokenSource? _fetchCts;
    private readonly object _fetchLock = new();

    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="TileCache"/> wrapping the specified data source.
    /// </summary>
    /// <param name="dataSource">The tile data source to cache.</param>
    /// <param name="options">Cache configuration options, or <c>null</c> for defaults.</param>
    public TileCache(ITileDataSource dataSource, TileCacheOptions? options = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _options = options ?? new TileCacheOptions();
    }

    /// <summary>
    /// Gets the tile size from the underlying data source.
    /// </summary>
    public Layout.Size TileSize => _dataSource.TileSize;

    /// <summary>
    /// Raised when background-fetched tiles become available.
    /// This event fires on a ThreadPool thread — handlers must be thread-safe.
    /// </summary>
    public event Action? TilesAvailable;

    /// <summary>
    /// Returns tiles for the specified region. Cached tiles are returned immediately.
    /// Uncached positions contain <c>default(TileData)</c> and are automatically
    /// queued for background fetching. This method never blocks.
    /// </summary>
    public TileData[,] GetTiles(int tileX, int tileY, int tilesWide, int tilesTall)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new TileData[tilesWide, tilesTall];
        var hasMissing = false;

        for (int y = 0; y < tilesTall; y++)
        {
            for (int x = 0; x < tilesWide; x++)
            {
                var key = (tileX + x, tileY + y);
                if (_cache.TryGetValue(key, out var tile))
                {
                    result[x, y] = tile;
                    // Update LRU access order
                    _accessOrder[key] = Interlocked.Increment(ref _accessCounter);
                }
                else
                {
                    hasMissing = true;
                    // result[x, y] remains default(TileData)
                }
            }
        }

        if (hasMissing)
        {
            RequestFetch(tileX, tileY, tilesWide, tilesTall);
        }

        return result;
    }

    /// <summary>
    /// Clears all cached tiles and cancels any pending background fetches.
    /// </summary>
    public void Clear()
    {
        CancelPendingFetch();
        _cache.Clear();
        _accessOrder.Clear();
    }

    // Track the region being fetched so we don't cancel duplicate requests
    private int _fetchTileX, _fetchTileY, _fetchTilesWide, _fetchTilesTall;

    private void RequestFetch(int tileX, int tileY, int tilesWide, int tilesTall)
    {
        CancellationTokenSource cts;
        lock (_fetchLock)
        {
            // If already fetching the same region, let it complete
            if (_fetchCts != null && !_fetchCts.IsCancellationRequested
                && _fetchTileX == tileX && _fetchTileY == tileY
                && _fetchTilesWide == tilesWide && _fetchTilesTall == tilesTall)
            {
                return;
            }

            // Different region — cancel previous and start new
            _fetchCts?.Cancel();
            _fetchCts?.Dispose();
            cts = new CancellationTokenSource();
            _fetchCts = cts;
            _fetchTileX = tileX;
            _fetchTileY = tileY;
            _fetchTilesWide = tilesWide;
            _fetchTilesTall = tilesTall;
        }

        var ct = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var tiles = await _dataSource.GetTilesAsync(tileX, tileY, tilesWide, tilesTall, ct);

                // Cache tiles even if cancellation was requested — the data source
                // already returned them, and they may overlap with the new view.
                var anyNew = false;
                for (int y = 0; y < tilesTall && y < tiles.GetLength(1); y++)
                {
                    for (int x = 0; x < tilesWide && x < tiles.GetLength(0); x++)
                    {
                        var key = (tileX + x, tileY + y);
                        var tile = tiles[x, y];

                        // Only store non-empty tiles
                        if (!string.IsNullOrEmpty(tile.Content))
                        {
                            _cache[key] = tile;
                            _accessOrder[key] = Interlocked.Increment(ref _accessCounter);
                            anyNew = true;
                        }
                    }
                }

                // Clear tracked region so future requests for the same area can re-fetch
                lock (_fetchLock)
                {
                    if (ReferenceEquals(_fetchCts, cts))
                    {
                        _fetchTileX = _fetchTileY = _fetchTilesWide = _fetchTilesTall = 0;
                    }
                }

                if (anyNew)
                {
                    EvictIfNeeded();
                    TilesAvailable?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when viewport changes during fetch
            }
            catch (Exception)
            {
                // Swallow fetch errors — tiles simply remain uncached and will be
                // retried on the next render cycle
            }
        }, ct);
    }

    private void EvictIfNeeded()
    {
        if (_cache.Count <= _options.MaxCachedTiles) return;

        // Evict least-recently-used tiles
        var toEvict = _accessOrder
            .OrderBy(kv => kv.Value)
            .Take(_cache.Count - _options.MaxCachedTiles)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in toEvict)
        {
            _cache.TryRemove(key, out _);
            _accessOrder.TryRemove(key, out _);
        }
    }

    private void CancelPendingFetch()
    {
        lock (_fetchLock)
        {
            _fetchCts?.Cancel();
            _fetchCts?.Dispose();
            _fetchCts = null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CancelPendingFetch();
        _cache.Clear();
        _accessOrder.Clear();
    }
}
