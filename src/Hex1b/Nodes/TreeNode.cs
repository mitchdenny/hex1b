using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Container node that manages tree layout, scrolling, and guide rendering.
/// </summary>
public sealed class TreeNode : Hex1bNode
{
    /// <summary>
    /// The root tree item nodes.
    /// </summary>
    public IReadOnlyList<TreeItemNode> Items { get; set; } = [];

    /// <summary>
    /// Whether multiple items can be selected.
    /// </summary>
    public bool MultiSelect { get; set; }

    /// <summary>
    /// Whether selecting a parent cascades to all children,
    /// and partial child selection shows indeterminate state.
    /// </summary>
    public bool CascadeSelection { get; set; }

    /// <summary>
    /// The style of guide lines.
    /// </summary>
    public TreeGuideStyle GuideStyle { get; set; }

    /// <summary>
    /// The source widget for this node.
    /// </summary>
    public TreeWidget? SourceWidget { get; set; }

    /// <summary>
    /// Flattened list of visible tree items for rendering and navigation.
    /// </summary>
    internal List<FlattenedTreeEntry> FlattenedItems { get; } = new();

    /// <summary>
    /// Index of the currently focused item in the flattened view.
    /// </summary>
    private int _focusedIndex;

    /// <summary>
    /// Scroll offset (index of first visible item).
    /// </summary>
    private int _scrollOffset;

    /// <summary>
    /// Number of visible rows in the viewport.
    /// </summary>
    private int _viewportHeight;

    // Event callbacks
    internal Func<InputBindingActionContext, Task>? SelectionChangedAction { get; set; }
    internal Func<InputBindingActionContext, TreeItemNode, Task>? ItemActivatedAction { get; set; }

    // Focus state - TreeNode itself is focusable
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
    
    // TreeNode manages internal focus visually, but is itself the focusable unit
    public override bool ManagesChildFocus => false;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        bindings.Key(Hex1bKey.UpArrow).Action(MoveFocusUp, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveFocusDown, "Move down");
        bindings.Key(Hex1bKey.LeftArrow).Action(HandleLeft, "Collapse or move to parent");
        bindings.Key(Hex1bKey.RightArrow).Action(HandleRight, "Expand or move to child");
        bindings.Key(Hex1bKey.Enter).Action(ActivateFocused, "Activate item");
        bindings.Key(Hex1bKey.Spacebar).Action(HandleSpace, "Toggle selection/expand");
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Select item");
        bindings.Mouse(MouseButton.ScrollUp).Action(MoveFocusUp, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(MoveFocusDown, "Scroll down");
    }

    private Task MoveFocusUp(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return Task.CompletedTask;
        
        var previousIndex = _focusedIndex;
        _focusedIndex = _focusedIndex <= 0 ? FlattenedItems.Count - 1 : _focusedIndex - 1;
        
        if (previousIndex != _focusedIndex)
        {
            UpdateFocus();
            EnsureFocusedVisible();
            MarkDirty();
        }
        
        return Task.CompletedTask;
    }

    private Task MoveFocusDown(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return Task.CompletedTask;
        
        var previousIndex = _focusedIndex;
        _focusedIndex = (_focusedIndex + 1) % FlattenedItems.Count;
        
        if (previousIndex != _focusedIndex)
        {
            UpdateFocus();
            EnsureFocusedVisible();
            MarkDirty();
        }
        
        return Task.CompletedTask;
    }

    private async Task HandleLeft(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return;
        
        var focused = FlattenedItems[_focusedIndex];
        if (focused.Node.IsExpanded && focused.Node.CanExpand)
        {
            // Collapse the current item
            await ToggleExpandAsync(focused.Node, ctx);
        }
        else if (focused.Depth > 0)
        {
            // Move to parent
            for (int i = _focusedIndex - 1; i >= 0; i--)
            {
                if (FlattenedItems[i].Depth < focused.Depth)
                {
                    _focusedIndex = i;
                    UpdateFocus();
                    EnsureFocusedVisible();
                    MarkDirty();
                    break;
                }
            }
        }
    }

    private async Task HandleRight(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return;
        
        var focused = FlattenedItems[_focusedIndex];
        if (!focused.Node.IsExpanded && focused.Node.CanExpand)
        {
            // Expand the current item
            await ToggleExpandAsync(focused.Node, ctx);
        }
        else if (focused.Node.IsExpanded && focused.Node.Children.Count > 0)
        {
            // Move to first child (it's the next item in flattened view)
            if (_focusedIndex + 1 < FlattenedItems.Count)
            {
                _focusedIndex++;
                UpdateFocus();
                EnsureFocusedVisible();
                MarkDirty();
            }
        }
    }

