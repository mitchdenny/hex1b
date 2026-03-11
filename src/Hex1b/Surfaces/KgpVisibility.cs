using Hex1b.Layout;

namespace Hex1b.Surfaces;

/// <summary>
/// Tracks visibility of a KGP image placement after occlusion by other content.
/// </summary>
/// <remarks>
/// <para>
/// Similar to <see cref="SixelVisibility"/> but leverages KGP's native source-rectangle
/// clipping — adjusting placement parameters is a pure metadata operation, no image
/// re-encoding is needed.
/// </para>
/// <para>
/// Usage: create a <see cref="KgpVisibility"/> for each KGP image, apply occlusions
/// from overlapping text or higher-layer content, then call <see cref="GeneratePlacements"/>
/// to produce clipped <see cref="KgpCellData"/> instances for emission.
/// </para>
/// </remarks>
public sealed class KgpVisibility
{
    private List<PixelRect> _visibleRegions;

    /// <summary>
    /// Gets the tracked KGP cell data.
    /// </summary>
    public TrackedObject<KgpCellData> Kgp { get; }

    /// <summary>
    /// Gets the anchor position (top-left cell) of the KGP image.
    /// </summary>
    public (int X, int Y) AnchorPosition { get; }

    /// <summary>
    /// Gets the layer index for z-ordering.
    /// </summary>
    public int LayerIndex { get; }

    /// <summary>
    /// Gets whether the image is fully visible (no occlusions applied).
    /// </summary>
    public bool IsFullyVisible { get; private set; }

    /// <summary>
    /// Gets whether the image is fully occluded (no visible regions).
    /// </summary>
    public bool IsFullyOccluded => _visibleRegions.Count == 0;

    /// <summary>
    /// Gets the visible pixel regions after occlusion.
    /// </summary>
    public IReadOnlyList<PixelRect> VisibleRegions => _visibleRegions;

    /// <summary>
    /// Creates a new KGP visibility tracker.
    /// </summary>
    public KgpVisibility(TrackedObject<KgpCellData> kgp, int anchorX, int anchorY, int layerIndex)
    {
        Kgp = kgp;
        AnchorPosition = (anchorX, anchorY);
        LayerIndex = layerIndex;
        IsFullyVisible = true;

        var data = kgp.Data;
        var pixelWidth = data.SourcePixelWidth > 0 ? (int)data.SourcePixelWidth : data.WidthInCells * 10;
        var pixelHeight = data.SourcePixelHeight > 0 ? (int)data.SourcePixelHeight : data.HeightInCells * 20;

        _visibleRegions = [new PixelRect(0, 0, pixelWidth, pixelHeight)];
    }

    /// <summary>
    /// Applies an occlusion from a cell rect, subtracting the occluded area from visible regions.
    /// </summary>
    /// <param name="cellRect">The occluding rectangle in cell coordinates, relative to the surface.</param>
    /// <param name="metrics">Cell metrics for pixel conversion.</param>
    public void ApplyOcclusion(Rect cellRect, CellMetrics metrics)
    {
        if (_visibleRegions.Count == 0)
            return;

        var data = Kgp.Data;
        var imgPixelW = data.SourcePixelWidth > 0 ? (int)data.SourcePixelWidth : data.WidthInCells * metrics.PixelWidth;
        var imgPixelH = data.SourcePixelHeight > 0 ? (int)data.SourcePixelHeight : data.HeightInCells * metrics.PixelHeight;

        // Convert cell rect to pixel coords relative to the image
        var relCellX = cellRect.X - AnchorPosition.X;
        var relCellY = cellRect.Y - AnchorPosition.Y;
        var pixelX = relCellX * imgPixelW / data.WidthInCells;
        var pixelY = relCellY * imgPixelH / data.HeightInCells;
        var pixelW = cellRect.Width * imgPixelW / data.WidthInCells;
        var pixelH = cellRect.Height * imgPixelH / data.HeightInCells;

        var occlusionRect = new PixelRect(pixelX, pixelY, pixelW, pixelH);

        var newRegions = new List<PixelRect>();
        foreach (var region in _visibleRegions)
        {
            foreach (var remainder in region.Subtract(occlusionRect))
            {
                newRegions.Add(remainder);
            }
        }

        _visibleRegions = newRegions;
        IsFullyVisible = false;
    }

    /// <summary>
    /// Generates placement data for each visible region.
    /// Each placement uses the same ImageId but different source rectangles.
    /// </summary>
    public IReadOnlyList<(KgpCellData Data, int CellX, int CellY)> GeneratePlacements(CellMetrics metrics)
    {
        if (IsFullyOccluded)
            return [];

        var data = Kgp.Data;

        if (IsFullyVisible)
        {
            return [(data, AnchorPosition.X, AnchorPosition.Y)];
        }

        var results = new List<(KgpCellData, int, int)>();
        var imgPixelW = data.SourcePixelWidth > 0 ? (int)data.SourcePixelWidth : data.WidthInCells * metrics.PixelWidth;
        var imgPixelH = data.SourcePixelHeight > 0 ? (int)data.SourcePixelHeight : data.HeightInCells * metrics.PixelHeight;

        foreach (var region in _visibleRegions)
        {
            var cellOffsetX = region.X * data.WidthInCells / imgPixelW;
            var cellOffsetY = region.Y * data.HeightInCells / imgPixelH;
            var cellWidth = Math.Max(1, region.Width * data.WidthInCells / imgPixelW);
            var cellHeight = Math.Max(1, region.Height * data.HeightInCells / imgPixelH);

            var clipped = data.WithClip(region.X, region.Y, region.Width, region.Height, cellWidth, cellHeight);
            results.Add((clipped, AnchorPosition.X + cellOffsetX, AnchorPosition.Y + cellOffsetY));
        }

        return results;
    }
}
