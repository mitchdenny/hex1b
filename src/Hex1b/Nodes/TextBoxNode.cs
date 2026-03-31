using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

public sealed class TextBoxNode : Hex1bNode
{
    /// <summary>
    /// The source widget that was reconciled into this node.
    /// Used to create typed event args.
    /// </summary>
    public TextBoxWidget? SourceWidget { get; set; }

    /// <summary>
    /// The text box state containing text, cursor position, and selection.
    /// </summary>
    internal TextBoxState State { get; set; } = new();

    /// <summary>
    /// Tracks the last text value provided by the widget during reconciliation.
    /// Used to detect when external code changes the text vs internal user input.
    /// </summary>
    internal string? LastWidgetText { get; set; }
    
    /// <summary>
    /// Gets or sets the text content. Convenience property that accesses State.Text.
    /// </summary>
    public string Text
    {
        get => State.Text;
        set
        {
            if (State.Text != value)
            {
                State.Text = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Internal action invoked when text content changes.
    /// </summary>
    internal Func<InputBindingActionContext, string, string, Task>? TextChangedAction { get; set; }

    /// <summary>
    /// Internal action invoked when Enter is pressed.
    /// </summary>
    internal Func<InputBindingActionContext, Task>? SubmitAction { get; set; }

    /// <summary>
    /// Custom paste handler that overrides the default text insertion behavior.
    /// When null, paste is handled by inserting text at cursor position.
    /// </summary>
    internal Func<Events.PasteEventArgs, Task>? CustomPasteAction { get; set; }

    /// <summary>
    /// Minimum width of the text box in columns.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Maximum width of the text box in columns.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// The character index of the first visible character in the viewport.
    /// Adjusted automatically to keep the cursor visible.
    /// </summary>
    internal int ScrollOffset { get; set; }

    /// <summary>
    /// When true, the text box supports multi-line editing.
    /// </summary>
    public bool IsMultiline { get; set; }

    /// <summary>
    /// Maximum number of lines allowed in multiline mode.
    /// When null, there is no limit.
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// When true, long lines are visually wrapped at word boundaries.
    /// </summary>
    public bool IsWordWrap { get; set; }

    /// <summary>
    /// Fixed height in lines. Null means 1 for single-line, content-based for multiline.
    /// </summary>
    public int? RequestedHeight { get; set; }

    /// <summary>
    /// The 0-based index of the first visible line for vertical scrolling.
    /// </summary>
    internal int VerticalScrollOffset { get; set; }
    
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

    private bool _isHovered;
    public override bool IsHovered 
    { 
        get => _isHovered; 
        set 
        {
            if (_isHovered != value)
            {
                _isHovered = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;

    /// <summary>
    /// TextBox uses a blinking bar cursor to indicate text input position.
    /// </summary>
    public override CursorShape PreferredCursorShape => CursorShape.BlinkingBar;

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Navigation
        bindings.Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.MoveLeft, MoveLeft, "Move left");
        bindings.Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.MoveRight, MoveRight, "Move right");
        bindings.Key(Hex1bKey.Home).Triggers(TextBoxWidget.MoveHome, MoveHome, "Go to start");
        bindings.Key(Hex1bKey.End).Triggers(TextBoxWidget.MoveEnd, MoveEnd, "Go to end");
        
        // Word navigation (Ctrl+Arrow)
        bindings.Ctrl().Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.MoveWordLeft, MoveWordLeft, "Move to previous word");
        bindings.Ctrl().Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.MoveWordRight, MoveWordRight, "Move to next word");
        
        // Selection navigation
        bindings.Shift().Key(Hex1bKey.LeftArrow).Triggers(TextBoxWidget.SelectLeft, SelectLeft, "Extend selection left");
        bindings.Shift().Key(Hex1bKey.RightArrow).Triggers(TextBoxWidget.SelectRight, SelectRight, "Extend selection right");
        bindings.Shift().Key(Hex1bKey.Home).Triggers(TextBoxWidget.SelectToStart, SelectToStart, "Select to start");
        bindings.Shift().Key(Hex1bKey.End).Triggers(TextBoxWidget.SelectToEnd, SelectToEnd, "Select to end");
        
        // Editing - use async handlers to fire callbacks
        bindings.Key(Hex1bKey.Backspace).Triggers(TextBoxWidget.DeleteBackward, DeleteBackwardAsync, "Delete backward");
        bindings.Key(Hex1bKey.Delete).Triggers(TextBoxWidget.DeleteForward, DeleteForwardAsync, "Delete forward");
        
        // Word deletion (Ctrl+Backspace/Delete)
        bindings.Ctrl().Key(Hex1bKey.Backspace).Triggers(TextBoxWidget.DeleteWordBackward, DeleteWordBackwardAsync, "Delete previous word");
        bindings.Ctrl().Key(Hex1bKey.Delete).Triggers(TextBoxWidget.DeleteWordForward, DeleteWordForwardAsync, "Delete next word");

        // Multiline navigation and Enter handling
        if (IsMultiline)
        {
            bindings.Key(Hex1bKey.UpArrow).Triggers(TextBoxWidget.MoveUp, MoveUp, "Move up");
            bindings.Key(Hex1bKey.DownArrow).Triggers(TextBoxWidget.MoveDown, MoveDownAsync, "Move down");
            bindings.Shift().Key(Hex1bKey.UpArrow).Triggers(TextBoxWidget.SelectUp, SelectUp, "Extend selection up");
            bindings.Shift().Key(Hex1bKey.DownArrow).Triggers(TextBoxWidget.SelectDown, SelectDown, "Extend selection down");
            bindings.Key(Hex1bKey.Enter).Triggers(TextBoxWidget.InsertNewline, InsertNewlineAsync, "Insert newline");
        }
        else if (SubmitAction != null)
        {
            // Submit (Enter key) — single-line only
            bindings.Key(Hex1bKey.Enter).Triggers(TextBoxWidget.Submit, SubmitAction, "Submit");
        }
        
        // Selection
        bindings.Ctrl().Key(Hex1bKey.A).Triggers(TextBoxWidget.SelectAll, SelectAll, "Select all");
        
        // Mouse: double-click to select all
        bindings.Mouse(MouseButton.Left).DoubleClick().Triggers(TextBoxWidget.SelectAll, SelectAll, "Select all");
        
        // Character input - matches any printable text (including emojis)
        bindings.AnyCharacter().Action(InsertTextAsync, "Type text");
    }

    /// <summary>
    /// Handles bracketed paste. If a custom paste handler is set via OnPaste(),
    /// it is called instead of the default text insertion behavior.
    /// For default behavior: reads full paste content and inserts at cursor position.
    /// For single-line text boxes, newlines are replaced with spaces.
    /// </summary>
    public override async Task<InputResult> HandlePasteAsync(Hex1bPasteEvent pasteEvent)
    {
        // Custom handler overrides default behavior
        if (CustomPasteAction != null)
        {
            await CustomPasteAction(new Events.PasteEventArgs(pasteEvent.Paste));
            return InputResult.Handled;
        }

        var pastedText = await pasteEvent.Paste.ReadToEndAsync();

        if (string.IsNullOrEmpty(pastedText))
            return InputResult.Handled;

        // Single-line text box: replace newlines with spaces
        // Multiline: normalize line endings to \n but preserve newlines
        pastedText = pastedText.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!IsMultiline)
        {
            pastedText = pastedText.Replace("\n", " ");
        }

        var oldText = State.Text;

        if (State.HasSelection)
        {
            DeleteSelection();
        }

        State.Text = State.Text.Insert(State.CursorPosition, pastedText);
        State.CursorPosition += pastedText.Length;
        State.ResetPreferredColumn();
        MarkDirty();

        if (TextChangedAction != null && oldText != State.Text)
        {
            // Create a minimal context for the paste callback
            var ctx = new InputBindingActionContext(null!, null, default);
            await TextChangedAction(ctx, oldText, State.Text);
        }

        return InputResult.Handled;
    }

