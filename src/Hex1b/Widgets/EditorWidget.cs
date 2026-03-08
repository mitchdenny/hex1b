using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Widget for multi-line document editing. Supports shared documents and synced cursors.
/// </summary>
public sealed record EditorWidget(EditorState State) : Hex1bWidget
{
    // ── Navigation ──────────────────────────────────────────
    /// <summary>Rebindable action: Move cursor left.</summary>
    public static readonly ActionId MoveLeft = new("Editor.MoveLeft");
    /// <summary>Rebindable action: Move cursor right.</summary>
    public static readonly ActionId MoveRight = new("Editor.MoveRight");
    /// <summary>Rebindable action: Move cursor up.</summary>
    public static readonly ActionId MoveUp = new("Editor.MoveUp");
    /// <summary>Rebindable action: Move cursor down.</summary>
    public static readonly ActionId MoveDown = new("Editor.MoveDown");
    /// <summary>Rebindable action: Move to line start.</summary>
    public static readonly ActionId MoveToLineStart = new("Editor.MoveToLineStart");
    /// <summary>Rebindable action: Move to line end.</summary>
    public static readonly ActionId MoveToLineEnd = new("Editor.MoveToLineEnd");
    /// <summary>Rebindable action: Move to document start.</summary>
    public static readonly ActionId MoveToDocumentStart = new("Editor.MoveToDocumentStart");
    /// <summary>Rebindable action: Move to document end.</summary>
    public static readonly ActionId MoveToDocumentEnd = new("Editor.MoveToDocumentEnd");
    /// <summary>Rebindable action: Move to previous word.</summary>
    public static readonly ActionId MoveWordLeft = new("Editor.MoveWordLeft");
    /// <summary>Rebindable action: Move to next word.</summary>
    public static readonly ActionId MoveWordRight = new("Editor.MoveWordRight");
    /// <summary>Rebindable action: Page up.</summary>
    public static readonly ActionId PageUp = new("Editor.PageUp");
    /// <summary>Rebindable action: Page down.</summary>
    public static readonly ActionId PageDown = new("Editor.PageDown");

    // ── Selection ───────────────────────────────────────────
    /// <summary>Rebindable action: Extend selection left.</summary>
    public static readonly ActionId SelectLeft = new("Editor.SelectLeft");
    /// <summary>Rebindable action: Extend selection right.</summary>
    public static readonly ActionId SelectRight = new("Editor.SelectRight");
    /// <summary>Rebindable action: Extend selection up.</summary>
    public static readonly ActionId SelectUp = new("Editor.SelectUp");
    /// <summary>Rebindable action: Extend selection down.</summary>
    public static readonly ActionId SelectDown = new("Editor.SelectDown");
    /// <summary>Rebindable action: Select to line start.</summary>
    public static readonly ActionId SelectToLineStart = new("Editor.SelectToLineStart");
    /// <summary>Rebindable action: Select to line end.</summary>
    public static readonly ActionId SelectToLineEnd = new("Editor.SelectToLineEnd");
    /// <summary>Rebindable action: Select page up.</summary>
    public static readonly ActionId SelectPageUp = new("Editor.SelectPageUp");
    /// <summary>Rebindable action: Select page down.</summary>
    public static readonly ActionId SelectPageDown = new("Editor.SelectPageDown");
    /// <summary>Rebindable action: Select to document start.</summary>
    public static readonly ActionId SelectToDocumentStart = new("Editor.SelectToDocumentStart");
    /// <summary>Rebindable action: Select to document end.</summary>
    public static readonly ActionId SelectToDocumentEnd = new("Editor.SelectToDocumentEnd");
    /// <summary>Rebindable action: Select to previous word.</summary>
    public static readonly ActionId SelectWordLeft = new("Editor.SelectWordLeft");
    /// <summary>Rebindable action: Select to next word.</summary>
    public static readonly ActionId SelectWordRight = new("Editor.SelectWordRight");
    /// <summary>Rebindable action: Select all text.</summary>
    public static readonly ActionId SelectAll = new("Editor.SelectAll");

