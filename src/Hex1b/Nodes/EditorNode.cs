using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Node for multi-line document editing. Manages scroll, viewport, and input.
/// Scroll offset is per-node to support different views on the same document.
/// Scrollbars are rendered and handled internally (not composed) because their
/// behavior is tightly coupled to the active IEditorViewRenderer.
/// </summary>
public sealed class EditorNode : Hex1bNode
{
    private int _scrollOffset; // First visible line (1-based)
    private int _horizontalScrollOffset; // First visible column (0-based)
    private int _viewportLines;
    private int _viewportColumns;
    private bool _showVerticalScrollbar;
    private bool _showHorizontalScrollbar;
    private bool _cursorDirty; // Set when cursor changes; cleared after Arrange adjusts scroll
    private char? _pendingNibble; // For hex renderers: first nibble of partially-entered byte
    private IHex1bDocument? _subscribedDocument;

    /// <summary>
    /// Marks that the cursor has changed and scroll should adjust to keep it visible
    /// on the next Arrange pass. Called automatically by input handlers; exposed for
    /// testing scenarios that modify EditorState directly.
    /// </summary>
    internal void NotifyCursorChanged() => _cursorDirty = true;

    /// <summary>The source widget that was reconciled into this node.</summary>
    public EditorWidget? SourceWidget { get; set; }

    /// <summary>The editor state (shared between nodes that share state).</summary>
    public EditorState State { get; set; } = null!;

