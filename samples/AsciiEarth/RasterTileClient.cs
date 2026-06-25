using System.Collections.Concurrent;

namespace AsciiEarth;

/// <summary>
/// Downloads raster PNG tiles from OpenStreetMap's tile server with
/// in-memory LRU caching and disk persistence.
/// </summary>
/// <remarks>
/// Follows OSM tile usage policy:
/// - Sends a descriptive User-Agent
/// - Caches aggressively (memory + disk) to minimize requests
/// - Capped zoom and bounded concurrency at the call sites avoid bulk downloads
/// </remarks>
internal sealed class RasterTileClient : IDisposable
{
    private const string TileUrlTemplate = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";
    private const string UserAgent = "Hex1b-AsciiEarth/1.0 (terminal globe viewer; https://github.com/mitchdenny/hex1b)";
    private const int MemoryCacheCapacity = 512;

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, byte[]> _memoryCache = new();
    private readonly LinkedList<string> _lruOrder = new();
    private readonly object _lruLock = new();
    private readonly string _diskCacheDir;

    public RasterTileClient()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        _diskCacheDir = Path.Combine(Path.GetTempPath(), "hex1b-asciiearth-cache");
        Directory.CreateDirectory(_diskCacheDir);
    }

    /// <summary>
    /// Gets a tile's PNG bytes, checking memory cache → disk cache → network.
    /// Returns null if the tile cannot be fetched.
    /// </summary>
    public async ValueTask<byte[]?> GetTileAsync(int zoom, int x, int y, CancellationToken ct = default)
    {
        var key = $"{zoom}/{x}/{y}";

        // Memory cache hit
        if (_memoryCache.TryGetValue(key, out var cached))
        {
            TouchLru(key);
            return cached;
        }

        // Disk cache hit
        var diskPath = GetDiskPath(zoom, x, y);
        if (File.Exists(diskPath))
        {
            try
            {
                var diskBytes = await File.ReadAllBytesAsync(diskPath, ct);
                AddToMemoryCache(key, diskBytes);
                return diskBytes;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                // Fall through to network on a corrupt/locked disk entry.
            }
        }

        // Network fetch
        try
        {
            var url = string.Format(TileUrlTemplate, zoom, x, y);
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            AddToMemoryCache(key, bytes);

            // Persist to disk (fire-and-forget, non-critical)
            _ = PersistToDiskAsync(diskPath, bytes);

            return bytes;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private void AddToMemoryCache(string key, byte[] data)
    {
        _memoryCache[key] = data;

        lock (_lruLock)
        {
            _lruOrder.Remove(key);
            _lruOrder.AddFirst(key);

            // Evict oldest entries
            while (_lruOrder.Count > MemoryCacheCapacity)
            {
                var oldest = _lruOrder.Last!.Value;
                _lruOrder.RemoveLast();
                _memoryCache.TryRemove(oldest, out _);
            }
        }
    }

    private void TouchLru(string key)
    {
        lock (_lruLock)
        {
            _lruOrder.Remove(key);
            _lruOrder.AddFirst(key);
        }
    }

    private string GetDiskPath(int zoom, int x, int y)
    {
        var zoomDir = Path.Combine(_diskCacheDir, zoom.ToString());
        Directory.CreateDirectory(zoomDir);
        return Path.Combine(zoomDir, $"{x}_{y}.png");
    }

    private static async Task PersistToDiskAsync(string path, byte[] data)
    {
        try
        {
            await File.WriteAllBytesAsync(path, data);
        }
        catch
        {
            // Disk cache is best-effort
        }
    }

    public void Dispose() => _http.Dispose();
}
