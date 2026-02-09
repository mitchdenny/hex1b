using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies which edge of a DragBarPanel the resize handle appears on.
/// </summary>
public enum DragBarEdge
{
    /// <summary>Handle on the left edge (drag left to grow, right to shrink).</summary>
    Left,
    /// <summary>Handle on the right edge (drag right to grow, left to shrink).</summary>
    Right,
    /// <summary>Handle on the top edge (drag up to grow, down to shrink).</summary>
    Top,
    /// <summary>Handle on the bottom edge (drag down to grow, up to shrink).</summary>
    Bottom
}

/// <summary>
/// A panel widget with a built-in resize handle on one edge.
/// The panel manages its own size state internally — dragging the handle
/// changes the panel's fixed size, triggering re-layout automatically.
/// </summary>
/// <remarks>
/// <para>
/// The handle edge is auto-detected from the parent layout context:
/// in an HStack, the first child gets a handle on the right; the last child
/// gets a handle on the left. Use <see cref="DragBarPanelExtensions.HandleEdge"/>
/// to override.
/// </para>
/// </remarks>
public sealed record DragBarPanelWidget : Hex1bWidget
{
    /// <summary>
    /// The content widget displayed inside the panel.
    /// </summary>
    internal Hex1bWidget? Content { get; init; }
    
    /// <summary>
    /// The initial size of the panel in characters (width or height depending on edge).
    /// When null, the panel starts at the content's intrinsic size.
    /// </summary>
    internal int? InitialSize { get; init; }
    
    /// <summary>
    /// The minimum allowed size in characters. Defaults to 5.
    /// </summary>
    internal int? MinimumSize { get; init; }
    
    /// <summary>
    /// The maximum allowed size in characters. When null, no maximum is enforced.
    /// </summary>
    internal int? MaximumSize { get; init; }
    
    /// <summary>
    /// Explicit edge override. When null, the edge is auto-detected from layout context.
    /// </summary>
    internal DragBarEdge? Edge { get; init; }
    
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DragBarPanelNode ?? new DragBarPanelNode();
        
        // Resolve edge: explicit override or auto-detect from layout context
        node.ResolvedEdge = Edge ?? DetectEdge(context);
        
        // Set size constraints
        node.MinSize = MinimumSize ?? 5;
        node.MaxSize = MaximumSize;
        
        // Set initial size only on creation — preserve user resizing after that
        if (context.IsNew && InitialSize.HasValue)
        {
            node.CurrentSize = InitialSize.Value;
        }
        
        // Reconcile content child
        if (Content != null)
        {
            node.ContentChild = await context.ReconcileChildAsync(node.ContentChild, Content, node);
        }
        else
        {
            node.ContentChild = null;
        }
        
        return node;
    }
    
    /// <summary>
    /// Detects the handle edge based on layout axis and child position.
    /// </summary>
    private static DragBarEdge DetectEdge(ReconcileContext context)
    {
        var isHorizontal = context.LayoutAxis == LayoutAxis.Horizontal;
        var isFirst = context.ChildIndex == 0;
        var isLast = context.ChildIndex.HasValue && context.ChildCount.HasValue 
                     && context.ChildIndex.Value == context.ChildCount.Value - 1;
        
        if (isHorizontal)
        {
            // HStack: first child → handle on Right, last child → handle on Left
            return isLast && !isFirst ? DragBarEdge.Left : DragBarEdge.Right;
        }
        else
        {
            // VStack: first child → handle on Bottom, last child → handle on Top
            return isLast && !isFirst ? DragBarEdge.Top : DragBarEdge.Bottom;
        }
    }

    internal override Type GetExpectedNodeType() => typeof(DragBarPanelNode);
}
