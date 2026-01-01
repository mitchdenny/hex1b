using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for MenuBarWidget.
/// Horizontal container for top-level menu triggers.
/// </summary>
public sealed class MenuBarNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The top-level menus.
    /// </summary>
    public IReadOnlyList<MenuWidget> Menus { get; set; } = [];
    
    /// <summary>
    /// The computed accelerators for each menu.
    /// </summary>
    public List<(MenuWidget Menu, char? Accelerator, int Index)> MenuAccelerators { get; set; } = [];
    
    /// <summary>
    /// The reconciled menu nodes.
    /// </summary>
    public List<MenuNode> MenuNodes { get; set; } = [];
    
    /// <summary>
    /// The index of the currently focused menu.
    /// </summary>
    private int _focusedIndex = 0;
    
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// </summary>
    public MenuBarWidget? SourceWidget { get; set; }

    public override bool IsFocusable => false; // Container, not directly focusable
    
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
        // Left/Right navigation between menu triggers
        bindings.Key(Hex1bKey.LeftArrow).Action(FocusPreviousMenu, "Previous menu");
        bindings.Key(Hex1bKey.RightArrow).Action(FocusNextMenu, "Next menu");
        
        // Tab navigation
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
        
        // Alt+Key accelerators for each menu
        foreach (var (menu, accel, _) in MenuAccelerators)
        {
            if (accel.HasValue)
            {
                var accelerator = accel.Value;
                bindings.Alt().Key(CharToHex1bKey(accelerator)).Action(ctx => OpenMenuByAccelerator(ctx, accelerator), $"Open {menu.Label}");
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
    
    private Task OpenMenuByAccelerator(InputBindingActionContext ctx, char accelerator)
    {
        for (int i = 0; i < MenuAccelerators.Count; i++)
        {
            var (_, accel, _) = MenuAccelerators[i];
            if (accel == accelerator && i < MenuNodes.Count)
            {
                // Focus and open the menu
                SetFocus(i);
                var menuNode = MenuNodes[i];
                if (!menuNode.IsOpen)
                {
                    menuNode.IsOpen = true;
                    menuNode.MarkDirty();
                    ctx.Popups.PushAnchored(menuNode, AnchorPosition.Below, () => new MenuPopupWidget(menuNode), focusRestoreNode: menuNode);
                }
                break;
            }
        }
        return Task.CompletedTask;
    }
    
    private Task FocusPreviousMenu(InputBindingActionContext ctx)
    {
        if (MenuNodes.Count == 0) return Task.CompletedTask;
        
        var newIndex = (_focusedIndex - 1 + MenuNodes.Count) % MenuNodes.Count;
        SetFocus(newIndex);
        return Task.CompletedTask;
    }
    
    private Task FocusNextMenu(InputBindingActionContext ctx)
    {
        if (MenuNodes.Count == 0) return Task.CompletedTask;
        
        var newIndex = (_focusedIndex + 1) % MenuNodes.Count;
        SetFocus(newIndex);
        return Task.CompletedTask;
    }
    
    private void SetFocus(int index)
    {
        // Clear old focus
        if (_focusedIndex >= 0 && _focusedIndex < MenuNodes.Count)
        {
            MenuNodes[_focusedIndex].IsFocused = false;
        }
        
        _focusedIndex = index;
        
        // Set new focus
        if (_focusedIndex >= 0 && _focusedIndex < MenuNodes.Count)
        {
            MenuNodes[_focusedIndex].IsFocused = true;
        }
        
        MarkDirty();
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        foreach (var menuNode in MenuNodes)
        {
            yield return menuNode;
        }
    }
    
    public override IEnumerable<Hex1bNode> GetChildren() => MenuNodes;

    public override Size Measure(Constraints constraints)
    {
        // Reconcile menu nodes
        ReconcileMenuNodes();
        
        // Sum widths of all menu triggers
        var totalWidth = 0;
        foreach (var node in MenuNodes)
        {
            var size = node.Measure(Constraints.Unbounded);
            totalWidth += size.Width;
        }
        
        return constraints.Constrain(new Size(totalWidth, 1));
    }
    
    private void ReconcileMenuNodes()
    {
        var newNodes = new List<MenuNode>();
        
        for (int i = 0; i < Menus.Count; i++)
        {
            var menu = Menus[i];
            var (_, accel, accelIndex) = MenuAccelerators[i];
            
            // Try to reuse existing node
            MenuNode node;
            if (i < MenuNodes.Count && MenuNodes[i].SourceWidget == menu)
            {
                node = MenuNodes[i];
            }
            else
            {
                node = new MenuNode();
            }
            
            node.Label = menu.Label;
            node.Children = menu.Children;
            node.Accelerator = accel;
            node.AcceleratorIndex = accelIndex;
            node.SourceWidget = menu;
            node.Parent = this;
            
            // Compute child accelerators
            node.ChildAccelerators = [];
            var usedAccels = new HashSet<char>();
            foreach (var child in menu.Children)
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
            
            newNodes.Add(node);
        }
        
        MenuNodes = newNodes;
        
        // Set initial focus if we have menus and none are focused
        if (MenuNodes.Count > 0 && !MenuNodes.Any(n => n.IsFocused))
        {
            _focusedIndex = 0;
            MenuNodes[0].IsFocused = true;
        }
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
        
        if (MenuNodes.Count == 0) return;
        
        // Arrange menu triggers horizontally
        var x = bounds.X;
        foreach (var node in MenuNodes)
        {
            var size = node.Measure(Constraints.Unbounded);
            var nodeBounds = new Rect(x, bounds.Y, size.Width, 1);
            node.Arrange(nodeBounds);
            x += size.Width;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var bg = theme.Get(MenuBarTheme.BackgroundColor);
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        // Set layout context
        var previousLayout = context.CurrentLayoutProvider;
        ParentLayoutProvider = previousLayout;
        context.CurrentLayoutProvider = this;
        
        // Fill background for the entire bar width
        var bgCode = bg.IsDefault ? "" : bg.ToBackgroundAnsi();
        if (!bg.IsDefault && Bounds.Width > 0)
        {
            var fill = new string(' ', Bounds.Width);
            context.WriteClipped(Bounds.X, Bounds.Y, $"{bgCode}{fill}{resetToGlobal}");
        }
        
        // Render menu triggers
        foreach (var node in MenuNodes)
        {
            node.Render(context);
        }
        
        context.CurrentLayoutProvider = previousLayout;
        ParentLayoutProvider = null;
    }
}
