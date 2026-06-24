using Hex1b.Scene.Textures;
using SkiaSharp;

namespace AsciiEarth;

/// <summary>
/// Assembles the bounded detail tile block (see <see cref="EarthView"/>) into a single fixed-size
/// <see cref="SceneTexture2D"/>, off the render thread, for the high-zoom overlay patch.
/// </summary>
/// <remarks>
/// Unlike the base globe's <see cref="EarthTextureBuilder"/>, no Mercator→equirectangular
/// reprojection is needed: the tiles are laid out in a simple grid and the overlay patch's UVs run
/// linearly across them. The texture is always
/// <see cref="EarthView.TilesX"/>·256 wide by <see cref="EarthView.TilesY"/>·256 tall. Each completed
/// build publishes a <em>new texture instance</em> together with its matching window/version, so the
/// render loop can swap texture + geometry atomically in one frame (no one-frame mismatch/pop while
/// panning). A generation counter cancels stale builds.
/// </remarks>
internal sealed class DetailTextureBuilder : IDisposable
{
    private const int TilePixels = EarthView.TilePixels;
    private const int MaxParallelDownloads = 6;

    // Neutral fill shown for missing/failed tiles before/while the block downloads.
    private static readonly uint FillColor = Pack(20, 40, 70, 255);

    private readonly RasterTileClient _client;
    private readonly OpenMeteoOverlayClient _overlayClient;
    private readonly int _tilesX;
    private readonly int _tilesY;
    private readonly object _gate = new();

    private int _generation;
    private EarthView.Window? _requested;
    private EarthView.Window? _published;
    private SceneTexture2D _publishedTexture;
    private int _version;
    private OverlayMode _overlayMode = OverlayMode.None;

    /// <summary>True while a detail block is downloading/assembling.</summary>
    public bool IsBuilding { get; private set; }

    /// <summary>Current published texture (blank initially, then latest built snapshot).</summary>
    public SceneTexture2D Texture
    {
        get { lock (_gate) return _publishedTexture; }
    }

    /// <summary>
    /// Snapshot of the latest published detail block: texture + matching window + version.
    /// Null until the first successful build completes.
    /// </summary>
    public PublishedDetail? Published
    {
        get
        {
            lock (_gate)
            {
                return _published is { } window
                    ? new PublishedDetail(window, _publishedTexture, _version)
                    : null;
            }
        }
    }

    public readonly record struct PublishedDetail(EarthView.Window Window, SceneTexture2D Texture, int Version);

    public OverlayMode OverlayMode
    {
        get { lock (_gate) return _overlayMode; }
    }

    public DetailTextureBuilder(RasterTileClient client)
    {
        _client = client;
        _overlayClient = new OpenMeteoOverlayClient();
        _tilesX = EarthView.TilesX;
        _tilesY = EarthView.TilesY;

        _publishedTexture = new SceneTexture2D(_tilesX * TilePixels, _tilesY * TilePixels);
        var blank = new uint[_publishedTexture.Width * _publishedTexture.Height];
        Array.Fill(blank, FillColor);
        _publishedTexture.SetPixels(blank);
    }

    public void SetOverlayMode(OverlayMode mode)
    {
        EarthView.Window? requested;
        var generation = 0;

        lock (_gate)
        {
            if (_overlayMode == mode)
                return;
            _overlayMode = mode;
            requested = _requested;
            if (requested is null)
                return;
            generation = ++_generation;
            IsBuilding = true;
        }

        _ = Task.Run(() => BuildAsync(requested.Value, generation));
    }

    /// <summary>
    /// Requests the detail texture be (re)built for <paramref name="window"/>. No-op if that block is
    /// already the most recent request. Runs asynchronously; returns immediately.
    /// </summary>
    public void Request(EarthView.Window window)
    {
        int generation;
        lock (_gate)
        {
            if (_requested is { } r && r.Zoom == window.Zoom && r.MinTileX == window.MinTileX && r.MinTileY == window.MinTileY)
                return;
            _requested = window;
            generation = ++_generation;
            IsBuilding = true;
        }

        _ = Task.Run(() => BuildAsync(window, generation));
    }

