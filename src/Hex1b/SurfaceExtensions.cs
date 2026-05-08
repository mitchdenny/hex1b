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
/// context.Surface(s => [
///     s.Layer(backgroundSurface),
///     s.Layer(surface => DrawSprite(surface)),
///     s.Layer(context => FogOfWar(context, s.MouseX, s.MouseY))
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
    /// <param name="context">The widget context.</param>
    /// <param name="builder">
    /// A function that receives a <see cref="SurfaceLayerContext"/> and returns
    /// the layers to composite. Called each render with current mouse position.
    /// </param>
    /// <returns>A new <see cref="SurfaceWidget"/> with the specified layers.</returns>
    /// <example>
    /// <code>
    /// context.Surface(s => [
    ///     s.Layer(terrainSurface),
    ///     s.Layer(context => Tint(context, Hex1bColor.Blue, 0.3))
    /// ])
    /// </code>
    /// </example>
    public static SurfaceWidget Surface<TParent>(
        this WidgetContext<TParent> context,
        Func<SurfaceLayerContext, IEnumerable<SurfaceLayer>> builder)
        where TParent : Hex1bWidget
        => new(builder);

    /// <summary>
    /// Creates a <see cref="SurfaceWidget"/> with a single static surface.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type in the current context.</typeparam>
    /// <param name="context">The widget context.</param>
    /// <param name="source">The surface source to display.</param>
    /// <returns>A new <see cref="SurfaceWidget"/> displaying the source.</returns>
    /// <example>
    /// <code>
    /// context.Surface(myPreRenderedSurface)
    /// </code>
    /// </example>
    public static SurfaceWidget Surface<TParent>(
        this WidgetContext<TParent> context,
        ISurfaceSource source)
        where TParent : Hex1bWidget
        => new(s => [s.Layer(source)]);
}
