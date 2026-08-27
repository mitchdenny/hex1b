namespace Hex1b;

/// <summary>
/// Describes exact image and clip bounds for a KGP placement in terminal-cell
/// units relative to the placement's row and column.
/// </summary>
/// <param name="ClipOffsetXInCells">Horizontal clip offset from the placement column.</param>
/// <param name="ClipOffsetYInCells">Vertical clip offset from the placement row.</param>
/// <param name="ClipWidthInCells">Width of the visible clip rectangle.</param>
/// <param name="ClipHeightInCells">Height of the visible clip rectangle.</param>
/// <param name="ImageOffsetXInCells">Horizontal offset of the complete scaled image.</param>
/// <param name="ImageOffsetYInCells">Vertical offset of the complete scaled image.</param>
/// <param name="ImageWidthInCells">Width of the complete scaled image.</param>
/// <param name="ImageHeightInCells">Height of the complete scaled image.</param>
/// <remarks>
/// Unicode-placeholder fragments can begin or end partway through a cell after
/// aspect-ratio fitting. Ordinary KGP placements do not require this metadata.
/// </remarks>
public readonly record struct KgpPlacementRenderGeometry(
    double ClipOffsetXInCells,
    double ClipOffsetYInCells,
    double ClipWidthInCells,
    double ClipHeightInCells,
    double ImageOffsetXInCells,
    double ImageOffsetYInCells,
    double ImageWidthInCells,
    double ImageHeightInCells);
