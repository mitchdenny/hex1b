using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Represents a reconciled float child with its positioning information.
/// </summary>
public sealed class FloatEntry
{
    /// <summary>The reconciled child node.</summary>
    public required Hex1bNode Node { get; set; }

    /// <summary>Absolute X. Null if using anchor alignment.</summary>
    public int? AbsoluteX { get; set; }

    /// <summary>Absolute Y. Null if using anchor alignment.</summary>
    public int? AbsoluteY { get; set; }

    /// <summary>The anchor node for horizontal alignment.</summary>
    public Hex1bNode? HorizontalAnchor { get; set; }

    /// <summary>How to align horizontally.</summary>
    public FloatHorizontalAlignment HorizontalAlignment { get; set; }

    /// <summary>Horizontal offset.</summary>
    public int HorizontalOffset { get; set; }

    /// <summary>The anchor node for vertical alignment.</summary>
    public Hex1bNode? VerticalAnchor { get; set; }

    /// <summary>How to align vertically.</summary>
    public FloatVerticalAlignment VerticalAlignment { get; set; }

    /// <summary>Vertical offset.</summary>
    public int VerticalOffset { get; set; }

    /// <summary>Unresolved anchor widget reference for horizontal alignment (resolved after flow reconciliation).</summary>
    internal Hex1bWidget? HorizontalAnchorWidget { get; set; }

    /// <summary>Unresolved anchor widget reference for vertical alignment (resolved after flow reconciliation).</summary>
    internal Hex1bWidget? VerticalAnchorWidget { get; set; }
}

/// <summary>
/// Shared logic for containers that support <see cref="FloatWidget"/> children.
/// Handles reconciliation, arrangement, rendering, and focus traversal of floated children.
/// </summary>
public static class FloatLayoutHelper
{
    /// <summary>
    /// Separates children into flow and float widgets, reconciles float children,
    /// and stores anchor widget references for later resolution.
    /// </summary>
    /// <param name="allChildren">All children from the container widget.</param>
    /// <param name="existingFloats">Previously reconciled float entries (for node reuse).</param>
    /// <param name="context">The reconciliation context.</param>
    /// <param name="parentNode">The parent container node.</param>
    /// <param name="widgetToNode">
    /// Map from widget instances to reconciled nodes — populated by the caller for flow children,
    /// used here to resolve anchor references.
    /// </param>
    /// <returns>
    /// A tuple of (flowChildren, floatEntries) where float entries have unresolved anchors.
    /// Call <see cref="ResolveAnchors"/> after flow children are reconciled to resolve anchor references.
    /// </returns>
    public static async Task<(List<Hex1bWidget> FlowChildren, List<FloatEntry> Floats)>
        ReconcileFloatsAsync(
            IReadOnlyList<Hex1bWidget> allChildren,
            List<FloatEntry> existingFloats,
            ReconcileContext context,
            Hex1bNode parentNode,
            Dictionary<Hex1bWidget, Hex1bNode> widgetToNode)
    {
        var flowChildren = new List<Hex1bWidget>();
        var floatWidgets = new List<(int Index, FloatWidget Float)>();

        // Pass 1: separate flow from float children
        for (int i = 0; i < allChildren.Count; i++)
        {
            if (allChildren[i] is FloatWidget fw)
            {
                floatWidgets.Add((i, fw));
            }
            else
            {
                flowChildren.Add(allChildren[i]);
            }
        }

        // If no floats, return early
        if (floatWidgets.Count == 0)
        {
            return (flowChildren, []);
        }

        // Pass 2: reconcile float children and store anchor widget refs
        var floatEntries = new List<FloatEntry>();
        for (int i = 0; i < floatWidgets.Count; i++)
        {
            var (_, fw) = floatWidgets[i];
            var existingChild = i < existingFloats.Count ? existingFloats[i].Node : null;
            var reconciledChild = await context.ReconcileChildAsync(existingChild, fw.Child, parentNode);

            if (reconciledChild != null)
            {
                var entry = new FloatEntry
                {
                    Node = reconciledChild,
                    AbsoluteX = fw.AbsoluteX,
                    AbsoluteY = fw.AbsoluteY,
                    HorizontalAlignment = fw.HorizontalAlignment,
                    HorizontalOffset = fw.HorizontalOffset,
                    VerticalAlignment = fw.VerticalAlignment,
                    VerticalOffset = fw.VerticalOffset,
                    // Store widget refs for later resolution
                    HorizontalAnchorWidget = fw.HorizontalAnchor,
                    VerticalAnchorWidget = fw.VerticalAnchor,
                };

                floatEntries.Add(entry);
            }
        }

        return (flowChildren, floatEntries);
    }

