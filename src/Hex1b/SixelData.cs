using System.Buffers.Binary;
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
/// <see cref="SixelData"/> instance only when their captured background,
/// persistent palette, protocol cell metrics, and cell span are also equal.
/// This deduplicates equivalent image resources without conflating placement
/// contexts that would crop or damage the raster differently.
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
    private ISixelRetainedResourceOwner? _retainedResourceOwner;
    private SixelRasterResult? _raster;
    private SixelPixelBuffer? _decodedPixels;
    private bool _decodeAttempted;

    /// <summary>
    /// Gets the complete framed DCS sequence used for rendering.
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
    /// Gets the immutable image-resource identity used for deduplication.
    /// </summary>
    /// <remarks>
    /// Identical payloads that also capture the same background, palette,
    /// protocol cell metrics, and cell span produce the same hash. Replay and
    /// serialization use this identity to share only resources whose placement
    /// geometry has identical pixel-to-cell behavior.
    /// </remarks>
    public byte[] ContentHash { get; }

    internal SixelParseResult ParseResult { get; }
    internal SixelRasterPreparation? RasterPreparation => _rasterPreparation;
    internal bool PayloadComplete { get; }

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
    /// preparation. First use reserves retained bytes atomically with the owning
    /// screen before caching the result. Data created without terminal state uses
    /// the deterministic default environment.
    /// </remarks>
    internal SixelRasterResult Raster
    {
        get
        {
            var owner = Volatile.Read(ref _retainedResourceOwner);
            if (owner is not null)
                return owner.GetRaster(this);

            lock (_decodeLock)
            {
                return GetRasterUnowned();
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
            SixelParser.ParsePayload(payload),
            payloadComplete: true)
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
        SixelCellMetrics? cellMetrics = null,
        bool payloadComplete = true)
    {
        Payload = payload;
        WidthInCells = widthInCells;
        HeightInCells = heightInCells;
        ContentHash = contentHash;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        ParseResult = parseResult ?? SixelParser.ParsePayload(payload);
        PayloadComplete = payloadComplete;
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

    internal static SixelData FromExactPixels(
        SixelPixelBuffer pixels, int widthInCells, int heightInCells, SixelCellMetrics metrics)
    {
        var encoded = SixelExactEncoder.EncodeBounded(
            pixels, 64 * 1024 * 1024, CancellationToken.None, reuseColorRegisters: true);
        if (encoded.Outcome != SixelExactEncoder.EncodingOutcome.Complete || encoded.Payload is null)
            throw new InvalidOperationException("The embedded Sixel presentation exceeds the encoded payload limit.");
        var payload = encoded.Payload;
        return new SixelData(
            payload, widthInCells, heightInCells,
            ComputeHash(payload, null, widthInCells, heightInCells, metrics),
            pixels.Width, pixels.Height, cellMetrics: metrics);
    }

    /// <summary>
    /// Gets the cell span for this Sixel image using the protocol cell metrics
    /// captured when the image was created.
    /// </summary>
    /// <returns>The width and height in cells.</returns>
    public (int Width, int Height) GetCellSpan()
    {
        if (ParseResult.LogicalCanvasExtent is { Width: > 0, Height: > 0 } logical)
        {
            return (CellMetrics.ColumnsFor(logical.Width), CellMetrics.RowsFor(logical.Height));
        }
        if (PixelWidth > 0 && PixelHeight > 0)
        {
            return (CellMetrics.ColumnsFor(PixelWidth), CellMetrics.RowsFor(PixelHeight));
        }
        // Fall back to stored cell dimensions
        return (WidthInCells, HeightInCells);
    }

    /// <summary>
    /// Gets the cell span for this Sixel image using the protocol cell metrics
    /// captured when the image was created.
    /// </summary>
    /// <param name="metrics">
    /// Ignored. Sixel placement metrics are captured when the image is created.
    /// </param>
    /// <returns>The width and height in cells.</returns>
    [Obsolete("Cell metrics are captured by SixelData. Use GetCellSpan().")]
    public (int Width, int Height) GetCellSpan(CellMetrics metrics) => GetCellSpan();

    /// <summary>
    /// Materializes the sixel payload as a dense pixel buffer.
    /// The result is cached when the owning live screen's retained-byte budget
    /// admits it. If that cache cannot fit, the returned buffer is not retained
    /// by the live terminal and a later call may materialize it again.
    /// </summary>
    /// <returns>
    /// The materialized pixel buffer, or <see langword="null"/> when the
    /// authoritative rasterizer produced a geometry-only result.
    /// </returns>
    public SixelPixelBuffer? GetPixels()
    {
        var owner = Volatile.Read(ref _retainedResourceOwner);
        if (owner is not null)
            return owner.GetPixels(this);

        lock (_decodeLock)
        {
            return GetPixelsUnowned();
        }
    }

    internal bool HasMaterializedPixels
    {
        get
        {
            lock (_decodeLock)
            {
                return _decodedPixels is not null;
            }
        }
    }

    internal long RetainedByteCount
    {
        get
        {
            lock (_decodeLock)
            {
                var total = SixelRetainedSize.GetInitialBytes(this);
                if (_raster is not null)
                    total = SaturatingAdd(total, SixelRetainedSize.GetRasterBytes(_raster));
                if (_decodedPixels is not null)
                    total = SaturatingAdd(total, SixelRetainedSize.GetDensePixelBytes(_decodedPixels));
                return total;
            }
        }
    }

    internal void AttachRetainedResourceOwner(ISixelRetainedResourceOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (Interlocked.CompareExchange(ref _retainedResourceOwner, owner, null) is not null)
            throw new InvalidOperationException("The Sixel image is already owned by a screen.");
    }

    internal void DetachRetainedResourceOwner(ISixelRetainedResourceOwner owner) =>
        Interlocked.CompareExchange(ref _retainedResourceOwner, null, owner);

    internal bool TryGetCachedRaster(out SixelRasterResult raster)
    {
        lock (_decodeLock)
        {
            raster = _raster!;
            return raster is not null;
        }
    }

    internal SixelRasterResult ComputeRasterCandidate()
    {
        lock (_decodeLock)
        {
            return _raster ??
                SixelRasterizer.Rasterize(ParseResult, GetRasterEnvironment());
        }
    }

    internal SixelRasterResult PublishRasterOwned(
        SixelRasterResult candidate,
        Func<long, bool> tryReserve)
    {
        lock (_decodeLock)
        {
            if (_raster is not null)
                return _raster;

            if (tryReserve(SixelRetainedSize.GetRasterBytes(candidate)))
                _raster = candidate;
            return candidate;
        }
    }

    internal SixelRasterResult PublishRasterUnowned(SixelRasterResult candidate)
    {
        lock (_decodeLock)
            return _raster ??= candidate;
    }

    internal bool TryGetCachedPixels(
        out SixelPixelBuffer? pixels,
        out bool attempted)
    {
        lock (_decodeLock)
        {
            pixels = _decodedPixels;
            attempted = _decodeAttempted;
            return pixels is not null;
        }
    }

    internal SixelPixelBuffer? ComputePixelCandidate(SixelRasterResult raster)
    {
        lock (_decodeLock)
        {
            if (_decodeAttempted)
                return _decodedPixels;
            return raster.Image?.Materialize();
        }
    }

    internal SixelPixelBuffer? PublishPixelsOwned(
        SixelRasterResult raster,
        SixelPixelBuffer? candidate,
        Func<long, bool> tryReserve)
    {
        lock (_decodeLock)
        {
            if (_decodeAttempted)
                return _decodedPixels;
            if (!ReferenceEquals(_raster, raster))
                return candidate;
            if (candidate is null)
            {
                _decodeAttempted = true;
                return null;
            }

            if (tryReserve(SixelRetainedSize.GetDensePixelBytes(candidate)))
            {
                _decodedPixels = candidate;
                _decodeAttempted = true;
            }

            return candidate;
        }
    }

    internal SixelPixelBuffer? PublishPixelsUnowned(
        SixelRasterResult raster,
        SixelPixelBuffer? candidate)
    {
        lock (_decodeLock)
        {
            _raster ??= raster;
            if (_decodeAttempted)
                return _decodedPixels;
            _decodedPixels = candidate;
            _decodeAttempted = true;
            return candidate;
        }
    }

    internal bool TryGetRasterDimensions(out int width, out int height)
    {
        var image = Raster.Image;
        if (image is null)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = image.Width;
        height = image.Height;
        return width > 0 && height > 0;
    }

    internal SixelExtent GetRenderedPixelExtent()
    {
        if (ParseResult.LogicalCanvasExtent is { Width: > 0, Height: > 0 } logical)
        {
            return logical;
        }

        if (TryGetRasterDimensions(out var width, out var height))
        {
            return new SixelExtent(width, height);
        }

        return new SixelExtent(
            (int)Math.Ceiling(WidthInCells * CellMetrics.SafeWidth),
            (int)Math.Ceiling(HeightInCells * CellMetrics.SafeHeight));
    }

    /// <summary>
    /// Computes a deduplication identity that combines the payload, raster
    /// state, cell span, and captured protocol cell metrics.
    /// </summary>
    /// <remarks>
    /// Identical payloads produce different pixels when the captured background
    /// or persistent palette differ. Even when the pixels are identical,
    /// different protocol metrics or cell spans change clipping and damage
    /// behavior, so the complete immutable placement context participates in
    /// resource reuse.
    /// </remarks>
    internal static byte[] ComputeHash(
        string payload,
        byte[]? rasterIdentity,
        int widthInCells,
        int heightInCells,
        SixelCellMetrics cellMetrics)
    {
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var payloadHash = SHA256.HashData(payloadBytes);
        return ComputeHash(
            payloadHash,
            rasterIdentity,
            widthInCells,
            heightInCells,
            cellMetrics);
    }

    internal static byte[] ComputeHash(
        ReadOnlySpan<byte> payloadHash,
        byte[]? rasterIdentity,
        int widthInCells,
        int heightInCells,
        SixelCellMetrics cellMetrics)
    {
        var rasterHash = ComputeRasterHash(payloadHash, rasterIdentity);
        return AddPlacementContext(rasterHash, widthInCells, heightInCells, cellMetrics);
    }

    private static byte[] ComputeRasterHash(ReadOnlySpan<byte> payloadHash, byte[]? rasterIdentity)
    {
        if (rasterIdentity is null)
        {
            return payloadHash.ToArray();
        }

        var combined = new byte[payloadHash.Length + rasterIdentity.Length];
        payloadHash.CopyTo(combined);
        rasterIdentity.CopyTo(combined, payloadHash.Length);
        return SHA256.HashData(combined);
    }

    private static byte[] AddPlacementContext(
        ReadOnlySpan<byte> rasterHash,
        int widthInCells,
        int heightInCells,
        SixelCellMetrics cellMetrics)
    {
        const int contextLength = 33;
        Span<byte> context = stackalloc byte[contextLength];
        context[0] = 1; // Identity format version.
        BinaryPrimitives.WriteInt32LittleEndian(context[1..5], widthInCells);
        BinaryPrimitives.WriteInt32LittleEndian(context[5..9], heightInCells);
        BinaryPrimitives.WriteInt64LittleEndian(
            context[9..17],
            BitConverter.DoubleToInt64Bits(cellMetrics.Width));
        BinaryPrimitives.WriteInt64LittleEndian(
            context[17..25],
            BitConverter.DoubleToInt64Bits(cellMetrics.Height));
        BinaryPrimitives.WriteInt32LittleEndian(context[25..29], (int)cellMetrics.Source);
        BinaryPrimitives.WriteInt32LittleEndian(context[29..33], (int)cellMetrics.Reliability);

        var combined = new byte[rasterHash.Length + contextLength];
        rasterHash.CopyTo(combined);
        context.CopyTo(combined.AsSpan(rasterHash.Length));
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

    internal SixelRasterResult GetRasterUnowned()
    {
        lock (_decodeLock)
        {
            return _raster ??= SixelRasterizer.Rasterize(
                ParseResult,
                GetRasterEnvironment());
        }
    }

    internal SixelPixelBuffer? GetPixelsUnowned()
    {
        lock (_decodeLock)
        {
            if (_decodeAttempted)
                return _decodedPixels;

            _raster ??= SixelRasterizer.Rasterize(
                ParseResult,
                GetRasterEnvironment());
            _decodedPixels = _raster.Image?.Materialize();
            _decodeAttempted = true;
            return _decodedPixels;
        }
    }

    private static long SaturatingAdd(long left, long right) =>
        right > long.MaxValue - left ? long.MaxValue : left + right;
}
