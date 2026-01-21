using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A collapsible panel widget that can be placed at the edge of an HStack or VStack.
/// In collapsed state, it shows minimal content (or nothing). In expanded state, 
/// it shows full content with collapse/dock controls.
/// </summary>
/// <remarks>
/// <para>
/// The drawer supports two rendering modes when expanded:
/// <list type="bullet">
///   <item><description><see cref="DrawerMode.Inline"/> - Pushes adjacent content (like VS Code sidebar)</description></item>
///   <item><description><see cref="DrawerMode.Overlay"/> - Floats above content (like mobile hamburger menus)</description></item>
/// </list>
/// </para>
/// <para>
/// The expansion direction is auto-detected based on the drawer's position in the parent stack.
/// </para>
/// </remarks>
public sealed record DrawerWidget : Hex1bWidget
{
    /// <summary>
    /// Builder function for collapsed state content.
    /// If null, the drawer is invisible when collapsed.
    /// </summary>
    internal Func<WidgetContext<DrawerWidget>, IEnumerable<Hex1bWidget>>? CollapsedContentBuilder { get; init; }
    
    /// <summary>
    /// Builder function for expanded state content.
    /// </summary>
    internal Func<WidgetContext<DrawerWidget>, IEnumerable<Hex1bWidget>>? ExpandedContentBuilder { get; init; }
    
    /// <summary>
    /// Whether the drawer is expanded. When null, the node manages its own state.
    /// When set, the drawer syncs to this value (user-controlled state).
    /// </summary>
    public bool? IsExpanded { get; init; }
    
    /// <summary>
    /// The current rendering mode for the expanded drawer.
    /// </summary>
    public DrawerMode Mode { get; init; } = DrawerMode.Inline;
    
    /// <summary>
    /// Handler called when the drawer expands.
    /// </summary>
    internal Action? ExpandedHandler { get; init; }
    
    /// <summary>
    /// Handler called when the drawer collapses.
    /// </summary>
    internal Action? CollapsedHandler { get; init; }
    
    /// <summary>
    /// Sets the content to display when the drawer is collapsed.
    /// </summary>
    /// <param name="builder">A function that returns the collapsed content widgets.</param>
    public DrawerWidget CollapsedContent(Func<WidgetContext<DrawerWidget>, IEnumerable<Hex1bWidget>> builder)
        => this with { CollapsedContentBuilder = builder };
    
    /// <summary>
    /// Sets the content to display when the drawer is expanded.
    /// </summary>
    /// <param name="builder">A function that returns the expanded content widgets.</param>
    public DrawerWidget ExpandedContent(Func<WidgetContext<DrawerWidget>, IEnumerable<Hex1bWidget>> builder)
        => this with { ExpandedContentBuilder = builder };
    
    /// <summary>
    /// Sets the drawer to expanded state.
    /// </summary>
    /// <param name="expanded">Whether the drawer should be expanded.</param>
    public DrawerWidget Expanded(bool expanded = true)
        => this with { IsExpanded = expanded };
    
    /// <summary>
    /// Sets the drawer to collapsed state.
    /// </summary>
    /// <param name="collapsed">Whether the drawer should be collapsed.</param>
    public DrawerWidget Collapsed(bool collapsed = true)
        => this with { IsExpanded = !collapsed };
    
    /// <summary>
    /// Sets the handler to call when the drawer expands.
    /// </summary>
    public DrawerWidget OnExpanded(Action handler)
        => this with { ExpandedHandler = handler };
    
    /// <summary>
    /// Sets the handler to call when the drawer collapses.
    /// </summary>
    public DrawerWidget OnCollapsed(Action handler)
        => this with { CollapsedHandler = handler };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DrawerNode ?? new DrawerNode();
        
        // Sync expansion state from widget if user is controlling it
        if (IsExpanded.HasValue)
        {
            node.IsExpanded = IsExpanded.Value;
        }
        
        // Store event handlers
        node.ExpandedAction = ExpandedHandler;
        node.CollapsedAction = CollapsedHandler;
        
        // Auto-detect direction from layout context and position
        node.Direction = DetectDirection(context);
        
        // Build content based on current state
        var widgetContext = new WidgetContext<DrawerWidget>();
        
        if (node.IsExpanded)
        {
            // Build expanded content
            if (ExpandedContentBuilder != null)
            {
                var expandedWidgets = ExpandedContentBuilder(widgetContext).ToList();
                // Wrap in a VStack for layout
                var contentWidget = new VStackWidget(expandedWidgets);
                node.Content = await context.ReconcileChildAsync(node.Content, contentWidget, node);
            }
            else
            {
                node.Content = null;
            }
        }
        else
        {
            // Build collapsed content
            if (CollapsedContentBuilder != null)
            {
                var collapsedWidgets = CollapsedContentBuilder(widgetContext).ToList();
                var contentWidget = new VStackWidget(collapsedWidgets);
                node.Content = await context.ReconcileChildAsync(node.Content, contentWidget, node);
            }
            else
            {
                node.Content = null;
            }
        }
        
        return node;
    }
    
    /// <summary>
    /// Detects the expansion direction based on layout axis and child position.
    /// </summary>
    private static DrawerDirection DetectDirection(ReconcileContext context)
    {
        var isHorizontal = context.LayoutAxis == LayoutAxis.Horizontal;
        var isFirst = context.ChildIndex == 0;
        var isLast = context.ChildIndex.HasValue && context.ChildCount.HasValue 
                     && context.ChildIndex.Value == context.ChildCount.Value - 1;
        
        // In HStack: first child expands right, last child expands left
        // In VStack: first child expands down, last child expands up
        // Default to first position if position info not available
        
        if (isHorizontal)
        {
            return isLast && !isFirst ? DrawerDirection.Left : DrawerDirection.Right;
        }
        else
        {
            return isLast && !isFirst ? DrawerDirection.Up : DrawerDirection.Down;
        }
    }

    internal override Type GetExpectedNodeType() => typeof(DrawerNode);
}
