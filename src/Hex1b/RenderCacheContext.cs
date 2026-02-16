namespace Hex1b;

/// <summary>
/// Context passed to a widget's render-cache predicate configured via <see cref="CachingExtensions.Cached{TWidget}"/>.
/// </summary>
/// <param name="Node">The node being considered for cached surface reuse.</param>
/// <param name="RenderContext">The render context for the current frame.</param>
public readonly record struct RenderCacheContext(Hex1bNode Node, Hex1bRenderContext RenderContext);