    /// <summary>The view renderer that controls how document content is displayed.</summary>
    public IEditorViewRenderer ViewRenderer { get; set; } = TextEditorViewRenderer.Instance;

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
            var maxLine = State != null ? ViewRenderer.GetTotalLines(State.Document, Bounds.Width) : 1;
            var clamped = Math.Clamp(value, 1, Math.Max(1, maxLine));
            if (_scrollOffset != clamped)
            {
                _scrollOffset = clamped;
                MarkDirty();
            }
        }
    }

    /// <summary>First visible column (0-based).</summary>
    public int HorizontalScrollOffset
    {
        get => _horizontalScrollOffset;
        set
        {
            var clamped = Math.Max(0, value);
            if (_horizontalScrollOffset != clamped)
            {
                _horizontalScrollOffset = clamped;
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

        // ── Multi-cursor (Ctrl+D) ───────────────────────────────
        bindings.Ctrl().Key(Hex1bKey.D).Action(AddCursorAtNextMatch, "Add cursor at next match");

        // ── Undo/Redo ───────────────────────────────────────────
        bindings.Ctrl().Key(Hex1bKey.Z).Action(UndoAction, "Undo");
        bindings.Ctrl().Key(Hex1bKey.Y).Action(RedoAction, "Redo");

        // ── Escape to collapse multi-cursor ─────────────────────
        // Note: Escape is only handled when we have multiple cursors

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

        // ── Mouse ────────────────────────────────────────────────
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Click to position cursor");
        bindings.Mouse(MouseButton.Left).Ctrl().Action(HandleCtrlClick, "Ctrl+Click to add/remove cursor");
        bindings.Mouse(MouseButton.Left).DoubleClick().Action(HandleMouseDoubleClick, "Double-click to select word");
        bindings.Mouse(MouseButton.Left).TripleClick().Action(HandleMouseTripleClick, "Triple-click to select line");
        bindings.Drag(MouseButton.Left).Action(HandleDragStart, "Drag to select text");
        bindings.Mouse(MouseButton.ScrollUp).Action(ScrollUp, "Scroll up");
        bindings.Mouse(MouseButton.ScrollDown).Action(ScrollDown, "Scroll down");
        bindings.Mouse(MouseButton.ScrollUp).Shift().Action(ScrollLeft, "Scroll left");
        bindings.Mouse(MouseButton.ScrollDown).Shift().Action(ScrollRight, "Scroll right");
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

        // Initialize scroll if needed (before scrollbar calculations)
        if (_scrollOffset == 0) _scrollOffset = 1;

        // Determine scrollbar visibility first (before computing content area)
        var totalLines = State != null ? ViewRenderer.GetTotalLines(State.Document, Bounds.Width) : 0;
        var maxLineWidth = State != null
            ? ViewRenderer.GetMaxLineWidth(State.Document, _scrollOffset, bounds.Height, bounds.Width)
            : 0;

        _showVerticalScrollbar = totalLines > bounds.Height && bounds.Width > 1;
        _showHorizontalScrollbar = maxLineWidth > bounds.Width - (_showVerticalScrollbar ? 1 : 0)
            && bounds.Height > 1;

        // Content area excludes scrollbar space
        _viewportLines = bounds.Height - (_showHorizontalScrollbar ? 1 : 0);
        _viewportColumns = bounds.Width - (_showVerticalScrollbar ? 1 : 0);

        // Re-check vertical after adjusting for horizontal scrollbar
        if (!_showVerticalScrollbar && totalLines > _viewportLines && _viewportColumns > 0)
        {
            _showVerticalScrollbar = true;
            _viewportColumns = bounds.Width - 1;
        }

        // Subscribe to document changes if not already
        SubscribeToDocument();

        // Only adjust scroll for cursor visibility when a cursor-changing action
        // occurred (AfterMove/AfterEdit set _cursorDirty). This prevents scrollbar
        // interactions from being "flicked back" to the cursor on layout passes.
        if (_cursorDirty)
        {
            EnsureCursorVisible();
            _cursorDirty = false;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        if (State == null) return;

        // Render text content at full bounds width — scrollbars render on top
        ViewRenderer.Render(context, State, Bounds, _scrollOffset, _horizontalScrollOffset, IsFocused, _pendingNibble);

        // Render scrollbars (self-rendered, not composed)
        if (_showVerticalScrollbar)
            RenderVerticalScrollbar(context);
        if (_showHorizontalScrollbar)
            RenderHorizontalScrollbar(context);

        // Corner cell when both scrollbars present
        if (_showVerticalScrollbar && _showHorizontalScrollbar)
        {
            var bg = context.Theme.Get(EditorTheme.BackgroundColor);
            context.WriteClipped(Bounds.X + Bounds.Width - 1, Bounds.Y + Bounds.Height - 1,
                $"{bg.ToBackgroundAnsi()} ");
        }
    }

    // ── Scrollbar rendering ─────────────────────────────────────

    private (int thumbSize, int thumbStart) CalculateVerticalThumb()
    {
        var totalLines = ViewRenderer.GetTotalLines(State.Document, Bounds.Width);
        var trackHeight = _viewportLines;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)trackHeight / totalLines * trackHeight));
        thumbSize = Math.Min(thumbSize, trackHeight);

        var maxScroll = Math.Max(1, totalLines - _viewportLines);
        var scrollFraction = maxScroll > 0 ? (double)(_scrollOffset - 1) / maxScroll : 0;
        var thumbStart = (int)Math.Round(scrollFraction * (trackHeight - thumbSize));
        thumbStart = Math.Clamp(thumbStart, 0, trackHeight - thumbSize);

        return (thumbSize, thumbStart);
    }

    private (int thumbSize, int thumbStart) CalculateHorizontalThumb()
    {
        var maxWidth = ViewRenderer.GetMaxLineWidth(State.Document, _scrollOffset, _viewportLines, _viewportColumns);
        var trackWidth = _viewportColumns;
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)trackWidth / maxWidth * trackWidth));
        thumbSize = Math.Min(thumbSize, trackWidth);

        var maxHScroll = Math.Max(1, maxWidth - _viewportColumns);
        var scrollFraction = maxHScroll > 0 ? (double)_horizontalScrollOffset / maxHScroll : 0;
        var thumbStart = (int)Math.Round(scrollFraction * (trackWidth - thumbSize));
        thumbStart = Math.Clamp(thumbStart, 0, trackWidth - thumbSize);

        return (thumbSize, thumbStart);
    }

    private void RenderVerticalScrollbar(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);
        var trackColor = theme.Get(ScrollTheme.TrackColor);
        var thumbColor = IsFocused
            ? theme.Get(ScrollTheme.FocusedThumbColor)
            : theme.Get(ScrollTheme.ThumbColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);

        var scrollX = Bounds.X + Bounds.Width - 1;
        var (thumbSize, thumbStart) = CalculateVerticalThumb();

        for (var row = 0; row < _viewportLines; row++)
        {
            var isThumb = row >= thumbStart && row < thumbStart + thumbSize;
            var ch = isThumb ? thumbChar : trackChar;
            var color = isThumb ? thumbColor : trackColor;
            context.WriteClipped(scrollX, Bounds.Y + row,
                $"{color.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{ch}");
        }
    }

    private void RenderHorizontalScrollbar(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var trackChar = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.HorizontalThumbCharacter);
        var trackColor = theme.Get(ScrollTheme.TrackColor);
        var thumbColor = IsFocused
            ? theme.Get(ScrollTheme.FocusedThumbColor)
            : theme.Get(ScrollTheme.ThumbColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);

        var scrollY = Bounds.Y + Bounds.Height - 1;
        var (thumbSize, thumbStart) = CalculateHorizontalThumb();

        for (var col = 0; col < _viewportColumns; col++)
        {
            var isThumb = col >= thumbStart && col < thumbStart + thumbSize;
            var ch = isThumb ? thumbChar : trackChar;
            var color = isThumb ? thumbColor : trackColor;
            context.WriteClipped(Bounds.X + col, scrollY,
                $"{color.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{ch}");
        }
    }

    private void EnsureCursorVisible()
    {
        if (State == null) return;
        // Clamp cursor to document bounds (cursor may lag behind after bulk delete)
        var offset = Math.Min(State.Cursor.Position.Value, State.Document.Length);
        var cursorPos = State.Document.OffsetToPosition(new DocumentOffset(offset));
        var cursorLine = cursorPos.Line;
        var cursorColumn = cursorPos.Column - 1; // 0-based

        // Vertical visibility
        if (cursorLine < _scrollOffset)
        {
            _scrollOffset = cursorLine;
        }
        else if (cursorLine >= _scrollOffset + _viewportLines)
        {
            _scrollOffset = cursorLine - _viewportLines + 1;
        }

        // Horizontal visibility
        if (cursorColumn < _horizontalScrollOffset)
        {
            _horizontalScrollOffset = cursorColumn;
        }
        else if (cursorColumn >= _horizontalScrollOffset + _viewportColumns)
        {
            _horizontalScrollOffset = cursorColumn - _viewportColumns + 1;
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
        // Clamp cursors in case an external edit shrunk the document
        State?.ClampAllCursors();
        MarkDirty();
    }

    // --- Input handlers: editing ---

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        // Let the view renderer intercept character input (e.g., hex byte editing)
        if (text.Length == 1 && ViewRenderer.HandlesCharInput)
        {
            var nibbleBefore = _pendingNibble;
            if (ViewRenderer.HandleCharInput(text[0], State, ref _pendingNibble, _viewportColumns))
            {
                if (_pendingNibble == null)
                {
                    // Byte was committed — trigger edit flow
                    _cursorDirty = true;
                    MarkDirty();
                    if (TextChangedAction != null) await TextChangedAction(ctx);
                }
                else
                {
                    // First nibble stored — just redraw, don't mark edit
                    MarkDirty();
                }
                return;
            }
        }

        _pendingNibble = null;
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

    private void MoveLeft()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Left, State, extend: false))
            State.MoveCursor(CursorDirection.Left);
        AfterMove();
    }

    private void MoveRight()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Right, State, extend: false))
            State.MoveCursor(CursorDirection.Right);
        AfterMove();
    }

    private void MoveUp()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Up, State, extend: false))
            State.MoveCursor(CursorDirection.Up);
        AfterMove();
    }

    private void MoveDown()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Down, State, extend: false))
            State.MoveCursor(CursorDirection.Down);
        AfterMove();
    }
    private void MoveToLineStart() { State.MoveToLineStart(); AfterMove(); }
    private void MoveToLineEnd() { State.MoveToLineEnd(); AfterMove(); }
    private void MoveToDocumentStart() { State.MoveToDocumentStart(); AfterMove(); }
    private void MoveToDocumentEnd() { State.MoveToDocumentEnd(); AfterMove(); }
    private void MoveWordLeft() { State.MoveWordLeft(); AfterMove(); }
    private void MoveWordRight() { State.MoveWordRight(); AfterMove(); }
    private void PageUp() { State.MovePageUp(ViewportLines); AfterMove(); }
    private void PageDown() { State.MovePageDown(ViewportLines); AfterMove(); }

    // --- Input handlers: selection ---

    private void SelectLeft()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Left, State, extend: true))
            State.MoveCursor(CursorDirection.Left, extend: true);
        AfterMove();
    }

    private void SelectRight()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Right, State, extend: true))
            State.MoveCursor(CursorDirection.Right, extend: true);
        AfterMove();
    }

    private void SelectUp()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Up, State, extend: true))
            State.MoveCursor(CursorDirection.Up, extend: true);
        AfterMove();
    }

    private void SelectDown()
    {
        if (!ViewRenderer.HandleNavigation(CursorDirection.Down, State, extend: true))
            State.MoveCursor(CursorDirection.Down, extend: true);
        AfterMove();
    }
    private void SelectToLineStart() { State.MoveToLineStart(extend: true); AfterMove(); }
    private void SelectToLineEnd() { State.MoveToLineEnd(extend: true); AfterMove(); }
    private void SelectToDocumentStart() { State.MoveToDocumentStart(extend: true); AfterMove(); }
    private void SelectToDocumentEnd() { State.MoveToDocumentEnd(extend: true); AfterMove(); }
    private void SelectWordLeft() { State.MoveWordLeft(extend: true); AfterMove(); }
    private void SelectWordRight() { State.MoveWordRight(extend: true); AfterMove(); }
    private void SelectPageUp() { State.MovePageUp(ViewportLines, extend: true); AfterMove(); }
    private void SelectPageDown() { State.MovePageDown(ViewportLines, extend: true); AfterMove(); }
    private void SelectAll() { State.SelectAll(); MarkDirty(); }

    // --- Input handlers: multi-cursor ---

    private void AddCursorAtNextMatch() { State.AddCursorAtNextMatch(); MarkDirty(); }

    // --- Input handlers: undo/redo ---

    private void UndoAction() { State.Undo(); AfterEdit(); }
    private void RedoAction() { State.Redo(); AfterEdit(); }

    // --- Common post-action helpers ---

    private void AfterMove()
    {
        _pendingNibble = null;
        _cursorDirty = true;
        MarkDirty();
    }

    private void AfterEdit()
    {
        _pendingNibble = null;
        _cursorDirty = true;
        MarkDirty();
    }

    // --- Mouse handlers ---

    /// <summary>
    /// Determines which zone a local coordinate falls in.
    /// </summary>
    private enum HitZone { Content, VerticalScrollbar, HorizontalScrollbar, Corner }

    private HitZone GetHitZone(int localX, int localY)
    {
        var inScrollbarCol = _showVerticalScrollbar && localX >= _viewportColumns;
        var inScrollbarRow = _showHorizontalScrollbar && localY >= _viewportLines;

        if (inScrollbarCol && inScrollbarRow) return HitZone.Corner;
        if (inScrollbarCol) return HitZone.VerticalScrollbar;
        if (inScrollbarRow) return HitZone.HorizontalScrollbar;
        return HitZone.Content;
    }

    /// <summary>
    /// Converts absolute screen coordinates to a document offset.
    /// Returns null if the coordinates are outside the content area.
    /// </summary>
    private DocumentOffset? HitTest(int absX, int absY)
    {
        if (State == null) return null;

        var localX = absX - Bounds.X;
        var localY = absY - Bounds.Y;

        if (GetHitZone(localX, localY) != HitZone.Content) return null;

        return ViewRenderer.HitTest(localX, localY, State, _viewportColumns, _viewportLines, _scrollOffset, _horizontalScrollOffset);
    }

    private void HandleMouseClick(InputBindingActionContext ctx)
    {
        var localX = ctx.MouseX - Bounds.X;
        var localY = ctx.MouseY - Bounds.Y;
        var zone = GetHitZone(localX, localY);

        switch (zone)
        {
            case HitZone.VerticalScrollbar:
                HandleVerticalScrollbarClick(localY);
                return;
            case HitZone.HorizontalScrollbar:
                HandleHorizontalScrollbarClick(localX);
                return;
            case HitZone.Corner:
                return; // Ignore corner clicks
        }

        var offset = HitTest(ctx.MouseX, ctx.MouseY);
        if (offset == null || State == null) return;
        State.SetCursorPosition(offset.Value);
        AfterMove();
    }

    private void HandleVerticalScrollbarClick(int localY)
    {
        if (State == null) return;
        var (thumbSize, thumbStart) = CalculateVerticalThumb();

        if (localY < thumbStart)
        {
            // Page up
            var pageSize = Math.Max(1, _viewportLines - 1);
            _scrollOffset = Math.Max(1, _scrollOffset - pageSize);
        }
        else if (localY >= thumbStart + thumbSize)
        {
            // Page down
            var totalLines = ViewRenderer.GetTotalLines(State.Document, Bounds.Width);
            var maxScroll = Math.Max(1, totalLines - _viewportLines + 1);
            var pageSize = Math.Max(1, _viewportLines - 1);
            _scrollOffset = Math.Min(maxScroll, _scrollOffset + pageSize);
        }

        MarkDirty();
    }

    private void HandleHorizontalScrollbarClick(int localX)
    {
        if (State == null) return;
        var (thumbSize, thumbStart) = CalculateHorizontalThumb();
        var maxWidth = ViewRenderer.GetMaxLineWidth(State.Document, _scrollOffset, _viewportLines, _viewportColumns);
        var maxHScroll = Math.Max(0, maxWidth - _viewportColumns);

        if (localX < thumbStart)
        {
            // Page left
            var pageSize = Math.Max(1, _viewportColumns - 1);
            _horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - pageSize);
        }
        else if (localX >= thumbStart + thumbSize)
        {
            // Page right
            var pageSize = Math.Max(1, _viewportColumns - 1);
            _horizontalScrollOffset = Math.Min(maxHScroll, _horizontalScrollOffset + pageSize);
        }

        MarkDirty();
    }

    private void HandleCtrlClick(InputBindingActionContext ctx)
    {
        var offset = HitTest(ctx.MouseX, ctx.MouseY);
        if (offset == null || State == null) return;

        State.AddCursorAtPosition(offset.Value);
        AfterMove();
    }

    private void HandleMouseDoubleClick(InputBindingActionContext ctx)
    {
        var offset = HitTest(ctx.MouseX, ctx.MouseY);
        if (offset == null || State == null) return;

        State.SelectWordAt(offset.Value);
        AfterMove();
    }

    private void HandleMouseTripleClick(InputBindingActionContext ctx)
    {
        var offset = HitTest(ctx.MouseX, ctx.MouseY);
        if (offset == null || State == null) return;

        State.SelectLineAt(offset.Value);
        AfterMove();
    }

    private DragHandler HandleDragStart(int startX, int startY)
    {
        var zone = GetHitZone(startX, startY);

        if (zone == HitZone.VerticalScrollbar)
            return HandleVerticalScrollbarDrag(startY);
        if (zone == HitZone.HorizontalScrollbar)
            return HandleHorizontalScrollbarDrag(startX);

        // Content area — text selection drag
        var absStartX = startX + Bounds.X;
        var absStartY = startY + Bounds.Y;
        var startOffset = HitTest(absStartX, absStartY);
        if (startOffset == null || State == null)
            return new DragHandler();

        State.SetCursorPosition(startOffset.Value);
        MarkDirty();

        return new DragHandler(
            onMove: (ctx, deltaX, deltaY) =>
            {
                var currentOffset = HitTest(absStartX + deltaX, absStartY + deltaY);
                if (currentOffset == null || State == null) return;

                State.SetCursorPosition(currentOffset.Value, extend: true);
                EnsureCursorVisible();
                MarkDirty();
            });
    }

    private DragHandler HandleVerticalScrollbarDrag(int localY)
    {
        if (State == null) return new DragHandler();
        var (thumbSize, thumbStart) = CalculateVerticalThumb();

        if (localY < thumbStart || localY >= thumbStart + thumbSize)
        {
            // Clicked on track, not thumb — page jump already handled by click
            HandleVerticalScrollbarClick(localY);
            return new DragHandler();
        }

        // Thumb drag
        var startScrollOffset = _scrollOffset;
        var totalLines = ViewRenderer.GetTotalLines(State.Document, Bounds.Width);
        var maxScroll = Math.Max(1, totalLines - _viewportLines);
        var scrollRange = _viewportLines - thumbSize;
        var contentPerPixel = scrollRange > 0 ? (double)maxScroll / scrollRange : 0;

        return DragHandler.Simple(
            onMove: (deltaX, deltaY) =>
            {
                if (contentPerPixel > 0)
                {
                    var newOffset = (int)Math.Round(startScrollOffset - 1 + deltaY * contentPerPixel) + 1;
                    _scrollOffset = Math.Clamp(newOffset, 1, maxScroll + 1);
                    MarkDirty();
                }
            });
    }

    private DragHandler HandleHorizontalScrollbarDrag(int localX)
    {
        if (State == null) return new DragHandler();
        var (thumbSize, thumbStart) = CalculateHorizontalThumb();

        if (localX < thumbStart || localX >= thumbStart + thumbSize)
        {
            HandleHorizontalScrollbarClick(localX);
            return new DragHandler();
        }

        // Thumb drag
        var startHOffset = _horizontalScrollOffset;
        var maxWidth = ViewRenderer.GetMaxLineWidth(State.Document, _scrollOffset, _viewportLines, _viewportColumns);
        var maxHScroll = Math.Max(1, maxWidth - _viewportColumns);
        var scrollRange = _viewportColumns - thumbSize;
        var contentPerPixel = scrollRange > 0 ? (double)maxHScroll / scrollRange : 0;

        return DragHandler.Simple(
            onMove: (deltaX, deltaY) =>
            {
                if (contentPerPixel > 0)
                {
                    var newOffset = (int)Math.Round(startHOffset + deltaX * contentPerPixel);
                    _horizontalScrollOffset = Math.Clamp(newOffset, 0, maxHScroll);
                    MarkDirty();
                }
            });
    }

    private void ScrollUp()
    {
        if (_scrollOffset > 1)
        {
            _scrollOffset = Math.Max(1, _scrollOffset - 3);
            MarkDirty();
        }
    }

    private void ScrollDown()
    {
        if (State == null) return;
        var totalLines = ViewRenderer.GetTotalLines(State.Document, Bounds.Width);
        var maxScroll = Math.Max(1, totalLines - _viewportLines + 1);
        if (_scrollOffset < maxScroll)
        {
            _scrollOffset = Math.Min(maxScroll, _scrollOffset + 3);
            MarkDirty();
        }
    }

    private void ScrollLeft()
    {
        if (_horizontalScrollOffset > 0)
        {
            _horizontalScrollOffset = Math.Max(0, _horizontalScrollOffset - 4);
            MarkDirty();
        }
    }

    private void ScrollRight()
    {
        if (State == null) return;
        var maxWidth = ViewRenderer.GetMaxLineWidth(State.Document, _scrollOffset, _viewportLines, _viewportColumns);
        var maxHScroll = Math.Max(0, maxWidth - _viewportColumns);
        if (_horizontalScrollOffset < maxHScroll)
        {
            _horizontalScrollOffset = Math.Min(maxHScroll, _horizontalScrollOffset + 4);
            MarkDirty();
        }
    }
}