    private async Task InsertTextAsync(string text, InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        // If there's a selection, delete it first
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        State.Text = State.Text.Insert(State.CursorPosition, text);
        State.CursorPosition += text.Length;
        State.ResetPreferredColumn();
        MarkDirty();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private void MoveLeft()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionStart;
            State.ClearSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveRight()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionEnd;
            State.ClearSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveHome()
    {
        State.ClearSelection();
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line);
        }
        else
        {
            State.CursorPosition = 0;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveEnd()
    {
        State.ClearSelection();
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
        }
        else
        {
            State.CursorPosition = State.Text.Length;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveWordLeft()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionStart;
            State.ClearSelection();
        }
        State.CursorPosition = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveWordRight()
    {
        if (State.HasSelection)
        {
            State.CursorPosition = State.SelectionEnd;
            State.ClearSelection();
        }
        State.CursorPosition = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectLeft()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition > 0)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectRight()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (State.CursorPosition < State.Text.Length)
        {
            // Move by grapheme cluster, not by char
            State.CursorPosition = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectToStart()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line);
        }
        else
        {
            State.CursorPosition = 0;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectToEnd()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        if (IsMultiline)
        {
            var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
        }
        else
        {
            State.CursorPosition = State.Text.Length;
        }
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectWordLeft()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectWordRight()
    {
        if (!State.SelectionAnchor.HasValue)
        {
            State.SelectionAnchor = State.CursorPosition;
        }
        State.CursorPosition = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void MoveUp()
    {
        State.MoveUp();
        MarkDirty();
    }

    private async Task MoveDownAsync(InputBindingActionContext ctx)
    {
        var (line, _) = State.OffsetToLineColumn(State.CursorPosition);
        if (line >= State.GetLineCount() - 1)
        {
            // No line below — move to end of current line and insert newline
            if (!CanInsertNewline())
                return;

            var oldText = State.Text;
            // Move cursor to end of current line
            State.CursorPosition = State.GetLineStartOffset(line) + State.GetLineLength(line);
            State.ClearSelection();
            State.InsertNewline();
            MarkDirty();

            if (TextChangedAction != null && oldText != State.Text)
                await TextChangedAction(ctx, oldText, State.Text);
        }
        else
        {
            State.MoveDown();
            MarkDirty();
        }
    }

    private void SelectUp()
    {
        State.MoveUp(extend: true);
        MarkDirty();
    }

    private void SelectDown()
    {
        State.MoveDown(extend: true);
        MarkDirty();
    }

    private bool CanInsertNewline()
        => MaxLines is null || State.GetLineCount() < MaxLines.Value;

    private async Task InsertNewlineAsync(InputBindingActionContext ctx)
    {
        if (!CanInsertNewline())
            return;

        var oldText = State.Text;
        State.InsertNewline();
        State.ResetPreferredColumn();
        MarkDirty();

        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteBackwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition > 0)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterStart = GraphemeHelper.GetPreviousClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = State.CursorPosition - clusterStart;
            State.Text = State.Text.Remove(clusterStart, clusterLength);
            State.CursorPosition = clusterStart;
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteForwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            // Delete the entire grapheme cluster, not just one char
            var clusterEnd = GraphemeHelper.GetNextClusterBoundary(State.Text, State.CursorPosition);
            var clusterLength = clusterEnd - State.CursorPosition;
            State.Text = State.Text.Remove(State.CursorPosition, clusterLength);
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteWordBackwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition > 0)
        {
            var wordStart = GraphemeHelper.GetPreviousWordBoundary(State.Text, State.CursorPosition);
            var deleteLength = State.CursorPosition - wordStart;
            State.Text = State.Text.Remove(wordStart, deleteLength);
            State.CursorPosition = wordStart;
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private async Task DeleteWordForwardAsync(InputBindingActionContext ctx)
    {
        var oldText = State.Text;
        
        if (State.HasSelection)
        {
            DeleteSelection();
        }
        else if (State.CursorPosition < State.Text.Length)
        {
            var wordEnd = GraphemeHelper.GetNextWordBoundary(State.Text, State.CursorPosition);
            var deleteLength = wordEnd - State.CursorPosition;
            State.Text = State.Text.Remove(State.CursorPosition, deleteLength);
            MarkDirty();
        }
        State.ResetPreferredColumn();
        
        // Fire callback if text changed
        if (TextChangedAction != null && oldText != State.Text)
        {
            await TextChangedAction(ctx, oldText, State.Text);
        }
    }

    private void DeleteSelection()
    {
        if (!State.HasSelection) return;
        var start = State.SelectionStart;
        var end = State.SelectionEnd;
        State.Text = State.Text[..start] + State.Text[end..];
        State.CursorPosition = start;
        State.ClearSelection();
        State.ResetPreferredColumn();
        MarkDirty();
    }

    private void SelectAll()
    {
        State.SelectAll();
        MarkDirty();
    }

    /// <summary>
    /// Handles mouse click to position the cursor within the text.
    /// </summary>
    public override InputResult HandleMouseClick(int localX, int localY, Hex1bMouseEvent mouseEvent)
    {
        // Only handle left clicks that aren't double/triple clicks
        if (mouseEvent.Button != MouseButton.Left || mouseEvent.ClickCount > 1)
        {
            return InputResult.NotHandled;
        }

        if (IsMultiline)
        {
            return HandleMouseClickMultiline(localX, localY);
        }

        // The TextBox renders as "[text]" so localX=0 is '[', localX=1 is first char
        var textColumn = localX - 1; // Subtract 1 for the '[' bracket
        
        if (textColumn < 0)
        {
            // Clicked on or before the '[' - position at start
            State.ClearSelection();
            State.CursorPosition = ScrollOffset;
        }
        else
        {
            // Find the cursor position in the visible text, then offset to full text
            var visStart = Math.Min(ScrollOffset, State.Text.Length);
            var visEnd = Math.Min(visStart + Math.Max(0, Bounds.Width - 2), State.Text.Length);
            var visibleText = State.Text[visStart..visEnd];
            var visPos = DisplayColumnToVisibleTextPosition(visibleText, textColumn);
            State.ClearSelection();
            State.CursorPosition = visStart + visPos;
        }

        return InputResult.Handled;
    }

    private InputResult HandleMouseClickMultiline(int localX, int localY)
    {
        var viewportWidth = Bounds.Width;
        var visualLines = BuildVisualLines(viewportWidth);

        var clickedVisualLine = VerticalScrollOffset + localY;
        if (clickedVisualLine >= visualLines.Count)
        {
            // Clicked below content — position at end
            State.ClearSelection();
            State.CursorPosition = State.Text.Length;
        }
        else
        {
            var vLine = visualLines[clickedVisualLine];
            var visPos = DisplayColumnToVisibleTextPosition(vLine.Text, localX);
            State.ClearSelection();
            State.CursorPosition = vLine.DocStartOffset + visPos;
        }

        MarkDirty();
        return InputResult.Handled;
    }

    /// <summary>
    /// Converts a display column (0-based, relative to text start) to a text cursor position.
    /// Accounts for variable-width characters (wide chars, emoji, etc.).
    /// </summary>
    private int DisplayColumnToTextPosition(int displayColumn)
    {
        if (string.IsNullOrEmpty(State.Text))
            return 0;

        int currentColumn = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(State.Text);
        int lastPosition = 0;

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            var graphemeStart = enumerator.ElementIndex;

            // If the click is before or at the start of this grapheme
            if (displayColumn < currentColumn + graphemeWidth)
            {
                // Click is in the first half of the grapheme - position before it
                // Click is in the second half - position after it
                var midpoint = currentColumn + (graphemeWidth / 2.0);
                if (displayColumn < midpoint)
                {
                    return graphemeStart;
                }
                else
                {
                    return graphemeStart + grapheme.Length;
                }
            }

            currentColumn += graphemeWidth;
            lastPosition = graphemeStart + grapheme.Length;
        }

        // Click is past the end of the text
        return State.Text.Length;
    }

    /// <summary>
    /// Maps a display column to a character position within a visible text string.
    /// Used for hover cursor positioning when text is scrolled.
    /// </summary>
    private static int DisplayColumnToVisibleTextPosition(string visibleText, int displayColumn)
    {
        if (string.IsNullOrEmpty(visibleText))
            return 0;

        int currentColumn = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(visibleText);
        int lastPosition = 0;

        while (enumerator.MoveNext())
        {
            var grapheme = (string)enumerator.Current;
            var graphemeWidth = DisplayWidth.GetGraphemeWidth(grapheme);
            var graphemeStart = enumerator.ElementIndex;

            if (displayColumn < currentColumn + graphemeWidth)
            {
                var midpoint = currentColumn + (graphemeWidth / 2.0);
                return displayColumn < midpoint ? graphemeStart : graphemeStart + grapheme.Length;
            }

            currentColumn += graphemeWidth;
            lastPosition = graphemeStart + grapheme.Length;
        }

        return visibleText.Length;
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        if (IsMultiline)
        {
            return MeasureMultiline(constraints);
        }

        var textDisplayWidth = Math.Max(DisplayWidth.GetStringWidth(State.Text), 1);

        int width;
        if (MinWidth.HasValue || MaxWidth.HasValue)
        {
            width = MinWidth.HasValue ? Math.Max(textDisplayWidth, MinWidth.Value) : textDisplayWidth;
            if (MaxWidth.HasValue)
                width = Math.Min(width, MaxWidth.Value);
        }
        else
        {
            // Default bracket mode: "[text]" - 2 chars for brackets
            width = textDisplayWidth + 2;
        }

        var height = 1;
        return constraints.Constrain(new Size(width, height));
    }

    private Size MeasureMultiline(Constraints constraints)
    {
        // Width: fill available space, or use min/max width
        int width;
        if (MinWidth.HasValue || MaxWidth.HasValue)
        {
            width = MinWidth.HasValue ? MinWidth.Value : 1;
            if (MaxWidth.HasValue)
                width = Math.Min(width, MaxWidth.Value);
        }
        else
        {
            // Fill available width
            width = constraints.MaxWidth > 0 ? constraints.MaxWidth : 40;
        }

        // Height: use requested height, or size to content
        int height;
        if (RequestedHeight.HasValue)
        {
            height = RequestedHeight.Value;
        }
        else
        {
            // Size to content — count visual lines (with word wrap if enabled)
            var viewportWidth = width; // fill mode in multiline
            height = IsWordWrap
                ? ComputeWrappedLineCount(viewportWidth)
                : State.GetLineCount();
            height = Math.Max(1, height);
        }

        return constraints.Constrain(new Size(width, height));
    }

    /// <summary>
    /// Adjusts <see cref="ScrollOffset"/> so the cursor position stays within the visible viewport.
    /// </summary>
    private void EnsureCursorInViewport(int viewportWidth)
    {
        var cursor = State.CursorPosition;
        var text = State.Text;

        // Clamp scroll offset to valid range
        ScrollOffset = Math.Clamp(ScrollOffset, 0, Math.Max(0, text.Length));

        // If cursor is before the viewport, scroll left
        if (cursor < ScrollOffset)
        {
            ScrollOffset = cursor;
        }

        // If cursor is at or past the right edge, scroll right.
        // We need at least 1 column for the cursor character (or the end-of-text cursor space).
        if (viewportWidth > 0 && cursor >= ScrollOffset + viewportWidth)
        {
            ScrollOffset = cursor - viewportWidth + 1;
        }

        // Final clamp
        ScrollOffset = Math.Max(0, ScrollOffset);
    }

    /// <summary>
    /// Extracts the visible portion of text for the current viewport, returning
    /// the visible string and the cursor position relative to the visible string.
    /// </summary>
    private (string visibleText, int visibleCursor, int visibleSelStart, int visibleSelEnd)
        GetViewportSlice(int viewportWidth)
    {
        var text = State.Text;
        var cursor = State.CursorPosition;

        // Calculate how many chars fit in the viewport starting from ScrollOffset
        var visStart = Math.Min(ScrollOffset, text.Length);
        var visEnd = Math.Min(visStart + viewportWidth, text.Length);
        var visibleText = text[visStart..visEnd];
        var visibleCursor = cursor - visStart;

        // Clamp selection to visible range
        var visSelStart = 0;
        var visSelEnd = 0;
        if (State.HasSelection)
        {
            visSelStart = Math.Clamp(State.SelectionStart - visStart, 0, visibleText.Length);
            visSelEnd = Math.Clamp(State.SelectionEnd - visStart, 0, visibleText.Length);
        }

        return (visibleText, visibleCursor, visSelStart, visSelEnd);
    }

    #region Multiline helpers

    /// <summary>
    /// Represents one visual line to be rendered, potentially a wrapped segment of a document line.
    /// </summary>
    private readonly record struct VisualLine(int DocLine, string Text, int DocStartOffset, bool IsContinuation);

    /// <summary>
    /// Computes the total number of visual lines after word wrapping.
    /// </summary>
    private int ComputeWrappedLineCount(int viewportWidth)
    {
        if (viewportWidth <= 0) return State.GetLineCount();
        var count = 0;
        var lineCount = State.GetLineCount();
        for (var i = 0; i < lineCount; i++)
        {
            var lineText = State.GetLineText(i);
            count += WrapLine(lineText, viewportWidth).Count;
        }
        return Math.Max(1, count);
    }

    /// <summary>
    /// Wraps a single line of text into segments that fit within viewportWidth.
    /// Returns a list of (segment text, start column in original line).
    /// </summary>
    private static List<(string text, int startCol)> WrapLine(string line, int viewportWidth)
    {
        if (viewportWidth <= 0 || line.Length <= viewportWidth)
            return [(line, 0)];

        var segments = new List<(string text, int startCol)>();
        var offset = 0;

        while (offset < line.Length)
        {
            var remaining = line.Length - offset;
            if (remaining <= viewportWidth)
            {
                segments.Add((line[offset..], offset));
                break;
            }

            // Try to break at a word boundary (space) within the viewport width
            var breakPoint = line.LastIndexOf(' ', offset + viewportWidth - 1, viewportWidth);
            if (breakPoint <= offset)
            {
                // No word boundary found — hard break
                breakPoint = offset + viewportWidth;
            }
            else
            {
                breakPoint++; // Include the space in this segment
            }

            segments.Add((line[offset..breakPoint], offset));
            offset = breakPoint;
        }

        if (segments.Count == 0)
            segments.Add(("", 0));

        return segments;
    }

    /// <summary>
    /// Builds the list of visual lines for the entire document, applying word wrap if enabled.
    /// </summary>
    private List<VisualLine> BuildVisualLines(int viewportWidth)
    {
        var result = new List<VisualLine>();
        var lineCount = State.GetLineCount();

        for (var docLine = 0; docLine < lineCount; docLine++)
        {
            var lineText = State.GetLineText(docLine);
            var lineStartOffset = State.GetLineStartOffset(docLine);

            if (IsWordWrap && viewportWidth > 0 && lineText.Length > viewportWidth)
            {
                var segments = WrapLine(lineText, viewportWidth);
                for (var i = 0; i < segments.Count; i++)
                {
                    result.Add(new VisualLine(docLine, segments[i].text, lineStartOffset + segments[i].startCol, i > 0));
                }
            }
            else
            {
                result.Add(new VisualLine(docLine, lineText, lineStartOffset, false));
            }
        }

        if (result.Count == 0)
            result.Add(new VisualLine(0, "", 0, false));

        return result;
    }

    /// <summary>
    /// Finds which visual line index contains the given document offset.
    /// </summary>
    private static int FindVisualLineForOffset(List<VisualLine> visualLines, int offset)
    {
        for (var i = visualLines.Count - 1; i >= 0; i--)
        {
            if (offset >= visualLines[i].DocStartOffset)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Ensures the cursor's visual line is visible within the vertical viewport.
    /// </summary>
    private void EnsureCursorLineInViewport(int visibleLines, List<VisualLine> visualLines)
    {
        var cursorVisualLine = FindVisualLineForOffset(visualLines, State.CursorPosition);
        VerticalScrollOffset = Math.Clamp(VerticalScrollOffset, 0, Math.Max(0, visualLines.Count - 1));

        if (cursorVisualLine < VerticalScrollOffset)
        {
            VerticalScrollOffset = cursorVisualLine;
        }
        else if (cursorVisualLine >= VerticalScrollOffset + visibleLines)
        {
            VerticalScrollOffset = cursorVisualLine - visibleLines + 1;
        }
    }

    /// <summary>
    /// Renders a multiline text box with vertical scrolling and per-line horizontal viewport.
    /// </summary>
    private void RenderMultiline(Hex1bRenderContext context, string globalColors, string resetToGlobal,
        Hex1bColor fillBg, Hex1bColor cursorFg, Hex1bColor cursorBg,
        Hex1bColor selFg, Hex1bColor selBg)
    {
        var viewportWidth = Bounds.Width;
        var viewportHeight = Bounds.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        var fillBgAnsi = fillBg.ToBackgroundAnsi();
        var visualLines = BuildVisualLines(viewportWidth);

        // Ensure cursor is vertically visible
        EnsureCursorLineInViewport(viewportHeight, visualLines);

        var cursor = State.CursorPosition;
        var selStart = State.HasSelection ? State.SelectionStart : -1;
        var selEnd = State.HasSelection ? State.SelectionEnd : -1;

        for (var row = 0; row < viewportHeight; row++)
        {
            var visualIdx = VerticalScrollOffset + row;
            var screenY = Bounds.Y + row;

            if (visualIdx >= visualLines.Count)
            {
                // Empty line — fill with background
                var emptyLine = new string(' ', viewportWidth);
                var output = $"{globalColors}{fillBgAnsi}{emptyLine}{resetToGlobal}";
                if (context.CurrentLayoutProvider != null)
                    context.WriteClipped(Bounds.X, screenY, output);
                else
                    context.Write(output);
                continue;
            }

            var vLine = visualLines[visualIdx];
            var lineText = vLine.Text;
            var lineDocStart = vLine.DocStartOffset;
            var lineDocEnd = lineDocStart + lineText.Length;

            // Compute cursor column within this visual line (if cursor is on this line)
            var cursorOnThisLine = cursor >= lineDocStart && cursor <= lineDocEnd;
            var cursorCol = cursorOnThisLine ? cursor - lineDocStart : -1;

            // Compute selection range clipped to this visual line
            var lineSelStart = -1;
            var lineSelEnd = -1;
            if (selStart >= 0 && selEnd > selStart && selEnd > lineDocStart && selStart < lineDocEnd)
            {
                lineSelStart = Math.Max(0, selStart - lineDocStart);
                lineSelEnd = Math.Min(lineText.Length, selEnd - lineDocStart);
            }

            // Per-line horizontal viewport scrolling (non-word-wrap only).
            // When word wrap is off, each line can extend beyond the viewport width.
            // The cursor line scrolls to keep the cursor visible; other lines snap to offset 0.
            if (!IsWordWrap && lineText.Length > viewportWidth)
            {
                var hScroll = 0;
                if (cursorOnThisLine && cursorCol >= viewportWidth)
                {
                    // Scroll so cursor is visible at the right edge
                    hScroll = cursorCol - viewportWidth + 1;
                }

                var visEnd = Math.Min(hScroll + viewportWidth, lineText.Length);
                lineText = lineText[hScroll..visEnd];

                // Adjust cursor and selection positions for the viewport slice
                if (cursorCol >= 0)
                    cursorCol -= hScroll;
                if (lineSelStart >= 0)
                {
                    lineSelStart = Math.Clamp(lineSelStart - hScroll, 0, lineText.Length);
                    lineSelEnd = Math.Clamp(lineSelEnd - hScroll, 0, lineText.Length);
                    if (lineSelStart >= lineSelEnd)
                    {
                        lineSelStart = -1;
                        lineSelEnd = -1;
                    }
                }
            }

            var lineOutput = RenderMultilineLine(lineText, cursorCol, lineSelStart, lineSelEnd,
                viewportWidth, globalColors, resetToGlobal, fillBgAnsi,
                cursorFg, cursorBg, selFg, selBg);

            if (context.CurrentLayoutProvider != null)
                context.WriteClipped(Bounds.X, screenY, lineOutput);
            else
                context.Write(lineOutput);
        }
    }

    /// <summary>
    /// Renders a single line of a multiline text box with cursor and selection highlighting.
    /// </summary>
    private string RenderMultilineLine(string lineText, int cursorCol, int selStart, int selEnd,
        int viewportWidth, string globalColors, string resetToGlobal, string fillBgAnsi,
        Hex1bColor cursorFg, Hex1bColor cursorBg, Hex1bColor selFg, Hex1bColor selBg)
    {
        var textDisplayWidth = DisplayWidth.GetStringWidth(lineText);
        var padding = Math.Max(0, viewportWidth - textDisplayWidth);

        if (IsFocused && cursorCol >= 0)
        {
            if (selStart >= 0 && selEnd > selStart)
            {
                // Selection on this line
                var beforeSel = lineText[..selStart];
                var selected = lineText[selStart..selEnd];
                var afterSel = lineText[selEnd..];
                var padStr = new string(' ', padding);
                return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
            }
            else
            {
                // Cursor on this line, no selection
                var before = lineText[..cursorCol];
                string cursorCluster;
                string after;
                var cursorPadding = padding;

                if (cursorCol < lineText.Length)
                {
                    var clusterLength = GraphemeHelper.GetClusterLength(lineText, cursorCol);
                    cursorCluster = lineText.Substring(cursorCol, clusterLength);
                    after = lineText[(cursorCol + clusterLength)..];
                }
                else
                {
                    cursorCluster = " ";
                    after = "";
                    cursorPadding = Math.Max(0, cursorPadding - 1);
                }

                var padStr = new string(' ', cursorPadding);
                return $"{globalColors}{fillBgAnsi}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorCluster}{resetToGlobal}{fillBgAnsi}{after}{padStr}{resetToGlobal}";
            }
        }
        else if (selStart >= 0 && selEnd > selStart)
        {
            // Selection on this line but not focused (shouldn't normally happen, but handle gracefully)
            var beforeSel = lineText[..selStart];
            var selected = lineText[selStart..selEnd];
            var afterSel = lineText[selEnd..];
            var padStr = new string(' ', padding);
            return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
        }
        else
        {
            // No cursor, no selection on this line
            var padStr = new string(' ', padding);
            return $"{globalColors}{fillBgAnsi}{lineText}{padStr}{resetToGlobal}";
        }
    }

    #endregion

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var leftBracket = theme.Get(TextBoxTheme.LeftBracket);
        var rightBracket = theme.Get(TextBoxTheme.RightBracket);
        var cursorFg = theme.Get(TextBoxTheme.CursorForegroundColor);
        var cursorBg = theme.Get(TextBoxTheme.CursorBackgroundColor);
        var selFg = theme.Get(TextBoxTheme.SelectionForegroundColor);
        var selBg = theme.Get(TextBoxTheme.SelectionBackgroundColor);
        var hoverCursorFg = theme.Get(TextBoxTheme.HoverCursorForegroundColor);
        var hoverCursorBg = theme.Get(TextBoxTheme.HoverCursorBackgroundColor);
        var useFillMode = theme.Get(TextBoxTheme.UseFillMode);
        var fillBg = IsFocused
            ? theme.Get(TextBoxTheme.FocusedFillBackgroundColor)
            : theme.Get(TextBoxTheme.FillBackgroundColor);
        
        var globalColors = theme.GetGlobalColorCodes();
        var resetToGlobal = theme.GetResetToGlobalCodes();

        // Multiline text boxes use their own render path
        if (IsMultiline)
        {
            RenderMultiline(context, globalColors, resetToGlobal, fillBg, cursorFg, cursorBg, selFg, selBg);
            return;
        }

        // Calculate viewport width (the number of text columns available)
        var viewportWidth = useFillMode ? Bounds.Width : Math.Max(0, Bounds.Width - 2);
        var textLen = State.Text.Length;

        // When bounds haven't been set (direct Render without layout) or the text fits,
        // show the full text without viewport slicing.
        var needsViewport = viewportWidth > 0 && textLen > viewportWidth;

        string visibleText;
        int visCursor, visSelStart, visSelEnd;

        if (needsViewport)
        {
            EnsureCursorInViewport(viewportWidth);
            (visibleText, visCursor, visSelStart, visSelEnd) = GetViewportSlice(viewportWidth);
        }
        else
        {
            ScrollOffset = 0;
            visibleText = State.Text;
            visCursor = State.CursorPosition;
            visSelStart = State.HasSelection ? State.SelectionStart : 0;
            visSelEnd = State.HasSelection ? State.SelectionEnd : 0;
        }

        string output;
        if (useFillMode)
        {
            output = RenderFillMode(visibleText, visCursor, globalColors, resetToGlobal,
                fillBg, cursorFg, cursorBg, selFg, selBg, hoverCursorFg, hoverCursorBg, context,
                visSelStart, visSelEnd);
        }
        else if (IsFocused)
        {
            if (State.HasSelection && visSelStart != visSelEnd)
            {
                var beforeSel = visibleText[..visSelStart];
                var selected = visibleText[visSelStart..visSelEnd];
                var afterSel = visibleText[visSelEnd..];
                
                output = $"{globalColors}{leftBracket}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{afterSel}{rightBracket}";
            }
            else
            {
                var before = visibleText[..visCursor];
                string cursorCluster;
                string after;
                if (visCursor < visibleText.Length)
                {
                    var clusterLength = GraphemeHelper.GetClusterLength(visibleText, visCursor);
                    cursorCluster = visibleText.Substring(visCursor, clusterLength);
                    after = visibleText[(visCursor + clusterLength)..];
                }
                else
                {
                    cursorCluster = " ";
                    after = "";
                }
                
                output = $"{globalColors}{leftBracket}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorCluster}{resetToGlobal}{after}{rightBracket}";
            }
        }
        else if (IsHovered && context.MouseX >= 0 && context.MouseY >= 0)
        {
            output = RenderWithHoverCursor(visibleText, leftBracket, rightBracket, 
                globalColors, resetToGlobal, hoverCursorFg, hoverCursorBg, context);
        }
        else
        {
            output = $"{globalColors}{leftBracket}{visibleText}{rightBracket}{resetToGlobal}";
        }
        
        // Use clipped rendering when a layout provider is active
        if (context.CurrentLayoutProvider != null)
        {
            context.WriteClipped(Bounds.X, Bounds.Y, output);
        }
        else
        {
            context.Write(output);
        }
    }

    /// <summary>
    /// Renders the text box in fill mode: no brackets, background-filled to the measured width.
    /// Text and cursor positions are already adjusted for the viewport.
    /// </summary>
    private string RenderFillMode(
        string text,
        int cursor,
        string globalColors,
        string resetToGlobal,
        Hex1bColor fillBg,
        Hex1bColor cursorFg,
        Hex1bColor cursorBg,
        Hex1bColor selFg,
        Hex1bColor selBg,
        Hex1bColor hoverCursorFg,
        Hex1bColor hoverCursorBg,
        Hex1bRenderContext context,
        int visSelStart = 0,
        int visSelEnd = 0)
    {
        var measuredWidth = Bounds.Width;
        var textDisplayWidth = DisplayWidth.GetStringWidth(text);
        var padding = Math.Max(0, measuredWidth - textDisplayWidth);
        var fillBgAnsi = fillBg.ToBackgroundAnsi();

        if (IsFocused)
        {
            if (State.HasSelection && visSelStart != visSelEnd)
            {
                var beforeSel = text[..visSelStart];
                var selected = text[visSelStart..visSelEnd];
                var afterSel = text[visSelEnd..];

                // Pad after the text to fill the measured width
                var afterSelWidth = DisplayWidth.GetStringWidth(afterSel);
                var padStr = new string(' ', Math.Max(0, padding - (IsFocused && cursor >= text.Length ? 1 : 0)));

                return $"{globalColors}{fillBgAnsi}{beforeSel}{selFg.ToForegroundAnsi()}{selBg.ToBackgroundAnsi()}{selected}{resetToGlobal}{fillBgAnsi}{afterSel}{padStr}{resetToGlobal}";
            }
            else
            {
                var before = text[..cursor];
                string cursorCluster;
                string after;
                int cursorClusterWidth;
                if (cursor < text.Length)
                {
                    var clusterLength = GraphemeHelper.GetClusterLength(text, cursor);
                    cursorCluster = text.Substring(cursor, clusterLength);
                    cursorClusterWidth = DisplayWidth.GetStringWidth(cursorCluster);
                    after = text[(cursor + clusterLength)..];
                }
                else
                {
                    cursorCluster = " ";
                    cursorClusterWidth = 1;
                    after = "";
                    // Cursor space consumes one padding char
                    padding = Math.Max(0, padding - 1);
                }

                var padStr = new string(' ', padding);
                return $"{globalColors}{fillBgAnsi}{before}{cursorFg.ToForegroundAnsi()}{cursorBg.ToBackgroundAnsi()}{cursorCluster}{resetToGlobal}{fillBgAnsi}{after}{padStr}{resetToGlobal}";
            }
        }
        else if (IsHovered && context.MouseX >= 0 && context.MouseY >= 0)
        {
            var localMouseX = context.MouseX - Bounds.X;
            // Map display column to position within the visible text
            var hoverCursorPos = DisplayColumnToVisibleTextPosition(text, localMouseX);

            string before = text[..hoverCursorPos];
            string hoverCluster;
            string after;
            int extraPaddingReduction = 0;

            if (hoverCursorPos < text.Length)
            {
                var clusterLength = GraphemeHelper.GetClusterLength(text, hoverCursorPos);
                hoverCluster = text.Substring(hoverCursorPos, clusterLength);
                after = text[(hoverCursorPos + clusterLength)..];
            }
            else
            {
                hoverCluster = " ";
                after = "";
                extraPaddingReduction = 1;
            }

            var hoverColors = "";
            if (!hoverCursorFg.IsDefault) hoverColors += hoverCursorFg.ToForegroundAnsi();
            if (!hoverCursorBg.IsDefault) hoverColors += hoverCursorBg.ToBackgroundAnsi();

            var padStr = new string(' ', Math.Max(0, padding - extraPaddingReduction));
            return $"{globalColors}{fillBgAnsi}{before}{hoverColors}{hoverCluster}{resetToGlobal}{fillBgAnsi}{after}{padStr}{resetToGlobal}";
        }
        else
        {
            var padStr = new string(' ', padding);
            return $"{globalColors}{fillBgAnsi}{text}{padStr}{resetToGlobal}";
        }
    }
    
    /// <summary>
    /// Renders the text with a faint hover cursor showing where clicking would position the cursor.
    /// </summary>
    private string RenderWithHoverCursor(
        string text, 
        string leftBracket, 
        string rightBracket,
        string globalColors,
        string resetToGlobal,
        Hex1bColor hoverCursorFg,
        Hex1bColor hoverCursorBg,
        Hex1bRenderContext context)
    {
        // Calculate local mouse position relative to this node
        var localMouseX = context.MouseX - Bounds.X;
        
        // Convert to text column (subtract 1 for '[' bracket)
        var textColumn = localMouseX - 1;
        
        // If mouse is outside the text area, show cursor at the nearest edge
        if (textColumn < 0)
        {
            // Mouse is on or before '[' - show cursor at start
            textColumn = 0;
        }
        
        // Find the cursor position within the visible text
        var hoverCursorPos = DisplayColumnToVisibleTextPosition(text, textColumn);
        
        // Get the grapheme cluster at the hover position (or space if at end)
        string before = text[..hoverCursorPos];
        string hoverCluster;
        string after;
        
        if (hoverCursorPos < text.Length)
        {
            var clusterLength = GraphemeHelper.GetClusterLength(text, hoverCursorPos);
            hoverCluster = text.Substring(hoverCursorPos, clusterLength);
            after = text[(hoverCursorPos + clusterLength)..];
        }
        else
        {
            // At end of text - show a space as the hover target
            hoverCluster = " ";
            after = "";
        }
        
        // Build the hover cursor color codes
        var hoverColors = "";
        if (!hoverCursorFg.IsDefault) hoverColors += hoverCursorFg.ToForegroundAnsi();
        if (!hoverCursorBg.IsDefault) hoverColors += hoverCursorBg.ToBackgroundAnsi();
        
        return $"{globalColors}{leftBracket}{before}{hoverColors}{hoverCluster}{resetToGlobal}{after}{rightBracket}";
    }
}
