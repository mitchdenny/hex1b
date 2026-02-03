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

    /// <summary>
    /// The style of guide lines used to draw tree hierarchy.
    /// </summary>
    public TreeGuideStyle GuideStyle { get; init; } = TreeGuideStyle.Unicode;

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

    /// <summary>
    /// Sets the guide style for drawing tree hierarchy lines.
    /// </summary>
    public TreeWidget WithGuideStyle(TreeGuideStyle style)
        => this with { GuideStyle = style };

    #endregion

    internal override async Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TreeNode ?? new TreeNode();
        var isNewNode = existingNode == null;

        node.MultiSelect = MultiSelect;
        node.CascadeSelection = CascadeSelection;
        node.GuideStyle = GuideStyle;
        node.SourceWidget = this;

        // Recursively reconcile tree items
        var newItems = new List<TreeItemNode>();
        await ReconcileItemsAsync(Items, node.Items, newItems, node, context);
        node.Items = newItems;

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

        // Rebuild flattened view
        node.RebuildFlattenedView();

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
            node.Icon = widget.Icon;
            // Only set expand/select state on new nodes - preserve user-driven state on existing nodes
            if (isNewNode)
            {
                node.IsExpanded = widget.IsExpanded;
                node.IsSelected = widget.IsSelected;
            }
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

            // Recursively reconcile children
            // BUT preserve dynamically loaded children if widget has none (lazy loading case)
            if (widget.Children.Count > 0)
            {
                var newChildren = new List<TreeItemNode>();
                await ReconcileItemsAsync(widget.Children, node.Children, newChildren, parentTree, context);
                node.Children = newChildren;
            }
            // If widget has no children but node has dynamically loaded children, preserve them
            // (this happens with OnExpanding lazy loading)

            outputNodes.Add(node);
        }
    }

    internal override Type GetExpectedNodeType() => typeof(TreeNode);
}
