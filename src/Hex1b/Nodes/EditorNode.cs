using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Node for multi-line document editing. Manages scroll, viewport, and input.
/// Scroll offset is per-node to support different views on the same document.
/// </summary>
public sealed class EditorNode : Hex1bNode
{
    private int _scrollOffset; // First visible line (1-based)
    private int _viewportLines;
    private int _viewportColumns;
    private IHex1bDocument? _subscribedDocument;

    /// <summary>The source widget that was reconciled into this node.</summary>
    public EditorWidget? SourceWidget { get; set; }

    /// <summary>The editor state (shared between nodes that share state).</summary>
    public EditorState State { get; set; } = null!;

    /// <summary>
    /// Internal action invoked when text content changes.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? TextChangedAction { get; set; }

    /// <summary>First visible line (1-based).</summary>
    public int ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            var clamped = Math.Clamp(value, 1, Math.Max(1, State?.Document.LineCount ?? 1));
            if (_scrollOffset != clamped)
            {
                _scrollOffset = clamped;
                MarkDirty();
            }
        }
    }

    /// <summary>Number of visible lines in the viewport.</summary>
    public int ViewportLines => _viewportLines;

    /// <summary>Number of visible columns in the viewport.</summary>
    public int ViewportColumns => _viewportColumns;

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;
    public override CursorShape PreferredCursorShape => CursorShape.BlinkingBar;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // ── Navigation ──────────────────────────────────────────
        bindings.Key(Hex1bKey.LeftArrow).Action(MoveLeft, "Move left");
        bindings.Key(Hex1bKey.RightArrow).Action(MoveRight, "Move right");
        bindings.Key(Hex1bKey.UpArrow).Action(MoveUp, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveDown, "Move down");
        bindings.Key(Hex1bKey.Home).Action(MoveToLineStart, "Go to line start");
        bindings.Key(Hex1bKey.End).Action(MoveToLineEnd, "Go to line end");
        bindings.Ctrl().Key(Hex1bKey.Home).Action(MoveToDocumentStart, "Go to document start");
        bindings.Ctrl().Key(Hex1bKey.End).Action(MoveToDocumentEnd, "Go to document end");
        bindings.Ctrl().Key(Hex1bKey.LeftArrow).Action(MoveWordLeft, "Move to previous word");
        bindings.Ctrl().Key(Hex1bKey.RightArrow).Action(MoveWordRight, "Move to next word");
        bindings.Key(Hex1bKey.PageUp).Action(PageUp, "Page up");
        bindings.Key(Hex1bKey.PageDown).Action(PageDown, "Page down");

        // ── Selection (Shift+Navigation) ────────────────────────
        bindings.Shift().Key(Hex1bKey.LeftArrow).Action(SelectLeft, "Extend selection left");
        bindings.Shift().Key(Hex1bKey.RightArrow).Action(SelectRight, "Extend selection right");
        bindings.Shift().Key(Hex1bKey.UpArrow).Action(SelectUp, "Extend selection up");
        bindings.Shift().Key(Hex1bKey.DownArrow).Action(SelectDown, "Extend selection down");
        bindings.Shift().Key(Hex1bKey.Home).Action(SelectToLineStart, "Select to line start");
        bindings.Shift().Key(Hex1bKey.End).Action(SelectToLineEnd, "Select to line end");
        bindings.Shift().Key(Hex1bKey.PageUp).Action(SelectPageUp, "Select page up");
        bindings.Shift().Key(Hex1bKey.PageDown).Action(SelectPageDown, "Select page down");

        // Ctrl+Shift bindings (requires direct InputBinding construction)
        AddCtrlShiftBinding(bindings, Hex1bKey.Home, SelectToDocumentStart, "Select to document start");
        AddCtrlShiftBinding(bindings, Hex1bKey.End, SelectToDocumentEnd, "Select to document end");
        AddCtrlShiftBinding(bindings, Hex1bKey.LeftArrow, SelectWordLeft, "Select to previous word");
        AddCtrlShiftBinding(bindings, Hex1bKey.RightArrow, SelectWordRight, "Select to next word");

        // ── Selection (Ctrl+A) ──────────────────────────────────
        bindings.Ctrl().Key(Hex1bKey.A).Action(SelectAll, "Select all");

        // ── Editing ─────────────────────────────────────────────
        bindings.Key(Hex1bKey.Backspace).Action(DeleteBackwardAsync, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Action(DeleteForwardAsync, "Delete forward");
        bindings.Ctrl().Key(Hex1bKey.Backspace).Action(DeleteWordBackwardAsync, "Delete previous word");
        bindings.Ctrl().Key(Hex1bKey.Delete).Action(DeleteWordForwardAsync, "Delete next word");
        AddCtrlShiftBinding(bindings, Hex1bKey.K, DeleteLineAsync, "Delete line");
        bindings.Key(Hex1bKey.Enter).Action(InsertNewlineAsync, "Insert newline");
        bindings.Key(Hex1bKey.Tab).Action(InsertTabAsync, "Insert tab");

        // ── Character input ─────────────────────────────────────
        bindings.AnyCharacter().Action(InsertTextAsync, "Type text");
    }

    private static void AddCtrlShiftBinding(InputBindingsBuilder bindings, Hex1bKey key, Action handler, string description)
    {
        var step = new KeyStep(key, Hex1bModifiers.Control | Hex1bModifiers.Shift);
        bindings.AddBinding(new InputBinding([step], handler, description));
    }

    private static void AddCtrlShiftBinding(InputBindingsBuilder bindings, Hex1bKey key, Func<InputBindingActionContext, Task> handler, string description)
    {
        var step = new KeyStep(key, Hex1bModifiers.Control | Hex1bModifiers.Shift);
        bindings.AddBinding(new InputBinding([step], handler, description));
    }

    public override Size Measure(Constraints constraints)
    {
        // Editor fills available space
        var width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 80;
        var height = constraints.MaxHeight > 0 ? constraints.MaxHeight : 24;
        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);
        _viewportLines = bounds.Height;
        _viewportColumns = bounds.Width;

        // Subscribe to document changes if not already
        SubscribeToDocument();

        // Initialize scroll if needed
        if (_scrollOffset == 0) _scrollOffset = 1;

        EnsureCursorVisible();
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (State == null) return;

        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);

        var doc = State.Document;
        var cursorPos = doc.OffsetToPosition(State.Cursor.Position);

        for (var viewLine = 0; viewLine < _viewportLines; viewLine++)
        {
            var docLine = _scrollOffset + viewLine;
            var screenY = Bounds.Y + viewLine;
            var screenX = Bounds.X;

            if (docLine > doc.LineCount)
            {
                // Past end of document — render tilde like vim
                var emptyLine = "~".PadRight(_viewportColumns);
                RenderLine(context, screenX, screenY, emptyLine, fg, bg, null, null, null, null);
                continue;
            }

            var lineText = doc.GetLineText(docLine);

            // Truncate or pad to viewport width
            string displayText;
            if (lineText.Length >= _viewportColumns)
            {
                displayText = lineText[.._viewportColumns];
            }
            else
            {
                displayText = lineText.PadRight(_viewportColumns);
            }

            // Determine if cursor is on this line
            if (IsFocused && docLine == cursorPos.Line)
            {
                var cursorCol = cursorPos.Column - 1; // 0-based for rendering
                RenderLine(context, screenX, screenY, displayText, fg, bg, cursorFg, cursorBg, cursorCol, null);
            }
            else
            {
                RenderLine(context, screenX, screenY, displayText, fg, bg, null, null, null, null);
            }
        }
    }

    private void RenderLine(
        Hex1bRenderContext context,
        int x, int y,
        string text,
        Hex1bColor fg, Hex1bColor bg,
        Hex1bColor? cursorFg, Hex1bColor? cursorBg,
        int? cursorCol,
        int? _)
    {
        string output;

        if (cursorCol is not null && cursorFg is not null && cursorBg is not null)
        {
            var col = cursorCol.Value;
            var before = col < text.Length ? text[..col] : text;
            var cursorChar = col < text.Length ? text[col].ToString() : " ";
            var after = col + 1 < text.Length ? text[(col + 1)..] : "";

            var globalColors = context.Theme.GetGlobalColorCodes();
            var resetToGlobal = context.Theme.GetResetToGlobalCodes();
            output = $"{globalColors}{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{before}" +
                     $"{cursorFg.Value.ToForegroundAnsi()}{cursorBg.Value.ToBackgroundAnsi()}{cursorChar}" +
                     $"{resetToGlobal}{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{after}";
        }
        else
        {
            output = $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{text}";
        }

        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(x, y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    private void EnsureCursorVisible()
    {
        if (State == null) return;
        var cursorPos = State.Document.OffsetToPosition(State.Cursor.Position);
        var cursorLine = cursorPos.Line;

        if (cursorLine < _scrollOffset)
        {
            _scrollOffset = cursorLine;
        }
        else if (cursorLine >= _scrollOffset + _viewportLines)
        {
            _scrollOffset = cursorLine - _viewportLines + 1;
        }
    }

    private void SubscribeToDocument()
    {
        if (State?.Document == _subscribedDocument) return;

        if (_subscribedDocument != null)
        {
            _subscribedDocument.Changed -= OnDocumentChanged;
        }

        _subscribedDocument = State?.Document;

        if (_subscribedDocument != null)
        {
            _subscribedDocument.Changed += OnDocumentChanged;
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        MarkDirty();
    }

    // --- Input handlers: editing ---

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        State.InsertText(text);
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task InsertNewlineAsync(InputBindingActionContext ctx)
    {
        State.InsertText("\n");
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task InsertTabAsync(InputBindingActionContext ctx)
    {
        State.InsertText(new string(' ', State.TabSize));
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task DeleteBackwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteBackward();
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task DeleteForwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteForward();
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task DeleteWordBackwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteWordBackward();
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task DeleteWordForwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteWordForward();
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    private async Task DeleteLineAsync(InputBindingActionContext ctx)
    {
        State.DeleteLine();
        AfterEdit();
        if (TextChangedAction != null) await TextChangedAction(ctx);
    }

    // --- Input handlers: navigation ---

    private void MoveLeft() { State.MoveCursor(CursorDirection.Left); AfterMove(); }
    private void MoveRight() { State.MoveCursor(CursorDirection.Right); AfterMove(); }
    private void MoveUp() { State.MoveCursor(CursorDirection.Up); AfterMove(); }
    private void MoveDown() { State.MoveCursor(CursorDirection.Down); AfterMove(); }
    private void MoveToLineStart() { State.MoveToLineStart(); AfterMove(); }
    private void MoveToLineEnd() { State.MoveToLineEnd(); AfterMove(); }
    private void MoveToDocumentStart() { State.MoveToDocumentStart(); AfterMove(); }
    private void MoveToDocumentEnd() { State.MoveToDocumentEnd(); AfterMove(); }
    private void MoveWordLeft() { State.MoveWordLeft(); AfterMove(); }
    private void MoveWordRight() { State.MoveWordRight(); AfterMove(); }
    private void PageUp() { State.MovePageUp(ViewportLines); AfterMove(); }
    private void PageDown() { State.MovePageDown(ViewportLines); AfterMove(); }

    // --- Input handlers: selection ---

    private void SelectLeft() { State.MoveCursor(CursorDirection.Left, extend: true); AfterMove(); }
    private void SelectRight() { State.MoveCursor(CursorDirection.Right, extend: true); AfterMove(); }
    private void SelectUp() { State.MoveCursor(CursorDirection.Up, extend: true); AfterMove(); }
    private void SelectDown() { State.MoveCursor(CursorDirection.Down, extend: true); AfterMove(); }
    private void SelectToLineStart() { State.MoveToLineStart(extend: true); AfterMove(); }
    private void SelectToLineEnd() { State.MoveToLineEnd(extend: true); AfterMove(); }
    private void SelectToDocumentStart() { State.MoveToDocumentStart(extend: true); AfterMove(); }
    private void SelectToDocumentEnd() { State.MoveToDocumentEnd(extend: true); AfterMove(); }
    private void SelectWordLeft() { State.MoveWordLeft(extend: true); AfterMove(); }
    private void SelectWordRight() { State.MoveWordRight(extend: true); AfterMove(); }
    private void SelectPageUp() { State.MovePageUp(ViewportLines, extend: true); AfterMove(); }
    private void SelectPageDown() { State.MovePageDown(ViewportLines, extend: true); AfterMove(); }
    private void SelectAll() { State.SelectAll(); MarkDirty(); }

    // --- Common post-action helpers ---

    private void AfterMove()
    {
        EnsureCursorVisible();
        MarkDirty();
    }

    private void AfterEdit()
    {
        EnsureCursorVisible();
        MarkDirty();
    }
}
