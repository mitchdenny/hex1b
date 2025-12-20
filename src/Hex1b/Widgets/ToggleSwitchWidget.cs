using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A horizontal toggle switch widget that allows selecting between multiple options.
/// Use arrow keys (left/right) to switch between options when focused.
/// </summary>
public sealed record ToggleSwitchWidget(ToggleSwitchState State) : Hex1bWidget
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
        node.State = State;
        node.SourceWidget = this;
        
        // Set up event handlers
        if (SelectionChangedHandler != null)
        {
            node.SelectionChangedAction = ctx =>
            {
                if (State.SelectedOption != null)
                {
                    var args = new ToggleSelectionChangedEventArgs(this, node, ctx, State.SelectedIndex, State.SelectedOption);
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
