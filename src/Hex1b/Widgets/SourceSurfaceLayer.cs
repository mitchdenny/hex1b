using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// A layer backed by an existing <see cref="ISurfaceSource"/>.
/// </summary>
/// <remarks>
/// Use this for static content that doesn't change between frames,
/// or for surfaces you manage externally and update as needed.
/// </remarks>
/// <param name="Source">The surface source providing cell data.</param>
/// <param name="OffsetX">X offset where the source's (0,0) will be placed.</param>
/// <param name="OffsetY">Y offset where the source's (0,0) will be placed.</param>
public record SourceSurfaceLayer(
    ISurfaceSource Source,
    int OffsetX = 0,
    int OffsetY = 0
) : SurfaceLayer;
