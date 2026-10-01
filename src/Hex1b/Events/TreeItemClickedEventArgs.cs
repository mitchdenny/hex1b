using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
