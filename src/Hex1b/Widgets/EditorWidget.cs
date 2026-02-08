using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for multi-line document editing. Supports shared documents and synced cursors.
/// </summary>
public sealed record EditorWidget(EditorState State) : Hex1bWidget
{
    /// <summary>
    /// Internal handler for text changed events.
    /// </summary>
    internal Func<EditorTextChangedEventArgs, Task>? TextChangedHandler { get; init; }

    /// <summary>
    /// Sets a synchronous handler called when the document text changes.
    /// </summary>
    public EditorWidget OnTextChanged(Action<EditorTextChangedEventArgs> handler)
        => this with { TextChangedHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous handler called when the document text changes.
    /// </summary>
    public EditorWidget OnTextChanged(Func<EditorTextChangedEventArgs, Task> handler)
        => this with { TextChangedHandler = handler };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as EditorNode ?? new EditorNode();

        node.SourceWidget = this;
        node.State = State;

        if (TextChangedHandler != null)
        {
            node.TextChangedAction = (ctx) =>
            {
                var args = new EditorTextChangedEventArgs(this, node, ctx);
                return TextChangedHandler(args);
            };
        }
        else
        {
            node.TextChangedAction = null;
        }

        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(EditorNode);
}
