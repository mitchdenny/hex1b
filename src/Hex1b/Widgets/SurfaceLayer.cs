using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// Base record for layers in a <see cref="SurfaceWidget"/>.
/// </summary>
/// <remarks>
/// Layers are composited bottom-up in the order they are added.
/// Each layer type provides different content:
/// <list type="bullet">
///   <item><see cref="SourceSurfaceLayer"/> - an existing <see cref="ISurfaceSource"/></item>
///   <item><see cref="DrawSurfaceLayer"/> - content drawn via callback</item>
///   <item><see cref="ComputedSurfaceLayer"/> - dynamically computed cells</item>
/// </list>
/// </remarks>
public abstract record SurfaceLayer;

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

/// <summary>
/// A layer whose cells are computed dynamically based on layers below.
/// </summary>
/// <remarks>
/// <para>
/// The compute delegate is called for each cell during compositing.
/// It receives a <see cref="ComputeContext"/> that provides access to
/// cells from layers below, enabling effects like fog of war, tinting,
/// drop shadows, and other compositing operations.
/// </para>
/// <para>
/// Computed layers are always sized to match the widget bounds and
/// cannot have offsets (they cover the entire surface).
/// </para>
/// </remarks>
/// <param name="Compute">The delegate that computes each cell's value.</param>
public record ComputedSurfaceLayer(
    CellCompute Compute
) : SurfaceLayer;
