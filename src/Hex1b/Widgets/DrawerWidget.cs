using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Specifies the edge from which the drawer expands.
/// </summary>
public enum DrawerPosition
{
    /// <summary>
    /// The drawer anchors to the left edge and expands rightward.
    /// </summary>
    Left,
    
    /// <summary>
    /// The drawer anchors to the right edge and expands leftward.
    /// </summary>
    Right,
    
    /// <summary>
    /// The drawer anchors to the top edge and expands downward.
    /// </summary>
    Top,
    
    /// <summary>
    /// The drawer anchors to the bottom edge and expands upward.
    /// </summary>
    Bottom
}

/// <summary>
/// Specifies how the drawer interacts with surrounding content.
/// </summary>
public enum DrawerMode
{
    /// <summary>
    /// The drawer is part of the normal layout flow. When expanded, it pushes adjacent content aside.
    /// </summary>
    Docked,
    
    /// <summary>
    /// The drawer renders above other content without affecting the layout.
    /// </summary>
    Overlay
}

/// <summary>
/// An expandable/collapsible panel widget that can contain arbitrary content.
/// Perfect for sidebars, settings panels, and navigation menus.
/// </summary>
/// <param name="IsExpanded">Whether the drawer is currently expanded.</param>
/// <param name="Header">The widget to display in the header row.</param>
/// <param name="Content">The widget to display when expanded.</param>
public sealed record DrawerWidget(
    bool IsExpanded,
    Hex1bWidget Header,
    Hex1bWidget Content) : Hex1bWidget
{
    /// <summary>
    /// The position of the drawer (which edge it anchors to). Defaults to Left.
    /// </summary>
    internal DrawerPosition Position { get; init; } = DrawerPosition.Left;
    
    /// <summary>
    /// The display mode of the drawer. Defaults to Docked.
    /// Note: Overlay mode requires the drawer to be placed within a ZStack.
    /// </summary>
    internal DrawerMode Mode { get; init; } = DrawerMode.Docked;
    
    /// <summary>
    /// The fixed size when expanded (width for left/right, height for top/bottom).
    /// If null, uses Content size. Defaults to null.
    /// </summary>
    internal int? ExpandedSize { get; init; }
    
    /// <summary>
    /// Handler called when the drawer is toggled.
    /// </summary>
    internal Func<DrawerToggledEventArgs, Task>? ToggleHandler { get; init; }

    /// <summary>
    /// Sets the position of the drawer.
    /// </summary>
    public DrawerWidget WithPosition(DrawerPosition position)
        => this with { Position = position };

    /// <summary>
    /// Sets the display mode of the drawer.
    /// </summary>
    public DrawerWidget WithMode(DrawerMode mode)
        => this with { Mode = mode };

    /// <summary>
    /// Sets the fixed size when expanded (width for left/right, height for top/bottom).
    /// </summary>
    public DrawerWidget WithExpandedSize(int size)
        => this with { ExpandedSize = size };

    /// <summary>
    /// Sets a synchronous toggle handler. Called when the drawer expansion state changes.
    /// </summary>
    public DrawerWidget OnToggle(Action<DrawerToggledEventArgs> handler)
        => this with { ToggleHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous toggle handler. Called when the drawer expansion state changes.
    /// </summary>
    public DrawerWidget OnToggle(Func<DrawerToggledEventArgs, Task> handler)
        => this with { ToggleHandler = handler };
    
    /// <summary>
    /// Sets a simple toggle handler that receives just the new expanded state.
    /// </summary>
    public DrawerWidget OnToggle(Action<bool> handler)
        => this with { ToggleHandler = args => { handler(args.IsExpanded); return Task.CompletedTask; } };

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as DrawerNode ?? new DrawerNode();
        
        // Mark dirty if properties changed
        if (node.IsExpanded != IsExpanded || 
            node.Position != Position || 
            node.Mode != Mode ||
            node.ExpandedSize != ExpandedSize)
        {
            node.MarkDirty();
        }
        
        node.IsExpanded = IsExpanded;
        node.Position = Position;
        node.Mode = Mode;
        node.ExpandedSize = ExpandedSize;
        node.SourceWidget = this;
        
        // Reconcile header and content
        node.Header = await context.ReconcileChildAsync(node.Header, Header, node);
        node.Content = await context.ReconcileChildAsync(node.Content, Content, node);
        
        // Convert the typed event handler to the internal InputBindingActionContext handler
        if (ToggleHandler != null)
        {
            node.ToggleAction = async ctx => 
            {
                var newExpanded = !node.IsExpanded;
                var args = new DrawerToggledEventArgs(this, node, ctx, newExpanded);
                await ToggleHandler(args);
            };
        }
        else
        {
            node.ToggleAction = null;
        }
        
        // Set initial focus only if this is a new node AND we're at the root or parent doesn't manage focus
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

    internal override Type GetExpectedNodeType() => typeof(DrawerNode);
}
