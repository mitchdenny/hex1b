using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

public sealed record TextBoxWidget(string? Text = null) : Hex1bWidget
{
    /// <summary>Rebindable action: Move cursor left.</summary>
    public static readonly ActionId MoveLeft = new($"{nameof(TextBoxWidget)}.{nameof(MoveLeft)}");
    /// <summary>Rebindable action: Move cursor right.</summary>
    public static readonly ActionId MoveRight = new($"{nameof(TextBoxWidget)}.{nameof(MoveRight)}");
    /// <summary>Rebindable action: Move cursor to start.</summary>
    public static readonly ActionId MoveHome = new($"{nameof(TextBoxWidget)}.{nameof(MoveHome)}");
    /// <summary>Rebindable action: Move cursor to end.</summary>
    public static readonly ActionId MoveEnd = new($"{nameof(TextBoxWidget)}.{nameof(MoveEnd)}");
    /// <summary>Rebindable action: Move cursor to previous word.</summary>
    public static readonly ActionId MoveWordLeft = new($"{nameof(TextBoxWidget)}.{nameof(MoveWordLeft)}");
    /// <summary>Rebindable action: Move cursor to next word.</summary>
    public static readonly ActionId MoveWordRight = new($"{nameof(TextBoxWidget)}.{nameof(MoveWordRight)}");
    /// <summary>Rebindable action: Extend selection left.</summary>
    public static readonly ActionId SelectLeft = new($"{nameof(TextBoxWidget)}.{nameof(SelectLeft)}");
    /// <summary>Rebindable action: Extend selection right.</summary>
    public static readonly ActionId SelectRight = new($"{nameof(TextBoxWidget)}.{nameof(SelectRight)}");
    /// <summary>Rebindable action: Select to start.</summary>
    public static readonly ActionId SelectToStart = new($"{nameof(TextBoxWidget)}.{nameof(SelectToStart)}");
    /// <summary>Rebindable action: Select to end.</summary>
    public static readonly ActionId SelectToEnd = new($"{nameof(TextBoxWidget)}.{nameof(SelectToEnd)}");
    /// <summary>Rebindable action: Delete character backward.</summary>
    public static readonly ActionId DeleteBackward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteBackward)}");
    /// <summary>Rebindable action: Delete character forward.</summary>
    public static readonly ActionId DeleteForward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteForward)}");
    /// <summary>Rebindable action: Delete previous word.</summary>
    public static readonly ActionId DeleteWordBackward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteWordBackward)}");
    /// <summary>Rebindable action: Delete next word.</summary>
    public static readonly ActionId DeleteWordForward = new($"{nameof(TextBoxWidget)}.{nameof(DeleteWordForward)}");
    /// <summary>Rebindable action: Select all text.</summary>
    public static readonly ActionId SelectAll = new($"{nameof(TextBoxWidget)}.{nameof(SelectAll)}");
    /// <summary>Rebindable action: Submit text.</summary>
    public static readonly ActionId Submit = new($"{nameof(TextBoxWidget)}.{nameof(Submit)}");
    /// <summary>Rebindable action: Insert typed text.</summary>
    public static readonly ActionId InsertText = new($"{nameof(TextBoxWidget)}.{nameof(InsertText)}");

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

    /// <summary>
    /// Internal handler for paste events. When set, replaces the default paste behavior
    /// (which inserts text at cursor position).
    /// </summary>
    internal Func<PasteEventArgs, Task>? PasteHandler { get; init; }

    /// <summary>
    /// Sets an asynchronous handler called when paste data is received.
    /// Overrides the default behavior of inserting pasted text at the cursor position.
    /// </summary>
    public TextBoxWidget OnPaste(Func<PasteEventArgs, Task> handler)
        => this with { PasteHandler = handler };

    /// <summary>
    /// Sets a synchronous handler called when paste data is received.
    /// Overrides the default behavior of inserting pasted text at the cursor position.
    /// </summary>
    public TextBoxWidget OnPaste(Action<PasteEventArgs> handler)
        => this with { PasteHandler = e => { handler(e); return Task.CompletedTask; } };

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
            var oldText = node.Text;
            node.Text = Text;
            node.LastWidgetText = Text;
            if (oldText != node.Text)
            {
                node.State.ClearSelection();
                node.State.CursorPosition = node.Text.Length;
            }
        }
        else if (!context.IsNew && Text != null && Text != node.LastWidgetText)
        {
            // External code changed the text value in the widget - update node
            var oldText = node.Text;
            node.Text = Text;
            node.LastWidgetText = Text;
            if (oldText != node.Text)
            {
                node.State.ClearSelection();
                node.State.CursorPosition = node.Text.Length;
            }
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

        // Wire paste handler
        node.CustomPasteAction = PasteHandler;
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TextBoxNode);
}
