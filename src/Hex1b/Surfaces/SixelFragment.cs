using Hex1b.Layout;

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
    public bool IsComplete => 
        PixelRegion.X == 0 && 
        PixelRegion.Y == 0 &&
        PixelRegion.Width == OriginalSixel.PixelWidth &&
        PixelRegion.Height == OriginalSixel.PixelHeight;

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

        _encodedPayload = SixelEncoder.Encode(pixels);
        return _encodedPayload;
    }

    /// <summary>
    /// Gets the cell span for this fragment using the specified metrics.
    /// </summary>
    public (int Width, int Height) GetCellSpan(CellMetrics metrics)
    {
        return metrics.PixelToCellSpan(PixelRegion.Width, PixelRegion.Height);
    }
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
    public bool IsFullyOccluded => VisibleRegions.Count == 0;

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
        
        // Use pixel dimensions if available, otherwise estimate from cell dimensions
        var pixelWidth = data.PixelWidth > 0 ? data.PixelWidth : data.WidthInCells * 10;
        var pixelHeight = data.PixelHeight > 0 ? data.PixelHeight : data.HeightInCells * 20;
        
        VisibleRegions = [new PixelRect(0, 0, pixelWidth, pixelHeight)];
        IsFullyVisible = true;
    }

    /// <summary>
    /// Applies an occlusion rectangle (in cell coordinates) to this sixel.
    /// </summary>
    /// <param name="occlusionCellRect">The occluding rectangle in cell coordinates.</param>
    /// <param name="metrics">Cell metrics for coordinate conversion.</param>
    public void ApplyOcclusion(Rect occlusionCellRect, CellMetrics metrics)
    {
        var data = Sixel.Data;
        
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
        var pixelOcclusion = new PixelRect(
            (intersection.X - AnchorPosition.X) * metrics.PixelWidth,
            (intersection.Y - AnchorPosition.Y) * metrics.PixelHeight,
            intersection.Width * metrics.PixelWidth,
            intersection.Height * metrics.PixelHeight);

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
    /// Generates fragments for the visible regions of this sixel.
    /// </summary>
    /// <param name="metrics">Cell metrics for position calculation.</param>
    /// <returns>List of fragments to render.</returns>
    public IReadOnlyList<SixelFragment> GenerateFragments(CellMetrics metrics)
    {
        if (IsFullyOccluded)
            return [];

        var fragments = new List<SixelFragment>();
        var data = Sixel.Data;

        foreach (var region in VisibleRegions)
        {
            // Calculate cell position for this fragment
            var cellOffsetX = region.X / metrics.PixelWidth;
            var cellOffsetY = region.Y / metrics.PixelHeight;

            fragments.Add(new SixelFragment(
                data,
                AnchorPosition.X + cellOffsetX,
                AnchorPosition.Y + cellOffsetY,
                region));
        }

        return fragments;
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
