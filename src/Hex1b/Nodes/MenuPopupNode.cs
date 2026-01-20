using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for menu popup content.
/// Displays the menu items in a bordered box.
/// </summary>
public sealed class MenuPopupNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The MenuNode that owns this popup.
    /// </summary>
    public MenuNode? OwnerNode { get; set; }
    
    /// <summary>
    /// The reconciled child nodes (menu items, separators, submenus).
    /// </summary>
    public List<Hex1bNode> ChildNodes { get; set; } = [];
    
    /// <summary>
    /// The calculated width of menu content (max item width).
    /// </summary>
    private int _contentWidth;

    public override bool IsFocusable => false; // Container, not directly focusable

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
        // Navigation within the menu - use global focus ring
        bindings.Key(Hex1bKey.DownArrow).Action(ctx => ctx.FocusNext(), "Next item");
        bindings.Key(Hex1bKey.UpArrow).Action(ctx => ctx.FocusPrevious(), "Previous item");
        bindings.Key(Hex1bKey.LeftArrow).Action(CloseMenu, "Close menu");
        bindings.Key(Hex1bKey.Escape).Action(CloseMenu, "Close menu");
        
        // Accelerator keys - register for each child
        if (OwnerNode != null)
        {
            foreach (var (child, accel, _) in OwnerNode.ChildAccelerators)
            {
                if (accel.HasValue)
                {
                    var accelerator = accel.Value;
                    bindings.Key(CharToHex1bKey(accelerator)).Action(ctx => ActivateByAccelerator(ctx, accelerator), $"Activate {accelerator}");
                    // Also bind lowercase
                    var lowerAccel = char.ToLowerInvariant(accelerator);
                    if (lowerAccel != accelerator)
                    {
                        bindings.Key(CharToHex1bKey(lowerAccel)).Action(ctx => ActivateByAccelerator(ctx, accelerator), $"Activate {accelerator}");
                    }
                }
            }
        }
    }
    
    private static Hex1bKey CharToHex1bKey(char c)
    {
        return c switch
        {
            >= 'A' and <= 'Z' => (Hex1bKey)((int)Hex1bKey.A + (c - 'A')),
            >= 'a' and <= 'z' => (Hex1bKey)((int)Hex1bKey.A + (c - 'a')),
            >= '0' and <= '9' => (Hex1bKey)((int)Hex1bKey.D0 + (c - '0')),
            _ => Hex1bKey.None
        };
    }
    
    private async Task ActivateByAccelerator(InputBindingActionContext ctx, char accelerator)
    {
        if (OwnerNode == null) return;
        
        // Find the child with this accelerator
        for (int i = 0; i < OwnerNode.ChildAccelerators.Count; i++)
        {
            var (child, accel, _) = OwnerNode.ChildAccelerators[i];
            if (accel == accelerator)
            {
                // Find the corresponding node
                var nodeIndex = GetNodeIndexForChild(i);
                if (nodeIndex >= 0 && nodeIndex < ChildNodes.Count)
                {
                    var node = ChildNodes[nodeIndex];
                    if (node is MenuItemNode itemNode && itemNode.ActivatedAction != null && !itemNode.IsDisabled)
                    {
                        await itemNode.ActivatedAction(ctx);
                    }
                    else if (node is MenuNode menuNode)
                    {
                        // Focus and open submenu using the FocusRing
                        ctx.Focus(menuNode);
                        // The menu will open via its own bindings
                    }
                }
                break;
            }
        }
    }
    
    private int GetNodeIndexForChild(int childIndex)
    {
        // ChildNodes corresponds 1:1 with Children (they're reconciled together)
        return childIndex;
    }
    
    private Task CloseMenu(InputBindingActionContext ctx)
    {
        // Pop this popup and get the focus restore node
        ctx.Popups.Pop(out var focusRestoreNode);
        
        // Mark owner as closed and deselected
        if (OwnerNode != null)
        {
            OwnerNode.IsOpen = false;
            OwnerNode.IsSelected = false;
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

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var child in ChildNodes)
        {
            foreach (var focusable in child.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }
    
    public override IEnumerable<Hex1bNode> GetChildren() => ChildNodes;

    public override Size Measure(Constraints constraints)
    {
        if (OwnerNode == null)
        {
            return constraints.Constrain(new Size(10, 3));
        }
        
        // Ensure children are created (should already be done during reconcile)
        if (ChildNodes.Count == 0)
        {
            ReconcileChildNodes();
        }
        
        // Calculate content width (max item width) and height
        var indicator = " ►"; // Submenu indicator
        _contentWidth = 0;
        var contentHeight = 0;
        
        for (int i = 0; i < OwnerNode.Children.Count; i++)
        {
            var child = OwnerNode.Children[i];
            switch (child)
            {
                case MenuItemWidget item:
                    _contentWidth = Math.Max(_contentWidth, item.Label.Length);
                    contentHeight++;
                    break;
                case MenuWidget submenu:
                    _contentWidth = Math.Max(_contentWidth, submenu.Label.Length + indicator.Length);
                    contentHeight++;
                    break;
                case MenuSeparatorWidget:
                    contentHeight++;
                    break;
            }
        }
        
        // Add padding
        _contentWidth += 2;
        
        // Set render width on children
        foreach (var node in ChildNodes)
        {
            switch (node)
            {
                case MenuItemNode itemNode:
                    itemNode.RenderWidth = _contentWidth;
                    break;
                case MenuNode menuNode:
                    menuNode.RenderWidth = _contentWidth;
                    break;
                case MenuSeparatorNode sepNode:
                    sepNode.RenderWidth = _contentWidth;
                    break;
            }
        }
        
        // Total size: content + 2 for borders
        var totalWidth = _contentWidth + 2;
        var totalHeight = contentHeight + 2;
        
        return constraints.Constrain(new Size(totalWidth, totalHeight));
    }
    
    /// <summary>
    /// Tracks the OwnerNode label to detect when we need to rebuild children.
    /// </summary>
    private string? _lastOwnerLabel;
    
    /// <summary>
    /// Creates child nodes from the owner's menu children.
    /// Called during reconciliation to ensure children are available for focus ring.
    /// </summary>
    internal void ReconcileChildNodes()
    {
        if (OwnerNode == null) return;
        
        // Skip if already built for the same owner (prevents double-creation)
        if (_lastOwnerLabel == OwnerNode.Label && ChildNodes.Count > 0) return;
        
        // Remember which owner we built for
        _lastOwnerLabel = OwnerNode.Label;
        
        // Simple reconciliation: create nodes for each child
        var newChildren = new List<Hex1bNode>();
        
        for (int i = 0; i < OwnerNode.Children.Count; i++)
        {
            var child = OwnerNode.Children[i];
            var (_, accel, accelIndex) = OwnerNode.ChildAccelerators[i];
            
            Hex1bNode node = child switch
            {
                MenuItemWidget item => CreateMenuItemNode(item, accel, accelIndex),
                MenuWidget submenu => CreateSubmenuNode(submenu, accel, accelIndex),
                MenuSeparatorWidget => new MenuSeparatorNode(),
                _ => throw new InvalidOperationException($"Unknown menu child type: {child.GetType()}")
            };
            
            node.Parent = this;
            newChildren.Add(node);
        }
        
        ChildNodes = newChildren;
        
        // Check if there are any focusable items (non-disabled menu items or submenus)
        var hasFocusableItems = ChildNodes.Any(n => n is MenuItemNode { IsDisabled: false } or MenuNode);
        
        // If no focusable items, make the first available child focusable as a fallback
        // This allows the user to navigate out of the menu using arrow keys
        // Priority: separator first (visual divider is obvious), then disabled item
        if (!hasFocusableItems)
        {
            var firstSeparator = ChildNodes.OfType<MenuSeparatorNode>().FirstOrDefault();
            if (firstSeparator != null)
            {
                firstSeparator.IsFallbackFocusable = true;
            }
            else
            {
                // No separator, try disabled menu items
                var firstDisabledItem = ChildNodes.OfType<MenuItemNode>().FirstOrDefault(n => n.IsDisabled);
                if (firstDisabledItem != null)
                {
                    firstDisabledItem.IsFallbackFocusable = true;
                }
            }
        }
        
        // Focus will be set by the popup opening mechanism through the FocusRing
    }
    
    private MenuItemNode CreateMenuItemNode(MenuItemWidget widget, char? accel, int accelIndex)
    {
        var node = new MenuItemNode
        {
            Label = widget.Label,
            IsDisabled = widget.IsDisabled,
            Accelerator = accel,
            AcceleratorIndex = accelIndex,
            SourceWidget = widget
        };
        
        // Set up activated action with auto-close behavior
        if (widget.ActivatedHandler != null)
        {
            node.ActivatedAction = async ctx =>
            {
                var args = new Events.MenuItemActivatedEventArgs(widget, node, ctx);
                await widget.ActivatedHandler(args);
                // Auto-close all menus after activation
                ctx.Popups.Clear();
            };
        }
        else
        {
            // Even without a handler, activation closes the menu
            node.ActivatedAction = ctx =>
            {
                ctx.Popups.Clear();
                return Task.CompletedTask;
            };
        }
        
        return node;
    }
    
    private MenuNode CreateSubmenuNode(MenuWidget widget, char? accel, int accelIndex)
    {
        var node = new MenuNode
        {
            Label = widget.Label,
            Children = widget.Children,
            ChildAccelerators = [],
            Accelerator = accel,
            AcceleratorIndex = accelIndex,
            SourceWidget = widget
        };
        
        // Compute child accelerators for the submenu
        var usedAccels = new HashSet<char>();
        foreach (var child in widget.Children)
        {
            switch (child)
            {
                case MenuWidget sub:
                    ProcessAccelerator(sub.Label, sub.ExplicitAccelerator, sub.AcceleratorIndex, sub.DisableAccelerator, sub, usedAccels, node.ChildAccelerators);
                    break;
                case MenuItemWidget item:
                    ProcessAccelerator(item.Label, item.ExplicitAccelerator, item.AcceleratorIndex, item.DisableAccelerator, item, usedAccels, node.ChildAccelerators);
                    break;
                case MenuSeparatorWidget sep:
                    node.ChildAccelerators.Add((sep, null, -1));
                    break;
            }
        }
        
        return node;
    }
    
    private static void ProcessAccelerator(
        string label,
        char? explicitAccel,
        int explicitIndex,
        bool disableAccel,
        IMenuChild child,
        HashSet<char> usedAccels,
        List<(IMenuChild, char?, int)> result)
    {
        if (disableAccel)
        {
            result.Add((child, null, -1));
            return;
        }
        
        if (explicitAccel.HasValue)
        {
            usedAccels.Add(explicitAccel.Value);
            result.Add((child, explicitAccel, explicitIndex));
            return;
        }
        
        // Auto-assign
        for (int i = 0; i < label.Length; i++)
        {
            var c = label[i];
            if (char.IsLetterOrDigit(c))
            {
                var upper = char.ToUpperInvariant(c);
                if (!usedAccels.Contains(upper))
                {
                    usedAccels.Add(upper);
                    result.Add((child, upper, i));
                    return;
                }
            }
        }
        
        result.Add((child, null, -1));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        
        if (ChildNodes.Count == 0) return;
        
        // Arrange children inside the border (offset by 1 for border)
        var y = bounds.Y + 1;
        foreach (var node in ChildNodes)
        {
            var childBounds = new Rect(bounds.X + 1, y, _contentWidth, 1);
            node.Arrange(childBounds);
            y++;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var bg = theme.Get(MenuTheme.BackgroundColor);
        var fg = theme.Get(MenuTheme.ForegroundColor);
        var borderColor = theme.Get(MenuTheme.BorderColor);
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // Border characters
        var topLeft = theme.Get(MenuTheme.BorderTopLeft);
        var topRight = theme.Get(MenuTheme.BorderTopRight);
        var bottomLeft = theme.Get(MenuTheme.BorderBottomLeft);
        var bottomRight = theme.Get(MenuTheme.BorderBottomRight);
        var horizontal = theme.Get(MenuTheme.BorderHorizontal);
        var vertical = theme.Get(MenuTheme.BorderVertical);
        
        var borderCodes = $"{borderColor.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}";
        var contentCodes = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}";
        
        // Set layout context
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        // Top border
        var topBorder = $"{borderCodes}{topLeft}{new string(horizontal, _contentWidth)}{topRight}{resetToGlobal}";
        context.WriteClipped(Bounds.X, Bounds.Y, topBorder);
        
        // Content rows (children handle their own rendering, but we need side borders)
        for (int i = 0; i < ChildNodes.Count; i++)
        {
            var y = Bounds.Y + 1 + i;
            
            // Left border
            context.WriteClipped(Bounds.X, y, $"{borderCodes}{vertical}{resetToGlobal}");
            
            // Child content (rendered by child)
            ChildNodes[i].Render(context);
            
            // Right border
            context.WriteClipped(Bounds.X + _contentWidth + 1, y, $"{borderCodes}{vertical}{resetToGlobal}");
        }
        
        // Bottom border
        var bottomY = Bounds.Y + ChildNodes.Count + 1;
        var bottomBorder = $"{borderCodes}{bottomLeft}{new string(horizontal, _contentWidth)}{bottomRight}{resetToGlobal}";
        context.WriteClipped(Bounds.X, bottomY, bottomBorder);
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }
}
