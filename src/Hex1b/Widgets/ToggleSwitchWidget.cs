using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A horizontal toggle switch widget that allows selecting between multiple options.
/// Use arrow keys (left/right) to switch between options when focused.
/// Selection state is owned by the node and preserved across reconciliation.
/// </summary>
public sealed record ToggleSwitchWidget(IReadOnlyList<string> Options, int SelectedIndex = 0) : Hex1bWidget
{
    /// <summary>
    /// Internal handler for selection changed events.
    /// </summary>
    internal Func<ToggleSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the selection changes.
    /// </summary>
    public ToggleSwitchWidget OnSelectionChanged(Action<ToggleSelectionChangedEventArgs> handler)
        => this with { SelectionChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the selection changes.
    /// </summary>
    public ToggleSwitchWidget OnSelectionChanged(Func<ToggleSelectionChangedEventArgs, Task> handler)
        => this with { SelectionChangedHandler = handler };

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ToggleSwitchNode ?? new ToggleSwitchNode();
        node.Options = Options;
        node.SourceWidget = this;
        
        // For a new node, set the initial selection from the widget
        if (context.IsNew)
        {
            node.SelectedIndex = SelectedIndex >= 0 && SelectedIndex < Options.Count ? SelectedIndex : 0;
        }
        
        // Clamp selection if options changed
        if (node.SelectedIndex >= Options.Count && Options.Count > 0)
        {
            node.SelectedIndex = Options.Count - 1;
        }
        else if (Options.Count == 0)
        {
            node.SelectedIndex = 0;
        }
        
        // Set up event handlers
        if (SelectionChangedHandler != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                var selectedOption = node.SelectedOption;
                if (selectedOption != null)
                {
                    var args = new ToggleSelectionChangedEventArgs(this, node, ctx, node.SelectedIndex, selectedOption);
                    return SelectionChangedHandler(args);
                }
                return Task.CompletedTask;
            };
        }
        else
        {
            node.SelectionChangedAction = null;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(ToggleSwitchNode);
}
