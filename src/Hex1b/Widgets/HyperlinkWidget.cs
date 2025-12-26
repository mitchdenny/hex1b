using Hex1b.Events;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that renders text as a clickable hyperlink using OSC 8 escape sequences.
/// In terminals that support OSC 8 (most modern terminals), the text will be rendered
/// as a clickable link. In unsupported terminals, the text will be rendered normally.
/// </summary>
/// <param name="Text">The visible text to display.</param>
/// <param name="Uri">The URI to link to when clicked (or hovered in terminal).</param>
/// <param name="Overflow">How to handle text that exceeds the available width.</param>
public sealed record HyperlinkWidget(string Text, string Uri, TextOverflow Overflow = TextOverflow.Overflow) : Hex1bWidget
{
    /// <summary>
    /// Optional parameters for the hyperlink (e.g., "id=unique-id").
    /// Per the OSC 8 spec, the id parameter can be used to group multiple hyperlink
    /// segments that should be treated as a single logical link.
    /// </summary>
    public string Parameters { get; init; } = "";
    
    /// <summary>
    /// The async click handler. Called when the hyperlink is activated via Enter or mouse click.
    /// If not set, clicking will do nothing (the terminal will still handle the link natively).
    /// </summary>
    internal Func<HyperlinkClickedEventArgs, Task>? ClickHandler { get; init; }

    /// <summary>
    /// Sets a synchronous click handler. Called when the hyperlink is activated via Enter or mouse click.
    /// </summary>
    public HyperlinkWidget OnClick(Action<HyperlinkClickedEventArgs> handler)
        => this with { ClickHandler = args => { handler(args); return Task.CompletedTask; } };

    /// <summary>
    /// Sets an asynchronous click handler. Called when the hyperlink is activated via Enter or mouse click.
    /// </summary>
    public HyperlinkWidget OnClick(Func<HyperlinkClickedEventArgs, Task> handler)
        => this with { ClickHandler = handler };

    /// <summary>
    /// Sets the optional parameters for the hyperlink.
    /// </summary>
    public HyperlinkWidget WithParameters(string parameters)
        => this with { Parameters = parameters };

    /// <summary>
    /// Sets a unique ID parameter for the hyperlink.
    /// This is useful for grouping multiple hyperlink segments together.
    /// </summary>
    public HyperlinkWidget WithId(string id)
        => this with { Parameters = $"id={id}" };

    internal override Hex1bNode Reconcile(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as HyperlinkNode ?? new HyperlinkNode();
        
        // Mark dirty if properties changed
        if (node.Text != Text || node.Uri != Uri || node.Parameters != Parameters || node.Overflow != Overflow)
        {
            node.MarkDirty();
        }
        
        node.Text = Text;
        node.Uri = Uri;
        node.Parameters = Parameters;
        node.Overflow = Overflow;
        node.SourceWidget = this;
        
        // Convert the typed event handler to the internal InputBindingActionContext handler
        if (ClickHandler != null)
        {
            node.ClickAction = async ctx => 
            {
                var args = new HyperlinkClickedEventArgs(this, node, ctx);
                await ClickHandler(args);
            };
        }
        else
        {
            node.ClickAction = null;
        }
        
        return node;
    }

    internal override Type GetExpectedNodeType() => typeof(HyperlinkNode);
}
