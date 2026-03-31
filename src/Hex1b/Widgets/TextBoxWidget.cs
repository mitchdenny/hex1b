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
    /// <summary>Rebindable action: Move cursor up one line (multiline only).</summary>
    public static readonly ActionId MoveUp = new($"{nameof(TextBoxWidget)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move cursor down one line (multiline only).</summary>
    public static readonly ActionId MoveDown = new($"{nameof(TextBoxWidget)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Extend selection up one line (multiline only).</summary>
    public static readonly ActionId SelectUp = new($"{nameof(TextBoxWidget)}.{nameof(SelectUp)}");
    /// <summary>Rebindable action: Extend selection down one line (multiline only).</summary>
    public static readonly ActionId SelectDown = new($"{nameof(TextBoxWidget)}.{nameof(SelectDown)}");
    /// <summary>Rebindable action: Insert a newline (multiline only).</summary>
    public static readonly ActionId InsertNewline = new($"{nameof(TextBoxWidget)}.{nameof(InsertNewline)}");

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

    /// <summary>
    /// Minimum width of the text box in columns. When set, the text box will measure
    /// at least this many columns wide regardless of content.
    /// </summary>
    public int? MinWidth { get; init; }

    /// <summary>
    /// Maximum width of the text box in columns. When set, the text box will not exceed
    /// this width. Defaults to the same value as MinWidth if not explicitly set.
    /// </summary>
    public int? MaxWidth { get; init; }

    /// <summary>
    /// When true, the text box supports multi-line editing.
    /// Enter inserts newlines, Up/Down navigate between lines.
    /// </summary>
    internal bool IsMultilineValue { get; init; }

    /// <summary>
    /// When true, long lines are visually wrapped at word boundaries.
    /// Only applies when <see cref="IsMultilineValue"/> is true.
    /// </summary>
    internal bool IsWordWrapValue { get; init; }

    /// <summary>
    /// Fixed height in lines for the text box.
    /// When null, single-line uses 1, multiline sizes to content.
    /// </summary>
    internal int? HeightValue { get; init; }

    /// <summary>
    /// Enables multi-line text editing. Enter inserts newlines,
    /// Up/Down arrows navigate between lines, and word wrapping can be enabled.
    /// </summary>
    public TextBoxWidget Multiline()
        => this with { IsMultilineValue = true };

    /// <summary>
    /// Enables word wrapping for multi-line text boxes. Long lines are
    /// visually broken at word boundaries to fit the available width.
    /// </summary>
    public TextBoxWidget WordWrap()
        => this with { IsWordWrapValue = true };

    /// <summary>
    /// Sets the height of the text box in lines.
    /// </summary>
    public TextBoxWidget Height(int lines)
        => this with { HeightValue = lines };

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

        // Sync min/max width to node
        node.MinWidth = MinWidth;
        node.MaxWidth = MaxWidth ?? MinWidth;

        // Sync multiline properties
        node.IsMultiline = IsMultilineValue;
        node.IsWordWrap = IsWordWrapValue;
        node.RequestedHeight = HeightValue;
        node.State.IsMultiline = IsMultilineValue;
        
        return Task.FromResult<Hex1bNode>(node);
    }

    internal override Type GetExpectedNodeType() => typeof(TextBoxNode);
}
