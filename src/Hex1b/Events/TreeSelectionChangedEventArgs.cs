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
