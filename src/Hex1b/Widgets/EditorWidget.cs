using Hex1b.Documents;
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
    public static readonly ActionId MoveLeft = new($"{nameof(EditorWidget)}.{nameof(MoveLeft)}");
    /// <summary>Rebindable action: Move cursor right.</summary>
    public static readonly ActionId MoveRight = new($"{nameof(EditorWidget)}.{nameof(MoveRight)}");
    /// <summary>Rebindable action: Move cursor up.</summary>
    public static readonly ActionId MoveUp = new($"{nameof(EditorWidget)}.{nameof(MoveUp)}");
    /// <summary>Rebindable action: Move cursor down.</summary>
    public static readonly ActionId MoveDown = new($"{nameof(EditorWidget)}.{nameof(MoveDown)}");
    /// <summary>Rebindable action: Move to line start.</summary>
    public static readonly ActionId MoveToLineStart = new($"{nameof(EditorWidget)}.{nameof(MoveToLineStart)}");
    /// <summary>Rebindable action: Move to line end.</summary>
    public static readonly ActionId MoveToLineEnd = new($"{nameof(EditorWidget)}.{nameof(MoveToLineEnd)}");
    /// <summary>Rebindable action: Move to document start.</summary>
    public static readonly ActionId MoveToDocumentStart = new($"{nameof(EditorWidget)}.{nameof(MoveToDocumentStart)}");
    /// <summary>Rebindable action: Move to document end.</summary>
    public static readonly ActionId MoveToDocumentEnd = new($"{nameof(EditorWidget)}.{nameof(MoveToDocumentEnd)}");
    /// <summary>Rebindable action: Move to previous word.</summary>
    public static readonly ActionId MoveWordLeft = new($"{nameof(EditorWidget)}.{nameof(MoveWordLeft)}");
    /// <summary>Rebindable action: Move to next word.</summary>
    public static readonly ActionId MoveWordRight = new($"{nameof(EditorWidget)}.{nameof(MoveWordRight)}");
    /// <summary>Rebindable action: Page up.</summary>
    public static readonly ActionId PageUp = new($"{nameof(EditorWidget)}.{nameof(PageUp)}");
    /// <summary>Rebindable action: Page down.</summary>
    public static readonly ActionId PageDown = new($"{nameof(EditorWidget)}.{nameof(PageDown)}");

    // ── Selection ───────────────────────────────────────────
    /// <summary>Rebindable action: Extend selection left.</summary>
    public static readonly ActionId SelectLeft = new($"{nameof(EditorWidget)}.{nameof(SelectLeft)}");
    /// <summary>Rebindable action: Extend selection right.</summary>
    public static readonly ActionId SelectRight = new($"{nameof(EditorWidget)}.{nameof(SelectRight)}");
    /// <summary>Rebindable action: Extend selection up.</summary>
    public static readonly ActionId SelectUp = new($"{nameof(EditorWidget)}.{nameof(SelectUp)}");
    /// <summary>Rebindable action: Extend selection down.</summary>
    public static readonly ActionId SelectDown = new($"{nameof(EditorWidget)}.{nameof(SelectDown)}");
    /// <summary>Rebindable action: Select to line start.</summary>
    public static readonly ActionId SelectToLineStart = new($"{nameof(EditorWidget)}.{nameof(SelectToLineStart)}");
    /// <summary>Rebindable action: Select to line end.</summary>
    public static readonly ActionId SelectToLineEnd = new($"{nameof(EditorWidget)}.{nameof(SelectToLineEnd)}");
    /// <summary>Rebindable action: Select page up.</summary>
    public static readonly ActionId SelectPageUp = new($"{nameof(EditorWidget)}.{nameof(SelectPageUp)}");
    /// <summary>Rebindable action: Select page down.</summary>
    public static readonly ActionId SelectPageDown = new($"{nameof(EditorWidget)}.{nameof(SelectPageDown)}");
    /// <summary>Rebindable action: Select to document start.</summary>
    public static readonly ActionId SelectToDocumentStart = new($"{nameof(EditorWidget)}.{nameof(SelectToDocumentStart)}");
    /// <summary>Rebindable action: Select to document end.</summary>
    public static readonly ActionId SelectToDocumentEnd = new($"{nameof(EditorWidget)}.{nameof(SelectToDocumentEnd)}");
    /// <summary>Rebindable action: Select to previous word.</summary>
    public static readonly ActionId SelectWordLeft = new($"{nameof(EditorWidget)}.{nameof(SelectWordLeft)}");
    /// <summary>Rebindable action: Select to next word.</summary>
    public static readonly ActionId SelectWordRight = new($"{nameof(EditorWidget)}.{nameof(SelectWordRight)}");
    /// <summary>Rebindable action: Select all text.</summary>
    public static readonly ActionId SelectAll = new($"{nameof(EditorWidget)}.{nameof(SelectAll)}");

    // ── Multi-cursor ────────────────────────────────────────
    /// <summary>Rebindable action: Add cursor at next match.</summary>
    public static readonly ActionId AddCursorAtNextMatch = new($"{nameof(EditorWidget)}.{nameof(AddCursorAtNextMatch)}");

    // ── Undo/Redo ───────────────────────────────────────────
    /// <summary>Rebindable action: Undo last edit.</summary>
    public static readonly ActionId Undo = new($"{nameof(EditorWidget)}.{nameof(Undo)}");
    /// <summary>Rebindable action: Redo last undone edit.</summary>
    public static readonly ActionId Redo = new($"{nameof(EditorWidget)}.{nameof(Redo)}");

    // ── Editing ─────────────────────────────────────────────
    /// <summary>Rebindable action: Delete backward.</summary>
    public static readonly ActionId DeleteBackward = new($"{nameof(EditorWidget)}.{nameof(DeleteBackward)}");
    /// <summary>Rebindable action: Delete forward.</summary>
    public static readonly ActionId DeleteForward = new($"{nameof(EditorWidget)}.{nameof(DeleteForward)}");
    /// <summary>Rebindable action: Delete previous word.</summary>
    public static readonly ActionId DeleteWordBackward = new($"{nameof(EditorWidget)}.{nameof(DeleteWordBackward)}");
    /// <summary>Rebindable action: Delete next word.</summary>
    public static readonly ActionId DeleteWordForward = new($"{nameof(EditorWidget)}.{nameof(DeleteWordForward)}");
    /// <summary>Rebindable action: Delete line.</summary>
    public static readonly ActionId DeleteLine = new($"{nameof(EditorWidget)}.{nameof(DeleteLine)}");
    /// <summary>Rebindable action: Insert newline.</summary>
    public static readonly ActionId InsertNewline = new($"{nameof(EditorWidget)}.{nameof(InsertNewline)}");
    /// <summary>Rebindable action: Insert tab.</summary>
    public static readonly ActionId InsertTab = new($"{nameof(EditorWidget)}.{nameof(InsertTab)}");

    // ── Mouse ───────────────────────────────────────────────
    /// <summary>Rebindable action: Click to position cursor.</summary>
    public static readonly ActionId Click = new($"{nameof(EditorWidget)}.{nameof(Click)}");
    /// <summary>Rebindable action: Ctrl+Click to add/remove cursor.</summary>
    public static readonly ActionId CtrlClick = new($"{nameof(EditorWidget)}.{nameof(CtrlClick)}");
    /// <summary>Rebindable action: Double-click to select word.</summary>
    public static readonly ActionId DoubleClick = new($"{nameof(EditorWidget)}.{nameof(DoubleClick)}");
    /// <summary>Rebindable action: Triple-click to select line.</summary>
    public static readonly ActionId TripleClick = new($"{nameof(EditorWidget)}.{nameof(TripleClick)}");
    /// <summary>Rebindable action: Scroll up.</summary>
    public static readonly ActionId ScrollUp = new($"{nameof(EditorWidget)}.{nameof(ScrollUp)}");
    /// <summary>Rebindable action: Scroll down.</summary>
    public static readonly ActionId ScrollDown = new($"{nameof(EditorWidget)}.{nameof(ScrollDown)}");
    /// <summary>Rebindable action: Scroll left.</summary>
    public static readonly ActionId ScrollLeft = new($"{nameof(EditorWidget)}.{nameof(ScrollLeft)}");
    /// <summary>Rebindable action: Scroll right.</summary>
    public static readonly ActionId ScrollRight = new($"{nameof(EditorWidget)}.{nameof(ScrollRight)}");

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
    /// Text decoration providers for syntax highlighting, diagnostics, etc.
    /// </summary>
    internal IReadOnlyList<ITextDecorationProvider>? DecorationProviders { get; init; }

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

    /// <summary>
    /// Adds a text decoration provider to the editor. Providers supply per-character styling
    /// such as syntax highlighting, diagnostic underlines, and other visual decorations.
    /// Multiple providers can be added; their decorations are resolved by priority.
    /// </summary>
    public EditorWidget Decorations(ITextDecorationProvider provider)
        => this with { DecorationProviders = [..(DecorationProviders ?? []), provider] };

    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
    {
        var node = existingNode as EditorNode ?? new EditorNode();

        node.SourceWidget = this;
        node.State = State;

        node.ViewRenderer = Renderer ?? TextEditorViewRenderer.Instance;
        node.DecorationProviders = DecorationProviders;

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
