using System.Collections.Concurrent;
using Hex1b.Scene.Textures;
using SkiaSharp;

namespace AsciiEarth;

/// <summary>
/// Owns a single equirectangular <see cref="SceneTexture2D"/> for the globe and rebuilds it,
/// off the render thread, from OpenStreetMap raster tiles at a chosen zoom level.
/// </summary>
/// <remarks>
/// The texture is latitude-linear (equirectangular), matching the UV sphere produced by
/// <see cref="SphereGeometry"/>. Each rebuild downloads every tile covering the globe at the
/// requested zoom (capped low — 2^Z × 2^Z tiles), reprojects them into the texture, then swaps
/// the pixels in place. A generation counter cancels stale rebuilds, and the previous texture
/// stays visible while a new one downloads.
/// </remarks>
internal sealed class EarthTextureBuilder : IDisposable
{
    private const int TilePixels = 256;
    private const int MaxParallelDownloads = 6;

    // A calm ocean blue used before the first tiles arrive and for missing/failed tiles.
    private static readonly uint OceanColor = Pack(20, 40, 70, 255);

    private readonly RasterTileClient _client;
    private readonly int _width;
    private readonly int _height;
    private readonly int _maxZoom;

    private readonly object _gate = new();
    private int _generation;
    private int _requestedZoom = -1;

    public SceneTexture2D Texture { get; }

    /// <summary>The zoom level of the texture currently displayed, or -1 before the first build.</summary>
    public int CurrentZoom { get; private set; } = -1;

    /// <summary>True while a rebuild is downloading/assembling tiles.</summary>
    public bool IsBuilding { get; private set; }

    public int MaxZoom => _maxZoom;

    public EarthTextureBuilder(RasterTileClient client, int width = 1024, int height = 512, int maxZoom = 4)
    {
        _client = client;
        _width = width;
        _height = height;
        _maxZoom = maxZoom;

        Texture = new SceneTexture2D(width, height);
        var blank = new uint[width * height];
        Array.Fill(blank, OceanColor);
        Texture.SetPixels(blank);
    }

    /// <summary>
    /// Requests the texture be rebuilt at <paramref name="zoom"/> (clamped to [0, MaxZoom]).
    /// No-op if that zoom is already requested. Runs asynchronously; returns immediately.
    /// </summary>
    public void RequestZoom(int zoom)
    {
        zoom = Math.Clamp(zoom, 0, _maxZoom);

        int generation;
        lock (_gate)
        {
            if (zoom == _requestedZoom)
                return;
            _requestedZoom = zoom;
            generation = ++_generation;
            IsBuilding = true;
        }

        _ = Task.Run(() => BuildAsync(zoom, generation));
    }

    private async Task BuildAsync(int zoom, int generation)
    {
        try
        {
            var pixels = await AssembleAsync(zoom, generation);
            if (pixels is null)
                return; // stale or cancelled

            lock (_gate)
            {
                if (generation != _generation)
                    return; // a newer request superseded this one
                Texture.SetPixels(pixels);
                CurrentZoom = zoom;
            }
        }
        catch
        {
            // Best-effort: a failed rebuild simply leaves the previous texture in place.
        }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation)
                    IsBuilding = false;
            }
        }
    }

    private async Task<uint[]?> AssembleAsync(int zoom, int generation)
    {
        var n = 1 << zoom;

        // Download + decode every tile covering the globe at this zoom (bounded concurrency).
        var decoded = new ConcurrentDictionary<(int X, int Y), DecodedTile>();
        using var throttle = new SemaphoreSlim(MaxParallelDownloads);
        var tasks = new List<Task>(n * n);

        for (var ty = 0; ty < n; ty++)
        {
            for (var tx = 0; tx < n; tx++)
            {
                var x = tx;
                var y = ty;
                tasks.Add(Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        if (generation != _generation)
                            return;
                        var bytes = await _client.GetTileAsync(zoom, x, y);
                        decoded[(x, y)] = Decode(bytes);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        if (generation != _generation)
            return null;

        // Reproject the decoded Mercator tiles into the equirectangular texture.
        var output = new uint[_width * _height];

        for (var row = 0; row < _height; row++)
        {
            // Row 0 = north pole. Latitude is linear across the texture (equirectangular).
            var lat = 90.0 - (double)row / (_height - 1) * 180.0;
            lat = Math.Clamp(lat, -TileCoordinates.MaxMercatorLatitude, TileCoordinates.MaxMercatorLatitude);
            var (_, tileYf) = TileCoordinates.LatLonToTile(lat, 0.0, zoom);
            var osmY = Math.Clamp((int)Math.Floor(tileYf), 0, n - 1);
            var pyFrac = tileYf - Math.Floor(tileYf);

            var rowBase = row * _width;
            for (var col = 0; col < _width; col++)
            {
                var lon = -180.0 + (double)col / (_width - 1) * 360.0;
                var (tileXf, _) = TileCoordinates.LatLonToTile(0.0, lon, zoom);
                var osmX = ((int)Math.Floor(tileXf) % n + n) % n;
                var pxFrac = tileXf - Math.Floor(tileXf);

                output[rowBase + col] = decoded.TryGetValue((osmX, osmY), out var tile)
                    ? tile.Sample(pxFrac, pyFrac)
                    : OceanColor;
            }
        }

        return output;
    }

    private static DecodedTile Decode(byte[]? pngBytes)
    {
        if (pngBytes is null)
            return DecodedTile.Empty;

        using var bitmap = SKBitmap.Decode(pngBytes);
        if (bitmap is null)
            return DecodedTile.Empty;

        var w = bitmap.Width;
        var h = bitmap.Height;
        var source = bitmap.Pixels;
        var pixels = new uint[w * h];
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = source[i];
            pixels[i] = Pack(c.Red, c.Green, c.Blue, c.Alpha);
        }

        return new DecodedTile(pixels, w, h);
    }

    private static uint Pack(byte r, byte g, byte b, byte a)
        => ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;

    public void Dispose()
    {
        lock (_gate)
        {
            // Invalidate any in-flight build so it won't touch the texture after disposal.
            _generation++;
        }
    }

    /// <summary>A decoded tile's RGBA32 pixels with nearest-neighbour sampling.</summary>
    private sealed class DecodedTile
    {
        public static readonly DecodedTile Empty = new(Array.Empty<uint>(), 0, 0);

        private readonly uint[] _pixels;
        private readonly int _w;
        private readonly int _h;

        public DecodedTile(uint[] pixels, int w, int h)
        {
            _pixels = pixels;
            _w = w;
            _h = h;
        }

        public uint Sample(double u, double v)
        {
            if (_w == 0 || _h == 0)
                return OceanColor;

            var px = Math.Clamp((int)(u * _w), 0, _w - 1);
            var py = Math.Clamp((int)(v * _h), 0, _h - 1);
            return _pixels[py * _w + px];
        }
    }
}
