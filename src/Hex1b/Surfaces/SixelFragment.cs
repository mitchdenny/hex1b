using Hex1b.Layout;
using Hex1b.Sixel;

namespace Hex1b.Surfaces;

/// <summary>
/// Represents a fragment of a sixel image after clipping or occlusion.
/// </summary>
/// <remarks>
/// When a sixel is partially occluded by overlapping content, it may be split
/// into multiple fragments. Each fragment contains the visible portion of the
/// original sixel along with its position.
/// </remarks>
public sealed class SixelFragment
{
    /// <summary>
    /// Gets the original sixel data this fragment was derived from.
    /// </summary>
    public SixelData OriginalSixel { get; }

    /// <summary>
    /// Gets the cell position where this fragment should be rendered.
    /// </summary>
    public (int X, int Y) CellPosition { get; }

    /// <summary>
    /// Gets the pixel region within the original sixel that this fragment represents.
    /// </summary>
    public PixelRect PixelRegion { get; }

    /// <summary>
    /// Gets whether this fragment represents the complete original sixel (no clipping).
    /// </summary>
    public bool IsComplete
    {
        get
        {
            var extent = OriginalSixel.GetRenderedPixelExtent();
            return PixelRegion.X == 0
                && PixelRegion.Y == 0
                && PixelRegion.Width == extent.Width
                && PixelRegion.Height == extent.Height;
        }
    }

    private string? _encodedPayload;
    private SixelPixelBuffer? _croppedPixels;

    /// <summary>
    /// Creates a new sixel fragment.
    /// </summary>
    /// <param name="originalSixel">The original sixel data.</param>
    /// <param name="cellX">The cell X position for rendering.</param>
    /// <param name="cellY">The cell Y position for rendering.</param>
    /// <param name="pixelRegion">The visible pixel region within the original.</param>
    public SixelFragment(SixelData originalSixel, int cellX, int cellY, PixelRect pixelRegion)
    {
        OriginalSixel = originalSixel;
        CellPosition = (cellX, cellY);
        PixelRegion = pixelRegion;
    }

    /// <summary>
    /// Creates a fragment representing the complete original sixel.
    /// </summary>
    public static SixelFragment Complete(SixelData sixel, int cellX, int cellY)
    {
        return new SixelFragment(
            sixel, 
            cellX, 
            cellY,
            new PixelRect(0, 0, sixel.PixelWidth, sixel.PixelHeight));
    }

    /// <summary>
    /// Gets the cropped pixel buffer for this fragment.
    /// The result is cached for subsequent calls.
    /// </summary>
    /// <returns>The cropped pixel buffer, or null if the original cannot be decoded.</returns>
    public SixelPixelBuffer? GetPixels()
    {
        if (_croppedPixels is not null)
            return _croppedPixels;

        if (IsComplete)
        {
            _croppedPixels = OriginalSixel.GetPixels();
            return _croppedPixels;
        }

        var original = OriginalSixel.GetPixels();
        if (original is null)
            return null;

        _croppedPixels = original.Crop(PixelRegion);
        return _croppedPixels;
    }

    /// <summary>
    /// Gets the encoded sixel payload for this fragment.
    /// For complete fragments, returns the original payload.
    /// For cropped fragments, re-encodes the cropped pixels.
    /// The result is cached for subsequent calls.
    /// </summary>
    /// <returns>The sixel payload string, or null if encoding fails.</returns>
    public string? GetPayload()
    {
        if (_encodedPayload is not null)
            return _encodedPayload;

        if (IsComplete)
        {
            _encodedPayload = OriginalSixel.Payload;
            return _encodedPayload;
        }

        var pixels = GetPixels();
        if (pixels is null)
            return null;

        _encodedPayload = SixelExactEncoder.EncodeBounded(
            pixels, int.MaxValue, CancellationToken.None, reuseColorRegisters: true).Payload;
        return _encodedPayload;
    }

    /// <summary>
    /// Gets the cell span for this fragment using the protocol cell metrics
    /// captured by the original Sixel image.
    /// </summary>
    /// <returns>The width and height in protocol cells.</returns>
    public (int Width, int Height) GetCellSpan()
    {
        var metrics = OriginalSixel.CellMetrics;
        return (metrics.ColumnsFor(PixelRegion.Width), metrics.RowsFor(PixelRegion.Height));
    }

    /// <summary>
    /// Gets the cell span for this fragment using the protocol cell metrics
    /// captured by the original Sixel image.
    /// </summary>
    /// <param name="metrics">
    /// Ignored. Sixel placement metrics are captured by <see cref="SixelData"/>.
    /// </param>
    /// <returns>The width and height in protocol cells.</returns>
    [Obsolete("Cell metrics are captured by SixelData. Use GetCellSpan().")]
    public (int Width, int Height) GetCellSpan(CellMetrics metrics) => GetCellSpan();
}
