using Hex1b.Surfaces;

namespace Hex1b.Widgets;

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
