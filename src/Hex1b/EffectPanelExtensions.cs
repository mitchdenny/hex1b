namespace Hex1b;

using Hex1b.Surfaces;
using Hex1b.Widgets;

/// <summary>
/// Extension methods for creating <see cref="EffectPanelWidget"/> instances.
/// </summary>
public static class EffectPanelExtensions
{
    /// <summary>
    /// Creates an effect panel that wraps the given child widget.
    /// Use <see cref="EffectPanelWidget.Effect"/> to apply a post-processing effect.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to wrap.</param>
    /// <returns>An <see cref="EffectPanelWidget"/> wrapping the child.</returns>
    public static EffectPanelWidget EffectPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child)
        where TParent : Hex1bWidget
        => new(child);

    /// <summary>
    /// Creates an effect panel that wraps the given child widget with an effect applied.
    /// </summary>
    /// <typeparam name="TParent">The parent widget type.</typeparam>
    /// <param name="ctx">The widget context.</param>
    /// <param name="child">The child widget to wrap.</param>
    /// <param name="effect">A callback that receives the rendered <see cref="Surface"/> for in-place modification.</param>
    /// <returns>An <see cref="EffectPanelWidget"/> with the effect applied.</returns>
    public static EffectPanelWidget EffectPanel<TParent>(
        this WidgetContext<TParent> ctx,
        Hex1bWidget child,
        Action<Surface> effect)
        where TParent : Hex1bWidget
        => new(child) { EffectCallback = effect };
}
