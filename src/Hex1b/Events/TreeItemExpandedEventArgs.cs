using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Events;

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
