using Hex1b.Layout;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A splitter/divider that separates two panes either horizontally (left/right) or vertically (top/bottom).
/// </summary>
/// <param name="First">The first child widget (left for horizontal, top for vertical).</param>
/// <param name="Second">The second child widget (right for horizontal, bottom for vertical).</param>
/// <param name="FirstSize">The size of the first pane in characters (width for horizontal, height for vertical).</param>
/// <param name="Orientation">The orientation of the splitter (Horizontal or Vertical).</param>
public sealed record SplitterWidget(
    Hex1bWidget First,
    Hex1bWidget Second,
    int FirstSize = 30,
    SplitterOrientation Orientation = SplitterOrientation.Horizontal) : Hex1bWidget
{
    // Legacy property aliases for backward compatibility
    internal Hex1bWidget Left => First;
    internal Hex1bWidget Right => Second;
    internal int LeftWidth => FirstSize;

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as SplitterNode ?? new SplitterNode();
        node.First = context.ReconcileChild(node.First, First, node);
        node.Second = context.ReconcileChild(node.Second, Second, node);
        node.Orientation = Orientation;
        
        // Only set FirstSize on initial creation - preserve user resizing
        if (context.IsNew)
        {
            node.FirstSize = FirstSize;
        }
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus if this is a new node
        if (context.IsNew)
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(SplitterNode);
}
