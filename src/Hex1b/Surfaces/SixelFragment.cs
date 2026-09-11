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

/// <summary>
/// Tracks visibility information for a sixel during compositing.
/// </summary>
public sealed class SixelVisibility
{
    /// <summary>
    /// Gets the original sixel data.
    /// </summary>
    public TrackedObject<SixelData> Sixel { get; }

    /// <summary>
    /// Gets the anchor cell position (top-left of the sixel).
    /// </summary>
    public (int X, int Y) AnchorPosition { get; }

    /// <summary>
    /// Gets the layer index this sixel belongs to.
    /// </summary>
    public int LayerIndex { get; }

    /// <summary>
    /// Gets the visible pixel regions after occlusion.
    /// Empty if fully occluded.
    /// </summary>
    public IReadOnlyList<PixelRect> VisibleRegions { get; private set; }

    /// <summary>
    /// Gets whether this sixel is fully visible (no occlusion).
    /// </summary>
    public bool IsFullyVisible { get; private set; }

    /// <summary>
    /// Gets whether this sixel is fully occluded (not visible at all).
    /// </summary>
    public bool IsFullyOccluded
    {
        get
        {
            var contentBounds = GetVisibleContentBounds();
            return contentBounds.IsEmpty ||
                !VisibleRegions.Any(region => !region.Intersect(contentBounds).IsEmpty);
        }
    }

    /// <summary>
    /// Gets whether this sixel is fragmented (partially occluded, multiple visible regions).
    /// </summary>
    public bool IsFragmented => VisibleRegions.Count > 1;

    /// <summary>
    /// Creates a new sixel visibility tracker.
    /// </summary>
    public SixelVisibility(TrackedObject<SixelData> sixel, int anchorX, int anchorY, int layerIndex)
    {
        Sixel = sixel;
        AnchorPosition = (anchorX, anchorY);
        LayerIndex = layerIndex;
        
        // Initially fully visible
        var data = sixel.Data;
        
        var extent = data.GetRenderedPixelExtent();
        
        VisibleRegions = [new PixelRect(0, 0, extent.Width, extent.Height)];
        IsFullyVisible = true;
    }

    /// <summary>
    /// Applies an occlusion rectangle in cell coordinates to this Sixel image,
    /// using the protocol cell metrics captured when the image was created.
    /// </summary>
    /// <param name="occlusionCellRect">The occluding rectangle in cell coordinates.</param>
    public void ApplyOcclusion(Rect occlusionCellRect)
    {
        var data = Sixel.Data;
        var metrics = data.CellMetrics;
        
        // Convert sixel bounds to cell rect
        var sixelCellRect = new Rect(
            AnchorPosition.X, 
            AnchorPosition.Y,
            data.WidthInCells,
            data.HeightInCells);

        // Check if occlusion overlaps with sixel
        var intersection = IntersectRects(sixelCellRect, occlusionCellRect);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return; // No overlap

        // Convert intersection to pixel coordinates relative to sixel origin
        // Use actual (floating-point) cell width for precise alignment
        var relX = intersection.X - AnchorPosition.X;
        var relY = intersection.Y - AnchorPosition.Y;
        var pixelLeft = metrics.GetPixelForColumnBoundary(relX);
        var pixelTop = metrics.GetPixelForRowBoundary(relY);
        var pixelOcclusion = new PixelRect(
            pixelLeft,
            pixelTop,
            metrics.GetPixelForColumnBoundary(relX + intersection.Width) - pixelLeft,
            metrics.GetPixelForRowBoundary(relY + intersection.Height) - pixelTop);

        // Apply occlusion to all visible regions
        var newRegions = new List<PixelRect>();
        foreach (var region in VisibleRegions)
        {
            newRegions.AddRange(region.Subtract(pixelOcclusion));
        }

        VisibleRegions = newRegions;
        IsFullyVisible = false;
    }

    /// <summary>
    /// Applies an occlusion rectangle in cell coordinates to this Sixel image,
    /// using the protocol cell metrics captured when the image was created.
    /// </summary>
    /// <param name="occlusionCellRect">The occluding rectangle in cell coordinates.</param>
    /// <param name="metrics">
    /// Ignored. Sixel placement metrics are captured by <see cref="SixelData"/>.
    /// </param>
    [Obsolete("Cell metrics are captured by SixelData. Use ApplyOcclusion(Rect).")]
    public void ApplyOcclusion(Rect occlusionCellRect, CellMetrics metrics)
        => ApplyOcclusion(occlusionCellRect);

    /// <summary>
    /// Generates fragments for the visible regions of this Sixel image using
    /// the protocol cell metrics captured when the image was created.
    /// </summary>
    /// <returns>List of fragments to render.</returns>
    public IReadOnlyList<SixelFragment> GenerateFragments()
    {
        if (IsFullyOccluded)
            return [];

        var data = Sixel.Data;
        var metrics = data.CellMetrics;
        var extent = data.GetRenderedPixelExtent();
        if (IsFullyVisible)
        {
            return
            [
                new SixelFragment(
                    data,
                    AnchorPosition.X,
                    AnchorPosition.Y,
                    new PixelRect(0, 0, extent.Width, extent.Height))
            ];
        }

        var fragments = new List<SixelFragment>();
        var contentBounds = GetVisibleContentBounds();

        foreach (var region in VisibleRegions)
        {
            var visibleRegion = region.Intersect(contentBounds);
            if (visibleRegion.IsEmpty)
                continue;

            // Calculate cell position for this fragment using actual cell width
            // The pixel region is in the original sixel's coordinate space
            var cellOffsetX = metrics.GetColumnOffsetForPixel(visibleRegion.X);
            var cellOffsetY = metrics.GetRowOffsetForPixel(visibleRegion.Y);
            
            fragments.Add(new SixelFragment(
                data,
                AnchorPosition.X + cellOffsetX,
                AnchorPosition.Y + cellOffsetY,
                visibleRegion));
        }

        return fragments;
    }

    /// <summary>
    /// Generates fragments for the visible regions of this Sixel image using
    /// the protocol cell metrics captured when the image was created.
    /// </summary>
    /// <param name="metrics">
    /// Ignored. Sixel placement metrics are captured by <see cref="SixelData"/>.
    /// </param>
    /// <returns>List of fragments to render.</returns>
    [Obsolete("Cell metrics are captured by SixelData. Use GenerateFragments().")]
    public IReadOnlyList<SixelFragment> GenerateFragments(CellMetrics metrics)
        => GenerateFragments();

    private PixelRect GetVisibleContentBounds()
    {
        var data = Sixel.Data;
        if (data.BackgroundMode == Hex1b.Sixel.SixelBackgroundMode.Transparent)
        {
            var painted = data.ParseResult.PaintedBounds;
            return new PixelRect(painted.X, painted.Y, painted.Width, painted.Height);
        }

        var extent = data.GetRenderedPixelExtent();
        return new PixelRect(0, 0, extent.Width, extent.Height);
    }

    private static Rect IntersectRects(Rect a, Rect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);

        if (right <= left || bottom <= top)
            return new Rect(0, 0, 0, 0);

        return new Rect(left, top, right - left, bottom - top);
    }
}
