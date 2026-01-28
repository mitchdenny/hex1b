namespace Hex1b;

using Hex1b.Surfaces;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="SurfaceWidget"/> instances using the fluent API.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable creation of surface widgets that expose the full Surface API
/// for arbitrary visualizations with layered compositing.
/// </para>
/// </remarks>
/// <example>
/// <para>Creating a surface with multiple layers:</para>
/// <code>
/// ctx.Surface(s => [
///     s.Layer(backgroundSurface),
///     s.Layer(surface => DrawSprite(surface)),
///     s.Layer(ctx => FogOfWar(ctx, s.MouseX, s.MouseY))
/// ])
/// </code>
/// </example>
/// <seealso cref="SurfaceWidget"/>
/// <seealso cref="SurfaceLayerContext"/>
public static class SurfaceExtensions
{
    /// <summary>
    /// Creates a <see cref="SurfaceWidget"/> with the specified layer builder.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="layerBuilder">
    /// A function that receives a <see cref="SurfaceLayerContext"/> and returns
    /// the layers to composite. Called each render with current mouse position.
    /// </param>
    /// <returns>A new <see cref="SurfaceWidget"/> with the specified layers.</returns>
    /// <example>
    /// <code>
    /// ctx.Surface(s => [
    ///     s.Layer(terrainSurface),
    ///     s.Layer(ctx => Tint(ctx, Hex1bColor.Blue, 0.3))
    /// ])
    /// </code>
    /// </example>
    public static SurfaceWidget Surface<TParent>(
        this WidgetContext<TParent> ctx,
        Func<SurfaceLayerContext, IEnumerable<SurfaceLayer>> layerBuilder)
        where TParent : Hex1bWidget
        => new(layerBuilder);

    /// <summary>
    /// Creates a <see cref="SurfaceWidget"/> with a single static surface.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="source">The surface source to display.</param>
    /// <returns>A new <see cref="SurfaceWidget"/> displaying the source.</returns>
    /// <example>
    /// <code>
    /// ctx.Surface(myPreRenderedSurface)
    /// </code>
    /// </example>
    public static SurfaceWidget Surface<TParent>(
        this WidgetContext<TParent> ctx,
        ISurfaceSource source)
        where TParent : Hex1bWidget
        => new(s => [s.Layer(source)]);
}
