using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for EditorState navigation and selection operations with the extend pattern.
/// </summary>
[TestClass]
public class EditorStateNavigationTests
{
    // ── MoveToLineStart / MoveToLineEnd ─────────────────────────

    [TestMethod]
    public void MoveToLineStart_MovesToColumnOne()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // "Wo|rld" (line 2, col 3)

        state.MoveToLineStart();

        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position); // start of "World"
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveToLineStart_AlreadyAtStart_NoOp()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(6); // start of line 2

        state.MoveToLineStart();

        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveToLineStart_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(9); // "Wor|ld"

        state.MoveToLineStart(extend: true);

        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(new DocumentOffset(9), state.Cursor.SelectionAnchor);
    }

    [TestMethod]
    public void MoveToLineEnd_MovesToEndOfLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(6); // start of "World"

        state.MoveToLineEnd();

        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position); // past "World"
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveToLineEnd_FirstLine_StopsBeforeNewline()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.MoveToLineEnd();

        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position); // end of "Hello", before \n
    }

    [TestMethod]
    public void MoveToLineEnd_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(7); // "W|orld"

        state.MoveToLineEnd(extend: true);

        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(new DocumentOffset(7), state.Cursor.SelectionAnchor);
    }

    // ── MoveToDocumentStart / MoveToDocumentEnd ─────────────────

    [TestMethod]
    public void MoveToDocumentStart_MovesToZero()
    {
        var doc = new Hex1bDocument("Hello\nWorld\nFoo");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(14);

        state.MoveToDocumentStart();

        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.Position);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveToDocumentEnd_MovesToLength()
    {
        var doc = new Hex1bDocument("Hello\nWorld\nFoo");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);

        state.MoveToDocumentEnd();

        Assert.AreEqual(new DocumentOffset(15), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveToDocumentStart_Extend_SelectsAll()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11); // end

        state.MoveToDocumentStart(extend: true);

        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.Position);
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.SelectionAnchor);
        Assert.IsTrue(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveToDocumentEnd_Extend_SelectsAll()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.MoveToDocumentEnd(extend: true);

        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.SelectionAnchor);
    }

    // ── MoveWordLeft / MoveWordRight ────────────────────────────

    [TestMethod]
    public void MoveWordLeft_SkipsToWordBoundary()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // "Hello Wo|rld"

        state.MoveWordLeft();

        // Should land at start of "World" (offset 6)
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordRight_SkipsToWordBoundary()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(2); // "He|llo World"

        state.MoveWordRight();

        // Skips remaining "llo" + space → lands at start of "World" (offset 6)
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordLeft_AtDocumentStart_NoOp()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.MoveWordLeft();

        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordRight_AtDocumentEnd_NoOp()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.MoveWordRight();

        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordLeft_AtLineStart_MovesToPreviousLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(6); // start of "World"

        state.MoveWordLeft();

        // Should cross the newline to end of previous line
        Assert.AreEqual(new DocumentOffset(5), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordRight_AtLineEnd_MovesToNextLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5); // end of "Hello"

        state.MoveWordRight();

        // Should cross the newline to start of next line
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
    }

    [TestMethod]
    public void MoveWordLeft_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11); // end

        state.MoveWordLeft(extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.SelectionAnchor);
    }

    [TestMethod]
    public void MoveWordRight_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.MoveWordRight(extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.SelectionAnchor);
    }

    // ── MovePageUp / MovePageDown ───────────────────────────────

    [TestMethod]
    public void MovePageDown_MovesDownByViewportLines()
    {
        var doc = new Hex1bDocument("L1\nL2\nL3\nL4\nL5\nL6\nL7\nL8\nL9\nL10");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero; // Line 1

        state.MovePageDown(5); // viewport is 5 lines, move by 4

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(5, pos.Line); // moved 4 lines down
    }

    [TestMethod]
    public void MovePageUp_MovesUpByViewportLines()
    {
        var doc = new Hex1bDocument("L1\nL2\nL3\nL4\nL5\nL6\nL7\nL8\nL9\nL10");
        var state = new EditorState(doc);
        // Move to line 8
        state.Cursor.Position = doc.PositionToOffset(new DocumentPosition(8, 1));

        state.MovePageUp(5); // viewport is 5 lines, move by 4

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(4, pos.Line); // moved 4 lines up
    }

    [TestMethod]
    public void MovePageDown_ClampsToLastLine()
    {
        var doc = new Hex1bDocument("L1\nL2\nL3");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.MovePageDown(100); // huge viewport

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(3, pos.Line); // clamped to last line
    }

    [TestMethod]
    public void MovePageUp_ClampsToFirstLine()
    {
        var doc = new Hex1bDocument("L1\nL2\nL3");
        var state = new EditorState(doc);
        state.Cursor.Position = doc.PositionToOffset(new DocumentPosition(3, 1));

        state.MovePageUp(100);

        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos.Line);
    }

    [TestMethod]
    public void MovePageDown_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("L1\nL2\nL3\nL4\nL5");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.MovePageDown(3, extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.SelectionAnchor);
    }

    // ── SelectAll ───────────────────────────────────────────────

    [TestMethod]
    public void SelectAll_SelectsEntireDocument()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.SelectAll();

        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.SelectionAnchor);
        Assert.AreEqual(new DocumentOffset(11), state.Cursor.Position);
        Assert.IsTrue(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void SelectAll_EmptyDocument_NoSelection()
    {
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        state.SelectAll();

        // Anchor is 0, position is 0 — HasSelection should be false
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    // ── MoveCursor with extend ──────────────────────────────────

    [TestMethod]
    public void MoveCursor_Left_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Left, extend: true);

        Assert.AreEqual(new DocumentOffset(2), state.Cursor.Position);
        Assert.AreEqual(new DocumentOffset(3), state.Cursor.SelectionAnchor);
        Assert.IsTrue(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveCursor_Right_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.MoveCursor(CursorDirection.Right, extend: true);

        Assert.AreEqual(new DocumentOffset(4), state.Cursor.Position);
        Assert.AreEqual(new DocumentOffset(3), state.Cursor.SelectionAnchor);
    }

    [TestMethod]
    public void MoveCursor_Up_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // Line 2, Col 3

        state.MoveCursor(CursorDirection.Up, extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(new DocumentOffset(8), state.Cursor.SelectionAnchor);
        // Cursor should be on line 1
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(1, pos.Line);
    }

    [TestMethod]
    public void MoveCursor_Down_Extend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(2); // Line 1, Col 3

        state.MoveCursor(CursorDirection.Down, extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(new DocumentOffset(2), state.Cursor.SelectionAnchor);
        var pos = doc.OffsetToPosition(state.Cursor.Position);
        Assert.AreEqual(2, pos.Line);
    }

    [TestMethod]
    public void MoveCursor_Left_WithExistingSelection_NoExtend_CollapsesToStart()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(4);
        state.Cursor.SelectionAnchor = new DocumentOffset(1);

        state.MoveCursor(CursorDirection.Left); // extend: false

        Assert.AreEqual(new DocumentOffset(1), state.Cursor.Position); // collapsed to selection start
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void MoveCursor_Right_WithExistingSelection_NoExtend_CollapsesToEnd()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(1);
        state.Cursor.SelectionAnchor = new DocumentOffset(4);

        state.MoveCursor(CursorDirection.Right); // extend: false

        Assert.AreEqual(new DocumentOffset(4), state.Cursor.Position); // collapsed to selection end
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    // ── Extend chains (multiple extends preserve anchor) ────────

    [TestMethod]
    public void MultipleExtends_PreserveOriginalAnchor()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5); // after "Hello"

        // Shift+Right three times
        state.MoveCursor(CursorDirection.Right, extend: true);
        state.MoveCursor(CursorDirection.Right, extend: true);
        state.MoveCursor(CursorDirection.Right, extend: true);

        Assert.AreEqual(new DocumentOffset(8), state.Cursor.Position);
        Assert.AreEqual(new DocumentOffset(5), state.Cursor.SelectionAnchor); // anchor unchanged
        Assert.IsTrue(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void Extend_ThenNoExtend_ClearsSelection()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.MoveCursor(CursorDirection.Right, extend: true);
        state.MoveCursor(CursorDirection.Right, extend: true);
        // Now clear by moving without extend
        state.MoveCursor(CursorDirection.Right);

        Assert.IsFalse(state.Cursor.HasSelection);
    }
}