    // ── Multi-cursor ────────────────────────────────────────
    /// <summary>Rebindable action: Add cursor at next match.</summary>
    public static readonly ActionId AddCursorAtNextMatch = new("Editor.AddCursorAtNextMatch");

    // ── Undo/Redo ───────────────────────────────────────────
    /// <summary>Rebindable action: Undo last edit.</summary>
    public static readonly ActionId Undo = new("Editor.Undo");
    /// <summary>Rebindable action: Redo last undone edit.</summary>
    public static readonly ActionId Redo = new("Editor.Redo");

    // ── Editing ─────────────────────────────────────────────
    /// <summary>Rebindable action: Delete backward.</summary>
    public static readonly ActionId DeleteBackward = new("Editor.DeleteBackward");
    /// <summary>Rebindable action: Delete forward.</summary>
    public static readonly ActionId DeleteForward = new("Editor.DeleteForward");
    /// <summary>Rebindable action: Delete previous word.</summary>
    public static readonly ActionId DeleteWordBackward = new("Editor.DeleteWordBackward");
    /// <summary>Rebindable action: Delete next word.</summary>
    public static readonly ActionId DeleteWordForward = new("Editor.DeleteWordForward");
    /// <summary>Rebindable action: Delete line.</summary>
    public static readonly ActionId DeleteLine = new("Editor.DeleteLine");
    /// <summary>Rebindable action: Insert newline.</summary>
    public static readonly ActionId InsertNewline = new("Editor.InsertNewline");
    /// <summary>Rebindable action: Insert tab.</summary>
    public static readonly ActionId InsertTab = new("Editor.InsertTab");

    // ── Mouse ───────────────────────────────────────────────
    /// <summary>Rebindable action: Click to position cursor.</summary>
    public static readonly ActionId Click = new("Editor.Click");
    /// <summary>Rebindable action: Ctrl+Click to add/remove cursor.</summary>
    public static readonly ActionId CtrlClick = new("Editor.CtrlClick");
    /// <summary>Rebindable action: Double-click to select word.</summary>
    public static readonly ActionId DoubleClick = new("Editor.DoubleClick");
    /// <summary>Rebindable action: Triple-click to select line.</summary>
    public static readonly ActionId TripleClick = new("Editor.TripleClick");
    /// <summary>Rebindable action: Scroll up.</summary>
    public static readonly ActionId ScrollUp = new("Editor.ScrollUp");
    /// <summary>Rebindable action: Scroll down.</summary>
    public static readonly ActionId ScrollDown = new("Editor.ScrollDown");
    /// <summary>Rebindable action: Scroll left.</summary>
    public static readonly ActionId ScrollLeft = new("Editor.ScrollLeft");
    /// <summary>Rebindable action: Scroll right.</summary>
    public static readonly ActionId ScrollRight = new("Editor.ScrollRight");

    /// <summary>
    /// Internal handler for text changed events.
    /// </summary>
    internal Func<EditorTextChangedEventArgs, Task>? TextChangedHandler { get; init; }

    /// <summary>
    /// The view renderer that controls how document content is displayed.
    /// Defaults to TextEditorViewRenderer (plain text).
    /// </summary>
    internal IEditorViewRenderer? Renderer { get; init; }

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

    /// <summary>
    /// Sets the view renderer for this editor. Use <see cref="TextEditorViewRenderer"/> for text
    /// or <see cref="HexEditorViewRenderer"/> for hex dump views.
    /// </summary>
    public EditorWidget WithViewRenderer(IEditorViewRenderer renderer)
        => this with { Renderer = renderer };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as EditorNode ?? new EditorNode();

        node.SourceWidget = this;
        node.State = State;

        node.ViewRenderer = Renderer ?? TextEditorViewRenderer.Instance;

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
