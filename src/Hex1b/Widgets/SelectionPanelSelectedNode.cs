using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// One entry in <see cref="SelectionPanelCopyEventArgs.Nodes"/>:
/// describes a single node whose layout intersects the active
/// selection at the moment the user committed the copy.
/// </summary>
public sealed class SelectionPanelSelectedNode
{
    /// <summary>
    /// The node whose layout intersects the selection. May be a
    /// container (e.g., <see cref="VStackNode"/>, <see cref="BorderNode"/>)
    /// or a leaf (e.g., <see cref="TextBlockNode"/>,
    /// <see cref="ButtonNode"/>). Consumers typically pattern-match on
    /// type or use the instance as a stable identity.
    /// </summary>
    public Hex1bNode Node { get; }

    /// <summary>
    /// <c>true</c> when every cell of the node's <c>Bounds</c> falls
    /// inside the selection. Exact for all three selection modes
    /// (<see cref="SelectionMode.Block"/>, <see cref="SelectionMode.Line"/>
    /// and <see cref="SelectionMode.Character"/>), computed from per-row
    /// selection coverage rather than corner sampling, so multi-row
    /// stream selections are handled correctly.
    /// </summary>
    public bool IsFullySelected { get; }

    /// <summary>
    /// Intersection of the node's bounds with the selection, expressed
    /// in node-local coordinates (<c>(0, 0)</c> is the top-left of the
    /// node). Reflects the actual selected cell footprint within the
    /// node, not the selection's bounding box — for example, a node on
    /// the start row of a multi-row character selection will see the
    /// rectangle clamped to the columns from the start point onwards.
    /// </summary>
    public Rect IntersectionInNode { get; }

    /// <summary>
    /// Intersection of the node's bounds with the selection, expressed
    /// in absolute terminal coordinates. Reflects the actual selected
    /// cell footprint within the node (see
    /// <see cref="IntersectionInNode"/>).
    /// </summary>
    public Rect IntersectionInTerminal { get; }

    /// <summary>
    /// Creates a new <see cref="SelectionPanelSelectedNode"/>.
    /// </summary>
    public SelectionPanelSelectedNode(
        Hex1bNode node,
        bool isFullySelected,
        Rect intersectionInNode,
        Rect intersectionInTerminal)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        IsFullySelected = isFullySelected;
        IntersectionInNode = intersectionInNode;
        IntersectionInTerminal = intersectionInTerminal;
    }
}
