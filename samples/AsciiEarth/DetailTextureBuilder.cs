using Hex1b.Scene.Textures;
using SkiaSharp;

namespace AsciiEarth;

/// <summary>
/// Assembles the bounded detail tile block (see <see cref="EarthView"/>) into a single fixed-size
/// <see cref="SceneTexture2D"/>, off the render thread, for the high-zoom patch.
/// </summary>
internal sealed class DetailTextureBuilder : IDisposable
{
    private const int TilePixels = EarthView.TilePixels;
    private const int MaxParallelDownloads = 6;
    private static readonly uint FillColor = Pack(20, 40, 70, 255);

    private readonly RasterTileClient _client;
    private readonly int _tilesX;
    private readonly int _tilesY;
    private readonly object _gate = new();

    private int _generation;
    private EarthView.Window? _requested;
    private EarthView.Window? _published;
    private SceneTexture2D _publishedTexture;
    private int _version;

    public bool IsBuilding { get; private set; }

    public SceneTexture2D Texture
    {
        get { lock (_gate) return _publishedTexture; }
    }

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

    public DetailTextureBuilder(RasterTileClient client)
    {
        _client = client;
        _tilesX = EarthView.TilesX;
        _tilesY = EarthView.TilesY;

        _publishedTexture = new SceneTexture2D(_tilesX * TilePixels, _tilesY * TilePixels);
        var blank = new uint[_publishedTexture.Width * _publishedTexture.Height];
        Array.Fill(blank, FillColor);
        _publishedTexture.SetPixels(blank);
    }

    /// <summary>
    /// Requests the detail texture be (re)built for <paramref name="window"/>. No-op if that block is
    /// already the most recent request.
    /// </summary>
    public void Request(EarthView.Window window)
    {
        int generation;
        lock (_gate)
        {
            if (_requested is { } r
                && r.Zoom == window.Zoom
                && r.MinTileX == window.MinTileX
                && r.MinTileY == window.MinTileY)
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
                return;

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
            // Best-effort: keep the previous texture on failure.
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
        var output = new uint[width * (_tilesY * TilePixels)];
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
                        if (generation != Volatile.Read(ref _generation))
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
        return generation != Volatile.Read(ref _generation) ? null : output;
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

    public void Dispose()
    {
        lock (_gate)
            _generation++;
    }
}
