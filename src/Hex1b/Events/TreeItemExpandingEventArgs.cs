using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
