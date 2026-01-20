using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MenuSeparatorWidget.
/// Normally non-focusable visual divider, but can accept focus as a fallback
/// when there are no other focusable items in the menu (to allow navigation out).
/// </summary>
public sealed class MenuSeparatorNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public MenuSeparatorWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The width to render (set by parent MenuNode during layout).
    /// </summary>
    public int RenderWidth { get; set; }
    
    /// <summary>
    /// When true, this separator acts as a fallback focus target to allow navigation
    /// out of a menu that has no other focusable items.
    /// </summary>
    public bool IsFallbackFocusable { get; set; }

    public override bool IsFocusable => IsFallbackFocusable;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // When acting as a fallback focus target, provide navigation to escape the menu
        if (!IsFallbackFocusable) return;
        
        // All navigation keys close the menu and return to the trigger
        bindings.Key(Hex1bKey.UpArrow).Action(CloseParentMenu, "Close menu");
        bindings.Key(Hex1bKey.DownArrow).Action(CloseParentMenu, "Close menu");
        bindings.Key(Hex1bKey.LeftArrow).Action(NavigateToPreviousMenu, "Previous menu");
        bindings.Key(Hex1bKey.RightArrow).Action(NavigateToNextMenu, "Next menu");
        bindings.Key(Hex1bKey.Escape).Action(CloseParentMenu, "Close menu");
    }
    
    /// <summary>
    /// Finds the parent MenuPopupNode in the node hierarchy.
    /// </summary>
    private MenuPopupNode? FindParentPopupNode()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is MenuPopupNode popup)
                return popup;
            current = current.Parent;
        }
        return null;
    }
    
    private Task CloseParentMenu(InputBindingActionContext ctx)
    {
        // Pop the current popup and get the focus restore node
        ctx.Popups.Pop(out var focusRestoreNode);
        
        // Find and close the owner menu
        var parent = Parent;
        while (parent != null)
        {
            if (parent is MenuPopupNode popupNode && popupNode.OwnerNode != null)
            {
                popupNode.OwnerNode.IsOpen = false;
                popupNode.OwnerNode.IsSelected = false;
                break;
            }
            parent = parent.Parent;
        }
        
        // Clear current focus and set focus directly on the restore node
        var currentFocused = ctx.FocusedNode;
        if (currentFocused != null)
        {
            currentFocused.IsFocused = false;
        }
        if (focusRestoreNode != null)
        {
            focusRestoreNode.IsFocused = true;
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Navigates to the next menu in the menu bar.
    /// </summary>
    private Task NavigateToNextMenu(InputBindingActionContext ctx)
    {
        return NavigateToAdjacentMenu(ctx, direction: 1);
    }
    
    /// <summary>
    /// Navigates to the previous menu in the menu bar.
    /// </summary>
    private Task NavigateToPreviousMenu(InputBindingActionContext ctx)
    {
        return NavigateToAdjacentMenu(ctx, direction: -1);
    }
    
    /// <summary>
    /// Navigates to an adjacent menu in the menu bar.
    /// </summary>
    private Task NavigateToAdjacentMenu(InputBindingActionContext ctx, int direction)
    {
        // Find the MenuPopupNode that contains this separator
        var popupNode = FindParentPopupNode();
        if (popupNode == null)
        {
            return CloseParentMenu(ctx);
        }
        
        // Get the owning MenuNode and its MenuBarNode parent
        var ownerNode = popupNode.OwnerNode;
        if (ownerNode == null || ownerNode.Parent is not MenuBarNode menuBar)
        {
            // We're in a submenu (not a top-level menu), just close this popup
            return CloseParentMenu(ctx);
        }
        
        // Find the current menu index by label
        var currentIndex = -1;
        for (int i = 0; i < menuBar.MenuNodes.Count; i++)
        {
            if (menuBar.MenuNodes[i].Label == ownerNode.Label)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex < 0)
        {
            return CloseParentMenu(ctx);
        }
        
        // Calculate target index with wraparound
        var count = menuBar.MenuNodes.Count;
        var targetIndex = (currentIndex + direction + count) % count;
        var targetMenu = menuBar.MenuNodes[targetIndex];
        
        // Close the current popup and clear owner state
        ctx.Popups.Pop();
        ownerNode.IsOpen = false;
        ownerNode.IsSelected = false;
        
        // Open the target menu
        targetMenu.IsOpen = true;
        targetMenu.IsSelected = true;
        targetMenu.MarkDirty();
        
        ctx.Popups.PushAnchored(targetMenu, AnchorPosition.Below, 
            () => new MenuPopupWidget(targetMenu), 
            focusRestoreNode: targetMenu,
            onDismiss: () =>
            {
                targetMenu.IsOpen = false;
                targetMenu.IsSelected = false;
                targetMenu.MarkDirty();
            });
        
        return Task.CompletedTask;
    }

    public override Size Measure(Constraints constraints)
    {
        // Separator is 1 row high, uses parent's width
        var width = RenderWidth > 0 ? RenderWidth : constraints.MaxWidth;
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var separatorChar = theme.Get(MenuSeparatorTheme.Character);
        var fgColor = theme.Get(MenuSeparatorTheme.Color);
        var bgColor = theme.Get(MenuSeparatorTheme.BackgroundColor);
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        var width = RenderWidth > 0 ? RenderWidth : Bounds.Width;
        var line = new string(separatorChar, width);
        var output = $"{fgColor.ToForegroundAnsi()}{bgColor.ToBackgroundAnsi()}{line}{resetToGlobal}";
        
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.SetCursorPosition(Bounds.X, Bounds.Y);
            context.Write(output);
        }
    }
}
