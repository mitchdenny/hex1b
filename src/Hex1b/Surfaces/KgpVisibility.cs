using Hex1b.Kgp;
using Hex1b.Layout;

namespace Hex1b.Surfaces;

/// <summary>
/// Tracks the visible regions of a KGP image after occlusion by overlapping content.
/// </summary>
/// <remarks>
/// <para>
/// Similar to <see cref="SixelVisibility"/>, this class manages the visible pixel
/// regions of a KGP image. When content (text, borders, other windows) overlaps the
/// image region, <see cref="ApplyOcclusion"/> subtracts the occluded area, potentially
/// splitting the visible region into multiple fragments.
/// </para>
/// <para>
/// Unlike sixels, KGP supports native source rectangle cropping via the <c>x,y,w,h</c>
/// parameters, so fragments don't need pixel re-encoding — each fragment is simply a
/// placement command with different source rect parameters.
/// </para>
/// </remarks>
public sealed class KgpVisibility
{
    /// <summary>
    /// Gets the original KGP cell data.
    /// </summary>
    public KgpCellData KgpData { get; }

    /// <summary>
    /// Gets the anchor cell position (top-left of the image placement).
    /// </summary>
    public (int X, int Y) AnchorPosition { get; }

    /// <summary>
    /// Gets the visible pixel regions after occlusion.
    /// Coordinates are relative to the image origin (0,0 = top-left of source image).
    /// Empty if fully occluded.
    /// </summary>
    public IReadOnlyList<PixelRect> VisibleRegions { get; private set; }

    /// <summary>
    /// Gets whether this image is fully visible (no occlusion).
    /// </summary>
    public bool IsFullyVisible { get; private set; }

    /// <summary>
    /// Gets whether this image is fully occluded (not visible at all).
    /// </summary>
    public bool IsFullyOccluded => VisibleRegions.Count == 0;

    /// <summary>
    /// Gets whether this image is fragmented (partially occluded, multiple visible regions).
    /// </summary>
    public bool IsFragmented => VisibleRegions.Count > 1;

    /// <summary>
    /// Creates a new KGP visibility tracker.
    /// </summary>
    /// <param name="kgpData">The KGP cell data.</param>
    /// <param name="anchorX">The cell X position of the image anchor.</param>
    /// <param name="anchorY">The cell Y position of the image anchor.</param>
    public KgpVisibility(KgpCellData kgpData, int anchorX, int anchorY)
    {
        KgpData = kgpData;
        AnchorPosition = (anchorX, anchorY);

        // Start with the effective source region (respecting any pre-existing clip)
        var effectiveW = kgpData.ClipW > 0 ? kgpData.ClipW : (int)kgpData.SourcePixelWidth;
        var effectiveH = kgpData.ClipH > 0 ? kgpData.ClipH : (int)kgpData.SourcePixelHeight;

        VisibleRegions = [new PixelRect(kgpData.ClipX, kgpData.ClipY, effectiveW, effectiveH)];
        IsFullyVisible = true;
    }

    /// <summary>
    /// Applies an occlusion rectangle (in cell coordinates) to this image.
    /// </summary>
    /// <param name="occlusionCellRect">The occluding rectangle in cell coordinates.</param>
    /// <param name="metrics">Cell metrics for coordinate conversion.</param>
    public void ApplyOcclusion(Rect occlusionCellRect, CellMetrics metrics)
    {
        // Convert image bounds to cell rect
        var imageCellRect = new Rect(
            AnchorPosition.X,
            AnchorPosition.Y,
            KgpData.WidthInCells,
            KgpData.HeightInCells);

        // Check if occlusion overlaps with image
        var intersection = IntersectRects(imageCellRect, occlusionCellRect);
        if (intersection.Width <= 0 || intersection.Height <= 0)
            return; // No overlap

        // Convert intersection to pixel coordinates relative to image origin
        var relX = intersection.X - AnchorPosition.X;
        var relY = intersection.Y - AnchorPosition.Y;

        // Map cell offsets to pixel offsets proportionally
        var effectiveW = KgpData.ClipW > 0 ? KgpData.ClipW : (int)KgpData.SourcePixelWidth;
        var effectiveH = KgpData.ClipH > 0 ? KgpData.ClipH : (int)KgpData.SourcePixelHeight;

        var pixelX = KgpData.ClipX + (int)(effectiveW * (long)relX / KgpData.WidthInCells);
        var pixelY = KgpData.ClipY + (int)(effectiveH * (long)relY / KgpData.HeightInCells);
        var pixelW = (int)(effectiveW * (long)intersection.Width / KgpData.WidthInCells);
        var pixelH = (int)(effectiveH * (long)intersection.Height / KgpData.HeightInCells);

        var pixelOcclusion = new PixelRect(pixelX, pixelY, pixelW, pixelH);

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
    /// Generates placement data for each visible fragment of the image.
    /// Each placement includes the cell position and a clipped <see cref="KgpCellData"/>.
    /// </summary>
    /// <param name="metrics">Cell metrics for position calculation.</param>
    /// <returns>List of (cellX, cellY, clippedData) tuples for each visible fragment.</returns>
    public IReadOnlyList<(int CellX, int CellY, KgpCellData Data)> GeneratePlacements(CellMetrics metrics)
    {
        if (IsFullyOccluded)
            return [];

        var placements = new List<(int, int, KgpCellData)>();

        foreach (var region in VisibleRegions)
        {
            // Calculate cell position for this fragment
            var effectiveW = KgpData.ClipW > 0 ? KgpData.ClipW : (int)KgpData.SourcePixelWidth;
            var effectiveH = KgpData.ClipH > 0 ? KgpData.ClipH : (int)KgpData.SourcePixelHeight;

            // Map pixel offset back to cell offset
            var pixelOffsetX = region.X - KgpData.ClipX;
            var pixelOffsetY = region.Y - KgpData.ClipY;
            var cellOffsetX = effectiveW > 0 ? (int)((long)pixelOffsetX * KgpData.WidthInCells / effectiveW) : 0;
            var cellOffsetY = effectiveH > 0 ? (int)((long)pixelOffsetY * KgpData.HeightInCells / effectiveH) : 0;

            // Calculate cell dimensions for this fragment
            var cellW = effectiveW > 0 ? Math.Max(1, (int)Math.Ceiling((double)region.Width * KgpData.WidthInCells / effectiveW)) : 1;
            var cellH = effectiveH > 0 ? Math.Max(1, (int)Math.Ceiling((double)region.Height * KgpData.HeightInCells / effectiveH)) : 1;

            var clipped = KgpData.WithClip(region.X, region.Y, region.Width, region.Height, cellW, cellH);
            placements.Add((AnchorPosition.X + cellOffsetX, AnchorPosition.Y + cellOffsetY, clipped));
        }

        return placements;
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