    private async Task HandleSpace(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return;
        
        var focused = FlattenedItems[_focusedIndex].Node;
        
        if (MultiSelect)
        {
            // Toggle selection
            await ToggleSelectionAsync(focused, ctx);
        }
        else if (focused.CanExpand)
        {
            // Toggle expand/collapse
            await ToggleExpandAsync(focused, ctx);
        }
    }

    /// <summary>
    /// Toggles the selection of a tree item.
    /// If cascade selection is enabled, also selects/deselects all descendants.
    /// </summary>
    internal async Task ToggleSelectionAsync(TreeItemNode node, InputBindingActionContext ctx)
    {
        if (CascadeSelection)
        {
            // Determine new state: if currently selected or indeterminate, deselect all; otherwise select all
            var currentState = node.ComputeSelectionState();
            var newSelected = currentState != TreeSelectionState.Selected;
            node.SetSelectionCascade(newSelected);
        }
        else
        {
            // Simple toggle
            node.IsSelected = !node.IsSelected;
        }

        if (SelectionChangedAction != null)
        {
            await SelectionChangedAction(ctx);
        }
        MarkDirty();
    }

    private async Task ActivateFocused(InputBindingActionContext ctx)
    {
        if (FlattenedItems.Count == 0) return;
        
        var focused = FlattenedItems[_focusedIndex].Node;
        if (focused.ActivateCallback != null)
        {
            await focused.ActivateCallback(ctx);
        }
    }

