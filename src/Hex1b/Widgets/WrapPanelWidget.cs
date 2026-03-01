using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A layout panel that arranges children sequentially and wraps to the next
/// row (horizontal) or column (vertical) when the available space is exceeded.
/// Works best with uniformly-sized children and is typically placed inside a
/// <see cref="ScrollPanelWidget"/>.
/// </summary>
/// <param name="Children">The child widgets to lay out.</param>
public sealed record WrapPanelWidget(IReadOnlyList<Hex1bWidget> Children) : Hex1bWidget
{
    /// <summary>
    /// The primary layout direction. <see cref="WrapOrientation.Horizontal"/> (default)
    /// lays children left-to-right and wraps top-to-bottom.
    /// <see cref="WrapOrientation.Vertical"/> lays children top-to-bottom and wraps left-to-right.
    /// </summary>
    internal WrapOrientation Orientation { get; init; } = WrapOrientation.Horizontal;

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as WrapPanelNode ?? new WrapPanelNode();

        if (node.Orientation != Orientation)
        {
            node.MarkDirty();
        }
        node.Orientation = Orientation;

        var childContext = context.WithLayoutAxis(
            Orientation == WrapOrientation.Horizontal ? LayoutAxis.Vertical : LayoutAxis.Horizontal);

        // Track removed children bounds
        var childCount = Children.Count;
        for (int i = childCount; i < node.Children.Count; i++)
        {
            var removedChild = node.Children[i];
            if (removedChild.Bounds.Width > 0 && removedChild.Bounds.Height > 0)
            {
                node.AddOrphanedChildBounds(removedChild.Bounds);
            }
        }

        // Reconcile children
        var newChildren = new List<Hex1bNode>();
        for (int i = 0; i < childCount; i++)
        {
            var existingChild = i < node.Children.Count ? node.Children[i] : null;
            var positionedContext = childContext.WithChildPosition(i, childCount);
            var reconciledChild = await positionedContext.ReconcileChildAsync(existingChild, Children[i], node);
            if (reconciledChild != null)
            {
                newChildren.Add(reconciledChild);
            }
        }
        node.Children = newChildren;

        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(WrapPanelNode);
}

/// <summary>
/// Specifies the primary layout direction for a <see cref="WrapPanelWidget"/>.
/// </summary>
public enum WrapOrientation
{
    /// <summary>
    /// Children flow left-to-right and wrap to the next row when the width is exceeded.
    /// </summary>
    Horizontal,

    /// <summary>
    /// Children flow top-to-bottom and wrap to the next column when the height is exceeded.
    /// </summary>
    Vertical,
}
