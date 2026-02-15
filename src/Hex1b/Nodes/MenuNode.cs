using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MenuWidget.
/// Context-aware rendering: renders as a trigger in MenuBar, or as a submenu item in a parent Menu.
/// When opened, pushes its content to the PopupStack.
/// </summary>
public sealed class MenuNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The display label for the menu.
    /// </summary>
    public string Label { get; set; } = "";
    
    /// <summary>
    /// The children of this menu.
    /// </summary>
    public IReadOnlyList<IMenuChild> Children { get; set; } = [];
    
    /// <summary>
    /// The computed accelerators for child items.
    /// </summary>
    public List<(IMenuChild Child, char? Accelerator, int Index)> ChildAccelerators { get; set; } = [];
    
    /// <summary>
    /// The accelerator character for this menu (uppercase).
    /// </summary>
    public char? Accelerator { get; set; }
    
    /// <summary>
    /// The index of the accelerator character in the label.
    /// </summary>
    public int AcceleratorIndex { get; set; } = -1;
    
    /// <summary>
    /// The width to render when displayed as a submenu item (set by parent MenuNode).
    /// </summary>
    public int RenderWidth { get; set; }
    
    /// <summary>
    /// Whether this menu is currently open (has pushed content to PopupStack).
    /// </summary>
    public bool IsOpen { get; set; }
    
    /// <summary>
    /// Whether this menu is currently selected (highlighted).
    /// Selection controls visual styling and is separate from focus.
    /// A menu can be selected even when focus moves to its child popup.
    /// </summary>
    public bool IsSelected { get; set; }
    
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public MenuWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// Reconciled child nodes (populated when menu is opened).
    /// </summary>
    public List<Hex1bNode> ChildNodes { get; set; } = [];
    
    /// <summary>
    /// The currently focused child index.
    /// </summary>
    public int FocusedChildIndex { get; set; } = -1;

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    private bool _isHovered;
    public override bool IsHovered
    {
        get => _isHovered;
        set
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;
    
    public override bool ManagesChildFocus => true;

    #region ILayoutProvider Implementation
    
    public Rect ClipRect => Bounds;
    public ClipMode ClipMode { get; set; } = ClipMode.Clip;
    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);
    
    #endregion

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Open menu on Enter/Space/Click
        bindings.Key(Hex1bKey.Enter).Action(OpenMenu, "Open menu");
        bindings.Key(Hex1bKey.Spacebar).Action(OpenMenu, "Open menu");
        bindings.Mouse(MouseButton.Left).Action(OpenMenu, "Open menu");
        
        // When this menu is rendered inside a popup (as a submenu trigger),
        // it needs navigation bindings like a menu item
        if (Parent is MenuPopupNode)
        {
            // Right arrow opens submenu (only in popup context)
            bindings.Key(Hex1bKey.RightArrow).Action(OpenMenu, "Open submenu");
            bindings.Key(Hex1bKey.DownArrow).Action(ctx => ctx.FocusNext(), "Next item");
            bindings.Key(Hex1bKey.UpArrow).Action(ctx => ctx.FocusPrevious(), "Previous item");
            bindings.Key(Hex1bKey.Escape).Action(CloseParentMenu, "Close menu");
            // Left arrow navigates to previous menu in menu bar (same as MenuItemNode)
            bindings.Key(Hex1bKey.LeftArrow).Action(NavigateToPreviousMenu, "Previous menu");
        }
        else
        {
            // When in a menu bar (or any non-popup context), Down arrow opens the menu
            // Left/Right navigate between menus without opening
            bindings.Key(Hex1bKey.DownArrow).Action(OpenMenu, "Open menu");
            bindings.Key(Hex1bKey.LeftArrow).Action(FocusPreviousMenuInBar, "Previous menu");
            bindings.Key(Hex1bKey.RightArrow).Action(FocusNextMenuInBar, "Next menu");
        }
    }
    
    /// <summary>
    /// Navigates to the previous menu in the menu bar, closing the current menu and opening the previous.
    /// </summary>
    private Task NavigateToPreviousMenu(InputBindingActionContext ctx)
    {
        return NavigateToAdjacentMenu(ctx, direction: -1);
    }
    
    /// <summary>
    /// Navigates to an adjacent menu in the menu bar.
    /// </summary>
    /// <param name="ctx">The input binding action context.</param>
    /// <param name="direction">1 for next, -1 for previous.</param>
    private Task NavigateToAdjacentMenu(InputBindingActionContext ctx, int direction)
    {
        // Find the MenuPopupNode that contains this submenu trigger
        var popupNode = FindParentPopupNode();
        if (popupNode == null)
        {
            return Task.CompletedTask;
        }
        
        // Get the owning MenuNode and its MenuBarNode parent
        var ownerNode = popupNode.OwnerNode;
        if (ownerNode == null || ownerNode.Parent is not MenuBarNode menuBar)
        {
            // We're in a nested submenu (not directly under a top-level menu), just close this popup
            return CloseParentMenu(ctx);
        }
        
        // Find the current menu index by label (MenuNodes may be recreated, so we match by label)
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
    
    /// <summary>
    /// Focuses the previous menu in the menu bar without opening it.
    /// </summary>
    private Task FocusPreviousMenuInBar(InputBindingActionContext ctx)
    {
        return FocusAdjacentMenuInBar(ctx, direction: -1);
    }
    
    /// <summary>
    /// Focuses the next menu in the menu bar without opening it.
    /// </summary>
    private Task FocusNextMenuInBar(InputBindingActionContext ctx)
    {
        return FocusAdjacentMenuInBar(ctx, direction: 1);
    }
    
    /// <summary>
    /// Focuses an adjacent menu in the menu bar without opening it.
    /// </summary>
    private Task FocusAdjacentMenuInBar(InputBindingActionContext ctx, int direction)
    {
        // Find the MenuBarNode in parent hierarchy
        var menuBar = FindParentMenuBar();
        if (menuBar == null)
        {
            return Task.CompletedTask;
        }
        
        // Find our index in the menu bar
        var currentIndex = -1;
        for (int i = 0; i < menuBar.MenuNodes.Count; i++)
        {
            if (menuBar.MenuNodes[i] == this)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex < 0 || menuBar.MenuNodes.Count == 0)
        {
            return Task.CompletedTask;
        }
        
        // Calculate target index with wraparound
        var count = menuBar.MenuNodes.Count;
        var targetIndex = (currentIndex + direction + count) % count;
        var targetMenu = menuBar.MenuNodes[targetIndex];
        
        // Just move focus to the target menu (don't open it)
        ctx.FocusWhere(node => node == targetMenu);
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Finds the MenuBarNode in the parent hierarchy.
    /// </summary>
    private MenuBarNode? FindParentMenuBar()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is MenuBarNode menuBar)
                return menuBar;
            current = current.Parent;
        }
        return null;
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
        
        // Restore focus to the designated node
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
    
    private Task OpenMenu(InputBindingActionContext ctx)
    {
        if (IsOpen) return Task.CompletedTask;
        
        IsOpen = true;
        IsSelected = true; // Mark as selected when opened
        MarkDirty();
        
        // Determine anchor position based on context
        var anchorPosition = Parent is MenuBarNode 
            ? AnchorPosition.Below 
            : AnchorPosition.Right;
        
        // Push the menu content to the popup stack with an onDismiss callback to clear state
        ctx.Popups.PushAnchored(this, anchorPosition, () => BuildMenuContent(), 
            focusRestoreNode: this,
            onDismiss: () =>
            {
                IsOpen = false;
                IsSelected = false;
                MarkDirty();
            });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Builds the menu popup content as a widget.
    /// </summary>
    private Hex1bWidget BuildMenuContent()
    {
        return new MenuPopupWidget(this);
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // The menu trigger itself is focusable
        yield return this;
    }
    
    public override IEnumerable<Hex1bNode> GetChildren() => ChildNodes;

    protected override Size MeasureCore(Constraints constraints)
    {
        // When in menu bar: size is label + padding
        // When in submenu: use RenderWidth or label + submenu indicator
        if (RenderWidth > 0)
        {
            return constraints.Constrain(new Size(RenderWidth, 1));
        }
        
        var width = Label.Length + 2; // Padding on each side
        return constraints.Constrain(new Size(width, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // Check if we're rendering as a bar item or submenu item
        var isBarItem = Parent is MenuBarNode;
        
        if (isBarItem)
        {
            RenderAsBarItem(context, theme, resetToGlobal);
        }
        else
        {
            RenderAsSubmenuItem(context, theme, resetToGlobal);
        }
    }
    
    private void RenderAsBarItem(Hex1bRenderContext context, Hex1bTheme theme, string resetToGlobal)
    {
        var text = $" {Label} ";
        var accelUnderline = theme.Get(MenuBarTheme.AcceleratorUnderline);
        
        // Adjust accelerator index for the leading space
        var adjustedIndex = AcceleratorIndex >= 0 ? AcceleratorIndex + 1 : -1;
        
        // Show focused styling when: focused (keyboard nav), selected, or open
        if (IsFocused || IsSelected || IsOpen)
        {
            var fg = theme.Get(MenuBarTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuBarTheme.FocusedBackgroundColor);
            // Use same colors for accelerator, but apply underline
            var output = RenderWithAccelerator(text, adjustedIndex, fg, bg, fg, bg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
        else if (IsHovered)
        {
            // Hovered: subtle gray highlight
            var fg = theme.Get(MenuBarTheme.HoveredForegroundColor);
            var bg = theme.Get(MenuBarTheme.HoveredBackgroundColor);
            // Use same colors for accelerator, but apply underline
            var output = RenderWithAccelerator(text, adjustedIndex, fg, bg, fg, bg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
        else
        {
            // Normal with accelerator
            var fg = theme.Get(MenuBarTheme.ForegroundColor);
            var bg = theme.Get(MenuBarTheme.BackgroundColor);
            var accelFg = theme.Get(MenuBarTheme.AcceleratorForegroundColor);
            var accelBg = theme.Get(MenuBarTheme.AcceleratorBackgroundColor);
            
            var output = RenderWithAccelerator(text, adjustedIndex, fg, bg, accelFg, accelBg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
    }
    
    private void RenderAsSubmenuItem(Hex1bRenderContext context, Hex1bTheme theme, string resetToGlobal)
    {
        var indicator = theme.Get(MenuItemTheme.SubmenuIndicator);
        var width = RenderWidth > 0 ? RenderWidth : Bounds.Width;
        
        // Format: "Label" + padding + indicator
        var labelWithIndicator = Label + indicator;
        var paddedLabel = labelWithIndicator.PadRight(width);
        var accelUnderline = theme.Get(MenuItemTheme.AcceleratorUnderline);
        
        // Use IsSelected for styling in submenus (focus navigates, selection highlights)
        if (IsSelected || IsFocused)
        {
            var fg = theme.Get(MenuItemTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuItemTheme.FocusedBackgroundColor);
            // Use same colors for accelerator, but apply underline
            var output = RenderWithAccelerator(paddedLabel, AcceleratorIndex, fg, bg, fg, bg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
        else if (IsHovered)
        {
            var fg = theme.Get(MenuItemTheme.HoveredForegroundColor);
            var bg = theme.Get(MenuItemTheme.HoveredBackgroundColor);
            // Use same colors for accelerator, but apply underline
            var output = RenderWithAccelerator(paddedLabel, AcceleratorIndex, fg, bg, fg, bg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
        else
        {
            // Normal with accelerator
            var fg = theme.Get(MenuItemTheme.ForegroundColor);
            var bg = theme.Get(MenuItemTheme.BackgroundColor);
            var accelFg = theme.Get(MenuItemTheme.AcceleratorForegroundColor);
            var accelBg = theme.Get(MenuItemTheme.AcceleratorBackgroundColor);
            
            var output = RenderWithAccelerator(paddedLabel, AcceleratorIndex, fg, bg, accelFg, accelBg, accelUnderline, resetToGlobal);
            WriteOutput(context, output);
        }
    }
    
    private void WriteOutput(Hex1bRenderContext context, string output)
    {
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
    
    private static string RenderWithAccelerator(
        string text,
        int accelIndex,
        Hex1bColor fg,
        Hex1bColor bg,
        Hex1bColor accelFg,
        Hex1bColor accelBg,
        bool accelUnderline,
        string resetToGlobal)
    {
        if (accelIndex < 0 || accelIndex >= text.Length)
        {
            return $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}{resetToGlobal}";
        }
        
        var before = text[..accelIndex];
        var accelChar = text[accelIndex];
        var after = text[(accelIndex + 1)..];
        
        var normalCodes = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}";
        
        // When accelBg is default, inherit the normal background color
        var effectiveAccelBg = accelBg.IsDefault ? bg : accelBg;
        var accelCodes = $"{accelFg.ToForegroundAnsi()}{effectiveAccelBg.ToBackgroundAnsi()}";
        if (accelUnderline)
        {
            accelCodes += "\x1b[4m";
        }
        var accelReset = accelUnderline ? "\x1b[24m" : "";
        
        return $"{normalCodes}{before}{accelCodes}{accelChar}{accelReset}{normalCodes}{after}{resetToGlobal}";
    }
}

/// <summary>
/// Internal widget used to render menu popup content.
/// This is pushed to the PopupStack when a menu is opened.
/// </summary>
/// <param name="OwnerNode">The MenuNode that owns this popup.</param>
internal sealed record MenuPopupWidget(MenuNode OwnerNode) : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as MenuPopupNode ?? new MenuPopupNode();
        node.OwnerNode = OwnerNode;
        
        // Create children during reconciliation so they're available for focus ring
        node.ReconcileChildNodes();
        
        node.MarkDirty();
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(MenuPopupNode);
}
