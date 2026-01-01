using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A visual separator between menu items.
/// Non-focusable and non-interactive.
/// </summary>
public sealed record MenuSeparatorWidget() : Hex1bWidget, IMenuChild
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuSeparatorNode ?? new MenuSeparatorNode();
        node.SourceWidget = this;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuSeparatorNode);
}
