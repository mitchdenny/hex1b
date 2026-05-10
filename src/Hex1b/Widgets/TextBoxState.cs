using Hex1b.Input;

namespace Hex1b.Widgets;

/// <summary>
/// Mutable state object for a <see cref="TextBoxWidget"/>. Owns the text buffer,
/// the cursor position, the optional selection anchor, and the multiline / line
/// limits configured through the widget.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="TextBoxState"/> can be created and managed inside a composite via
/// <see cref="Composition.CompositionContext.UseState{T}(System.Func{T})"/> and routed
/// into a <see cref="TextBoxWidget"/> with
/// <see cref="TextBoxWidget.State(TextBoxState)"/>. When state is supplied this way,
/// the textbox becomes a pure view of the instance — the parent can mutate
/// <see cref="Text"/> directly and the change is reflected on the next reconcile,
/// without an <c>OnTextChanged</c> shadow-sync handler.
/// </para>
/// <para>
/// When the parent does not supply a state instance, the framework allocates one
/// per <see cref="TextBoxWidget"/> node automatically and seeds it from the
/// widget's <see cref="TextBoxWidget.Text"/> constructor argument.
/// </para>
/// </remarks>
public class TextBoxState
{
    private string _text = "";
    private int _cursorPosition = 0;

    /// <summary>
    /// Monotonically-increasing counter that bumps every time the buffer or
    /// cursor mutates. Used by <c>TextBoxNode</c> to detect out-of-band parent
    /// mutations on hoisted state and re-render in response. Internal because
    /// there is no scenario where a caller needs to read or reset it directly.
    /// </summary>
    internal long Version { get; private set; }

    private void BumpVersion() => Version++;

    /// <summary>
    /// Creates an empty text box state with the cursor at offset zero.
    /// </summary>
    public TextBoxState()
    {
    }

    /// <summary>
    /// Creates a text box state seeded with <paramref name="initialText"/>. The
    /// cursor is positioned at the end of the supplied text and there is no
    /// active selection.
    /// </summary>
    /// <param name="initialText">The initial text to populate the buffer with.
    /// A <c>null</c> value is treated as an empty string.</param>
    public TextBoxState(string initialText)
    {
        _text = initialText ?? "";
        _cursorPosition = _text.Length;
        BumpVersion();
    }

    /// <summary>
    /// When true, the text box operates in multi-line mode.
    /// Enter inserts newlines, Up/Down navigate between lines, Home/End operate on the current line.
    /// </summary>
    public bool IsMultiline { get; set; }

    /// <summary>
    /// Maximum number of lines allowed in multiline mode.
    /// When null, there is no limit.
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// Tracks the desired column when navigating vertically.
    /// Preserved across Up/Down movements so moving through short lines remembers the target column.
    /// Reset to null on any horizontal movement or text edit.
    /// </summary>
    private int? _preferredColumn;

    /// <summary>
    /// Resets the preferred column used for vertical navigation.
    /// Must be called after any horizontal cursor movement or text edit
    /// so that the next Up/Down arrow uses the actual cursor column.
    /// </summary>
    internal void ResetPreferredColumn() => _preferredColumn = null;

    public string Text
    {
        get => _text;
        set
        {
            var newValue = value ?? "";
            if (_text == newValue)
            {
                return;
            }
            _text = newValue;
            // Clamp cursor position to valid range when text changes
            _cursorPosition = Math.Clamp(_cursorPosition, 0, _text.Length);
            // Also clamp selection anchor if it exists
            if (SelectionAnchor.HasValue)
            {
                SelectionAnchor = Math.Clamp(SelectionAnchor.Value, 0, _text.Length);
            }
            BumpVersion();
        }
    }

    public int CursorPosition
    {
        get => _cursorPosition;
        set
        {
            var clamped = Math.Clamp(value, 0, _text.Length);
            if (_cursorPosition == clamped)
            {
                return;
            }
            _cursorPosition = clamped;
            BumpVersion();
        }
    }
    
    /// <summary>
    /// The anchor position for text selection. If null, no selection is active.
    /// Selection range is from min(SelectionAnchor, CursorPosition) to max(SelectionAnchor, CursorPosition).
    /// </summary>
    public int? SelectionAnchor { get; set; } = null;

