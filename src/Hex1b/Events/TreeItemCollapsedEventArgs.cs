using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
