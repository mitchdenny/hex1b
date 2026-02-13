using Hex1b.Animation;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A context for building the child tree of a <see cref="StatePanelWidget"/>.
/// Provides access to the state key that anchors this panel's identity.
/// </summary>
/// <remarks>
/// This context is created during reconciliation (not during widget building),
/// so the underlying node has already been resolved via identity matching.
/// On first render, a new node is created.
/// </remarks>
public sealed class StatePanelContext : WidgetContext<StatePanelWidget>
{
    private readonly StatePanelNode _node;

    internal StatePanelContext(StatePanelNode node)
    {
        _node = node;
    }

    /// <summary>
    /// The state key this panel is anchored to.
    /// </summary>
    public object StateKey => _node.StateKey;

    /// <summary>
    /// The animation collection for this identity scope.
    /// Animations persist across reconciliation frames for the same state key.
    /// Use <see cref="AnimationCollection.Get{T}"/> to create or retrieve named animators.
    /// </summary>
    public AnimationCollection Animations => _node.Animations;
}
