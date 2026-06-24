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
    private static readonly TimeSpan WindFrameInterval = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan WindFieldRefreshInterval = TimeSpan.FromMinutes(12);
    private const int WindFieldGridX = 12;
    private const int WindFieldGridY = 6;
    private const int WindParticleCount = 520;

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
    private uint[]? _publishedBasePixels;
    private int _version;
    private OverlayMode _overlayMode = OverlayMode.None;
    private DateTime _lastWindFrameUtc = DateTime.MinValue;
    private WindFieldSnapshot? _windField;
    private DateTime _nextWindFieldRefreshUtc = DateTime.MinValue;
    private DateTime _windRetryAfterUtc = DateTime.MinValue;
    private int _windRetryStep;
    private readonly Random _rng = new(0x5EED);
    private readonly WindParticle[] _windParticles = new WindParticle[WindParticleCount];

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

    private sealed class WindFieldSnapshot
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required double[] SpeedKmh { get; init; }
        public required double[] DirectionDeg { get; init; }
    }

    private struct WindParticle
    {
        public double Lat;
        public double Lon;
        public double PrevLat;
        public double PrevLon;
        public double Life;
    }

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

        _ = Task.Run(() => BuildAsync(requested.Value, generation, preferPublishedBase: true));
    }

    /// <summary>
    /// Requests the detail texture be (re)built for <paramref name="window"/>. No-op if that block is
    /// already the most recent request. Runs asynchronously; returns immediately.
    /// </summary>
    public void Request(EarthView.Window window)
    {
        int generation;
        var preferPublishedBase = false;
        var nowUtc = DateTime.UtcNow;
        lock (_gate)
        {
            if (_requested is { } r && r.Zoom == window.Zoom && r.MinTileX == window.MinTileX && r.MinTileY == window.MinTileY)
            {
                var canAnimateWind = _overlayMode == OverlayMode.Wind
                    && !IsBuilding
                    && nowUtc - _lastWindFrameUtc >= WindFrameInterval;
                if (!canAnimateWind)
                    return;
                generation = ++_generation;
                IsBuilding = true;
                preferPublishedBase = true;
                _requested = window;
                _ = Task.Run(() => BuildAsync(window, generation, preferPublishedBase));
                return;
            }

            _requested = window;
            generation = ++_generation;
            IsBuilding = true;
        }

        _ = Task.Run(() => BuildAsync(window, generation, preferPublishedBase));
    }

    private async Task BuildAsync(EarthView.Window window, int generation, bool preferPublishedBase = false)
    {
        try
        {
            uint[]? basePixels = null;
            if (preferPublishedBase)
            {
                lock (_gate)
                {
                    var samePublished = _published is { } p
                        && p.Zoom == window.Zoom
                        && p.MinTileX == window.MinTileX
                        && p.MinTileY == window.MinTileY;
                    if (samePublished && _publishedBasePixels is { } existingBase)
                        basePixels = (uint[])existingBase.Clone();
                }
            }

            if (basePixels is null)
            {
                basePixels = await AssembleAsync(window, generation);
                if (basePixels is null)
                    return; // stale or cancelled
            }

            var pixels = (uint[])basePixels.Clone();

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
                _publishedBasePixels = basePixels;
                _published = window;
                _version++;
                if (mode == OverlayMode.Wind)
                    _lastWindFrameUtc = DateTime.UtcNow;
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
        return mode switch
        {
            OverlayMode.Temperature => await ApplyTemperatureOverlayAsync(pixels, window, generation),
            OverlayMode.Wind => await ApplyWindOverlayAsync(pixels, window, generation),
            _ => true
        };
    }

    private async ValueTask<bool> ApplyTemperatureOverlayAsync(
        uint[] pixels,
        EarthView.Window window,
        int generation)
    {
        // Keep request pressure low: Open-Meteo free endpoint rate-limits aggressive multi-point
        // sampling. A coarse grid is enough for a broad, readable terminal overlay.
        var sampleGridX = 8;
        var sampleGridY = 4;
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

        var sampleValues = new double[samplePoints.Length];
        try
        {
            var samples = await _overlayClient.GetCurrentSamplesAsync(samplePoints);
            lock (_gate)
            {
                if (generation != _generation)
                    return false;
            }

            for (var i = 0; i < samples.Length; i++)
                    sampleValues[i] = samples[i].TempC;
            }
            catch
            {
                // If the weather API is unavailable or throttled, fall back to a deterministic synthetic
                // field so overlay mode remains visibly distinct instead of appearing broken/no-op.
                for (var i = 0; i < samplePoints.Length; i++)
                {
                    var (lat, lon) = samplePoints[i];
                    sampleValues[i] = EstimateTemperature(lat, lon);
                }
            }

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

                pixels[(y * width) + x] = ApplyOverlayTint(pixels[(y * width) + x], value, OverlayMode.Temperature);
            }
        }

        return true;
    }

    private async ValueTask<bool> ApplyWindOverlayAsync(uint[] pixels, EarthView.Window window, int generation)
    {
        GrayScaleBase(pixels);

        var field = await EnsureWindFieldAsync(generation);
        lock (_gate)
        {
            if (generation != _generation)
                return false;
        }

        var nowUtc = DateTime.UtcNow;
        var dt = _lastWindFrameUtc == DateTime.MinValue
            ? WindFrameInterval.TotalSeconds
            : Math.Clamp((nowUtc - _lastWindFrameUtc).TotalSeconds, 0.04, 0.30);

        var width = _tilesX * TilePixels;
        var height = _tilesY * TilePixels;
        var maxX = Math.Max(1, width - 1);
        var maxY = Math.Max(1, height - 1);

        for (var i = 0; i < _windParticles.Length; i++)
        {
            ref var p = ref _windParticles[i];
            if (p.Life <= 0.0)
            {
                SpawnParticle(ref p, window);
            }

            p.PrevLat = p.Lat;
            p.PrevLon = p.Lon;

            var (speedKmh, dirDeg) = SampleWind(field, p.Lat, p.Lon);
            var dirRad = dirDeg * Math.PI / 180.0;
            var uEast = -speedKmh * Math.Sin(dirRad);
            var vNorth = -speedKmh * Math.Cos(dirRad);

            var latStep = (vNorth / 111.0) * (dt / 3600.0);
            var cosLat = Math.Max(0.18, Math.Cos(p.Lat * Math.PI / 180.0));
            var lonStep = (uEast / (111.0 * cosLat)) * (dt / 3600.0);

            p.Lat = Math.Clamp(p.Lat + latStep, -85.0, 85.0);
            p.Lon = NormalizeLon(p.Lon + lonStep);
            p.Life -= dt;

            if (!TryLatLonToUv(window, p.PrevLat, p.PrevLon, out var u0, out var v0) ||
                !TryLatLonToUv(window, p.Lat, p.Lon, out var u1, out var v1))
            {
                p.Life = 0;
                continue;
            }

            var x0 = (int)Math.Round(u0 * maxX);
            var y0 = (int)Math.Round(v0 * maxY);
            var x1 = (int)Math.Round(u1 * maxX);
            var y1 = (int)Math.Round(v1 * maxY);
            var color = HeatColor(Math.Clamp(speedKmh / 120.0, 0.0, 1.0));
            DrawLine(pixels, width, height, x0, y0, x1, y1, color, 0.86);
        }

        return true;
    }

    private async ValueTask<WindFieldSnapshot?> EnsureWindFieldAsync(int generation)
    {
        var nowUtc = DateTime.UtcNow;
        WindFieldSnapshot? existing;
        lock (_gate)
        {
            existing = _windField;
            if (existing is not null && nowUtc < _nextWindFieldRefreshUtc)
                return existing;
            if (nowUtc < _windRetryAfterUtc)
                return existing;
        }

        try
        {
            var points = new List<(double Lat, double Lon)>(WindFieldGridX * WindFieldGridY);
            for (var gy = 0; gy < WindFieldGridY; gy++)
            {
                var t = WindFieldGridY <= 1 ? 0.0 : (double)gy / (WindFieldGridY - 1);
                var lat = 75.0 - (t * 150.0);
                for (var gx = 0; gx < WindFieldGridX; gx++)
                {
                    var u = WindFieldGridX <= 1 ? 0.0 : (double)gx / (WindFieldGridX - 1);
                    var lon = -180.0 + (u * 360.0);
                    points.Add((lat, lon));
                }
            }

            var samples = await _overlayClient.GetCurrentSamplesAsync(points);
            lock (_gate)
            {
                if (generation != _generation)
                    return _windField;

                var speed = new double[samples.Length];
                var dir = new double[samples.Length];
                for (var i = 0; i < samples.Length; i++)
                {
                    speed[i] = samples[i].WindKmh;
                    dir[i] = samples[i].WindDirDeg;
                }

                _windField = new WindFieldSnapshot
                {
                    Width = WindFieldGridX,
                    Height = WindFieldGridY,
                    SpeedKmh = speed,
                    DirectionDeg = dir
                };
                _nextWindFieldRefreshUtc = nowUtc + WindFieldRefreshInterval;
                _windRetryAfterUtc = DateTime.MinValue;
                _windRetryStep = 0;
                return _windField;
            }
        }
        catch
        {
            lock (_gate)
            {
                if (generation == _generation)
                {
                    var backoffMinutes = _windRetryStep switch
                    {
                        0 => 2,
                        1 => 5,
                        2 => 15,
                        3 => 30,
                        _ => 60
                    };
                    _windRetryAfterUtc = nowUtc + TimeSpan.FromMinutes(backoffMinutes);
                    _windRetryStep = Math.Min(_windRetryStep + 1, 5);
                }

                return _windField;
            }
        }
    }

    private static (double SpeedKmh, double DirectionDeg) SampleWind(WindFieldSnapshot? field, double lat, double lon)
    {
        if (field is null || field.SpeedKmh.Length == 0 || field.DirectionDeg.Length == 0)
            return (EstimateWind(lat, lon), EstimateWindDirection(lat, lon));

        var xNorm = (NormalizeLon(lon) + 180.0) / 360.0;
        var yNorm = (75.0 - lat) / 150.0;
        var x = Math.Clamp(xNorm, 0.0, 1.0) * (field.Width - 1);
        var y = Math.Clamp(yNorm, 0.0, 1.0) * (field.Height - 1);
        var x0 = Math.Clamp((int)Math.Floor(x), 0, field.Width - 1);
        var x1 = Math.Min(x0 + 1, field.Width - 1);
        var y0 = Math.Clamp((int)Math.Floor(y), 0, field.Height - 1);
        var y1 = Math.Min(y0 + 1, field.Height - 1);
        var tx = x - x0;
        var ty = y - y0;

        var i00 = (y0 * field.Width) + x0;
        var i10 = (y0 * field.Width) + x1;
        var i01 = (y1 * field.Width) + x0;
        var i11 = (y1 * field.Width) + x1;

        var speed0 = Lerp(field.SpeedKmh[i00], field.SpeedKmh[i10], tx);
        var speed1 = Lerp(field.SpeedKmh[i01], field.SpeedKmh[i11], tx);
        var speed = Lerp(speed0, speed1, ty);

        // Average direction by vector components (never average angles directly).
        var (u00, v00) = WindDirToUv(field.SpeedKmh[i00], field.DirectionDeg[i00]);
        var (u10, v10) = WindDirToUv(field.SpeedKmh[i10], field.DirectionDeg[i10]);
        var (u01, v01) = WindDirToUv(field.SpeedKmh[i01], field.DirectionDeg[i01]);
        var (u11, v11) = WindDirToUv(field.SpeedKmh[i11], field.DirectionDeg[i11]);
        var u0 = Lerp(u00, u10, tx);
        var u1 = Lerp(u01, u11, tx);
        var v0 = Lerp(v00, v10, tx);
        var v1 = Lerp(v01, v11, tx);
        var u = Lerp(u0, u1, ty);
        var v = Lerp(v0, v1, ty);

        var dir = UvToMeteorologicalDir(u, v);
        return (Math.Max(0.0, speed), dir);
    }

    private void SpawnParticle(ref WindParticle p, EarthView.Window window)
    {
        var v = _rng.NextDouble();
        var u = _rng.NextDouble();
        var tileY = window.MinTileY + (v * EarthView.TilesY);
        var tileX = window.MinTileX + (u * EarthView.TilesX);
        p.Lat = TileCoordinates.TileToLatLon(0, tileY, window.Zoom).Lat;
        p.Lon = TileCoordinates.TileToLatLon(tileX, 0, window.Zoom).Lon;
        p.PrevLat = p.Lat;
        p.PrevLon = p.Lon;
        p.Life = 4.0 + (_rng.NextDouble() * 5.0);
    }

    private static bool TryLatLonToUv(EarthView.Window window, double lat, double lon, out double u, out double v)
    {
        var (tileX, tileY) = TileCoordinates.LatLonToTile(lat, lon, window.Zoom);
        var n = 1 << window.Zoom;
        var dx = WrapTileDelta(tileX - window.MinTileX, n);
        var dy = tileY - window.MinTileY;
        u = dx / EarthView.TilesX;
        v = dy / EarthView.TilesY;
        return u >= -0.02 && u <= 1.02 && v >= -0.02 && v <= 1.02;
    }

    private static double WrapTileDelta(double delta, int n)
    {
        if (n <= 0)
            return delta;
        while (delta < -n * 0.5) delta += n;
        while (delta > n * 0.5) delta -= n;
        return delta;
    }

    private static void GrayScaleBase(uint[] pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            var r = (byte)(p >> 24);
            var g = (byte)(p >> 16);
            var b = (byte)(p >> 8);
            var a = (byte)p;
            var gray = (byte)((r * 77 + g * 150 + b * 29) >> 8);
            pixels[i] = Pack(gray, gray, gray, a);
        }
    }

    private static void DrawLine(
        uint[] pixels,
        int width,
        int height,
        int x0,
        int y0,
        int x1,
        int y1,
        (byte R, byte G, byte B) color,
        double alpha)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;
        var x = x0;
        var y = y0;

        while (true)
        {
            BlendPixel(pixels, width, height, x, y, color, alpha);
            BlendPixel(pixels, width, height, x + 1, y, color, alpha * 0.60);
            BlendPixel(pixels, width, height, x, y + 1, color, alpha * 0.60);

            if (x == x1 && y == y1)
                break;

            var e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    private static void BlendPixel(
        uint[] pixels,
        int width,
        int height,
        int x,
        int y,
        (byte R, byte G, byte B) color,
        double alpha)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || alpha <= 0.001)
            return;

        var idx = (y * width) + x;
        var src = pixels[idx];
        var r = (byte)(src >> 24);
        var g = (byte)(src >> 16);
        var b = (byte)(src >> 8);
        var a = (byte)src;

        var outR = (byte)Math.Clamp((int)Math.Round((r * (1.0 - alpha)) + (color.R * alpha)), 0, 255);
        var outG = (byte)Math.Clamp((int)Math.Round((g * (1.0 - alpha)) + (color.G * alpha)), 0, 255);
        var outB = (byte)Math.Clamp((int)Math.Round((b * (1.0 - alpha)) + (color.B * alpha)), 0, 255);
        pixels[idx] = Pack(outR, outG, outB, a);
    }

    private static (double U, double V) WindDirToUv(double speedKmh, double dirDeg)
    {
        var rad = dirDeg * Math.PI / 180.0;
        var u = -speedKmh * Math.Sin(rad);
        var v = -speedKmh * Math.Cos(rad);
        return (u, v);
    }

    private static double UvToMeteorologicalDir(double uEast, double vNorth)
    {
        // Meteorological: direction wind is coming FROM.
        var dirRad = Math.Atan2(-uEast, -vNorth);
        var dirDeg = dirRad * 180.0 / Math.PI;
        return dirDeg < 0 ? dirDeg + 360.0 : dirDeg;
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

    private static double EstimateTemperature(double lat, double lon)
    {
        var latFactor = 32.0 - (Math.Abs(lat) * 0.60);
        var wave = 8.0 * Math.Sin((lon * Math.PI / 180.0) * 2.0);
        return latFactor + wave;
    }

    private static double EstimateWind(double lat, double lon)
    {
        var latRad = lat * Math.PI / 180.0;
        var lonRad = lon * Math.PI / 180.0;
        var belts = Math.Abs(Math.Sin(latRad * 2.0));
        var waves = 0.5 + (0.5 * Math.Abs(Math.Cos(lonRad * 2.5)));
        return 10.0 + (95.0 * belts * waves);
    }

    private static double EstimateWindDirection(double lat, double lon)
    {
        var latRad = lat * Math.PI / 180.0;
        var lonRad = lon * Math.PI / 180.0;
        var d = 180.0 + (85.0 * Math.Sin(lonRad * 1.8)) + (35.0 * Math.Cos(latRad * 1.5));
        d %= 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    private static double NormalizeLon(double lon)
    {
        while (lon > 180.0) lon -= 360.0;
        while (lon < -180.0) lon += 360.0;
        return lon;
    }

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
            ? (0.42 + (0.46 * normalized))
            : 0.70;

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
