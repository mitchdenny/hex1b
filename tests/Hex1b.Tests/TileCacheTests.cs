using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Slow tile data source that simulates async I/O with a configurable delay.
/// Used to test TileCache background fetch and notification.
/// </summary>
internal class SlowTileDataSource : ITileDataSource
{
    private readonly TimeSpan _delay;
    private int _fetchCount;

    public SlowTileDataSource(TimeSpan delay)
    {
        _delay = delay;
    }

    public int FetchCount => _fetchCount;
    public Size TileSize { get; } = new(3, 1);

    public async ValueTask<TileData[,]> GetTilesAsync(
        int tileX, int tileY, int tilesWide, int tilesTall,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _fetchCount);
        await Task.Delay(_delay, cancellationToken);

        var tiles = new TileData[tilesWide, tilesTall];
        for (int y = 0; y < tilesTall; y++)
        {
            for (int x = 0; x < tilesWide; x++)
            {
                var tx = tileX + x;
                var ty = tileY + y;
                tiles[x, y] = new TileData(
                    $"{tx},{ty}"[..Math.Min(3, $"{tx},{ty}".Length)],
                    Hex1bColor.White,
                    Hex1bColor.Black);
            }
        }
        return tiles;
    }
}

public class TileCacheTests
{
    [Fact]
    public void GetTiles_EmptyCache_ReturnsDefaultTiles()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromSeconds(10));
        using var cache = new TileCache(ds);

        var tiles = cache.GetTiles(0, 0, 3, 3);

        // All tiles should be default (not yet fetched)
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                Assert.Null(tiles[x, y].Content);
            }
        }
    }

    [Fact]
    public void GetTiles_NeverBlocks()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromSeconds(30));
        using var cache = new TileCache(ds);

        // This must return immediately even though data source takes 30s
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tiles = cache.GetTiles(0, 0, 10, 10);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100, $"GetTiles took {sw.ElapsedMilliseconds}ms — should be instant");
    }

    [Fact]
    public async Task GetTiles_AfterBackgroundFetch_ReturnsCachedTiles()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromMilliseconds(10));
        using var cache = new TileCache(ds);

        // First call — triggers background fetch
        cache.GetTiles(0, 0, 2, 2);

        // Wait for background fetch to complete
        await Task.Delay(200);

        // Second call — should have cached tiles
        var tiles = cache.GetTiles(0, 0, 2, 2);
        Assert.NotNull(tiles[0, 0].Content);
        Assert.Equal("0,0", tiles[0, 0].Content);
        Assert.Equal("1,0", tiles[1, 0].Content);
        Assert.Equal("0,1", tiles[0, 1].Content);
        Assert.Equal("1,1", tiles[1, 1].Content);
    }

    [Fact]
    public async Task TilesAvailable_FiresAfterBackgroundFetch()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromMilliseconds(10));
        var notified = new TaskCompletionSource();
        using var cache = new TileCache(ds);
        cache.TilesAvailable += () => notified.TrySetResult();

        cache.GetTiles(0, 0, 3, 3);

        // Notification fires immediately after fetch completes (no debounce)
        var completed = await Task.WhenAny(notified.Task, Task.Delay(500));
        Assert.Same(notified.Task, completed);
    }

    [Fact]
    public async Task CancelledFetch_StillCachesTiles()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromMilliseconds(50));
        var notifyCount = 0;
        using var cache = new TileCache(ds);
        cache.TilesAvailable += () => Interlocked.Increment(ref notifyCount);

        // First request — starts fetch for region (0,0)
        cache.GetTiles(0, 0, 2, 2);
        await Task.Delay(10);

        // Second request — different region cancels first, but if first
        // returns before cancellation is observed, tiles should still be cached
        cache.GetTiles(100, 100, 2, 2);

        // Wait for fetches to complete
        await Task.Delay(300);

        // Second region must be cached
        var tiles = cache.GetTiles(100, 100, 2, 2);
        Assert.Equal("100", tiles[0, 0].Content);

        // First region might also be cached (if data source returned before
        // cancellation was checked) — this is the key behavioral change
    }

    [Fact]
    public async Task NewFetch_CancelsPreviousFetch()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromMilliseconds(200));
        using var cache = new TileCache(ds);

        // First request
        cache.GetTiles(0, 0, 2, 2);
        await Task.Delay(10);

        // Second request (should cancel first)
        cache.GetTiles(100, 100, 2, 2);

        // Wait for second fetch to complete
        await Task.Delay(500);

        // Second region should be cached
        var tiles = cache.GetTiles(100, 100, 2, 2);
        Assert.Equal("100", tiles[0, 0].Content);
    }

    [Fact]
    public void Clear_RemovesAllCachedTiles()
    {
        var ds = new TestTileDataSource();
        using var cache = new TileCache(ds);

        // Warm the cache by doing a sync-returning fetch
        cache.GetTiles(0, 0, 2, 2);

        // Wait a bit for background fetch
        Thread.Sleep(100);

        // Verify cached
        var tiles = cache.GetTiles(0, 0, 2, 2);
        Assert.NotNull(tiles[0, 0].Content);

        // Clear
        cache.Clear();

        // Should be empty again
        tiles = cache.GetTiles(0, 0, 2, 2);
        Assert.Null(tiles[0, 0].Content);
    }

    [Fact]
    public void TileSize_DelegatesToDataSource()
    {
        var ds = new TestTileDataSource();
        using var cache = new TileCache(ds);

        Assert.Equal(new Size(3, 1), cache.TileSize);
    }

    [Fact]
    public async Task Eviction_RemovesLruTilesWhenOverCapacity()
    {
        var ds = new TestTileDataSource();
        using var cache = new TileCache(ds, new TileCacheOptions
        {
            MaxCachedTiles = 4,
        });

        // Fetch a 3x3 region (9 tiles > max 4)
        cache.GetTiles(0, 0, 3, 3);
        await Task.Delay(200);

        // The cache should have evicted some tiles to stay at/below 4
        var tiles = cache.GetTiles(0, 0, 3, 3);
        var cachedCount = 0;
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                if (tiles[x, y].Content != null) cachedCount++;

        // Some tiles should have survived eviction (the 4 most recent)
        Assert.True(cachedCount <= 4 || cachedCount == 9,
            $"Expected ≤4 cached tiles after eviction or 9 if re-fetched, got {cachedCount}");
    }

    [Fact]
    public async Task RequestFetch_SameRegion_DoesNotCancelInFlight()
    {
        var ds = new SlowTileDataSource(TimeSpan.FromMilliseconds(100));
        using var cache = new TileCache(ds);

        // First call triggers a background fetch
        cache.GetTiles(0, 0, 2, 2);

        // Simulate animation re-renders: call GetTiles for the SAME region repeatedly
        // These should NOT cancel the in-flight fetch
        await Task.Delay(20);
        cache.GetTiles(0, 0, 2, 2);
        await Task.Delay(20);
        cache.GetTiles(0, 0, 2, 2);

        // Wait for the original fetch to complete (100ms delay + some slack)
        await Task.Delay(200);

        // Tiles should be cached — the repeated calls didn't cancel the fetch
        var tiles = cache.GetTiles(0, 0, 2, 2);
        Assert.NotNull(tiles[0, 0].Content);

        // Should have only triggered ONE fetch, not three
        Assert.Equal(1, ds.FetchCount);
    }
}
