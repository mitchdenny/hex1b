using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A splitter/divider that separates two panes either horizontally (left/right) or vertically (top/bottom).
/// Each pane is implicitly wrapped in a LayoutWidget for proper clipping.
/// </summary>
public sealed record SplitterWidget : Hex1bWidget
{
    /// <summary>
    /// The first child widget (left for horizontal, top for vertical).
    /// Wrapped in a LayoutWidget for clipping.
    /// </summary>
    public Hex1bWidget First { get; }
    
    /// <summary>
    /// The second child widget (right for horizontal, bottom for vertical).
    /// Wrapped in a LayoutWidget for clipping.
    /// </summary>
    public Hex1bWidget Second { get; }
    
    /// <summary>
    /// The size of the first pane in characters (width for horizontal, height for vertical).
    /// </summary>
    public int FirstSize { get; init; }
    
    /// <summary>
    /// The orientation of the splitter (Horizontal or Vertical).
    /// </summary>
    public SplitterOrientation Orientation { get; init; }

    /// <summary>
    /// Creates a new SplitterWidget with the specified children.
    /// Children are automatically wrapped in LayoutWidgets for proper clipping.
    /// </summary>
    /// <param name="first">The first child widget (left for horizontal, top for vertical).</param>
    /// <param name="second">The second child widget (right for horizontal, bottom for vertical).</param>
    /// <param name="firstSize">The size of the first pane in characters.</param>
    /// <param name="orientation">The orientation of the splitter.</param>
    public SplitterWidget(
        Hex1bWidget first,
        Hex1bWidget second,
        int firstSize = 30,
        SplitterOrientation orientation = SplitterOrientation.Horizontal)
    {
        // Implicitly wrap children in LayoutWidgets for clipping
        First = new LayoutWidget(first, ClipMode.Clip);
        Second = new LayoutWidget(second, ClipMode.Clip);
        FirstSize = firstSize;
        Orientation = orientation;
    }

    // Legacy property aliases for backward compatibility
    internal Hex1bWidget Left => First;
    internal Hex1bWidget Right => Second;
    internal int LeftWidth => FirstSize;

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SplitterNode ?? new SplitterNode();
        node.First = await context.ReconcileChildAsync(node.First, First, node);
        node.Second = await context.ReconcileChildAsync(node.Second, Second, node);
        node.Orientation = Orientation;
        
        // Only set FirstSize on initial creation - preserve user resizing
        if (context.IsNew)
        {
            node.FirstSize = FirstSize;
        }
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        // This prevents nested splitters from each calling SetInitialFocus and leaving multiple nodes focused
        if (context.IsNew && !context.ParentManagesFocus())
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SplitterNode);
}
