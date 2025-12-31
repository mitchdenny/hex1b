using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A dropdown picker widget that shows a selected value and opens a list popup when activated.
/// Selection state is owned by the node and preserved across reconciliation.
/// </summary>
/// <param name="Items">The list of items to choose from.</param>
/// <remarks>
/// <para>
/// Picker is a composite widget that renders as a button showing the current selection.
/// When clicked (or activated via keyboard), it opens an anchored popup containing
/// a list of all available items. Selecting an item updates the picker and closes the popup.
/// </para>
/// <para>
/// The selection state (<see cref="PickerNode.SelectedIndex"/>) is owned by the node,
/// not the widget. This means the picker maintains its selection across re-renders.
/// Use the OnSelectionChanged method to react to selection changes.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ctx.Picker(["Apple", "Banana", "Cherry"])
///     .OnSelectionChanged(e => Console.WriteLine($"Selected: {e.SelectedText}"));
/// </code>
/// </example>
public sealed record PickerWidget(IReadOnlyList<string> Items) : CompositeWidget<PickerNode>
{
    /// <summary>
    /// The initial selected index when the picker is first created.
    /// Defaults to 0 (first item).
    /// </summary>
    public int InitialSelectedIndex { get; init; } = 0;
    
    /// <summary>
    /// Handler invoked when the selection changes.
    /// </summary>
    internal Func<PickerSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the selection changes.
    /// </summary>
    public PickerWidget OnSelectionChanged(Action<PickerSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the selection changes.
    /// </summary>
    public PickerWidget OnSelectionChanged(Func<PickerSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    /// <summary>
    /// Updates the node with widget properties before building content.
    /// </summary>
    protected override void UpdateNode(PickerNode node)
    {
        // Sync items to node
        node.Items = Items;
        
        // Apply initial selection only once (on first reconcile)
        if (!node.HasAppliedInitialSelection && Items.Count > 0)
        {
            node.SelectedIndex = Math.Clamp(InitialSelectedIndex, 0, Items.Count - 1);
            node.HasAppliedInitialSelection = true;
        }
        
        // Clamp selection if items changed and selection is now out of bounds
        if (node.SelectedIndex >= Items.Count && Items.Count > 0)
        {
            node.SelectedIndex = Items.Count - 1;
        }
        
        // Set up selection changed handler
        if (SelectionChangedHandler != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                var args = new PickerSelectionChangedEventArgs(this, node, ctx, node.SelectedIndex, node.SelectedText);
                return SelectionChangedHandler(args);
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }
    }

    /// <summary>
    /// Builds the picker's button content with popup behavior.
    /// </summary>
    protected override Task<Hex1bWidget> BuildContentAsync(PickerNode node, ReconcileContext context)
    {
        var displayText = node.SelectedText;
        if (string.IsNullOrEmpty(displayText) && Items.Count == 0)
        {
            displayText = "(empty)";
        }
        
        // Add down arrow to indicate this is a dropdown
        var labelWithArrow = $"{displayText} ▼";
        
        // Build a button that shows current selection
        // When clicked, show the popup list anchored below
        var button = new ButtonWidget(labelWithArrow)
            .OnClick(async e =>
            {
                // Store context for later use
                node.CurrentContext = e.Context;
                
                // Build and show popup list
                e.PushAnchored(AnchorPosition.Below, () => BuildPickerList(node, e.Context));
            })
            .WithInputBindings(bindings =>
            {
                // Down arrow: open popup with next item selected (or current if at end)
                bindings.Key(Hex1bKey.DownArrow).Action(ctx =>
                {
                    node.CurrentContext = ctx;
                    node.OpenWithNextItem(ctx, this);
                }, "Next item");
                
                // Up arrow: open popup with previous item selected (or current if at start)
                bindings.Key(Hex1bKey.UpArrow).Action(ctx =>
                {
                    node.CurrentContext = ctx;
                    node.OpenWithPreviousItem(ctx, this);
                }, "Previous item");
            });
        
        return Task.FromResult<Hex1bWidget>(button);
    }

    /// <summary>
    /// Builds the popup list widget for item selection.
    /// </summary>
    private Hex1bWidget BuildPickerList(PickerNode node, InputBindingActionContext context)
    {
        // Create a list with the items, pre-selecting the current selection
        var list = new ListWidget(Items) { InitialSelectedIndex = node.SelectedIndex }
            .OnItemActivated(async e =>
            {
                await node.SelectItemAsync(e.ActivatedIndex, e.Context);
            });
        
        // Wrap in a border for visual distinction
        return new BorderWidget(list);
    }
}
