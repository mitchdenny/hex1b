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
        // Navigation
        bindings.Key(Hex1bKey.LeftArrow).Action(MoveLeft, "Move left");
        bindings.Key(Hex1bKey.RightArrow).Action(MoveRight, "Move right");
        bindings.Key(Hex1bKey.UpArrow).Action(MoveUp, "Move up");
        bindings.Key(Hex1bKey.DownArrow).Action(MoveDown, "Move down");

        // Editing
        bindings.Key(Hex1bKey.Backspace).Action(DeleteBackwardAsync, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Action(DeleteForwardAsync, "Delete forward");
        bindings.Key(Hex1bKey.Enter).Action(InsertNewlineAsync, "Insert newline");
        bindings.Key(Hex1bKey.Tab).Action(InsertTabAsync, "Insert tab");

        // Character input
        bindings.AnyCharacter().Action(InsertTextAsync, "Type text");
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

    // --- Input handlers ---

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        State.InsertText(text);
        EnsureCursorVisible();
        MarkDirty();
        if (TextChangedAction != null)
        {
            await TextChangedAction(ctx);
        }
    }

    private async Task InsertNewlineAsync(InputBindingActionContext ctx)
    {
        State.InsertText("\n");
        EnsureCursorVisible();
        MarkDirty();
        if (TextChangedAction != null)
        {
            await TextChangedAction(ctx);
        }
    }

    private async Task InsertTabAsync(InputBindingActionContext ctx)
    {
        State.InsertText(new string(' ', State.TabSize));
        EnsureCursorVisible();
        MarkDirty();
        if (TextChangedAction != null)
        {
            await TextChangedAction(ctx);
        }
    }

    private async Task DeleteBackwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteBackward();
        EnsureCursorVisible();
        MarkDirty();
        if (TextChangedAction != null)
        {
            await TextChangedAction(ctx);
        }
    }

    private async Task DeleteForwardAsync(InputBindingActionContext ctx)
    {
        State.DeleteForward();
        EnsureCursorVisible();
        MarkDirty();
        if (TextChangedAction != null)
        {
            await TextChangedAction(ctx);
        }
    }

    private void MoveLeft()
    {
        State.MoveCursor(CursorDirection.Left);
        EnsureCursorVisible();
        MarkDirty();
    }

    private void MoveRight()
    {
        State.MoveCursor(CursorDirection.Right);
        EnsureCursorVisible();
        MarkDirty();
    }

    private void MoveUp()
    {
        State.MoveCursor(CursorDirection.Up);
        EnsureCursorVisible();
        MarkDirty();
    }

    private void MoveDown()
    {
        State.MoveCursor(CursorDirection.Down);
        EnsureCursorVisible();
        MarkDirty();
    }
}
