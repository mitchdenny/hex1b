using Hex1b.Nodes;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A passthrough widget that fills its bounds with a background color
/// before rendering its child. All layout, focus, and input is delegated
/// to the child unchanged.
/// </summary>
/// <param name="Color">The background color to fill.</param>
/// <param name="Child">The child widget to render on top of the background.</param>
public sealed record BackgroundPanelWidget(Hex1bColor Color, Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as BackgroundPanelNode ?? new BackgroundPanelNode();

        if (!node.Color.Equals(Color))
        {
            node.Color = Color;
            node.MarkDirty();
        }

        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(BackgroundPanelNode);
}
