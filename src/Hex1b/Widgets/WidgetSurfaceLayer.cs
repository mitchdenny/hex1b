using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// A layer whose content is rendered from a widget tree.
/// </summary>
/// <remarks>
/// <para>
/// The widget tree is reconciled, measured, arranged, and rendered to a
/// <see cref="Surface"/> each frame. This enables using the full widget system
/// to produce layer content — useful for transition screens, splash screens,
/// or any non-interactive snapshot of a UI.
/// </para>
/// <para>
/// Widget layers are non-interactive: they do not receive input events.
/// To make the content interactive, swap the <see cref="SurfaceWidget"/> for
/// the actual widget tree.
/// </para>
/// </remarks>
/// <param name="Widget">The widget tree to render as a layer.</param>
public record WidgetSurfaceLayer(
    Hex1bWidget Widget
) : SurfaceLayer;
