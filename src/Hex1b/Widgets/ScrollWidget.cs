using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A scroll widget that provides scrolling capability for content that exceeds the available space.
/// Only supports one direction at a time (vertical or horizontal).
/// </summary>
public sealed record ScrollWidget : Hex1bWidget
{
    /// <summary>
    /// The child widget to scroll.
    /// </summary>
    public Hex1bWidget Child { get; }
    
    /// <summary>
    /// The scroll state (offset, content size, viewport size).
    /// </summary>
    public ScrollState State { get; }
    
    /// <summary>
    /// The scroll orientation (vertical or horizontal).
    /// </summary>
    public ScrollOrientation Orientation { get; init; }
    
    /// <summary>
    /// Whether to show the scrollbar when content is scrollable.
    /// </summary>
    public bool ShowScrollbar { get; init; }

    /// <summary>
    /// Creates a new ScrollWidget.
    /// </summary>
    /// <param name="child">The child widget to scroll.</param>
    /// <param name="state">The scroll state. If null, a new state is created.</param>
    /// <param name="orientation">The scroll orientation. Defaults to Vertical.</param>
    /// <param name="showScrollbar">Whether to show the scrollbar. Defaults to true.</param>
    public ScrollWidget(
        Hex1bWidget child,
        ScrollState? state = null,
        ScrollOrientation orientation = ScrollOrientation.Vertical,
        bool showScrollbar = true)
    {
        Child = child;
        State = state ?? new ScrollState();
        Orientation = orientation;
        ShowScrollbar = showScrollbar;
    }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ScrollNode ?? new ScrollNode();
        node.Child = context.ReconcileChild(node.Child, Child, node);
        node.State = State;
        node.Orientation = Orientation;
        node.ShowScrollbar = ShowScrollbar;
        
        // Invalidate focus cache since children may have changed
        node.InvalidateFocusCache();
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
        if (context.IsNew && !context.ParentManagesFocus())
        {
            node.SetInitialFocus();
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ScrollNode);
}
