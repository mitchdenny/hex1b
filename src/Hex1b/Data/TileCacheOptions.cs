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
