using Hex1b.Documents;

namespace Hex1b.Widgets;

/// <summary>
/// User-owned mutable state for an editor widget.
/// Multiple EditorWidgets can share the same EditorState for synced cursors.
/// Multiple EditorStates can share the same IHex1bDocument for independent views.
/// </summary>
public class EditorState
{
    public IHex1bDocument Document { get; }
    public DocumentCursor Cursor { get; } = new();
    public bool IsReadOnly { get; set; }
    public int TabSize { get; set; } = 4;

    public EditorState(IHex1bDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    // ── Editing ──────────────────────────────────────────────────

    /// <summary>Insert text at the cursor position, replacing any selection.</summary>
    public void InsertText(string text)
    {
        if (IsReadOnly) return;

        if (Cursor.HasSelection)
        {
            var range = Cursor.SelectionRange;
            Document.Apply(new ReplaceOperation(range, text));
            Cursor.Position = range.Start + text.Length;
            Cursor.ClearSelection();
        }
        else
        {
            Document.Apply(new InsertOperation(Cursor.Position, text));
            Cursor.Position = Cursor.Position + text.Length;
        }
    }

    /// <summary>Delete the character before the cursor (Backspace).</summary>
    public void DeleteBackward()
    {
        if (IsReadOnly) return;

        if (Cursor.HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (Cursor.Position.Value == 0) return;

        var deleteStart = new DocumentOffset(Cursor.Position.Value - 1);
        Document.Apply(new DeleteOperation(new DocumentRange(deleteStart, Cursor.Position)));
        Cursor.Position = deleteStart;
    }

    /// <summary>Delete the character after the cursor (Delete key).</summary>
    public void DeleteForward()
    {
        if (IsReadOnly) return;

        if (Cursor.HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (Cursor.Position.Value >= Document.Length) return;

        var deleteEnd = new DocumentOffset(Cursor.Position.Value + 1);
        Document.Apply(new DeleteOperation(new DocumentRange(Cursor.Position, deleteEnd)));
    }

    /// <summary>Delete the word before the cursor (Ctrl+Backspace).</summary>
    public void DeleteWordBackward()
    {
        if (IsReadOnly) return;

        if (Cursor.HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (Cursor.Position.Value == 0) return;

        var lineText = GetCurrentLineText(out var lineStartOffset);
        var colInLine = Cursor.Position.Value - lineStartOffset;
        var wordBoundary = GraphemeHelper.GetPreviousWordBoundary(lineText, colInLine);
        var deleteStart = new DocumentOffset(lineStartOffset + wordBoundary);

        if (deleteStart == Cursor.Position) return;
        Document.Apply(new DeleteOperation(new DocumentRange(deleteStart, Cursor.Position)));
        Cursor.Position = deleteStart;
    }

    /// <summary>Delete the word after the cursor (Ctrl+Delete).</summary>
    public void DeleteWordForward()
    {
        if (IsReadOnly) return;

        if (Cursor.HasSelection)
        {
            DeleteSelection();
            return;
        }

        if (Cursor.Position.Value >= Document.Length) return;

        var lineText = GetCurrentLineText(out var lineStartOffset);
        var colInLine = Cursor.Position.Value - lineStartOffset;
        var wordBoundary = GraphemeHelper.GetNextWordBoundary(lineText, colInLine);
        var deleteEnd = new DocumentOffset(lineStartOffset + wordBoundary);

        if (deleteEnd == Cursor.Position) return;
        Document.Apply(new DeleteOperation(new DocumentRange(Cursor.Position, deleteEnd)));
    }

    /// <summary>Delete the entire current line (Ctrl+Shift+K).</summary>
    public void DeleteLine()
    {
        if (IsReadOnly) return;

        Cursor.ClearSelection();
        var pos = Document.OffsetToPosition(Cursor.Position);
        var lineStart = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));

        DocumentOffset lineEnd;
        if (pos.Line < Document.LineCount)
        {
            // Delete through the newline to join with next line
            lineEnd = Document.PositionToOffset(new DocumentPosition(pos.Line + 1, 1));
        }
        else
        {
            // Last line: delete to end of document
            lineEnd = new DocumentOffset(Document.Length);
            // Also delete the preceding newline if not the only line
            if (pos.Line > 1)
            {
                var prevLineEnd = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
                lineStart = prevLineEnd - 1; // include the \n before this line
            }
        }

        if (lineStart == lineEnd) return;
        Document.Apply(new DeleteOperation(new DocumentRange(lineStart, lineEnd)));
        Cursor.Position = lineStart;
        Cursor.Clamp(Document.Length);
    }

    // ── Navigation ───────────────────────────────────────────────

    /// <summary>Move the cursor in a direction. With extend, selection is extended.</summary>
    public void MoveCursor(CursorDirection direction, bool extend = false)
    {
        // For Left/Right without extend: collapse selection to boundary instead of moving
        if (!extend && Cursor.HasSelection)
        {
            switch (direction)
            {
                case CursorDirection.Left:
                    Cursor.Position = Cursor.SelectionStart;
                    Cursor.ClearSelection();
                    return;
                case CursorDirection.Right:
                    Cursor.Position = Cursor.SelectionEnd;
                    Cursor.ClearSelection();
                    return;
            }
        }

        ApplyExtend(extend);

        switch (direction)
        {
            case CursorDirection.Left:
                if (Cursor.Position.Value > 0)
                    Cursor.Position = Cursor.Position - 1;
                break;

            case CursorDirection.Right:
                if (Cursor.Position.Value < Document.Length)
                    Cursor.Position = Cursor.Position + 1;
                break;

            case CursorDirection.Up:
                MoveVertical(-1);
                break;

            case CursorDirection.Down:
                MoveVertical(1);
                break;
        }
    }

    /// <summary>Move cursor to start of current line (Home).</summary>
    public void MoveToLineStart(bool extend = false)
    {
        ApplyExtend(extend);
        var pos = Document.OffsetToPosition(Cursor.Position);
        Cursor.Position = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
    }

    /// <summary>Move cursor to end of current line (End).</summary>
    public void MoveToLineEnd(bool extend = false)
    {
        ApplyExtend(extend);
        var pos = Document.OffsetToPosition(Cursor.Position);
        var lineLen = Document.GetLineLength(pos.Line);
        Cursor.Position = Document.PositionToOffset(new DocumentPosition(pos.Line, lineLen + 1));
    }

    /// <summary>Move cursor to start of document (Ctrl+Home).</summary>
    public void MoveToDocumentStart(bool extend = false)
    {
        ApplyExtend(extend);
        Cursor.Position = DocumentOffset.Zero;
    }

    /// <summary>Move cursor to end of document (Ctrl+End).</summary>
    public void MoveToDocumentEnd(bool extend = false)
    {
        ApplyExtend(extend);
        Cursor.Position = new DocumentOffset(Document.Length);
    }

    /// <summary>Move cursor to previous word boundary (Ctrl+Left).</summary>
    public void MoveWordLeft(bool extend = false)
    {
        ApplyExtend(extend);

        if (Cursor.Position.Value == 0) return;

        var lineText = GetCurrentLineText(out var lineStartOffset);
        var colInLine = Cursor.Position.Value - lineStartOffset;

        if (colInLine == 0)
        {
            // At start of line — move to end of previous line
            Cursor.Position = Cursor.Position - 1;
        }
        else
        {
            var boundary = GraphemeHelper.GetPreviousWordBoundary(lineText, colInLine);
            Cursor.Position = new DocumentOffset(lineStartOffset + boundary);
        }
    }

    /// <summary>Move cursor to next word boundary (Ctrl+Right).</summary>
    public void MoveWordRight(bool extend = false)
    {
        ApplyExtend(extend);

        if (Cursor.Position.Value >= Document.Length) return;

        var lineText = GetCurrentLineText(out var lineStartOffset);
        var colInLine = Cursor.Position.Value - lineStartOffset;

        if (colInLine >= lineText.Length)
        {
            // At end of line — move to start of next line
            Cursor.Position = Cursor.Position + 1;
        }
        else
        {
            var boundary = GraphemeHelper.GetNextWordBoundary(lineText, colInLine);
            Cursor.Position = new DocumentOffset(lineStartOffset + boundary);
        }
    }

    /// <summary>Move cursor up by viewport height (PageUp).</summary>
    public void MovePageUp(int viewportLines, bool extend = false)
    {
        ApplyExtend(extend);
        MoveVertical(-Math.Max(1, viewportLines - 1));
    }

    /// <summary>Move cursor down by viewport height (PageDown).</summary>
    public void MovePageDown(int viewportLines, bool extend = false)
    {
        ApplyExtend(extend);
        MoveVertical(Math.Max(1, viewportLines - 1));
    }

    // ── Selection ────────────────────────────────────────────────

    /// <summary>Select all text in the document (Ctrl+A).</summary>
    public void SelectAll()
    {
        Cursor.SelectionAnchor = DocumentOffset.Zero;
        Cursor.Position = new DocumentOffset(Document.Length);
    }

    // ── Internals ────────────────────────────────────────────────

    private void ApplyExtend(bool extend)
    {
        if (extend) Cursor.EnsureSelectionAnchor();
        else Cursor.ClearSelection();
    }

    private void DeleteSelection()
    {
        if (!Cursor.HasSelection) return;
        var range = Cursor.SelectionRange;
        Document.Apply(new DeleteOperation(range));
        Cursor.Position = range.Start;
        Cursor.ClearSelection();
    }

    private void MoveVertical(int lineDelta)
    {
        var pos = Document.OffsetToPosition(Cursor.Position);
        var targetLine = Math.Clamp(pos.Line + lineDelta, 1, Document.LineCount);
        if (targetLine == pos.Line) return;

        var targetLineLength = Document.GetLineLength(targetLine);
        var targetColumn = Math.Min(pos.Column, targetLineLength + 1);
        var targetPos = new DocumentPosition(targetLine, targetColumn);
        Cursor.Position = Document.PositionToOffset(targetPos);
    }

    private string GetCurrentLineText(out int lineStartOffset)
    {
        var pos = Document.OffsetToPosition(Cursor.Position);
        var lineStart = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
        lineStartOffset = lineStart.Value;
        return Document.GetLineText(pos.Line);
    }
}

public enum CursorDirection
{
    Left,
    Right,
    Up,
    Down
}
