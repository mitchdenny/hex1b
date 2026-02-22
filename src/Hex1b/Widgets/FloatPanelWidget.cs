using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a positioned child within a <see cref="FloatPanelWidget"/>.
/// </summary>
/// <param name="X">The X coordinate (in characters) within the panel.</param>
/// <param name="Y">The Y coordinate (in characters) within the panel.</param>
/// <param name="Widget">The child widget to render at the specified position.</param>
public readonly record struct FloatChild(int X, int Y, Hex1bWidget Widget);

/// <summary>
/// A container that positions children at absolute (x, y) character coordinates.
/// Children float on top of one another — widget order implies z-order
/// (first child is at the bottom, last child is on top).
/// </summary>
/// <remarks>
/// FloatPanel is useful for overlays, HUDs, map markers, and any scenario where
/// widgets need to be placed at arbitrary positions within a container.
/// </remarks>
/// <example>
/// <code>
/// ctx.FloatPanel(f => [
///     f.Place(10, 5, f.Icon("📍")),
///     f.Place(20, 8, f.Text("Hello")),
/// ])
/// </code>
/// </example>
/// <param name="Children">The positioned child widgets.</param>
public sealed record FloatPanelWidget(IReadOnlyList<FloatChild> Children) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as FloatPanelNode ?? new FloatPanelNode();

        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);

        // Track children that will be removed (their bounds need clearing)
        for (int i = Children.Count; i < node.Children.Count; i++)
        {
            var removedChild = node.Children[i];
            if (removedChild.Node.Bounds.Width > 0 && removedChild.Node.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removedChild.Node.Bounds);
            }
        }

        // Reconcile children
        var newChildren = new List<FloatPanelNode.PositionedNode>();
        for (int i = 0; i < Children.Count; i++)
        {
            var floatChild = Children[i];
            var existingChild = i < node.Children.Count ? node.Children[i].Node : null;
            var reconciledChild = await childContext.ReconcileChildAsync(existingChild, floatChild.Widget, node);
            if (reconciledChild != null)
            {
                newChildren.Add(new FloatPanelNode.PositionedNode(floatChild.X, floatChild.Y, reconciledChild));
            }
        }
        node.Children = newChildren;

        // Set initial focus only if this is a new node and parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            var focusables = node.GetFocusableNodes().ToList();
            if (focusables.Count > 0)
            {
                ReconcileContext.SetNodeFocus(focusables[0], true);
            }
        }

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(FloatPanelNode);
}
