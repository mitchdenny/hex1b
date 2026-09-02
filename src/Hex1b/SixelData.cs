using System.Security.Cryptography;
using Hex1b.Sixel;
using Hex1b.Surfaces;

namespace Hex1b;

/// <summary>
/// Immutable data containing Sixel graphics information.
/// </summary>
/// <remarks>
/// <para>
/// Sixel data is content-addressable. Identical payloads share the same
/// <see cref="SixelData"/> instance only when their captured background and
/// persistent palette inputs also produce the same raster. This deduplicates
/// equivalent images without conflating terminal graphics state.
/// </para>
/// <para>
/// The raw DCS sequence is stored so it can be re-emitted during rendering.
/// Declared pixel dimensions are parsed from the raster attributes in the payload.
/// </para>
/// </remarks>
public sealed class SixelData
{
    private readonly object _decodeLock = new();
    private SixelRasterResult? _raster;
    private SixelPixelBuffer? _decodedPixels;
    private bool _decodeAttempted;

    /// <summary>
    /// Gets the raw Sixel DCS sequence (ESC P ... ESC \).
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Gets the horizontal extent declared by DECGRA, or zero when none was declared.
    /// </summary>
    public int PixelWidth { get; }

    /// <summary>
    /// Gets the vertical extent declared by DECGRA, or zero when none was declared.
    /// </summary>
    public int PixelHeight { get; }

    /// <summary>
    /// Gets the width of the Sixel image in cells.
    /// </summary>
    public int WidthInCells { get; }

    /// <summary>
    /// Gets the height of the Sixel image in cells.
    /// </summary>
    public int HeightInCells { get; }

    /// <summary>
    /// Gets the content hash used for deduplication.
    /// </summary>
    internal byte[] ContentHash { get; }

    internal SixelParseResult ParseResult { get; }

    /// <summary>
    /// Gets the authoritative bounded rasterization of <see cref="ParseResult"/>.
    /// </summary>
    /// <remarks>
    /// When the terminal supplied a raster at creation time, that result is used
    /// verbatim so the captured background and persistent palette are honored.
    /// Otherwise the payload is rasterized on first use against the deterministic
    /// default environment.
    /// </remarks>
    internal SixelRasterResult Raster
    {
        get
        {
            lock (_decodeLock)
            {
                return _raster ??= SixelRasterizer.Rasterize(
                    ParseResult,
                    SixelRasterEnvironment.CreateDefault());
            }
        }
    }

    internal SixelData(
        string payload,
        int widthInCells,
        int heightInCells,
        byte[] contentHash)
        : this(
            payload,
            widthInCells,
            heightInCells,
            contentHash,
            0,
            0,
            SixelParser.ParsePayload(payload))
    {
    }

    internal SixelData(
        string payload,
        int widthInCells,
        int heightInCells,
        byte[] contentHash,
        int pixelWidth,
        int pixelHeight,
        SixelParseResult? parseResult = null,
        SixelRasterResult? raster = null)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = contentHash;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        ParseResult = parseResult ?? SixelParser.ParsePayload(payload);
        _raster = raster;
    }

    /// <summary>
    /// Gets the cell span for this sixel using the specified cell metrics.
    /// </summary>
    /// <param name="metrics">The cell metrics to use for conversion.</param>
    /// <returns>The width and height in cells.</returns>
    public (int Width, int Height) GetCellSpan(CellMetrics metrics)
    {
        if (ParseResult.LogicalCanvasExtent is { Width: > 0, Height: > 0 } logical)
        {
            return metrics.PixelToCellSpan(logical.Width, logical.Height);
        }
        if (PixelWidth > 0 && PixelHeight > 0)
        {
            return metrics.PixelToCellSpan(PixelWidth, PixelHeight);
        }
        // Fall back to stored cell dimensions
        return (WidthInCells, HeightInCells);
    }

    /// <summary>
    /// Materializes the sixel payload as a dense pixel buffer.
    /// The result is cached, and repeated calls produce equal content.
    /// </summary>
    /// <returns>
    /// The materialized pixel buffer, or <see langword="null"/> when the
    /// authoritative rasterizer produced a geometry-only result.
    /// </returns>
    public SixelPixelBuffer? GetPixels()
    {
        lock (_decodeLock)
        {
            if (_decodeAttempted)
            {
                return _decodedPixels;
            }

            _raster ??= SixelRasterizer.Rasterize(
                ParseResult,
                SixelRasterEnvironment.CreateDefault());
            _decodedPixels = _raster.Image?.Materialize();
            _decodeAttempted = true;
            return _decodedPixels;
        }
    }

    /// <summary>
    /// Computes a content hash for a Sixel payload.
    /// </summary>
    internal static byte[] ComputeHash(string payload) => ComputeHash(payload, null);

    /// <summary>
    /// Computes a deduplication hash that combines the payload with the raster
    /// state identity.
    /// </summary>
    /// <remarks>
    /// Identical payloads produce different pixels when the captured background
    /// or the persistent palette differ, so the raster identity must participate
    /// in content-addressable reuse.
    /// </remarks>
    internal static byte[] ComputeHash(string payload, SixelRasterResult? raster)
    {
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        if (raster is null)
        {
            return SHA256.HashData(payloadBytes);
        }

        var combined = new byte[payloadBytes.Length + raster.Identity.Length];
        payloadBytes.CopyTo(combined, 0);
        raster.Identity.CopyTo(combined, payloadBytes.Length);
        return SHA256.HashData(combined);
    }

    /// <summary>
    /// Checks if two content hashes are equal.
    /// </summary>
    internal static bool HashEquals(byte[] a, byte[] b)
    {
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }
}
