using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for displaying a selectable list of items.
/// Selection state is owned by the node and preserved across reconciliation.
/// </summary>
public sealed record ListWidget(IReadOnlyList<string> Items) : Hex1bWidget
{
    /// <summary>
    /// Called when the selection changes.
    /// </summary>
    public Func<ListSelectionChangedEventArgs, Task>? OnSelectionChanged { get; init; }

    /// <summary>
    /// Called when an item is activated (Enter, Space, or click).
    /// </summary>
    public Func<ListItemActivatedEventArgs, Task>? OnItemActivated { get; init; }

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode ?? new ListNode();
        node.Items = Items;
        node.SourceWidget = this;
        
        // Clamp selection if items changed
        if (node.SelectedIndex >= Items.Count && Items.Count > 0)
        {
            node.SelectedIndex = Items.Count - 1;
        }
        else if (Items.Count == 0)
        {
            node.SelectedIndex = 0;
        }
        
        // Set up event handlers
        if (OnSelectionChanged != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (node.SelectedText != null)
                {
                    var args = new ListSelectionChangedEventArgs(this, node, ctx, node.SelectedIndex, node.SelectedText);
                    return OnSelectionChanged(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }

        if (OnItemActivated != null)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (node.SelectedText != null)
                {
                    var args = new ListItemActivatedEventArgs(this, node, ctx, node.SelectedIndex, node.SelectedText);
                    return OnItemActivated(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.ItemActivatedAction = null;
        }
        
        // Set initial focus if this is a new node (ListNode is always focusable)
        if (context.IsNew)
        {
            node.IsFocused = true;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode);
}
