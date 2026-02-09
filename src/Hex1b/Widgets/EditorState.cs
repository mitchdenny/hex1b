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

    /// <summary>All cursors (sorted, non-overlapping). Use for multi-cursor access.</summary>
    public CursorSet Cursors { get; } = new();

    /// <summary>The primary cursor. Alias for Cursors.Primary.</summary>
    public DocumentCursor Cursor => Cursors.Primary;

    /// <summary>Undo/redo history.</summary>
    public EditHistory History { get; } = new();

    public bool IsReadOnly { get; set; }
    public int TabSize { get; set; } = 4;

    public EditorState(IHex1bDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
    }

    // ── Editing ──────────────────────────────────────────────────

    /// <summary>Insert text at all cursor positions, replacing any selections.</summary>
    public void InsertText(string text)
    {
        if (IsReadOnly) return;

        var coalescable = text.Length == 1 && text[0] != '\n' && Cursors.Count == 1;
        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            if (cursor.HasSelection)
            {
                var range = cursor.SelectionRange;
                var result = Document.Apply(new ReplaceOperation(range, text));
                CollectOps(result, ops);
                cursor.Position = range.Start + text.Length;
                cursor.ClearSelection();
            }
            else
            {
                var result = Document.Apply(new InsertOperation(cursor.Position, text));
                CollectOps(result, ops);
                cursor.Position = cursor.Position + text.Length;
            }

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, coalescable);
    }

    /// <summary>Delete the character before each cursor (Backspace).</summary>
    public void DeleteBackward()
    {
        if (IsReadOnly) return;

        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            if (cursor.HasSelection)
            {
                DeleteCursorSelection(cursor, ops);
            }
            else if (cursor.Position.Value > 0)
            {
                var deleteStart = new DocumentOffset(cursor.Position.Value - 1);
                var result = Document.Apply(new DeleteOperation(new DocumentRange(deleteStart, cursor.Position)));
                CollectOps(result, ops);
                cursor.Position = deleteStart;
            }
            else
            {
                continue;
            }

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, false);
    }

    /// <summary>Delete the character after each cursor (Delete key).</summary>
    public void DeleteForward()
    {
        if (IsReadOnly) return;

        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            if (cursor.HasSelection)
            {
                DeleteCursorSelection(cursor, ops);
            }
            else if (cursor.Position.Value < Document.Length)
            {
                var deleteEnd = new DocumentOffset(cursor.Position.Value + 1);
                var result = Document.Apply(new DeleteOperation(new DocumentRange(cursor.Position, deleteEnd)));
                CollectOps(result, ops);
            }
            else
            {
                continue;
            }

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, false);
    }

    /// <summary>Delete the word before each cursor (Ctrl+Backspace).</summary>
    public void DeleteWordBackward()
    {
        if (IsReadOnly) return;

        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            if (cursor.HasSelection)
            {
                DeleteCursorSelection(cursor, ops);
            }
            else if (cursor.Position.Value > 0)
            {
                var lineText = GetLineTextForCursor(cursor, out var lineStartOffset);
                var colInLine = cursor.Position.Value - lineStartOffset;
                var wordBoundary = GraphemeHelper.GetPreviousWordBoundary(lineText, colInLine);
                var deleteStart = new DocumentOffset(lineStartOffset + wordBoundary);

                if (deleteStart == cursor.Position) continue;
                var result = Document.Apply(new DeleteOperation(new DocumentRange(deleteStart, cursor.Position)));
                CollectOps(result, ops);
                cursor.Position = deleteStart;
            }
            else
            {
                continue;
            }

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, false);
    }

    /// <summary>Delete the word after each cursor (Ctrl+Delete).</summary>
    public void DeleteWordForward()
    {
        if (IsReadOnly) return;

        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            if (cursor.HasSelection)
            {
                DeleteCursorSelection(cursor, ops);
            }
            else if (cursor.Position.Value < Document.Length)
            {
                var lineText = GetLineTextForCursor(cursor, out var lineStartOffset);
                var colInLine = cursor.Position.Value - lineStartOffset;
                var wordBoundary = GraphemeHelper.GetNextWordBoundary(lineText, colInLine);
                var deleteEnd = new DocumentOffset(lineStartOffset + wordBoundary);

                if (deleteEnd == cursor.Position) continue;
                var result = Document.Apply(new DeleteOperation(new DocumentRange(cursor.Position, deleteEnd)));
                CollectOps(result, ops);
            }
            else
            {
                continue;
            }

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, false);
    }

    /// <summary>Delete the entire current line for each cursor (Ctrl+Shift+K).</summary>
    public void DeleteLine()
    {
        if (IsReadOnly) return;

        var cursorsBefore = Cursors.Snapshot();
        var versionBefore = Document.Version;
        var ops = new List<(EditOperation Op, EditOperation Inverse)>();

        foreach (var (cursor, idx) in Cursors.InReverseOrder())
        {
            var docLenBefore = Document.Length;

            cursor.ClearSelection();
            var pos = Document.OffsetToPosition(cursor.Position);
            var lineStart = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));

            DocumentOffset lineEnd;
            if (pos.Line < Document.LineCount)
            {
                lineEnd = Document.PositionToOffset(new DocumentPosition(pos.Line + 1, 1));
            }
            else
            {
                lineEnd = new DocumentOffset(Document.Length);
                if (pos.Line > 1)
                {
                    var prevLineEnd = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
                    lineStart = prevLineEnd - 1;
                }
            }

            if (lineStart == lineEnd) continue;
            var result = Document.Apply(new DeleteOperation(new DocumentRange(lineStart, lineEnd)));
            CollectOps(result, ops);
            cursor.Position = lineStart;
            cursor.Clamp(Document.Length);

            AdjustProcessedCursors(idx, Document.Length - docLenBefore);
        }

        Cursors.MergeOverlapping();
        FinishEditBatch(ops, cursorsBefore, versionBefore, false);
    }

    // ── Navigation ───────────────────────────────────────────────

    /// <summary>Move all cursors in a direction. With extend, selection is extended.</summary>
    public void MoveCursor(CursorDirection direction, bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            // For Left/Right without extend: collapse selection to boundary
            if (!extend && cursor.HasSelection)
            {
                switch (direction)
                {
                    case CursorDirection.Left:
                        cursor.Position = cursor.SelectionStart;
                        cursor.ClearSelection();
                        continue;
                    case CursorDirection.Right:
                        cursor.Position = cursor.SelectionEnd;
                        cursor.ClearSelection();
                        continue;
                }
            }

            ApplyExtendForCursor(cursor, extend);

            switch (direction)
            {
                case CursorDirection.Left:
                    if (cursor.Position.Value > 0)
                        cursor.Position = cursor.Position - 1;
                    break;

                case CursorDirection.Right:
                    if (cursor.Position.Value < Document.Length)
                        cursor.Position = cursor.Position + 1;
                    break;

                case CursorDirection.Up:
                    MoveVertical(cursor, -1);
                    break;

                case CursorDirection.Down:
                    MoveVertical(cursor, 1);
                    break;
            }
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to start of their current line (Home).</summary>
    public void MoveToLineStart(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            var pos = Document.OffsetToPosition(cursor.Position);
            cursor.Position = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to end of their current line (End).</summary>
    public void MoveToLineEnd(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            var pos = Document.OffsetToPosition(cursor.Position);
            var lineLen = Document.GetLineLength(pos.Line);
            cursor.Position = Document.PositionToOffset(new DocumentPosition(pos.Line, lineLen + 1));
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to start of document (Ctrl+Home).</summary>
    public void MoveToDocumentStart(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            cursor.Position = DocumentOffset.Zero;
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to end of document (Ctrl+End).</summary>
    public void MoveToDocumentEnd(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            cursor.Position = new DocumentOffset(Document.Length);
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to previous word boundary (Ctrl+Left).</summary>
    public void MoveWordLeft(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);

            if (cursor.Position.Value == 0) continue;

            var lineText = GetLineTextForCursor(cursor, out var lineStartOffset);
            var colInLine = cursor.Position.Value - lineStartOffset;

            if (colInLine == 0)
            {
                cursor.Position = cursor.Position - 1;
            }
            else
            {
                var boundary = GraphemeHelper.GetPreviousWordBoundary(lineText, colInLine);
                cursor.Position = new DocumentOffset(lineStartOffset + boundary);
            }
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors to next word boundary (Ctrl+Right).</summary>
    public void MoveWordRight(bool extend = false)
    {
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);

            if (cursor.Position.Value >= Document.Length) continue;

            var lineText = GetLineTextForCursor(cursor, out var lineStartOffset);
            var colInLine = cursor.Position.Value - lineStartOffset;

            if (colInLine >= lineText.Length)
            {
                cursor.Position = cursor.Position + 1;
            }
            else
            {
                var boundary = GraphemeHelper.GetNextWordBoundary(lineText, colInLine);
                cursor.Position = new DocumentOffset(lineStartOffset + boundary);
            }
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors up by viewport height (PageUp).</summary>
    public void MovePageUp(int viewportLines, bool extend = false)
    {
        var delta = -Math.Max(1, viewportLines - 1);
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            MoveVertical(cursor, delta);
        }

        AfterNavigation(extend);
    }

    /// <summary>Move all cursors down by viewport height (PageDown).</summary>
    public void MovePageDown(int viewportLines, bool extend = false)
    {
        var delta = Math.Max(1, viewportLines - 1);
        foreach (var cursor in Cursors)
        {
            ApplyExtendForCursor(cursor, extend);
            MoveVertical(cursor, delta);
        }

        AfterNavigation(extend);
    }

    // ── Selection ────────────────────────────────────────────────

    /// <summary>Select all text in the document (Ctrl+A). Collapses to single cursor.</summary>
    public void SelectAll()
    {
        Cursors.CollapseToSingle();
        Cursor.SelectionAnchor = DocumentOffset.Zero;
        Cursor.Position = new DocumentOffset(Document.Length);
    }

    // ── Multi-cursor ─────────────────────────────────────────────

    /// <summary>
    /// Add a cursor at the next occurrence of the currently selected text (Ctrl+D).
    /// If no text is selected, selects the word under the primary cursor.
    /// </summary>
    public void AddCursorAtNextMatch()
    {
        var primary = Cursor;

        if (!primary.HasSelection)
        {
            // Select the word under cursor
            SelectWordUnderCursor(primary);
            if (!primary.HasSelection) return;
        }

        var selectedText = Document.GetText(primary.SelectionRange);
        if (string.IsNullOrEmpty(selectedText)) return;

        // Search from after the last cursor's selection end
        var lastCursor = Cursors[Cursors.Count - 1];
        var searchFrom = lastCursor.HasSelection ? lastCursor.SelectionEnd : lastCursor.Position;

        var docText = Document.GetText();
        var foundIndex = docText.IndexOf(selectedText, searchFrom.Value, StringComparison.Ordinal);

        // Wrap around if not found after last cursor
        if (foundIndex < 0)
        {
            foundIndex = docText.IndexOf(selectedText, 0, StringComparison.Ordinal);
            // Don't add a cursor that already exists
            if (foundIndex >= 0)
            {
                foreach (var cursor in Cursors)
                {
                    if (cursor.HasSelection &&
                        cursor.SelectionStart.Value == foundIndex &&
                        cursor.SelectionEnd.Value == foundIndex + selectedText.Length)
                    {
                        foundIndex = -1;
                        break;
                    }
                }
            }
        }

        if (foundIndex < 0) return;

        var matchStart = new DocumentOffset(foundIndex);
        var matchEnd = new DocumentOffset(foundIndex + selectedText.Length);
        Cursors.Add(matchEnd, matchStart); // Position at end, anchor at start
    }

    /// <summary>Collapse all cursors to just the primary.</summary>
    public void CollapseToSingleCursor()
    {
        Cursors.CollapseToSingle();
    }

    // ── Undo/Redo ────────────────────────────────────────────────

    /// <summary>Undo the last edit group. Restores cursors to pre-edit positions.</summary>
    public void Undo()
    {
        var group = History.Undo();
        if (group == null) return;

        // Apply inverse operations to revert the document
        foreach (var inverse in group.InverseOperations)
        {
            Document.Apply(inverse, "undo");
        }

        // Restore cursor state from before the edit
        Cursors.Restore(group.CursorsBefore);
        Cursors.ClampAll(Document.Length);
    }

    /// <summary>Redo the last undone edit group. Restores cursors to post-edit positions.</summary>
    public void Redo()
    {
        var group = History.Redo();
        if (group == null) return;

        // Re-apply the original operations
        foreach (var op in group.Operations)
        {
            Document.Apply(op, "redo");
        }

        // Restore cursor state from after the edit
        if (group.CursorsAfter != null)
        {
            Cursors.Restore(group.CursorsAfter);
            Cursors.ClampAll(Document.Length);
        }
    }

    // ── Internals ────────────────────────────────────────────────

    private static void ApplyExtendForCursor(DocumentCursor cursor, bool extend)
    {
        if (extend) cursor.EnsureSelectionAnchor();
        else cursor.ClearSelection();
    }

    private void DeleteCursorSelection(DocumentCursor cursor, List<(EditOperation Op, EditOperation Inverse)> ops)
    {
        if (!cursor.HasSelection) return;
        var range = cursor.SelectionRange;
        var result = Document.Apply(new DeleteOperation(range));
        CollectOps(result, ops);
        cursor.Position = range.Start;
        cursor.ClearSelection();
    }

    private static void CollectOps(EditResult result, List<(EditOperation Op, EditOperation Inverse)> ops)
    {
        for (var i = 0; i < result.Applied.Count; i++)
        {
            ops.Add((result.Applied[i], result.Inverse[i]));
        }
    }

    /// <summary>
    /// Record a batch of operations to history. For single-char typing, uses coalescing.
    /// For multi-op batches, uses explicit grouping.
    /// </summary>
    private void FinishEditBatch(
        List<(EditOperation Op, EditOperation Inverse)> ops,
        CursorSetSnapshot cursorsBefore,
        long versionBefore,
        bool coalescable)
    {
        if (ops.Count == 0) return;

        var versionAfter = Document.Version;

        if (coalescable && ops.Count == 1)
        {
            // Single operation, try coalescing with previous typing
            History.RecordEdit(
                ops[0].Op,
                ops[0].Inverse,
                Cursors,
                versionBefore,
                versionAfter,
                coalescable: true);
        }
        else
        {
            // Multiple operations or non-coalescable: explicit group
            var group = new EditGroup(cursorsBefore, versionBefore);
            foreach (var (op, inverse) in ops)
            {
                group.AddOperation(op, inverse);
            }
            group.CursorsAfter = Cursors.Snapshot();
            group.VersionAfter = versionAfter;
            History.PushGroup(group);
        }
    }

    private void MoveVertical(DocumentCursor cursor, int lineDelta)
    {
        var pos = Document.OffsetToPosition(cursor.Position);
        var targetLine = Math.Clamp(pos.Line + lineDelta, 1, Document.LineCount);
        if (targetLine == pos.Line) return;

        var targetLineLength = Document.GetLineLength(targetLine);
        var targetColumn = Math.Min(pos.Column, targetLineLength + 1);
        var targetPos = new DocumentPosition(targetLine, targetColumn);
        cursor.Position = Document.PositionToOffset(targetPos);
    }

    private string GetLineTextForCursor(DocumentCursor cursor, out int lineStartOffset)
    {
        var pos = Document.OffsetToPosition(cursor.Position);
        var lineStart = Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
        lineStartOffset = lineStart.Value;
        return Document.GetLineText(pos.Line);
    }

    private void SelectWordUnderCursor(DocumentCursor cursor)
    {
        if (Document.Length == 0) return;

        var offset = cursor.Position.Value;
        var text = Document.GetText();

        // Find word boundaries
        var start = offset;
        while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
            start--;

        var end = offset;
        while (end < text.Length && char.IsLetterOrDigit(text[end]))
            end++;

        if (start == end) return;

        cursor.SelectionAnchor = new DocumentOffset(start);
        cursor.Position = new DocumentOffset(end);
    }

    private void AfterNavigation(bool extend)
    {
        if (!extend)
        {
            Cursors.Sort();
            Cursors.MergeOverlapping();
        }
        else
        {
            Cursors.Sort();
        }
    }

    /// <summary>
    /// After processing a cursor edit at index idx (reverse iteration),
    /// adjust all already-processed cursors (higher indices) by the document length delta.
    /// </summary>
    private void AdjustProcessedCursors(int idx, int delta)
    {
        if (delta == 0) return;
        for (var j = idx + 1; j < Cursors.Count; j++)
        {
            var other = Cursors[j];
            other.Position = new DocumentOffset(Math.Max(0, other.Position.Value + delta));
            if (other.SelectionAnchor != null)
            {
                other.SelectionAnchor = new DocumentOffset(
                    Math.Max(0, other.SelectionAnchor.Value.Value + delta));
            }
        }
    }
}

public enum CursorDirection
{
    Left,
    Right,
    Up,
    Down
}