    private async Task BuildAsync(EarthView.Window window, int generation)
    {
        try
        {
            var pixels = await AssembleAsync(window, generation);
            if (pixels is null)
                return; // stale or cancelled

            OverlayMode mode;
            lock (_gate)
            {
                if (generation != _generation)
                    return;
                mode = _overlayMode;
            }

            if (mode != OverlayMode.None)
            {
                var applied = await ApplyOverlayAsync(pixels, window, mode, generation);
                if (!applied)
                    return; // stale or cancelled
            }

            lock (_gate)
            {
                if (generation != _generation)
                    return;
                var texture = new SceneTexture2D(_tilesX * TilePixels, _tilesY * TilePixels);
                texture.SetPixels(pixels);
                _publishedTexture = texture;
                _published = window;
                _version++;
            }
        }
        catch
        {
            // Best-effort: a failed build leaves the previous detail texture in place.
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

    private async Task<uint[]?> AssembleAsync(EarthView.Window window, int generation)
    {
        var n = 1 << window.Zoom;
        var width = _tilesX * TilePixels;
        var height = _tilesY * TilePixels;
        var output = new uint[width * height];
        Array.Fill(output, FillColor);

        using var throttle = new SemaphoreSlim(MaxParallelDownloads);
        var tasks = new List<Task>(_tilesX * _tilesY);

        for (var by = 0; by < _tilesY; by++)
        {
            for (var bx = 0; bx < _tilesX; bx++)
            {
                var blockX = bx;
                var blockY = by;
                var worldX = (((window.MinTileX + bx) % n) + n) % n;
                var worldY = window.MinTileY + by;
                if (worldY < 0 || worldY >= n)
                    continue;

                tasks.Add(Task.Run(async () =>
                {
                    await throttle.WaitAsync();
                    try
                    {
                        if (generation != _generation)
                            return;
                        var bytes = await _client.GetTileAsync(window.Zoom, worldX, worldY);
                        var tile = Decode(bytes);
                        if (tile is not null)
                            Blit(output, width, tile, blockX * TilePixels, blockY * TilePixels);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        return generation != _generation ? null : output;
    }

    private async ValueTask<bool> ApplyOverlayAsync(
        uint[] pixels,
        EarthView.Window window,
        OverlayMode mode,
        int generation)
    {
        var sampleGridX = 20;
        var sampleGridY = 12;
        var samplePoints = new (double Lat, double Lon)[sampleGridX * sampleGridY];
        var pi = 0;
        for (var gy = 0; gy < sampleGridY; gy++)
        {
            var fy = sampleGridY <= 1 ? 0.0 : (double)gy / (sampleGridY - 1);
            var tileY = window.MinTileY + (fy * EarthView.TilesY);
            var lat = TileCoordinates.TileToLatLon(0, tileY, window.Zoom).Lat;

            for (var gx = 0; gx < sampleGridX; gx++)
            {
                var fx = sampleGridX <= 1 ? 0.0 : (double)gx / (sampleGridX - 1);
                var tileX = window.MinTileX + (fx * EarthView.TilesX);
                var lon = TileCoordinates.TileToLatLon(tileX, 0, window.Zoom).Lon;
                samplePoints[pi++] = (lat, lon);
            }
        }

        var samples = await _overlayClient.GetCurrentSamplesAsync(samplePoints);
        lock (_gate)
        {
            if (generation != _generation)
                return false;
        }

        var sampleValues = new double[samples.Length];
        for (var i = 0; i < samples.Length; i++)
            sampleValues[i] = mode == OverlayMode.Temperature ? samples[i].TempC : samples[i].WindKmh;

        var width = _tilesX * TilePixels;
        var height = _tilesY * TilePixels;
        var maxX = Math.Max(1, width - 1);
        var maxY = Math.Max(1, height - 1);
        var maxGridX = sampleGridX - 1;
        var maxGridY = sampleGridY - 1;

        for (var y = 0; y < height; y++)
        {
            var v = (double)y / maxY;
            var gy = v * maxGridY;
            var y0 = Math.Clamp((int)Math.Floor(gy), 0, maxGridY);
            var y1 = Math.Min(y0 + 1, maxGridY);
            var ty = gy - y0;

            for (var x = 0; x < width; x++)
            {
                var u = (double)x / maxX;
                var gx = u * maxGridX;
                var x0 = Math.Clamp((int)Math.Floor(gx), 0, maxGridX);
                var x1 = Math.Min(x0 + 1, maxGridX);
                var tx = gx - x0;

                var i00 = (y0 * sampleGridX) + x0;
                var i10 = (y0 * sampleGridX) + x1;
                var i01 = (y1 * sampleGridX) + x0;
                var i11 = (y1 * sampleGridX) + x1;
                var s0 = Lerp(sampleValues[i00], sampleValues[i10], tx);
                var s1 = Lerp(sampleValues[i01], sampleValues[i11], tx);
                var value = Lerp(s0, s1, ty);

                pixels[(y * width) + x] = ApplyOverlayTint(pixels[(y * width) + x], value, mode);
            }
        }

        return true;
    }

    private static void Blit(uint[] target, int targetWidth, uint[] tile, int destX, int destY)
    {
        for (var row = 0; row < TilePixels; row++)
        {
            var srcOffset = row * TilePixels;
            var dstOffset = (destY + row) * targetWidth + destX;
            Array.Copy(tile, srcOffset, target, dstOffset, TilePixels);
        }
    }

    private static uint[]? Decode(byte[]? pngBytes)
    {
        if (pngBytes is null)
            return null;

        using var bitmap = SKBitmap.Decode(pngBytes);
        if (bitmap is null)
            return null;

        var source = bitmap.Pixels;
        var pixels = new uint[TilePixels * TilePixels];
        var count = Math.Min(source.Length, pixels.Length);
        for (var i = 0; i < count; i++)
        {
            var c = source[i];
            pixels[i] = Pack(c.Red, c.Green, c.Blue, c.Alpha);
        }

        return pixels;
    }

    private static uint Pack(byte r, byte g, byte b, byte a)
        => ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static uint ApplyOverlayTint(uint source, double value, OverlayMode mode)
    {
        var r = (byte)(source >> 24);
        var g = (byte)(source >> 16);
        var b = (byte)(source >> 8);
        var a = (byte)source;

        var gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
        var normalized = mode switch
        {
            OverlayMode.Temperature => Math.Clamp((value - (-20.0)) / (45.0 - (-20.0)), 0.0, 1.0),
            OverlayMode.Wind => Math.Clamp(value / 120.0, 0.0, 1.0),
            _ => 0.0
        };

        var (cr, cg, cb) = HeatColor(normalized);
        var alpha = mode == OverlayMode.Wind
            ? (0.30 + (0.50 * normalized))
            : 0.58;

        var outR = (byte)Math.Clamp((int)Math.Round((gray * (1.0 - alpha)) + (cr * alpha)), 0, 255);
        var outG = (byte)Math.Clamp((int)Math.Round((gray * (1.0 - alpha)) + (cg * alpha)), 0, 255);
        var outB = (byte)Math.Clamp((int)Math.Round((gray * (1.0 - alpha)) + (cb * alpha)), 0, 255);
        return Pack(outR, outG, outB, a);
    }

    private static (byte R, byte G, byte B) HeatColor(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        var blue = (R: 24.0, G: 96.0, B: 255.0);
        var orange = (R: 255.0, G: 140.0, B: 0.0);
        var red = (R: 220.0, G: 20.0, B: 20.0);

        if (t <= 0.5)
        {
            var s = t * 2.0;
            return (
                (byte)Math.Round(Lerp(blue.R, orange.R, s)),
                (byte)Math.Round(Lerp(blue.G, orange.G, s)),
                (byte)Math.Round(Lerp(blue.B, orange.B, s)));
        }

        var u = (t - 0.5) * 2.0;
        return (
            (byte)Math.Round(Lerp(orange.R, red.R, u)),
            (byte)Math.Round(Lerp(orange.G, red.G, u)),
            (byte)Math.Round(Lerp(orange.B, red.B, u)));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _generation++; // invalidate any in-flight build
        }
        _overlayClient.Dispose();
    }
}
