namespace Hex1b.Surfaces;

/// <summary>
/// A simple per-dimension pool for reusing <see cref="Surface"/> instances between render frames.
/// </summary>
/// <remarks>
/// This pool is intentionally simple and single-threaded. It is designed to reduce GC pressure
/// from per-frame <see cref="Surface"/> allocations in nodes like <c>SurfaceNode</c> and
/// <c>EffectPanelNode</c>. Buckets are keyed by (width, height, cell metrics) and are evicted
/// if they haven't been rented in a configurable number of render cycles.
/// </remarks>
internal sealed class SurfacePool
{
    private readonly Dictionary<SurfacePoolKey, Bucket> _buckets = new();
    private readonly int _maxSurfacesPerBucket;
    private readonly int _maxIdleFrames;

    private int _frameIndex;

    public SurfacePool(int maxSurfacesPerBucket, int maxIdleFrames)
    {
        _maxSurfacesPerBucket = Math.Max(0, maxSurfacesPerBucket);
        _maxIdleFrames = Math.Max(0, maxIdleFrames);
    }

    public void NextFrame()
    {
        _frameIndex++;

        // Trim lazily to keep overhead negligible.
        // 64 is arbitrary: keeps cleanup amortized without holding memory too long.
        if (_maxIdleFrames <= 0 || _buckets.Count == 0 || (_frameIndex & 0x3F) != 0)
            return;

        TrimIdleBuckets();
    }

    public Surface Rent(int width, int height, CellMetrics cellMetrics)
    {
        var key = new SurfacePoolKey(width, height, cellMetrics);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Bucket();
            _buckets[key] = bucket;
        }

        bucket.LastRentedFrame = _frameIndex;
        return bucket.Surfaces.TryPop(out var surface)
            ? surface
            : new Surface(width, height, cellMetrics);
    }

    public void Return(Surface surface)
    {
        var key = new SurfacePoolKey(surface.Width, surface.Height, surface.CellMetrics);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Bucket();
            _buckets[key] = bucket;
        }

        // Ensure pooled surfaces don't retain references to tracked objects (sixels/hyperlinks).
        // This avoids retaining large object graphs across frames.
        surface.ClearAndReleaseTrackedObjects();

        if (_maxSurfacesPerBucket == 0 || bucket.Surfaces.Count >= _maxSurfacesPerBucket)
            return;

        bucket.Surfaces.Push(surface);
    }

    private void TrimIdleBuckets()
    {
        List<SurfacePoolKey>? removeKeys = null;

        foreach (var (key, bucket) in _buckets)
        {
            // "Not used" means "not rented" after X render cycles.
            if (_frameIndex - bucket.LastRentedFrame <= _maxIdleFrames)
                continue;

            (removeKeys ??= []).Add(key);
        }

        if (removeKeys is null)
            return;

        foreach (var key in removeKeys)
            _buckets.Remove(key);
    }

    private sealed class Bucket
    {
        public readonly Stack<Surface> Surfaces = new();
        public int LastRentedFrame;
    }

    private readonly record struct SurfacePoolKey(int Width, int Height, CellMetrics CellMetrics);
}

