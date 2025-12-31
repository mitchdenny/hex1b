using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record ButtonWidget(string Label) : Hex1bWidget
{
    /// <summary>
    /// The async click handler. Called when the button is activated via Enter, Space, or mouse click.
    /// </summary>
    internal Func<ButtonClickedEventArgs, Task>? ClickHandler { get; init; }

    /// <summary>
    /// Sets a synchronous click handler. Called when the button is activated via Enter, Space, or mouse click.
    /// </summary>
    public ButtonWidget OnClick(Action<ButtonClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous click handler. Called when the button is activated via Enter, Space, or mouse click.
    /// </summary>
    public ButtonWidget OnClick(Func<ButtonClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as ButtonNode ?? new ButtonNode();
        
        // Mark dirty if label changed
        if (node.Label != Label)
        {
            node.MarkDirty();
        }
        
        node.Label = Label;
        node.SourceWidget = this;
        
        // Convert the typed event handler to the internal InputBindingActionContext handler
        if (ClickHandler != null)
        {
            node.ClickAction = async ctx => 
            {
                var args = new ButtonClickedEventArgs(this, node, ctx);
                await ClickHandler(args);
            };
        }
        else
        {
            node.ClickAction = null;
        }
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(ButtonNode);
}
