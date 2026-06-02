using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class EditorStateTests
{
    [TestMethod]
    public void InsertText_InsertsAtCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.InsertText(" world");

        Assert.AreEqual("Hello world", doc.GetText());
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
    }

    [TestMethod]
    public void InsertText_WhenReadOnly_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(5);

        state.InsertText(" world");

        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void InsertText_WithSelection_ReplacesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11);
        state.Cursor.SelectionAnchor = new DocumentOffset(6);

        state.InsertText("earth");

        Assert.AreEqual("Hello earth", doc.GetText());
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void InsertText_EmptyString_DoesNotChangeDocument()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);
        var versionBefore = doc.Version;

        state.InsertText("");

        Assert.AreEqual("Hello", doc.GetText());
        // Empty insert still goes through Apply but is a no-op internally
    }

    [TestMethod]
    public void InsertText_AtBeginning_Prepends()
    {
        var doc = new Hex1bDocument("World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.InsertText("Hello ");

        Assert.AreEqual("Hello World", doc.GetText());
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void InsertText_Newline_SplitsLine()
    {
        var doc = new Hex1bDocument("HelloWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.InsertText("\n");

        Assert.AreEqual("Hello\nWorld", doc.GetText());
        Assert.AreEqual(2, doc.LineCount);
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteBackward_DeletesCharBeforeCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteBackward();

        Assert.AreEqual("Hell", doc.GetText());
        Assert.AreEqual(new DocumentOffset(4), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteBackward_AtStart_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.DeleteBackward();

        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(new DocumentOffset(0), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteBackward_WhenReadOnly_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(3);

        state.DeleteBackward();

        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void DeleteForward_DeletesCharAfterCursor()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.DeleteForward();

        Assert.AreEqual("ello", doc.GetText());
        Assert.AreEqual(new DocumentOffset(0), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteForward_AtEnd_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteForward();

        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteForward_WhenReadOnly_DoesNothing()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(0);

        state.DeleteForward();

        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void DeleteForward_WithSelection_DeletesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);
        state.Cursor.SelectionAnchor = new DocumentOffset(0);

        state.DeleteForward();

        Assert.AreEqual(" world", doc.GetText());
        Assert.AreEqual(new DocumentOffset(0), state.Cursor.Position);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveCursor_Left_DecrementsCursorPosition()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Left);

        Assert.AreEqual(new DocumentOffset(2), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveCursor_Left_AtStart_StaysAtZero()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.MoveCursor(CursorDirection.Left);

        Assert.AreEqual(new DocumentOffset(0), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveCursor_Right_IncrementsCursorPosition()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Right);

        Assert.AreEqual(new DocumentOffset(4), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveCursor_Right_AtEnd_StaysAtEnd()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.MoveCursor(CursorDirection.Right);

        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveCursor_Up_MovesPreviousLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // Line 2, Col 3 ("Wo|rld")

        state.MoveCursor(CursorDirection.Up);

        // Should be at Line 1, Col 3
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos.Line);
        Assert.AreEqual(3, pos.Column);
    }

    [TestMethod]
    public void MoveCursor_Up_OnFirstLine_StaysOnFirstLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3); // Line 1

        state.MoveCursor(CursorDirection.Up);

        // Should stay on line 1
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos.Line);
    }

    [TestMethod]
    public void MoveCursor_Down_MovesNextLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(2); // Line 1, Col 3

        state.MoveCursor(CursorDirection.Down);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(2, pos.Line);
        Assert.AreEqual(3, pos.Column);
    }

    [TestMethod]
    public void MoveCursor_Down_OnLastLine_StaysOnLastLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // Line 2

        state.MoveCursor(CursorDirection.Down);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(2, pos.Line);
    }

    [TestMethod]
    public void MoveCursor_Down_ClampsToShorterLine()
    {
        var doc = new Hex1bDocument("Hello World\nHi");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(10); // Col 11 on line 1

        state.MoveCursor(CursorDirection.Down);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(2, pos.Line);
        // Should clamp to end of "Hi" (col 3 = past end)
        Assert.IsTrue(pos.Column <= 3);
    }

    [TestMethod]
    public void MoveCursor_Up_ClampsToShorterLine()
    {
        var doc = new Hex1bDocument("Hi\nHello World");
        var state = new EditorState(doc);
        // Position at end of line 2 ("Hello World" = 11 chars, col 12)
        state.Cursor.Position = new DocumentOffset(13); // "Hi\n" + "Hello Worl" = offset 13

        state.MoveCursor(CursorDirection.Up);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos.Line);
        Assert.IsTrue(pos.Column <= 3); // "Hi" only has 2 chars + 1 past end
    }

    [TestMethod]
    public void MoveCursor_ClearsSelection()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);
        state.Cursor.SelectionAnchor = new DocumentOffset(0);

        state.MoveCursor(CursorDirection.Right);

        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveCursor_SingleLineDocument_UpDown_NoOp()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Up);
        Assert.AreEqual(new DocumentOffset(3), state.Cursor.Position);

        state.MoveCursor(CursorDirection.Down);
        Assert.AreEqual(new DocumentOffset(3), state.Cursor.Position);
    }

    [TestMethod]
    public void SharedDocument_EditsVisibleInBothStates()
    {
        var doc = new Hex1bDocument("Hello");
        var state1 = new EditorState(doc);
        var state2 = new EditorState(doc);

        state1.Cursor.Position = new DocumentOffset(5);
        state1.InsertText(" world");

        // Both see the change through the shared document
        Assert.AreEqual("Hello world", doc.GetText());
        Assert.AreEqual(11, doc.Length);

        // state2 can also read the updated content
        Assert.AreEqual("Hello world", state2.Document.GetText());
    }

    [TestMethod]
    public void SharedDocument_ChangedEventNotifiesBothStates()
    {
        var doc = new Hex1bDocument("Hello");
        var changedCount = 0;
        doc.Changed += (_, _) => changedCount++;

        var state1 = new EditorState(doc);
        state1.Cursor.Position = new DocumentOffset(5);
        state1.InsertText("!");

        Assert.AreEqual(1, changedCount);
    }

    [TestMethod]
    public void DeleteBackward_WithSelection_DeletesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11);
        state.Cursor.SelectionAnchor = new DocumentOffset(5);

        state.DeleteBackward();

        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void TabSize_DefaultIsFour()
    {
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);
        Assert.AreEqual(4, state.TabSize);
    }

    [TestMethod]
    public void TabSize_CanBeChanged()
    {
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc) { TabSize = 2 };
        Assert.AreEqual(2, state.TabSize);
    }

    [TestMethod]
    public void Constructor_NullDocument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new EditorState(null!));
    }

    [TestMethod]
    public void InsertText_EmptyDocument_Works()
    {
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        state.InsertText("Hello");

        Assert.AreEqual("Hello", doc.GetText());
        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
    }

    [TestMethod]
    public void DeleteBackward_OnNewline_JoinsLines()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(6); // Start of "World"

        state.DeleteBackward();

        Assert.AreEqual("HelloWorld", doc.GetText());
        Assert.AreEqual(1, doc.LineCount);
    }

    [TestMethod]
    public void DeleteForward_OnNewline_JoinsLines()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5); // At the \n

        state.DeleteForward();

        Assert.AreEqual("HelloWorld", doc.GetText());
        Assert.AreEqual(1, doc.LineCount);
    }

    [TestMethod]
    public void MoveCursor_ThroughNewline_Left()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3); // Start of "C"

        state.MoveCursor(CursorDirection.Left);

        // Should be at offset 2 (the \n char)
        Assert.AreEqual(new DocumentOffset(2), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveCursor_ThroughNewline_Right()
    {
        var doc = new Hex1bDocument("AB\nCD");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(2); // At the \n

        state.MoveCursor(CursorDirection.Right);

        Assert.AreEqual(new DocumentOffset(3), state.Cursor.Position);
    }

    [TestMethod]
    public void InsertText_WithSelection_ReverseAnchor_Works()
    {
        // Selection where anchor is AFTER position (backward selection)
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(6);
        state.Cursor.SelectionAnchor = new DocumentOffset(11);

        state.InsertText("earth");

        Assert.AreEqual("Hello earth", doc.GetText());
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MultipleEdits_ThenMoveCursor_WorksCorrectly()
    {
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        state.InsertText("Hello\nWorld\nFoo");

        // Cursor should be at end
        Assert.AreEqual(new DocumentOffset(15), state.Cursor.Position);

        // Move up twice
        state.MoveCursor(CursorDirection.Up);
        var pos1 = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(2, pos1.Line);

        state.MoveCursor(CursorDirection.Up);
        var pos2 = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos2.Line);
    }
}
