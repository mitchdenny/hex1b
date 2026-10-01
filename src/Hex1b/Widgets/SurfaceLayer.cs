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
///   <item><see cref="WidgetSurfaceLayer"/> - a widget tree rendered to a surface</item>
/// </list>
/// </remarks>
public abstract record SurfaceLayer;
