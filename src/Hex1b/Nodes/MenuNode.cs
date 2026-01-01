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
            bindings.Key(Hex1bKey.LeftArrow).Action(CloseParentMenu, "Close menu");
        }
        else
        {
            // When in a menu bar, Down arrow opens the menu (standard menu bar behavior)
            // Right/Left arrow navigation is handled by MenuBarNode
            bindings.Key(Hex1bKey.DownArrow).Action(OpenMenu, "Open menu");
        }
    }
    
    private Task CloseParentMenu(InputBindingActionContext ctx)
    {
        // Pop the current popup
        ctx.Popups.Pop();
        
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
        
        // Push the menu content to the popup stack
        ctx.Popups.PushAnchored(this, anchorPosition, () => BuildMenuContent(), focusRestoreNode: this);
        
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

    public override Size Measure(Constraints constraints)
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
        
        // Use IsSelected or IsOpen for styling in menu bar, not IsFocused
        // This prevents highlighting just because the menu bar has keyboard focus
        if (IsSelected || IsOpen)
        {
            var fg = theme.Get(MenuBarTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuBarTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else if (IsHovered)
        {
            var fg = theme.Get(MenuBarTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuBarTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else
        {
            // Normal with accelerator
            var fg = theme.Get(MenuBarTheme.ForegroundColor);
            var bg = theme.Get(MenuBarTheme.BackgroundColor);
            var accelFg = theme.Get(MenuBarTheme.AcceleratorForegroundColor);
            var accelBg = theme.Get(MenuBarTheme.AcceleratorBackgroundColor);
            var accelUnderline = theme.Get(MenuBarTheme.AcceleratorUnderline);
            
            // Adjust accelerator index for the leading space
            var adjustedIndex = AcceleratorIndex >= 0 ? AcceleratorIndex + 1 : -1;
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
        
        // Use IsSelected for styling in submenus (focus navigates, selection highlights)
        if (IsSelected || IsFocused)
        {
            var fg = theme.Get(MenuItemTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuItemTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{paddedLabel}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else if (IsHovered)
        {
            var fg = theme.Get(MenuItemTheme.FocusedForegroundColor);
            var bg = theme.Get(MenuItemTheme.FocusedBackgroundColor);
            var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{paddedLabel}{resetToGlobal}";
            WriteOutput(context, output);
        }
        else
        {
            // Normal with accelerator
            var fg = theme.Get(MenuItemTheme.ForegroundColor);
            var bg = theme.Get(MenuItemTheme.BackgroundColor);
            var accelFg = theme.Get(MenuItemTheme.AcceleratorForegroundColor);
            var accelBg = theme.Get(MenuItemTheme.AcceleratorBackgroundColor);
            var accelUnderline = theme.Get(MenuItemTheme.AcceleratorUnderline);
            
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
        var accelCodes = $"{accelFg.ToForegroundAnsi()}{accelBg.ToBackgroundAnsi()}";
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
