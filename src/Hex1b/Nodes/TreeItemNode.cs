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
    /// Spinner node for loading animation. Reconciled when IsLoading is true.
    /// </summary>
    public SpinnerNode? LoadingSpinnerNode { get; set; }
    
    /// <summary>
    /// Expand indicator node (▶/▼). Null for leaf nodes.
    /// </summary>
    public IconNode? ExpandIndicatorNode { get; set; }
    
    /// <summary>
    /// Checkbox node for multi-select mode. Null when not in multi-select.
    /// </summary>
    public CheckboxNode? CheckboxNode { get; set; }
    
    /// <summary>
    /// User icon node. Null if no icon specified.
    /// </summary>
    public IconNode? UserIconNode { get; set; }
    
    /// <summary>
    /// User data value associated with this item.
    /// </summary>
    internal object? DataValue { get; set; }
    
    /// <summary>
    /// The type of the user data, for runtime validation.
    /// </summary>
    internal Type? DataType { get; set; }
    
    /// <summary>
    /// Gets the typed data associated with this item.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <returns>The data cast to the specified type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no data is set.</exception>
    /// <exception cref="InvalidCastException">Thrown if the data type doesn't match.</exception>
    /// <example>
    /// <code>
    /// var server = node.GetData&lt;Server&gt;();
    /// </code>
    /// </example>
    public T GetData<T>()
    {
        if (DataValue == null && DataType == null)
        {
            throw new InvalidOperationException("No data has been set on this tree item. Use Data<T>() when creating the item.");
        }
        
        if (DataType != null && DataType != typeof(T))
        {
            throw new InvalidCastException($"Tree item data is of type '{DataType.Name}', not '{typeof(T).Name}'.");
        }
        
        return (T)DataValue!;
    }
    
    /// <summary>
    /// Tries to get the typed data associated with this item.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="data">The data if successful, default otherwise.</param>
    /// <returns>True if data exists and matches the type, false otherwise.</returns>
    public bool TryGetData<T>([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T data)
    {
        if (DataType == typeof(T) && DataValue != null)
        {
            data = (T)DataValue;
            return true;
        }
        
        data = default;
        return false;
    }
    
    /// <summary>
    /// Gets the typed data, or a default value if not set or wrong type.
    /// </summary>
    /// <typeparam name="T">The expected type of the data.</typeparam>
    /// <param name="defaultValue">The default value to return if data is not available.</param>
    /// <returns>The data or the default value.</returns>
    public T? GetDataOrDefault<T>(T? defaultValue = default)
    {
        if (DataType == typeof(T) && DataValue != null)
        {
            return (T)DataValue;
        }
        return defaultValue;
    }
    
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
    internal Func<InputBindingActionContext, Task>? ClickCallback { get; set; }
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

    protected override Size MeasureCore(Constraints constraints)
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

    /// <summary>
    /// Computes the selection state for cascade selection mode.
    /// Returns Selected if this item and all descendants are selected,
    /// Indeterminate if some descendants are selected, None otherwise.
    /// </summary>
    public TreeSelectionState ComputeSelectionState()
    {
        if (Children.Count == 0)
        {
            // Leaf node - simple boolean state
            return IsSelected ? TreeSelectionState.Selected : TreeSelectionState.None;
        }

        // Parent node - check children
        var hasSelected = false;
        var hasUnselected = false;

        foreach (var child in Children)
        {
            var childState = child.ComputeSelectionState();
            if (childState == TreeSelectionState.Indeterminate)
            {
                // If any child is indeterminate, parent is indeterminate
                return TreeSelectionState.Indeterminate;
            }
            if (childState == TreeSelectionState.Selected)
            {
                hasSelected = true;
            }
            else
            {
                hasUnselected = true;
            }

            if (hasSelected && hasUnselected)
            {
                // Mixed selection - indeterminate
                return TreeSelectionState.Indeterminate;
            }
        }

        if (hasSelected && !hasUnselected)
        {
            return TreeSelectionState.Selected;
        }
        
        return TreeSelectionState.None;
    }

    /// <summary>
    /// Sets the selection state for this item and all descendants (for cascade selection).
    /// </summary>
    public void SetSelectionCascade(bool selected)
    {
        IsSelected = selected;
        foreach (var child in Children)
        {
            child.SetSelectionCascade(selected);
        }
    }
    
    /// <summary>
    /// Gets the composed child nodes (indicator, checkbox, icon) for hit testing.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (LoadingSpinnerNode != null) yield return LoadingSpinnerNode;
        else if (ExpandIndicatorNode != null) yield return ExpandIndicatorNode;
        if (CheckboxNode != null) yield return CheckboxNode;
        if (UserIconNode != null) yield return UserIconNode;
    }
    
    /// <summary>
    /// Gets focusable nodes in this item's composed children.
    /// The checkbox is focusable for click handling.
    /// </summary>
    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // TreeItemNode itself is not directly focusable by users - 
        // TreeNode manages focus. But we expose composed children.
        // Only expose CheckboxNode for hit testing so clicks route to it.
        if (CheckboxNode != null)
        {
            yield return CheckboxNode;
        }
    }
    
    /// <summary>
    /// Arranges the composed child nodes with real bounds.
    /// Called by TreeNode during arrangement.
    /// </summary>
    /// <param name="x">Starting X position for this item's content (after guides).</param>
    /// <param name="y">Y position for this item.</param>
    /// <param name="theme">Theme for measuring elements.</param>
    public void ArrangeComposedNodes(int x, int y, Theming.Hex1bTheme theme)
    {
        var currentX = x;
        
        // Indicator (spinner or expand icon)
        if (LoadingSpinnerNode != null)
        {
            LoadingSpinnerNode.Arrange(new Layout.Rect(currentX, y, 2, 1));
            currentX += 2; // spinner + space
        }
        else if (ExpandIndicatorNode != null)
        {
            ExpandIndicatorNode.Arrange(new Layout.Rect(currentX, y, 2, 1));
            currentX += 2; // icon + space
        }
        // Leaf nodes have no indicator - nothing added
        
        // Checkbox - only the visual box (3 chars), trailing space is tree layout
        if (CheckboxNode != null)
        {
            CheckboxNode.Arrange(new Layout.Rect(currentX, y, 3, 1));
            currentX += 4; // "[x] " - space handled by tree render
        }
        
        // User icon
        if (UserIconNode != null)
        {
            var iconWidth = UserIconNode.Icon != null ? DisplayWidth.GetStringWidth(UserIconNode.Icon) + 1 : 0;
            UserIconNode.Arrange(new Layout.Rect(currentX, y, iconWidth, 1));
            currentX += iconWidth;
        }
        
        // Store the label start position for potential future use
        // (not strictly needed but useful for debugging)
    }
}
