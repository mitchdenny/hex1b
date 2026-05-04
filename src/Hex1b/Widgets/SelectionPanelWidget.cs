using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A pass-through container that wraps a child widget. The intent (not yet
/// implemented) is to add a copy/select mode that snapshots its contents and
/// allows keyboard- or mouse-driven selection over the rendered cells, in the
/// same style as <see cref="TerminalWidget"/>'s copy mode.
/// </summary>
/// <remarks>
/// At this stage <see cref="SelectionPanelWidget"/> is purely a pass-through:
/// layout, focus, and input are delegated to the child unchanged. The widget
/// exists so that consumers can wrap content in anticipation of future copy
/// behaviour without yet exposing any new API surface.
/// </remarks>
/// <param name="Child">The child widget to wrap.</param>
public sealed record SelectionPanelWidget(Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SelectionPanelNode ?? new SelectionPanelNode();
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SelectionPanelNode);
}
