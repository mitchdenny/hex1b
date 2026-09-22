using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// A layer whose content is drawn via a callback.
/// </summary>
/// <remarks>
/// The callback receives a fresh <see cref="Surface"/> sized to the widget bounds.
/// Use this for dynamic content that needs to be redrawn each frame.
/// </remarks>
/// <param name="Draw">The callback that draws content to the surface.</param>
/// <param name="OffsetX">X offset where the drawn surface's (0,0) will be placed.</param>
/// <param name="OffsetY">Y offset where the drawn surface's (0,0) will be placed.</param>
public record DrawSurfaceLayer(
    Action<Surface> Draw,
    int OffsetX = 0,
    int OffsetY = 0
) : SurfaceLayer;
