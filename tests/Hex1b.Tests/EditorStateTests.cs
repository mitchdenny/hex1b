using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorStateTests
{
    [Fact]
    public void InsertText_InsertsAtCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.InsertText(" world");

        Assert.Equal("Hello world", doc.GetText());
        Assert.Equal(new DocumentOffset(11), state.Cursor.Position);
    }

    [Fact]
    public void InsertText_WhenReadOnly_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(5);

        state.InsertText(" world");

        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void InsertText_WithSelection_ReplacesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11);
        state.Cursor.SelectionAnchor = new DocumentOffset(6);

        state.InsertText("earth");

        Assert.Equal("Hello earth", doc.GetText());
        Assert.Equal(new DocumentOffset(11), state.Cursor.Position);
        Assert.False(state.Cursor.HasSelection);
    }

    [Fact]
    public void DeleteBackward_DeletesCharBeforeCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteBackward();

        Assert.Equal("Hell", doc.GetText());
        Assert.Equal(new DocumentOffset(4), state.Cursor.Position);
    }

    [Fact]
    public void DeleteBackward_AtStart_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.DeleteBackward();

        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void DeleteForward_DeletesCharAfterCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.DeleteForward();

        Assert.Equal("ello", doc.GetText());
        Assert.Equal(new DocumentOffset(0), state.Cursor.Position);
    }

    [Fact]
    public void DeleteForward_AtEnd_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteForward();

        Assert.Equal("Hello", doc.GetText());
    }

    [Fact]
    public void MoveCursor_Left_DecrementsCursorPosition()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Left);

        Assert.Equal(new DocumentOffset(2), state.Cursor.Position);
    }

    [Fact]
    public void MoveCursor_Right_IncrementsCursorPosition()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Right);

        Assert.Equal(new DocumentOffset(4), state.Cursor.Position);
    }

    [Fact]
    public void MoveCursor_Up_MovesPreviousLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // Line 2, Col 3 ("Wo|rld")

        state.MoveCursor(CursorDirection.Up);

        // Should be at Line 1, Col 3
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.Equal(1, pos.Line);
        Assert.Equal(3, pos.Column);
    }

    [Fact]
    public void MoveCursor_Down_MovesNextLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(2); // Line 1, Col 3

        state.MoveCursor(CursorDirection.Down);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.Equal(2, pos.Line);
        Assert.Equal(3, pos.Column);
    }

    [Fact]
    public void MoveCursor_Down_ClampsToShorterLine()
    {
        var doc = new Hex1bDocument("Hello World\nHi");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(10); // Col 11 on line 1

        state.MoveCursor(CursorDirection.Down);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.Equal(2, pos.Line);
        // Should clamp to end of "Hi" (col 3 = past end)
        Assert.True(pos.Column <= 3);
    }

    [Fact]
    public void SharedDocument_EditsVisibleInBothStates()
    {
        var doc = new Hex1bDocument("Hello");
        var state1 = new EditorState(doc);
        var state2 = new EditorState(doc);

        state1.Cursor.Position = new DocumentOffset(5);
        state1.InsertText(" world");

        // Both see the change through the shared document
        Assert.Equal("Hello world", doc.GetText());
        Assert.Equal(11, doc.Length);

        // state2 can also read the updated content
        Assert.Equal("Hello world", state2.Document.GetText());
    }

    [Fact]
    public void SharedDocument_ChangedEventNotifiesBothStates()
    {
        var doc = new Hex1bDocument("Hello");
        var changedCount = 0;
        doc.Changed += (_, _) => changedCount++;

        var state1 = new EditorState(doc);
        state1.Cursor.Position = new DocumentOffset(5);
        state1.InsertText("!");

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void DeleteBackward_WithSelection_DeletesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11);
        state.Cursor.SelectionAnchor = new DocumentOffset(5);

        state.DeleteBackward();

        Assert.Equal("Hello", doc.GetText());
        Assert.Equal(new DocumentOffset(5), state.Cursor.Position);
        Assert.False(state.Cursor.HasSelection);
    }
}
