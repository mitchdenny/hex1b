using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

/// <summary>
/// Event arguments for tree selection change events.
/// </summary>
public sealed class TreeSelectionChangedEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The items that are currently selected.
    /// </summary>
    public IReadOnlyList<TreeItemNode> SelectedItems { get; }

    public TreeSelectionChangedEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        IReadOnlyList<TreeItemNode> selectedItems)
        : base(widget, node, context)
    {
        SelectedItems = selectedItems;
    }
}

/// <summary>
/// Event arguments for tree item activation events (Enter key or double-click).
/// </summary>
public sealed class TreeItemActivatedEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The tree item node that was activated.
    /// Use <see cref="TreeItemNode.GetData{T}"/> to retrieve typed data.
    /// </summary>
    public TreeItemNode Item { get; }

    public TreeItemActivatedEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        TreeItemNode item)
        : base(widget, node, context)
    {
        Item = item;
    }
}

/// <summary>
/// Event arguments for tree item expanding events (before children are loaded).
/// Used for lazy loading of children.
/// </summary>
public sealed class TreeItemExpandingEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The tree item node that is expanding.
    /// Use <see cref="TreeItemNode.GetData{T}"/> to retrieve typed data.
    /// </summary>
    public TreeItemNode Item { get; }

    public TreeItemExpandingEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        TreeItemNode item)
        : base(widget, node, context)
    {
        Item = item;
    }
}

/// <summary>
/// Event arguments for tree item expanded events (after expansion completes).
/// </summary>
public sealed class TreeItemExpandedEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The tree item node that was expanded.
    /// </summary>
    public TreeItemNode Item { get; }

    public TreeItemExpandedEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        TreeItemNode item)
        : base(widget, node, context)
    {
        Item = item;
    }
}

/// <summary>
/// Event arguments for tree item collapsed events.
/// </summary>
public sealed class TreeItemCollapsedEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The tree item node that was collapsed.
    /// </summary>
    public TreeItemNode Item { get; }

    public TreeItemCollapsedEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        TreeItemNode item)
        : base(widget, node, context)
    {
        Item = item;
    }
}

/// <summary>
/// Event arguments for tree item clicked events (single-click).
/// </summary>
public sealed class TreeItemClickedEventArgs : WidgetEventArgs<TreeWidget, TreeNode>
{
    /// <summary>
    /// The tree item node that was clicked.
    /// Use <see cref="TreeItemNode.GetData{T}"/> to retrieve typed data.
    /// </summary>
    public TreeItemNode Item { get; }

    public TreeItemClickedEventArgs(
        TreeWidget widget,
        TreeNode node,
        InputBindingActionContext context,
        TreeItemNode item)
        : base(widget, node, context)
    {
        Item = item;
    }
}
