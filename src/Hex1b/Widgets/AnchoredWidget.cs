using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget that positions its child relative to an anchor node.
/// Used internally by PopupStack for anchored popups.
/// </summary>
/// <param name="Child">The popup content to position.</param>
/// <param name="AnchorNode">The node to anchor to.</param>
/// <param name="Position">Where to position relative to the anchor.</param>
internal sealed record AnchoredWidget(
    Hex1bWidget Child,
    Hex1bNode AnchorNode,
    AnchorPosition Position) : Hex1bWidget
{
    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as AnchoredNode ?? new AnchoredNode();
        
        node.AnchorNode = AnchorNode;
        node.Position = Position;
        
        // Reconcile the child
        var childContext = context.WithLayoutAxis(LayoutAxis.Vertical);
        node.Child = await childContext.ReconcileChildAsync(node.Child, Child, node);
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(AnchoredNode);
}

/// <summary>
/// Render node for <see cref="AnchoredWidget"/>.
/// Positions its child relative to an anchor node's bounds.
/// </summary>
internal sealed class AnchoredNode : Hex1bNode
{
    public Hex1bNode? Child { get; set; }
    public Hex1bNode? AnchorNode { get; set; }
    public AnchorPosition Position { get; set; }
    
    private Size _childSize;
    
    /// <summary>
    /// Returns the child's bounds for hit testing, not our full layout bounds.
    /// This allows BackdropNode to correctly detect clicks outside the popup content.
    /// </summary>
    public override Rect ContentBounds => Child?.Bounds ?? Bounds;
    
    public override Size Measure(Constraints constraints)
    {
        // Measure child to get its natural size
        if (Child == null) return Size.Zero;
        
        // Use loose constraints since the popup can be any size
        var childConstraints = new Constraints(0, constraints.MaxWidth, 0, constraints.MaxHeight);
        _childSize = Child.Measure(childConstraints);
        
        // Return FULL available size so BackdropNode gives us full bounds
        // (we'll position the child within those bounds in Arrange)
        return new Size(constraints.MaxWidth, constraints.MaxHeight);
    }
    
    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (Child == null || AnchorNode == null) return;
        
        var anchorBounds = AnchorNode.Bounds;
        
        // Calculate position based on anchor and preferred position
        var (x, y) = CalculatePosition(anchorBounds, _childSize, bounds);
        
        // Clamp to screen bounds
        x = Math.Max(0, Math.Min(x, bounds.Width - _childSize.Width));
        y = Math.Max(0, Math.Min(y, bounds.Height - _childSize.Height));
        
        var childBounds = new Rect(x, y, _childSize.Width, _childSize.Height);
        Child.Arrange(childBounds);
    }
    
    private (int x, int y) CalculatePosition(Rect anchor, Size childSize, Rect screenBounds)
    {
        return Position switch
        {
            AnchorPosition.Below => (
                anchor.X,  // Left-aligned with anchor
                anchor.Y + anchor.Height  // Just below anchor
            ),
            
            AnchorPosition.Above => (
                anchor.X,  // Left-aligned with anchor
                anchor.Y - childSize.Height  // Just above anchor
            ),
            
            AnchorPosition.Left => (
                anchor.X - childSize.Width,  // To the left of anchor
                anchor.Y  // Top-aligned with anchor
            ),
            
            AnchorPosition.Right => (
                anchor.X + anchor.Width,  // Right edge of anchor
                anchor.Y  // Top-aligned with anchor
            ),
            
            _ => (anchor.X, anchor.Y + anchor.Height)  // Default to Below
        };
    }
    
    public override void Render(Hex1bRenderContext context)
    {
        if (Child == null) return;
        
        context.SetCursorPosition(Child.Bounds.X, Child.Bounds.Y);
        Child.Render(context);
    }
    
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (Child == null) yield break;
        foreach (var focusable in Child.GetFocusableNodes())
        {
            yield return focusable;
        }
    }
    
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Child != null) yield return Child;
    }
}
