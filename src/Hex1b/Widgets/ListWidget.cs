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
    /// The initial selected index when the list is first created.
    /// Defaults to 0 (first item). Only applied when the node is new.
    /// </summary>
    public int InitialSelectedIndex { get; init; } = 0;
    
    /// <summary>
    /// Internal handler for selection changed events.
    /// </summary>
    internal Func<ListSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Internal handler for item activated events.
    /// </summary>
    internal Func<ListItemActivatedEventArgs, Task>? ItemActivatedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the selection changes.
    /// </summary>
    public ListWidget OnSelectionChanged(Action<ListSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the selection changes.
    /// </summary>
    public ListWidget OnSelectionChanged(Func<ListSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when an item is activated (Enter, Space, or click).
    /// </summary>
    public ListWidget OnItemActivated(Action<ListItemActivatedEventArgs> handler)
        => this with { ItemActivatedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when an item is activated (Enter, Space, or click).
    /// </summary>
    public ListWidget OnItemActivated(Func<ListItemActivatedEventArgs, Task> handler)
        => this with { ItemActivatedHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ListNode ?? new ListNode();
        var isNewNode = existingNode == null;
        node.Items = Items;
        node.SourceWidget = this;
        
        // Apply initial selection for new nodes
        if (isNewNode && Items.Count > 0)
        {
            node.SelectedIndex = Math.Clamp(InitialSelectedIndex, 0, Items.Count - 1);
        }
        // Clamp selection if items changed
        else if (node.SelectedIndex >= Items.Count && Items.Count > 0)
        {
            node.SelectedIndex = Items.Count - 1;
        }
        else if (Items.Count == 0)
        {
            node.SelectedIndex = 0;
        }
        
        // Set up event handlers
        if (SelectionChangedHandler != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (node.SelectedText != null)
                {
                    var args = new ListSelectionChangedEventArgs(this, node, ctx, node.SelectedIndex, node.SelectedText);
                    return SelectionChangedHandler(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }

        if (ItemActivatedHandler != null)
        {
            node.ItemActivatedAction = ctx =>
            {
                if (node.SelectedText != null)
                {
                    var args = new ListItemActivatedEventArgs(this, node, ctx, node.SelectedIndex, node.SelectedText);
                    return ItemActivatedHandler(args);
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
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ListNode);
}
