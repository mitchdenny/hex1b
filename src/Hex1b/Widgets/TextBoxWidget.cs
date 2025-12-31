using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record TextBoxWidget(string? Text = null) : Hex1bWidget
{
    /// <summary>
    /// Internal handler for text changed events.
    /// </summary>
    internal Func<TextChangedEventArgs, Task>? TextChangedHandler { get; init; }

    /// <summary>
    /// Internal handler for submit events.
    /// </summary>
    internal Func<TextSubmittedEventArgs, Task>? SubmitHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the text content changes.
    /// </summary>
    public TextBoxWidget OnTextChanged(Action<TextChangedEventArgs> handler)
        => this with { TextChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the text content changes.
    /// </summary>
    public TextBoxWidget OnTextChanged(Func<TextChangedEventArgs, Task> handler)
        => this with { TextChangedHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when Enter is pressed in the text box.
    /// </summary>
    public TextBoxWidget OnSubmit(Action<TextSubmittedEventArgs> handler)
        => this with { SubmitHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when Enter is pressed in the text box.
    /// </summary>
    public TextBoxWidget OnSubmit(Func<TextSubmittedEventArgs, Task> handler)
        => this with { SubmitHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as TextBoxNode ?? new TextBoxNode();
        
        // Store reference to source widget for event args
        node.SourceWidget = this;
        
        // Set the text from the widget only if:
        // 1. This is a new node and Text is provided
        // 2. The widget's text changed from what it provided last time (external control)
        if (context.IsNew && Text != null)
        {
            node.Text = Text;
            node.LastWidgetText = Text;
        }
        else if (!context.IsNew && Text != null && Text != node.LastWidgetText)
        {
            // External code changed the text value in the widget - update node
            node.Text = Text;
            node.LastWidgetText = Text;
        }
        
        // Set up event handlers - wrap to convert InputBindingActionContext to typed event args
        if (TextChangedHandler != null)
        {
            node.TextChangedAction = (ctx, oldText, newText) =>
            {
                var args = new TextChangedEventArgs(this, node, ctx, oldText, newText);
                return TextChangedHandler(args);
            };
        }
        else
        {
            node.TextChangedAction = null;
        }

        if (SubmitHandler != null)
        {
            node.SubmitAction = ctx =>
            {
                var args = new TextSubmittedEventArgs(this, node, ctx, node.Text);
                return SubmitHandler(args);
            };
        }
        else
        {
            node.SubmitAction = null;
        }
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TextBoxNode);
}
