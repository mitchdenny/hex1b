using Hex1b.Nodes;
using Hex1b.Surfaces;

namespace Hex1b.Widgets;

/// <summary>
/// A single-child container widget that applies visual post-processing effects.
/// The child remains fully interactive (focus, input, hit testing work normally).
/// During rendering, the child's output is captured to a temporary <see cref="Surface"/>,
/// the effect callback modifies it, then the result is composited to the parent.
/// </summary>
/// <param name="Child">The child widget to render and apply effects to.</param>
public sealed record EffectPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    /// <summary>
    /// Gets the effect callback that post-processes the rendered surface.
    /// </summary>
    internal Action<Surface>? Effect { get; init; }

    /// <summary>
    /// Sets the effect callback that post-processes the child's rendered surface.
    /// </summary>
    /// <param name="effect">A callback that receives the rendered <see cref="Surface"/> for in-place modification.</param>
    /// <returns>A new <see cref="EffectPanelWidget"/> with the effect applied.</returns>
    public EffectPanelWidget WithEffect(Action<Surface> effect)
        => this with { Effect = effect };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as EffectPanelNode ?? new EffectPanelNode();

        node.Effect = Effect;

        // Reconcile the child widget
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);

        // Apply common widget properties (bindings, size hints)
        node.BindingsConfigurator = BindingsConfigurator;
        node.WidthHint = WidthHint;
        node.HeightHint = HeightHint;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(EffectPanelNode);
}