    private async Task HandleMouseClick(InputBindingActionContext ctx)
    {
        var localY = ctx.MouseY - Bounds.Y;
        var localX = ctx.MouseX - Bounds.X;
        var itemIndex = localY + _scrollOffset;
        
        if (itemIndex >= 0 && itemIndex < FlattenedItems.Count)
        {
            var clickedEntry = FlattenedItems[itemIndex];
            var clickedNode = clickedEntry.Node;
            
            // Calculate click regions for this item
            // Guide width: depth * 3 chars for guides
            var guideWidth = clickedEntry.Depth * 3;
            
            // Indicator region: 2 chars (e.g., "▼ " or "▶ " or " ")
            var indicatorStartX = guideWidth;
            var indicatorEndX = indicatorStartX + 2;
            
            // Checkbox region (if multi-select): 4 chars "[x] "
            var checkboxStartX = indicatorEndX;
            var checkboxEndX = checkboxStartX + 4;
            
            // Update focus first
            _focusedIndex = itemIndex;
            UpdateFocus();
            
            // Check if click is on expand/collapse indicator (for expandable nodes)
            if (clickedNode.CanExpand && localX >= indicatorStartX && localX < indicatorEndX)
            {
                await ToggleExpandAsync(clickedNode, ctx);
            }
            // Check if click is in checkbox area (only in multi-select mode)
            else if (MultiSelect && localX >= checkboxStartX && localX < checkboxEndX)
            {
                await ToggleSelectionAsync(clickedNode, ctx);
            }
            else
            {
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Toggles the expand/collapse state of a tree item.
    /// Handles lazy loading if needed.
    /// </summary>
    internal async Task ToggleExpandAsync(TreeItemNode node, InputBindingActionContext ctx)
    {
        if (node.IsExpanded)
        {
            // Collapse
            node.IsExpanded = false;
            
            // Fire collapsed event
            if (node.SourceWidget?.CollapsedHandler != null && SourceWidget != null)
            {
                var args = new TreeItemCollapsedEventArgs(SourceWidget, this, ctx, node);
                await node.SourceWidget.CollapsedHandler(args);
            }
        }
        else
        {
            // Expand - check for lazy loading
            if (node.Children.Count == 0 && node.HasChildren && SourceWidget != null)
            {
                var widget = node.SourceWidget;
                if (widget?.ExpandingHandler != null)
                {
                    // Sync lazy load
                    var expandingArgs = new TreeItemExpandingEventArgs(SourceWidget, this, ctx, node);
                    var children = widget.ExpandingHandler(expandingArgs);
                    await LoadChildrenAsync(node, children);
                }
                else if (widget?.ExpandingAsyncHandler != null)
                {
                    // Async lazy load
                    node.IsLoading = true;
                    MarkDirty();
                    ctx.Invalidate(); // Wake up render loop to show loading state
                    
                    var expandingArgs = new TreeItemExpandingEventArgs(SourceWidget, this, ctx, node);
                    var children = await widget.ExpandingAsyncHandler(expandingArgs);
                    await LoadChildrenAsync(node, children);
                    
                    node.IsLoading = false;
                }
            }
            
            node.IsExpanded = true;
            
            // Fire expanded event
            if (node.SourceWidget?.ExpandedHandler != null && SourceWidget != null)
            {
                var args = new TreeItemExpandedEventArgs(SourceWidget, this, ctx, node);
                await node.SourceWidget.ExpandedHandler(args);
            }
        }
        
        RebuildFlattenedView();
        MarkDirty();
        ctx.Invalidate(); // Wake up render loop to show the result
    }

    private Task LoadChildrenAsync(TreeItemNode parent, IEnumerable<TreeItemWidget> childWidgets)
    {
        var children = new List<TreeItemNode>();
        var childList = childWidgets.ToList();
        
        for (int i = 0; i < childList.Count; i++)
        {
            var widget = childList[i];
            var node = new TreeItemNode
            {
                Label = widget.Label,
                Icon = widget.Icon,
                IsExpanded = widget.IsExpanded,
                IsSelected = widget.IsSelected,
                HasChildren = widget.HasChildren || widget.Children.Count > 0,
                Tag = widget.Tag,
                SourceWidget = widget,
                IsLastChild = i == childList.Count - 1
            };
            
            // Wire up callbacks
            node.ActivateCallback = async ctx =>
            {
                if (widget.ActivatedHandler != null && SourceWidget != null)
                {
                    var args = new TreeItemActivatedEventArgs(SourceWidget, this, ctx, node);
                    await widget.ActivatedHandler(args);
                }
                if (ItemActivatedAction != null)
                {
                    await ItemActivatedAction(ctx, node);
                }
            };
            
            node.ToggleExpandCallback = async ctx => await ToggleExpandAsync(node, ctx);
            
            if (MultiSelect)
            {
                node.ToggleSelectCallback = async ctx =>
                {
                    node.IsSelected = !node.IsSelected;
                    if (SelectionChangedAction != null)
                    {
                        await SelectionChangedAction(ctx);
                    }
                };
            }
            
            // Recursively handle pre-loaded children
            if (widget.Children.Count > 0)
            {
                LoadChildrenAsync(node, widget.Children);
            }
            
            children.Add(node);
        }
        
        parent.Children = children;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rebuilds the flattened view of visible tree items.
    /// </summary>
    internal void RebuildFlattenedView()
    {
        FlattenedItems.Clear();
        var isLastAtDepth = new List<bool>();
        FlattenItems(Items, 0, isLastAtDepth);
        
        // Clamp focus index
        if (_focusedIndex >= FlattenedItems.Count)
        {
            _focusedIndex = Math.Max(0, FlattenedItems.Count - 1);
        }
        
        UpdateFocus();
    }

    private void FlattenItems(IReadOnlyList<TreeItemNode> items, int depth, List<bool> isLastAtDepth)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.Depth = depth;
            item.IsLastChild = i == items.Count - 1;
            
            // Build the IsLastAtDepth array for this item
            while (isLastAtDepth.Count <= depth)
            {
                isLastAtDepth.Add(false);
            }
            isLastAtDepth[depth] = item.IsLastChild;
            item.IsLastAtDepth = isLastAtDepth.Take(depth + 1).ToArray();
            
            FlattenedItems.Add(new FlattenedTreeEntry(item, depth, item.IsLastAtDepth));
            
            // Recursively add children if expanded
            if (item.IsExpanded && item.Children.Count > 0)
            {
                FlattenItems(item.Children, depth + 1, isLastAtDepth);
            }
        }
    }

    private void UpdateFocus()
    {
        for (int i = 0; i < FlattenedItems.Count; i++)
        {
            FlattenedItems[i].Node.IsFocused = i == _focusedIndex;
        }
    }

    private void EnsureFocusedVisible()
    {
        if (_viewportHeight <= 0 || FlattenedItems.Count == 0) return;

        if (_focusedIndex < _scrollOffset)
        {
            _scrollOffset = _focusedIndex;
        }
        else if (_focusedIndex >= _scrollOffset + _viewportHeight)
        {
            _scrollOffset = _focusedIndex - _viewportHeight + 1;
        }

        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, FlattenedItems.Count - _viewportHeight));
    }

