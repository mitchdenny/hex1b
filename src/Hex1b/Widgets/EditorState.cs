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

    /// <summary>Move the cursor in a direction.</summary>
    public void MoveCursor(CursorDirection direction)
    {
        Cursor.ClearSelection();

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
}

public enum CursorDirection
{
    Left,
    Right,
    Up,
    Down
}
