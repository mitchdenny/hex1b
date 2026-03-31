using System.Collections.Concurrent;
using Hex1b.Data;
using Hex1b.Layout;
using Hex1b.Theming;

namespace FormDemo;

/// <summary>
/// A decoded OSM tile: character content, foreground and background colors.
/// Each OSM tile (256×256 pixels) decodes to a 256×128 character grid.
/// </summary>
internal sealed class DecodedTile
{
    public string[,] Chars { get; }
    public Hex1bColor[,] Fg { get; }
    public Hex1bColor[,] Bg { get; }

    public DecodedTile(string[,] chars, Hex1bColor[,] fg, Hex1bColor[,] bg)
    {
        Chars = chars;
        Fg = fg;
        Bg = bg;
    }
}

/// <summary>
/// An <see cref="ITileDataSource"/> that fetches raster PNG tiles from OpenStreetMap
/// and converts them to terminal characters using half-block rendering.
/// </summary>
/// <remarks>
/// Uses TileSize=(1,1) so each TilePanel tile = one character cell.
/// Character coordinates map to OSM tiles:
///   osmTileX = charX / 256, osmTileY = charY / 128
///   pixelX = charX % 256, pixelY = (charY % 128) * 2
/// </remarks>
internal sealed class OsmTileDataSource : ITileDataSource
{
    private const int CharsPerTileX = 256;
    private const int CharsPerTileY = 128;

    public Size TileSize => new(1, 1);

    private readonly RasterTileClient _client;
    private readonly MapCamera _camera;
    private readonly ConcurrentDictionary<string, DecodedTile> _decodedCache = new();

    public OsmTileDataSource(RasterTileClient client, MapCamera camera)
    {
        _client = client;
        _camera = camera;
    }

    public ValueTask<TileData[,]> GetTilesAsync(
        int tileX, int tileY, int tilesWide, int tilesTall,
        CancellationToken cancellationToken = default)
    {
        var zoom = _camera.ZoomLevel;
        var maxOsmTile = 1 << zoom;
        var result = new TileData[tilesWide, tilesTall];

        // Determine which OSM tiles cover the requested character range
        var minOsmX = FloorDiv(tileX, CharsPerTileX);
        var maxOsmX = FloorDiv(tileX + tilesWide - 1, CharsPerTileX);
        var minOsmY = FloorDiv(tileY, CharsPerTileY);
        var maxOsmY = FloorDiv(tileY + tilesTall - 1, CharsPerTileY);

        // Pre-fetch all needed OSM tiles
        for (var oy = minOsmY; oy <= maxOsmY; oy++)
        {
            for (var ox = minOsmX; ox <= maxOsmX; ox++)
            {
                EnsureDecoded(WrapCoord(ox, maxOsmTile), oy, zoom, maxOsmTile);
            }
        }

        // Sample each character cell from the decoded tiles
        for (var cy = 0; cy < tilesTall; cy++)
        {
            for (var cx = 0; cx < tilesWide; cx++)
            {
                var charX = tileX + cx;
                var charY = tileY + cy;

                var osmX = WrapCoord(FloorDiv(charX, CharsPerTileX), maxOsmTile);
                var osmY = FloorDiv(charY, CharsPerTileY);

                var pixelX = FloorMod(charX, CharsPerTileX);
                var pixelY = FloorMod(charY, CharsPerTileY);

                var key = $"{zoom}/{osmX}/{osmY}";
                if (_decodedCache.TryGetValue(key, out var decoded)
                    && pixelX < decoded.Chars.GetLength(0)
                    && pixelY < decoded.Chars.GetLength(1))
                {
                    result[cx, cy] = new TileData(
                        decoded.Chars[pixelX, pixelY],
                        decoded.Fg[pixelX, pixelY],
                        decoded.Bg[pixelX, pixelY]);
                }
                else
                {
                    // Out of range or not loaded — dark placeholder
                    result[cx, cy] = new TileData(" ", Hex1bColor.Default, Hex1bColor.FromRgb(20, 20, 30));
                }
            }
        }

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Clears the decoded tile cache. Call when zoom changes so stale tiles are discarded.
    /// </summary>
    public void ClearDecodedCache() => _decodedCache.Clear();

    private void EnsureDecoded(int osmX, int osmY, int zoom, int maxOsmTile)
    {
        var key = $"{zoom}/{osmX}/{osmY}";
        if (_decodedCache.ContainsKey(key))
            return;

        // Out-of-range Y → placeholder
        if (osmY < 0 || osmY >= maxOsmTile)
        {
            var (c, f, b) = HalfBlockRenderer.CreatePlaceholder(CharsPerTileX, CharsPerTileY, "");
            _decodedCache[key] = new DecodedTile(c, f, b);
            return;
        }

        var pngBytes = _client.GetTileAsync(zoom, osmX, osmY).AsTask().GetAwaiter().GetResult();
        if (pngBytes is null)
        {
            var (c, f, b) = HalfBlockRenderer.CreatePlaceholder(CharsPerTileX, CharsPerTileY, "loading...");
            _decodedCache[key] = new DecodedTile(c, f, b);
            return;
        }

        var (chars, fg, bg) = HalfBlockRenderer.Render(pngBytes);
        _decodedCache[key] = new DecodedTile(chars, fg, bg);
    }

    private static int WrapCoord(int coord, int max) =>
        ((coord % max) + max) % max;

    /// <summary>Floor division that rounds toward negative infinity.</summary>
    private static int FloorDiv(int a, int b) =>
        a >= 0 ? a / b : (a - b + 1) / b;

    /// <summary>Floor modulo that always returns a non-negative result.</summary>
    private static int FloorMod(int a, int b)
    {
        var r = a % b;
        return r < 0 ? r + b : r;
    }
}
