using Hex1b.Surfaces;

namespace Hex1b.Sixel;

/// <summary>
/// Bounded sparse raster storage for a decoded Sixel graphic.
/// </summary>
/// <remarks>
/// <para>
/// Painted pixels are stored in lazily allocated square tiles so a sequence that
/// declares a very large transparent or background-filled canvas never allocates
/// in proportion to its declared extent. Unpainted pixels resolve to
/// <see cref="UnpaintedPixel"/>, which is the captured background for opaque
/// graphics and transparent for <c>P2=1</c>.
/// </para>
/// <para>
/// Painted Sixel pixels are always fully opaque, so a zero alpha inside a tile
/// unambiguously means "not painted".
/// </para>
/// </remarks>
internal sealed class SixelRasterImage
{
    private readonly Dictionary<long, Rgba32[]> _tiles = [];
    private readonly int _tileSize;
    private readonly int _tileColumns;
    private readonly int _maximumTiles;

    public SixelRasterImage(int width, int height, Rgba32 unpaintedPixel, SixelCompatibilityPolicy policy)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        UnpaintedPixel = unpaintedPixel;
        _tileSize = policy.RasterTileSize;
        _tileColumns = ((width - 1) / _tileSize) + 1;
        _maximumTiles = policy.MaximumRasterTiles;
    }

    /// <summary>
    /// Gets the logical width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the logical height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the color used for pixels that were never painted.
    /// </summary>
    public Rgba32 UnpaintedPixel { get; }

    /// <summary>
    /// Gets the number of tiles currently allocated.
    /// </summary>
    public int AllocatedTileCount => _tiles.Count;

    /// <summary>
    /// Gets the number of pixels that would be materialized densely.
    /// </summary>
    public long PixelCount => (long)Width * Height;

    /// <summary>
    /// Gets the resolved color at a logical coordinate.
    /// </summary>
    public Rgba32 this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "The coordinate is outside the raster.");
            }

            if (!_tiles.TryGetValue(TileKey(x, y), out var tile))
            {
                return UnpaintedPixel;
            }

            var pixel = tile[((y % _tileSize) * _tileSize) + (x % _tileSize)];
            return pixel.A == 0 ? UnpaintedPixel : pixel;
        }
    }

    /// <summary>
    /// Paints a pixel, allocating its tile on demand.
    /// </summary>
    /// <returns><see langword="false"/> when the tile budget refuses the write.</returns>
    public bool TryPaint(int x, int y, Rgba32 color)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return true;
        }

        var key = TileKey(x, y);
        if (!_tiles.TryGetValue(key, out var tile))
        {
            if (_tiles.Count >= _maximumTiles)
            {
                return false;
            }

            tile = new Rgba32[_tileSize * _tileSize];
            _tiles[key] = tile;
        }

        tile[((y % _tileSize) * _tileSize) + (x % _tileSize)] = color;
        return true;
    }

    /// <summary>
    /// Materializes a dense RGBA buffer. Repeated calls produce equal content.
    /// </summary>
    public SixelPixelBuffer Materialize()
    {
        var pixels = new Rgba32[checked((int)PixelCount)];
        if (UnpaintedPixel.A != 0)
        {
            Array.Fill(pixels, UnpaintedPixel);
        }

        foreach (var (key, tile) in _tiles)
        {
            var tileX = (int)(key % _tileColumns) * _tileSize;
            var tileY = (int)(key / _tileColumns) * _tileSize;
            var rows = Math.Min(_tileSize, Height - tileY);
            var columns = Math.Min(_tileSize, Width - tileX);
            for (var row = 0; row < rows; row++)
            {
                var sourceOffset = row * _tileSize;
                var destinationOffset = ((tileY + row) * Width) + tileX;
                for (var column = 0; column < columns; column++)
                {
                    var pixel = tile[sourceOffset + column];
                    if (pixel.A != 0)
                    {
                        pixels[destinationOffset + column] = pixel;
                    }
                }
            }
        }

        return new SixelPixelBuffer(Width, Height, pixels);
    }

    private long TileKey(int x, int y) => ((long)(y / _tileSize) * _tileColumns) + (x / _tileSize);
}
