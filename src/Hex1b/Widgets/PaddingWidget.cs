using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that adds invisible padding around its child content.
/// </summary>
/// <example>
/// <code>
/// ctx.Padding(1, 1, 0, 0, p => p.Text("Indented text"))
/// </code>
/// </example>
public sealed record PaddingWidget(int Left, int Right, int Top, int Bottom, Hex1bWidget Child) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as PaddingNode ?? new PaddingNode();

        node.Left = Left;
        node.Right = Right;
        node.Top = Top;
        node.Bottom = Bottom;
        node.Child = await context.ReconcileChildAsync(node.Child, Child, node);

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(PaddingNode);
}
