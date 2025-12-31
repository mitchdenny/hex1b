using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A button widget with rescue-specific styling.
/// </summary>
internal sealed record RescueButtonWidget(string Label, Action ClickAction) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as RescueButtonNode ?? new RescueButtonNode();
        node.Label = Label;
        node.ClickAction = ClickAction;
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(RescueButtonNode);
}
