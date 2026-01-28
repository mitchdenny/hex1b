using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that renders layered surfaces with compositing support.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SurfaceWidget"/> provides direct access to the Surface API,
/// enabling arbitrary visualizations with multiple composited layers.
/// Each layer can be a static surface, dynamically drawn content,
/// or computed cells that can query layers below.
/// </para>
/// <para>
/// Layers are composited bottom-up in the order returned by the builder.
/// The first layer is at the bottom, and subsequent layers are drawn on top.
/// </para>
/// <example>
/// <code>
/// ctx.Surface(s => [
///     s.Layer(terrainSurface),                           // Static background
///     s.Layer(entitySurface, offsetX: 10, offsetY: 5),   // Positioned sprite
///     s.Layer(ctx => FogOfWar(ctx, s.MouseX, s.MouseY)), // Effect layer
/// ])
/// </code>
/// </example>
/// </remarks>
/// <param name="LayerBuilder">
/// A function that receives a <see cref="SurfaceLayerContext"/> and returns
/// the layers to composite. Called each render with current mouse position.
/// </param>
public record SurfaceWidget(
    Func<SurfaceLayerContext, IEnumerable<SurfaceLayer>> LayerBuilder
) : Hex1bWidget
{
    /// <summary>
    /// Gets the width sizing hint for this widget.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SizeHint.Fill"/> to take all available width.
    /// </remarks>
    public new SizeHint? WidthHint { get; init; } = SizeHint.Fill;

    /// <summary>
    /// Gets the height sizing hint for this widget.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="SizeHint.Fill"/> to take all available height.
    /// </remarks>
    public new SizeHint? HeightHint { get; init; } = SizeHint.Fill;

    /// <summary>
    /// Returns a new widget with the specified width hint.
    /// </summary>
    /// <param name="hint">The width sizing hint.</param>
    /// <returns>A new widget with the updated width hint.</returns>
    public SurfaceWidget Width(SizeHint hint) => this with { WidthHint = hint };

    /// <summary>
    /// Returns a new widget with the specified height hint.
    /// </summary>
    /// <param name="hint">The height sizing hint.</param>
    /// <returns>A new widget with the updated height hint.</returns>
    public SurfaceWidget Height(SizeHint hint) => this with { HeightHint = hint };

    /// <summary>
    /// Returns a new widget with fixed dimensions.
    /// </summary>
    /// <param name="width">The fixed width in columns.</param>
    /// <param name="height">The fixed height in rows.</param>
    /// <returns>A new widget with fixed dimensions.</returns>
    public SurfaceWidget Size(int width, int height) => this with
    {
        WidthHint = SizeHint.Fixed(width),
        HeightHint = SizeHint.Fixed(height)
    };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SurfaceNode ?? new SurfaceNode();

        // Always mark dirty since layers may have changed content
        // (we can't easily compare layer contents)
        node.MarkDirty();

        node.LayerBuilder = LayerBuilder;
        node.WidthHint = WidthHint;
        node.HeightHint = HeightHint;

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(SurfaceNode);
}