    /// <summary>
    /// Gets all selected items (for multi-select mode).
    /// </summary>
    public IReadOnlyList<TreeItemNode> GetSelectedItems()
    {
        var selected = new List<TreeItemNode>();
        CollectSelectedItems(Items, selected);
        return selected;
    }

    private void CollectSelectedItems(IReadOnlyList<TreeItemNode> items, List<TreeItemNode> selected)
    {
        foreach (var item in items)
        {
            if (item.IsSelected)
            {
                selected.Add(item);
            }
            CollectSelectedItems(item.Children, selected);
        }
    }

    public override Size Measure(Constraints constraints)
    {
        if (FlattenedItems.Count == 0)
        {
            return constraints.Constrain(new Size(0, 0));
        }

        // Calculate max width needed
        var maxWidth = 0;
        foreach (var entry in FlattenedItems)
        {
            var guideWidth = entry.Depth * 3; // Each depth level adds 3 chars for guides
            var indicatorWidth = 2; // Expand/collapse indicator
            var checkboxWidth = MultiSelect ? 4 : 0; // "[x] " or "[ ] "
            var contentWidth = DisplayWidth.GetStringWidth(entry.Node.GetDisplayText());
            var totalWidth = guideWidth + indicatorWidth + checkboxWidth + contentWidth;
            maxWidth = Math.Max(maxWidth, totalWidth);
        }

        var height = FlattenedItems.Count;
        var constrainedSize = constraints.Constrain(new Size(maxWidth, height));
        _viewportHeight = constrainedSize.Height;

        return constrainedSize;
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        _viewportHeight = bounds.Height;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, FlattenedItems.Count - _viewportHeight));
        EnsureFocusedVisible();
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        
        // Get guide strings based on style
        var (branch, lastBranch, vertical, space) = GetGuideStrings(theme);
        var guideColor = theme.Get(TreeTheme.GuideColor);
        
        // Get indicators
        var expandedIndicator = theme.Get(TreeTheme.ExpandedIndicator);
        var collapsedIndicator = theme.Get(TreeTheme.CollapsedIndicator);
        var leafIndicator = theme.Get(TreeTheme.LeafIndicator);
        var loadingIndicator = theme.Get(TreeTheme.LoadingIndicator);
        var checkboxChecked = theme.Get(TreeTheme.CheckboxChecked);
        var checkboxUnchecked = theme.Get(TreeTheme.CheckboxUnchecked);
        var checkboxIndeterminate = theme.Get(TreeTheme.CheckboxIndeterminate);
        
        // Get colors
        var fg = theme.Get(TreeTheme.ForegroundColor);
        var bg = theme.Get(TreeTheme.BackgroundColor);
        var focusedFg = theme.Get(TreeTheme.FocusedForegroundColor);
        var focusedBg = theme.Get(TreeTheme.FocusedBackgroundColor);
        var selectedFg = theme.Get(TreeTheme.SelectedForegroundColor);
        var hoveredFg = theme.Get(TreeTheme.HoveredForegroundColor);
        var hoveredBg = theme.Get(TreeTheme.HoveredBackgroundColor);
        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();
        
        var visibleEnd = Math.Min(_scrollOffset + _viewportHeight, FlattenedItems.Count);
        
        for (int i = _scrollOffset; i < visibleEnd; i++)
        {
            var entry = FlattenedItems[i];
            var node = entry.Node;
            var y = Bounds.Y + (i - _scrollOffset);
            var x = Bounds.X;
            
            var line = new System.Text.StringBuilder();
            
            // Build guide prefix
            var guidePrefix = new System.Text.StringBuilder();
            
            // For each ancestor level, draw guides that show whether more siblings exist
            // at that level. The guide at column c (0-indexed) shows whether the ancestor
            // at depth c+1 was the last among its siblings.
            for (int d = 0; d < entry.Depth; d++)
            {
                // For the last depth level (d == depth-1), we add the branch connector
                if (d == entry.Depth - 1)
                {
                    // This is where we connect to the parent - add branch or last branch
                    guidePrefix.Append(node.IsLastChild ? lastBranch : branch);
                }
                else
                {
                    // For earlier columns, check if the ancestor at depth d+1 was the last child
                    // If the ancestor at depth d+1 was last, we don't need a vertical continuation
                    // Otherwise, we need a vertical to show more items will appear at that depth
                    var ancestorDepth = d + 1;
                    if (ancestorDepth < entry.IsLastAtDepth.Length && entry.IsLastAtDepth[ancestorDepth])
                    {
                        guidePrefix.Append(space);
                    }
                    else
                    {
                        guidePrefix.Append(vertical);
                    }
                }
            }
            
            // Add guide with color
            if (guidePrefix.Length > 0)
            {
                line.Append(guideColor.ToForegroundAnsi());
                line.Append(guidePrefix);
                line.Append(resetToGlobal);
            }
            
            // Determine item colors - only show focused style when TreeNode has focus
            string itemFg, itemBg;
            var isCurrentItem = i == _focusedIndex;
            if (isCurrentItem && IsFocused)
            {
                itemFg = focusedFg.ToForegroundAnsi();
                itemBg = focusedBg.ToBackgroundAnsi();
            }
            else if (node.IsHovered)
            {
                itemFg = hoveredFg.ToForegroundAnsi();
                itemBg = hoveredBg.ToBackgroundAnsi();
            }
            else if (node.IsSelected)
            {
                itemFg = selectedFg.ToForegroundAnsi();
                itemBg = bg.ToBackgroundAnsi();
            }
            else
            {
                itemFg = globalColors;
                itemBg = "";
            }
            
            line.Append(itemFg);
            line.Append(itemBg);
            
            // Add expand/collapse indicator
            if (node.IsLoading)
            {
                line.Append(loadingIndicator);
            }
            else if (node.CanExpand)
            {
                line.Append(node.IsExpanded ? expandedIndicator : collapsedIndicator);
            }
            else
            {
                line.Append(leafIndicator);
            }
            
            // Add checkbox if multi-select
            if (MultiSelect)
            {
                if (CascadeSelection)
                {
                    // Use computed selection state for cascade mode
                    var selectionState = node.ComputeSelectionState();
                    line.Append(selectionState switch
                    {
                        TreeSelectionState.Selected => checkboxChecked,
                        TreeSelectionState.Indeterminate => checkboxIndeterminate,
                        _ => checkboxUnchecked
                    });
                }
                else
                {
                    // Simple boolean selection
                    line.Append(node.IsSelected ? checkboxChecked : checkboxUnchecked);
                }
            }
            
            // Add content
            line.Append(node.GetDisplayText());
            line.Append(resetToGlobal);
            
            // Write the line
            if (context.CurrentLayoutProvider != null)
            {
                context.WriteClipped(x, y, line.ToString());
            }
            else
            {
                context.SetCursorPosition(x, y);
                context.Write(line.ToString());
            }
        }
    }

    private (string branch, string lastBranch, string vertical, string space) GetGuideStrings(Hex1bTheme theme)
    {
        return GuideStyle switch
        {
            TreeGuideStyle.Ascii => (
                theme.Get(TreeTheme.AsciiBranch),
                theme.Get(TreeTheme.AsciiLastBranch),
                theme.Get(TreeTheme.AsciiVertical),
                theme.Get(TreeTheme.AsciiSpace)),
            TreeGuideStyle.Bold => (
                theme.Get(TreeTheme.BoldBranch),
                theme.Get(TreeTheme.BoldLastBranch),
                theme.Get(TreeTheme.BoldVertical),
                theme.Get(TreeTheme.BoldSpace)),
            TreeGuideStyle.Double => (
                theme.Get(TreeTheme.DoubleBranch),
                theme.Get(TreeTheme.DoubleLastBranch),
                theme.Get(TreeTheme.DoubleVertical),
                theme.Get(TreeTheme.DoubleSpace)),
            _ => (
                theme.Get(TreeTheme.UnicodeBranch),
                theme.Get(TreeTheme.UnicodeLastBranch),
                theme.Get(TreeTheme.UnicodeVertical),
                theme.Get(TreeTheme.UnicodeSpace))
        };
    }
}

/// <summary>
/// Internal structure for flattened tree view (used for rendering/navigation).
/// </summary>
internal readonly record struct FlattenedTreeEntry(
    TreeItemNode Node,
    int Depth,
    bool[] IsLastAtDepth);
