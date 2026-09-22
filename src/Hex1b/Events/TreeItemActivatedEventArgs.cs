using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