    /// <summary>
    /// Resolves anchor widget references to their reconciled nodes.
    /// Must be called AFTER flow children have been reconciled and added to <paramref name="widgetToNode"/>.
    /// </summary>
    public static void ResolveAnchors(List<FloatEntry> floats, Dictionary<Hex1bWidget, Hex1bNode> widgetToNode)
    {
        foreach (var entry in floats)
        {
            if (entry.HorizontalAnchorWidget != null)
            {
                entry.HorizontalAnchor = ResolveAnchor(entry.HorizontalAnchorWidget, widgetToNode);
            }
            if (entry.VerticalAnchorWidget != null)
            {
                entry.VerticalAnchor = ResolveAnchor(entry.VerticalAnchorWidget, widgetToNode);
            }
        }
    }

    /// <summary>
    /// Builds a declaration-order list of all children (flow + float) for focus traversal.
    /// Must be called AFTER flow children have been reconciled and added to <paramref name="widgetToNode"/>.
    /// </summary>
    public static List<Hex1bNode> BuildDeclarationOrder(
        IReadOnlyList<Hex1bWidget> allChildren,
        List<FloatEntry> floatEntries,
        Dictionary<Hex1bWidget, Hex1bNode> widgetToNode)
    {
        var allInOrder = new List<Hex1bNode>();
        int floatIdx = 0;
        for (int i = 0; i < allChildren.Count; i++)
        {
            if (allChildren[i] is FloatWidget)
            {
                if (floatIdx < floatEntries.Count)
                {
                    allInOrder.Add(floatEntries[floatIdx].Node);
                    floatIdx++;
                }
            }
            else
            {
                if (widgetToNode.TryGetValue(allChildren[i], out var node))
                {
                    allInOrder.Add(node);
                }
            }
        }
        return allInOrder;
    }

    /// <summary>
    /// Arranges all float entries after the flow layout has completed.
    /// </summary>
    public static void ArrangeFloats(List<FloatEntry> floats, Rect containerBounds)
    {
        foreach (var entry in floats)
        {
            var childSize = entry.Node.Measure(Constraints.Loose(containerBounds.Size));

            int x = containerBounds.X;
            int y = containerBounds.Y;

            // Resolve horizontal position
            if (entry.AbsoluteX.HasValue)
            {
                x = containerBounds.X + entry.AbsoluteX.Value;
            }
            else if (entry.HorizontalAnchor != null)
            {
                var anchor = entry.HorizontalAnchor.Bounds;
                x = entry.HorizontalAlignment switch
                {
                    FloatHorizontalAlignment.AlignLeft => anchor.X,
                    FloatHorizontalAlignment.AlignRight => anchor.X + anchor.Width - childSize.Width,
                    FloatHorizontalAlignment.ExtendRight => anchor.X + anchor.Width,
                    FloatHorizontalAlignment.ExtendLeft => anchor.X - childSize.Width,
                    _ => containerBounds.X,
                };
                x += entry.HorizontalOffset;
            }

            // Resolve vertical position
            if (entry.AbsoluteY.HasValue)
            {
                y = containerBounds.Y + entry.AbsoluteY.Value;
            }
            else if (entry.VerticalAnchor != null)
            {
                var anchor = entry.VerticalAnchor.Bounds;
                y = entry.VerticalAlignment switch
                {
                    FloatVerticalAlignment.AlignTop => anchor.Y,
                    FloatVerticalAlignment.AlignBottom => anchor.Y + anchor.Height - childSize.Height,
                    FloatVerticalAlignment.ExtendBottom => anchor.Y + anchor.Height,
                    FloatVerticalAlignment.ExtendTop => anchor.Y - childSize.Height,
                    _ => containerBounds.Y,
                };
                y += entry.VerticalOffset;
            }

            entry.Node.Arrange(new Rect(x, y, childSize.Width, childSize.Height));
        }
    }

    /// <summary>
    /// Renders float entries (called after rendering flow children).
    /// </summary>
    public static void RenderFloats(List<FloatEntry> floats, Hex1bRenderContext context)
    {
        foreach (var entry in floats)
        {
            context.RenderChild(entry.Node);
        }
    }

    private static Hex1bNode? ResolveAnchor(Hex1bWidget anchorWidget, Dictionary<Hex1bWidget, Hex1bNode> widgetToNode)
    {
        // Try reference equality first (most common — same variable)
        if (widgetToNode.TryGetValue(anchorWidget, out var node))
        {
            return node;
        }

        // Fall back to record equality (same content)
        foreach (var (widget, n) in widgetToNode)
        {
            if (widget.Equals(anchorWidget))
            {
                return n;
            }
        }

        return null;
    }
}