    /// <summary>
    /// Returns true if there is an active text selection.
    /// </summary>
    public bool HasSelection => SelectionAnchor.HasValue && SelectionAnchor.Value != CursorPosition;

    /// <summary>
    /// Gets the start index of the selection (inclusive).
    /// </summary>
    public int SelectionStart => HasSelection ? Math.Min(SelectionAnchor!.Value, CursorPosition) : CursorPosition;

    /// <summary>
    /// Gets the end index of the selection (exclusive).
    /// </summary>
    public int SelectionEnd => HasSelection ? Math.Max(SelectionAnchor!.Value, CursorPosition) : CursorPosition;

    /// <summary>
    /// Gets the selected text.
    /// </summary>
    public string SelectedText => HasSelection ? Text[SelectionStart..SelectionEnd] : "";

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        SelectionAnchor = null;
    }

    /// <summary>
    /// Deletes the selected text and returns cursor to selection start.
    /// </summary>
    internal void DeleteSelection()
    {
        if (!HasSelection) return;
        
        var start = SelectionStart;
        var end = SelectionEnd;
        _text = _text[..start] + _text[end..];
        _cursorPosition = start;
        ClearSelection();
        _preferredColumn = null;
    }

    /// <summary>
    /// Selects all text.
    /// </summary>
    public void SelectAll()
    {
        if (Text.Length == 0) return;
        SelectionAnchor = 0;
        CursorPosition = Text.Length;
    }

    #region Line-computation helpers

    /// <summary>
    /// Returns the number of lines in the text (1-based; empty string has 1 line).
    /// </summary>
    public int GetLineCount()
    {
        if (_text.Length == 0) return 1;
        var count = 1;
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\n') count++;
        }
        return count;
    }

    /// <summary>
    /// Returns the flat offset where the given 0-based line starts.
    /// Line 0 starts at offset 0.
    /// </summary>
    public int GetLineStartOffset(int line)
    {
        if (line <= 0) return 0;
        var current = 0;
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                current++;
                if (current == line) return i + 1;
            }
        }
        // Line is beyond the last line — clamp to end
        return _text.Length;
    }

    /// <summary>
    /// Returns the length of the given 0-based line (excluding the newline character).
    /// </summary>
    public int GetLineLength(int line)
    {
        var start = GetLineStartOffset(line);
        var end = _text.IndexOf('\n', start);
        return end < 0 ? _text.Length - start : end - start;
    }

    /// <summary>
    /// Converts a flat cursor offset to a (line, column) pair (both 0-based).
    /// </summary>
    public (int line, int column) OffsetToLineColumn(int offset)
    {
        offset = Math.Clamp(offset, 0, _text.Length);
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (_text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }
        return (line, offset - lineStart);
    }

    /// <summary>
    /// Converts a (line, column) pair (both 0-based) to a flat cursor offset.
    /// Column is clamped to the line's length.
    /// </summary>
    public int LineColumnToOffset(int line, int column)
    {
        var lineStart = GetLineStartOffset(line);
        var lineLen = GetLineLength(line);
        return lineStart + Math.Clamp(column, 0, lineLen);
    }

    /// <summary>
    /// Gets the text content of the given 0-based line (excluding newline).
    /// </summary>
    public string GetLineText(int line)
    {
        var start = GetLineStartOffset(line);
        var len = GetLineLength(line);
        return _text.Substring(start, len);
    }

    #endregion

    #region Vertical navigation

    /// <summary>
    /// Moves the cursor up one line, preserving the preferred column.
    /// </summary>
    public void MoveUp(bool extend = false)
    {
        var (line, col) = OffsetToLineColumn(_cursorPosition);
        if (line == 0) return; // Already on first line

        if (extend && !SelectionAnchor.HasValue)
            SelectionAnchor = _cursorPosition;
        else if (!extend)
            ClearSelection();

        _preferredColumn ??= col;
        var targetCol = Math.Min(_preferredColumn.Value, GetLineLength(line - 1));
        CursorPosition = LineColumnToOffset(line - 1, targetCol);
    }

    /// <summary>
    /// Moves the cursor down one line, preserving the preferred column.
    /// </summary>
    public void MoveDown(bool extend = false)
    {
        var (line, col) = OffsetToLineColumn(_cursorPosition);
        if (line >= GetLineCount() - 1) return; // Already on last line

        if (extend && !SelectionAnchor.HasValue)
            SelectionAnchor = _cursorPosition;
        else if (!extend)
            ClearSelection();

        _preferredColumn ??= col;
        var targetCol = Math.Min(_preferredColumn.Value, GetLineLength(line + 1));
        CursorPosition = LineColumnToOffset(line + 1, targetCol);
    }

    #endregion

    /// <summary>
    /// Inserts a newline at the current cursor position.
    /// </summary>
    public void InsertNewline()
    {
        // Enforce max lines limit
        if (MaxLines.HasValue && GetLineCount() >= MaxLines.Value)
            return;

        if (HasSelection)
            DeleteSelection();

        _text = _text.Insert(_cursorPosition, "\n");
        _cursorPosition++;
        _preferredColumn = null;
    }

    /// <summary>
    /// Handles keyboard input for the text box.
    /// Returns true if the input was handled, false if it should be passed to parent containers.
    /// </summary>
    public bool HandleInput(Hex1bKeyEvent evt)
    {
        // Tab is never handled by TextBox - let it bubble up for focus navigation
        if (evt.Key == Hex1bKey.Tab)
        {
            return false;
        }

        // Ctrl+A: Select all
        if (evt.Modifiers.HasFlag(Hex1bModifiers.Control) && evt.Key == Hex1bKey.A)
        {
            SelectAll();
            return true;
        }

        switch (evt.Key)
        {
            case Hex1bKey.Backspace:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition > 0)
                {
                    _text = _text.Remove(_cursorPosition - 1, 1);
                    _cursorPosition--;
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.Delete:
                if (HasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition < _text.Length)
                {
                    _text = _text.Remove(_cursorPosition, 1);
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.LeftArrow:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                    if (CursorPosition > 0)
                    {
                        CursorPosition--;
                    }
                }
                else
                {
                    if (HasSelection)
                    {
                        CursorPosition = SelectionStart;
                        ClearSelection();
                    }
                    else if (CursorPosition > 0)
                    {
                        CursorPosition--;
                    }
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.RightArrow:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                    if (CursorPosition < Text.Length)
                    {
                        CursorPosition++;
                    }
                }
                else
                {
                    if (HasSelection)
                    {
                        CursorPosition = SelectionEnd;
                        ClearSelection();
                    }
                    else if (CursorPosition < Text.Length)
                    {
                        CursorPosition++;
                    }
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.UpArrow:
                if (IsMultiline)
                {
                    MoveUp(evt.Modifiers.HasFlag(Hex1bModifiers.Shift));
                    return true;
                }
                return false;

            case Hex1bKey.DownArrow:
                if (IsMultiline)
                {
                    MoveDown(evt.Modifiers.HasFlag(Hex1bModifiers.Shift));
                    return true;
                }
                return false;

            case Hex1bKey.Home:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                }
                else
                {
                    ClearSelection();
                }
                if (IsMultiline && !evt.Modifiers.HasFlag(Hex1bModifiers.Control))
                {
                    // Move to start of current line
                    var (line, _) = OffsetToLineColumn(_cursorPosition);
                    CursorPosition = GetLineStartOffset(line);
                }
                else
                {
                    CursorPosition = 0;
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.End:
                if (evt.Modifiers.HasFlag(Hex1bModifiers.Shift))
                {
                    if (!SelectionAnchor.HasValue)
                    {
                        SelectionAnchor = CursorPosition;
                    }
                }
                else
                {
                    ClearSelection();
                }
                if (IsMultiline && !evt.Modifiers.HasFlag(Hex1bModifiers.Control))
                {
                    // Move to end of current line
                    var (line, _) = OffsetToLineColumn(_cursorPosition);
                    CursorPosition = GetLineStartOffset(line) + GetLineLength(line);
                }
                else
                {
                    CursorPosition = Text.Length;
                }
                _preferredColumn = null;
                return true;

            case Hex1bKey.Enter:
                if (IsMultiline)
                {
                    InsertNewline();
                    return true;
                }
                return false;

            default:
                // Insert printable characters
                if (!char.IsControl(evt.Character))
                {
                    if (HasSelection)
                    {
                        DeleteSelection();
                    }
                    _text = _text.Insert(_cursorPosition, evt.Character.ToString());
                    _cursorPosition++;
                    _preferredColumn = null;
                    return true;
                }
                // Non-printable, non-handled key
                return false;
        }
    }
}
