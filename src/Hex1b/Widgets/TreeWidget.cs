using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Container widget that displays hierarchical data in a tree structure with guides and indentation.
/// Supports keyboard navigation, expand/collapse, and multi-selection.
/// </summary>
/// <param name="Items">The root tree items to display.</param>
public sealed record TreeWidget(IReadOnlyList<TreeItemWidget> Items) : Hex1bWidget
{
    /// <summary>
    /// Whether multiple items can be selected. Default is false (single focus).
    /// </summary>
    public bool MultiSelect { get; init; } = false;

    /// <summary>
    /// Whether selecting a parent item automatically selects all children,
    /// and deselecting a child shows partial selection on parent.
    /// Only applies when <see cref="MultiSelect"/> is true.
    /// </summary>
    public bool CascadeSelection { get; init; } = false;

    // Container-level event handlers
    internal Func<TreeSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }
    internal Func<TreeItemActivatedEventArgs, Task>? ItemActivatedHandler { get; init; }

    #region Fluent API

    /// <summary>
    /// Sets a synchronous handler called when the selection changes (in multi-select mode).
    /// </summary>
    public TreeWidget OnSelectionChanged(Action<TreeSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the selection changes (in multi-select mode).
    /// </summary>
    public TreeWidget OnSelectionChanged(Func<TreeSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when an item is activated (Enter key).
    /// </summary>
    public TreeWidget OnItemActivated(Action<TreeItemActivatedEventArgs> handler)
        => this with { ItemActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when an item is activated (Enter key).
    /// </summary>
    public TreeWidget OnItemActivated(Func<TreeItemActivatedEventArgs, Task> handler)
        => this with { ItemActivatedHandler = handler };

    /// <summary>
    /// Enables multi-select mode with checkboxes.
    /// </summary>
    public TreeWidget WithMultiSelect(bool multiSelect = true)
        => this with { MultiSelect = multiSelect };

    /// <summary>
    /// Enables cascade selection mode where selecting a parent selects all children,
    /// and partial child selection shows an indeterminate state on the parent.
    /// Automatically enables multi-select if not already enabled.
    /// </summary>
    public TreeWidget WithCascadeSelection(bool cascadeSelection = true)
        => this with { CascadeSelection = cascadeSelection, MultiSelect = cascadeSelection || MultiSelect };

    #endregion

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TreeNode ?? new TreeNode();
        var isNewNode = existingNode == null;

        node.MultiSelect = MultiSelect;
        node.CascadeSelection = CascadeSelection;
        node.SourceWidget = this;
        node.InvalidateCallback = context.InvalidateCallback;

        // Recursively reconcile tree items
        var newItems = new List<TreeItemNode>();
        await ReconcileItemsAsync(Items, node.Items, newItems, node, context);
        node.Items = newItems;

        // Rebuild flattened view
        node.RebuildFlattenedView();

        // Wire up container-level event handlers
        if (SelectionChangedHandler != null)
        {
            node.SelectionChangedAction = async ctx =>
            {
                var selectedItems = node.GetSelectedItems();
                var args = new TreeSelectionChangedEventArgs(this, node, ctx, selectedItems);
                await SelectionChangedHandler(args);
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }

        if (ItemActivatedHandler != null)
        {
            node.ItemActivatedAction = async (ctx, item) =>
            {
                var args = new TreeItemActivatedEventArgs(this, node, ctx, item);
                await ItemActivatedHandler(args);
            };
        }
        else
        {
            node.ItemActivatedAction = null;
        }

        // Set initial focus on the TreeNode itself if this is a new node
        if (isNewNode)
        {
            node.IsFocused = true;
        }

        return node;
    }

    private async Task ReconcileItemsAsync(
        IReadOnlyList<TreeItemWidget> widgets,
        IReadOnlyList<TreeItemNode> existingNodes,
        List<TreeItemNode> outputNodes,
        TreeNode parentTree,
        ReconcileContext context)
    {
        for (int i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            var existingNode = i < existingNodes.Count ? existingNodes[i] : null;

            // Reuse existing node if label matches, otherwise create new
            var isNewNode = existingNode?.Label != widget.Label;
            var node = isNewNode ? new TreeItemNode() : existingNode!;

            // Update properties
            node.Label = widget.Label;
            node.Icon = widget.IconValue;
            // Only set expand/select state on new nodes - preserve user-driven state on existing nodes
            if (isNewNode)
            {
                node.IsExpanded = widget.IsExpanded;
                node.IsSelected = widget.IsSelected;
            }
            // Loading state: use widget's state OR preserve node's internal loading state
            // (node.IsLoading is set by async expand operation, widget.IsLoading is external state)
            if (widget.IsLoading)
            {
                node.IsLoading = true;
            }
            // Don't set to false here - let the async operation complete and set it to false
            node.HasChildren = widget.HasChildren || widget.Children.Count > 0;
            node.Tag = widget.Tag;
            node.SourceWidget = widget;
            node.IsLastChild = i == widgets.Count - 1;

            // Wire up callbacks through the parent tree
            node.ActivateCallback = async ctx =>
            {
                // Call item-level handler first
                if (widget.ActivatedHandler != null)
                {
                    var args = new TreeItemActivatedEventArgs(this, parentTree, ctx, node);
                    await widget.ActivatedHandler(args);
                }
                // Then container-level handler
                if (parentTree.ItemActivatedAction != null)
                {
                    await parentTree.ItemActivatedAction(ctx, node);
                }
            };

            node.ToggleExpandCallback = async ctx =>
            {
                await parentTree.ToggleExpandAsync(node, ctx);
            };

            if (MultiSelect)
            {
                node.ToggleSelectCallback = async ctx =>
                {
                    node.IsSelected = !node.IsSelected;
                    if (parentTree.SelectionChangedAction != null)
                    {
                        await parentTree.SelectionChangedAction(ctx);
                    }
                };
            }
            else
            {
                node.ToggleSelectCallback = null;
            }

            // Compose child widgets
            
            // 1. Expand indicator (or spinner when loading)
            if (node.IsLoading)
            {
                // Loading: show spinner instead of expand indicator
                var spinnerWidget = new SpinnerWidget();
                node.LoadingSpinnerNode = await context.ReconcileChildAsync(
                    node.LoadingSpinnerNode, spinnerWidget, node) as SpinnerNode;
                node.ExpandIndicatorNode = null;
            }
            else if (node.CanExpand)
            {
                // Has children: show expand/collapse indicator
                node.LoadingSpinnerNode = null;
                var indicatorIcon = node.IsExpanded ? "▼" : "▶";
                var indicatorWidget = new IconWidget(indicatorIcon);
                node.ExpandIndicatorNode = await context.ReconcileChildAsync(
                    node.ExpandIndicatorNode, indicatorWidget, node) as IconNode;
            }
            else
            {
                // Leaf node: no indicator
                node.LoadingSpinnerNode = null;
                node.ExpandIndicatorNode = null;
            }

            // Recursively reconcile children FIRST
            // This must happen before checkbox reconciliation so ComputeSelectionState works
            // BUT preserve dynamically loaded children if widget has none (lazy loading case)
            if (widget.Children.Count > 0)
            {
                var newChildren = new List<TreeItemNode>();
                await ReconcileItemsAsync(widget.Children, node.Children, newChildren, parentTree, context);
                node.Children = newChildren;
            }
            // If widget has no children but node has dynamically loaded children, preserve them
            // (this happens with OnExpanding lazy loading)
            
            // 2. Checkbox (when multi-select) - AFTER children are reconciled
            if (MultiSelect)
            {
                var checkboxState = CascadeSelection 
                    ? node.ComputeSelectionState() switch
                    {
                        TreeSelectionState.Selected => CheckboxState.Checked,
                        TreeSelectionState.Indeterminate => CheckboxState.Indeterminate,
                        _ => CheckboxState.Unchecked
                    }
                    : node.IsSelected ? CheckboxState.Checked : CheckboxState.Unchecked;
                    
                var checkboxWidget = new CheckboxWidget(checkboxState);
                node.CheckboxNode = await context.ReconcileChildAsync(
                    node.CheckboxNode, checkboxWidget, node) as CheckboxNode;
            }
            else
            {
                node.CheckboxNode = null;
            }
            
            // 3. User icon
            if (node.Icon != null)
            {
                var iconWidget = new IconWidget(node.Icon);
                node.UserIconNode = await context.ReconcileChildAsync(
                    node.UserIconNode, iconWidget, node) as IconNode;
            }
            else
            {
                node.UserIconNode = null;
            }

            outputNodes.Add(node);
        }
    }

    /// <inheritdoc/>
    internal override TimeSpan? GetEffectiveRedrawDelay()
    {
        // If explicitly set, use that value
        if (RedrawDelay.HasValue)
        {
            return RedrawDelay;
        }

        // Check if any item is loading - if so, schedule redraws for spinner animation
        if (HasAnyLoadingItems(Items))
        {
            return SpinnerStyle.Dots.Interval;
        }

        return null;
    }

    private static bool HasAnyLoadingItems(IReadOnlyList<TreeItemWidget> items)
    {
        foreach (var item in items)
        {
            if (item.IsLoading)
            {
                return true;
            }
            if (item.Children.Count > 0 && HasAnyLoadingItems(item.Children))
            {
                return true;
            }
        }
        return false;
    }

    internal override Type GetExpectedNodeType() => typeof(TreeNode);
}
