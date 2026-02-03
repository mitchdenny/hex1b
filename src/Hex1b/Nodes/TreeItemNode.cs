using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Render node for a single tree item.
/// Managed by the parent TreeNode which handles tree structure and navigation.
/// </summary>
public sealed class TreeItemNode : Hex1bNode
{
    /// <summary>
    /// The display label for this item.
    /// </summary>
    public string Label { get; set; } = "";
    
    /// <summary>
    /// Optional icon/emoji prefix displayed before the label.
    /// </summary>
    public string? Icon { get; set; }
    
    /// <summary>
    /// Child tree item nodes.
    /// </summary>
    public IReadOnlyList<TreeItemNode> Children { get; set; } = [];
    
    /// <summary>
    /// Whether this item is expanded to show children.
    /// </summary>
    private bool _isExpanded;
    public bool IsExpanded 
    { 
        get => _isExpanded; 
        set 
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Whether this item is selected (for multi-select mode).
    /// </summary>
    private bool _isSelected;
    public bool IsSelected 
    { 
        get => _isSelected; 
        set 
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                MarkDirty();
            }
        }
    }
    
    /// <summary>
    /// Whether this item has children (actual or hinted for lazy loading).
    /// </summary>
    public bool HasChildren { get; set; }
    
    /// <summary>
    /// Whether children are currently being loaded (async lazy load in progress).
    /// </summary>
    public bool IsLoading { get; set; }
    
    /// <summary>
    /// User data associated with this item.
    /// </summary>
    public object? Tag { get; set; }
    
    /// <summary>
    /// The source widget for this node.
    /// </summary>
    public TreeItemWidget? SourceWidget { get; set; }
    
    /// <summary>
    /// The depth of this item in the tree (0 = root level).
    /// Set by the parent TreeNode during arrangement.
    /// </summary>
    public int Depth { get; set; }
    
    /// <summary>
    /// Whether this is the last child at its level.
    /// Used by TreeNode to draw correct guide lines.
    /// </summary>
    public bool IsLastChild { get; set; }
    
    /// <summary>
    /// Array tracking whether ancestors at each depth are last children.
    /// Used by TreeNode to draw correct vertical guide lines.
    /// </summary>
    public bool[] IsLastAtDepth { get; set; } = [];

    // Focus tracking
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

    // Callbacks set by the parent TreeNode
    internal Func<InputBindingActionContext, Task>? ActivateCallback { get; set; }
    internal Func<InputBindingActionContext, Task>? ToggleExpandCallback { get; set; }
    internal Func<InputBindingActionContext, Task>? ToggleSelectCallback { get; set; }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Enter to activate
        bindings.Key(Hex1bKey.Enter).Action(ctx => ActivateCallback?.Invoke(ctx) ?? Task.CompletedTask, "Activate item");
        
        // Space to toggle selection (in multi-select mode) or expand/collapse
        bindings.Key(Hex1bKey.Spacebar).Action(ctx => 
        {
            if (ToggleSelectCallback != null)
                return ToggleSelectCallback(ctx);
            return ToggleExpandCallback?.Invoke(ctx) ?? Task.CompletedTask;
        }, "Toggle selection/expand");
        
        // Left to collapse (or move to parent - handled by TreeNode)
        bindings.Key(Hex1bKey.LeftArrow).Action(ctx => 
        {
            if (IsExpanded && HasChildren)
                return ToggleExpandCallback?.Invoke(ctx) ?? Task.CompletedTask;
            return Task.CompletedTask; // TreeNode handles moving to parent
        }, "Collapse");
        
        // Right to expand (or move to first child - handled by TreeNode)
        bindings.Key(Hex1bKey.RightArrow).Action(ctx =>
        {
            if (!IsExpanded && HasChildren)
                return ToggleExpandCallback?.Invoke(ctx) ?? Task.CompletedTask;
            return Task.CompletedTask; // TreeNode handles moving to child
        }, "Expand");
    }

    public override Size Measure(Constraints constraints)
    {
        // TreeItemNode doesn't measure itself directly - 
        // TreeNode handles the overall tree layout.
        // This returns the size of just this item's content (icon + label).
        // Use DisplayWidth for proper emoji/wide character handling.
        var iconWidth = Icon != null ? DisplayWidth.GetStringWidth(Icon) + 1 : 0; // +1 for space after icon
        var labelWidth = DisplayWidth.GetStringWidth(Label);
        return constraints.Constrain(new Size(iconWidth + labelWidth, 1));
    }

    public override void Render(Hex1bRenderContext context)
    {
        // TreeItemNode doesn't render itself directly -
        // TreeNode handles rendering with proper indentation and guides.
        // This is a fallback that just renders the label.
        var theme = context.Theme;
        var fg = IsFocused 
            ? theme.Get(TreeTheme.FocusedForegroundColor) 
            : theme.Get(TreeTheme.ForegroundColor);
        var bg = IsFocused 
            ? theme.Get(TreeTheme.FocusedBackgroundColor) 
            : theme.Get(TreeTheme.BackgroundColor);
        
        var text = Icon != null ? $"{Icon} {Label}" : Label;
        var output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}{theme.GetResetToGlobalCodes()}";
        
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
    
    /// <summary>
    /// Gets the display text for this item (icon + label).
    /// </summary>
    public string GetDisplayText()
    {
        return Icon != null ? $"{Icon} {Label}" : Label;
    }
    
    /// <summary>
    /// Determines if this item can be expanded (has children or hints at having children).
    /// </summary>
    public bool CanExpand => HasChildren || Children.Count > 0;
}
