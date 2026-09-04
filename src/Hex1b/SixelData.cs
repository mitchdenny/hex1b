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
    private readonly SixelRasterPreparation? _rasterPreparation;
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
    /// <remarks>
    /// Identical payloads that also capture the same background and palette
    /// state produce the same hash, which content-addressed replay and
    /// serialization can use to reference this image without repeating its
    /// pixel payload.
    /// </remarks>
    public byte[] ContentHash { get; }

    internal SixelParseResult ParseResult { get; }

    /// <summary>
    /// Gets the authoritative parser outcome for this image's payload.
    /// </summary>
    public SixelParseOutcome Outcome => ParseResult.Outcome;

    /// <summary>
    /// Gets the explicit parser diagnostics explaining any downgraded or
    /// annotated outcome. Empty when <see cref="Outcome"/> is
    /// <see cref="SixelParseOutcome.Complete"/> with nothing to report.
    /// </summary>
    public IReadOnlyList<SixelDiagnostic> Diagnostics => ParseResult.Diagnostics;

    /// <summary>
    /// Gets whether unpainted pixels resolve to the captured background color
    /// or remain transparent.
    /// </summary>
    public SixelBackgroundMode BackgroundMode => ParseResult.Header.BackgroundMode;

    /// <summary>
    /// Gets the authoritative bounded rasterization of <see cref="ParseResult"/>.
    /// </summary>
    /// <remarks>
    /// Terminal-created data captures an immutable background and palette
    /// preparation so rasterization can occur on first use without holding the
    /// terminal buffer lock. Data created without terminal state uses the
    /// deterministic default environment.
    /// </remarks>
    internal SixelRasterResult Raster
    {
        get
        {
            lock (_decodeLock)
            {
                return _raster ??= SixelRasterizer.Rasterize(
                    ParseResult,
                    GetRasterEnvironment());
            }
        }
    }

    /// <summary>
    /// Gets whether the authoritative rasterizer produced pixels for this
    /// image, or explicitly refused allocation (a geometry-only outcome).
    /// </summary>
    /// <remarks>
    /// A geometry-only image still carries its declared/logical extents and
    /// <see cref="RasterDiagnostics"/> explaining the downgrade; it is never
    /// silently indistinguishable from a fully rasterized one.
    /// </remarks>
    public SixelRasterStatus RasterStatus => Raster.Status;

    /// <summary>
    /// Gets the explicit rasterizer diagnostics explaining a geometry-only
    /// outcome or other annotated raster result. Empty when
    /// <see cref="RasterStatus"/> is <see cref="SixelRasterStatus.Rasterized"/>
    /// with nothing to report.
    /// </summary>
    public IReadOnlyList<SixelRasterDiagnostic> RasterDiagnostics => Raster.Diagnostics;

    /// <summary>
    /// Gets this image's logical/rendered/declared/data/painted extents and
    /// effective pixel aspect ratio.
    /// </summary>
    public SixelRasterExtents Extents => Raster.Extents;

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
        SixelRasterResult? raster = null,
        SixelRasterPreparation? rasterPreparation = null,
        SixelCellMetrics? cellMetrics = null)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = contentHash;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        ParseResult = parseResult ?? SixelParser.ParsePayload(payload);
        _raster = raster;
        _rasterPreparation = rasterPreparation;
        CellMetrics = cellMetrics ?? SixelCellMetrics.Unknown;
    }

    /// <summary>
    /// Gets the protocol cell metrics captured when this image was created.
    /// </summary>
    /// <remarks>
    /// Metrics are captured once so a later metric change cannot retroactively
    /// alter the occupancy already recorded for a placement of this image.
    /// </remarks>
    public SixelCellMetrics CellMetrics { get; }

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
                GetRasterEnvironment());
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
    internal static byte[] ComputeHash(string payload, byte[]? rasterIdentity)
    {
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        if (rasterIdentity is null)
        {
            return SHA256.HashData(payloadBytes);
        }

        var combined = new byte[payloadBytes.Length + rasterIdentity.Length];
        payloadBytes.CopyTo(combined, 0);
        rasterIdentity.CopyTo(combined, payloadBytes.Length);
        return SHA256.HashData(combined);
    }

    /// <summary>
    /// Checks if two content hashes are equal.
    /// </summary>
    internal static bool HashEquals(byte[] a, byte[] b)
    {
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }

    private SixelRasterEnvironment GetRasterEnvironment() =>
        _rasterPreparation?.Environment ?? SixelRasterEnvironment.CreateDefault();
}
