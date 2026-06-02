using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for EditorState word-level and line-level deletion operations.
/// </summary>
[TestClass]
public class EditorStateEditingTests
{
    // ── DeleteWordBackward ──────────────────────────────────────

    [TestMethod]
    public void DeleteWordBackward_DeletesWord()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11); // end

        state.DeleteWordBackward();

        // Should delete "World" (word boundary from end)
        Assert.AreEqual(new DocumentOffset(6), state.Cursor.Position);
        Assert.AreEqual("Hello ", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordBackward_AtDocumentStart_NoOp()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.DeleteWordBackward();

        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordBackward_ReadOnly_NoOp()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(11);

        state.DeleteWordBackward();

        Assert.AreEqual("Hello World", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordBackward_WithSelection_DeletesSelection()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(11);
        state.Cursor.SelectionAnchor = new DocumentOffset(6);

        state.DeleteWordBackward();

        Assert.AreEqual("Hello ", doc.GetText());
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void DeleteWordBackward_MiddleOfWord_DeletesPartialWord()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(8); // "Hello Wo|rld"

        state.DeleteWordBackward();

        // Word boundary back from "Wo" position should go to start of "World"
        var text = doc.GetText();
        Assert.StartsWith("Hello ", text);
    }

    // ── DeleteWordForward ───────────────────────────────────────

    [TestMethod]
    public void DeleteWordForward_DeletesWord()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = DocumentOffset.Zero;

        state.DeleteWordForward();

        // Deletes "Hello " (word + trailing space per word boundary behavior)
        Assert.AreEqual(DocumentOffset.Zero, state.Cursor.Position);
        Assert.AreEqual("World", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordForward_AtDocumentEnd_NoOp()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteWordForward();

        Assert.AreEqual("Hello", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordForward_ReadOnly_NoOp()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = DocumentOffset.Zero;

        state.DeleteWordForward();

        Assert.AreEqual("Hello World", doc.GetText());
    }

    [TestMethod]
    public void DeleteWordForward_WithSelection_DeletesSelection()
    {
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursor.SelectionAnchor = new DocumentOffset(5);

        state.DeleteWordForward();

        Assert.AreEqual(" World", doc.GetText());
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    // ── DeleteLine ──────────────────────────────────────────────

    [TestMethod]
    public void DeleteLine_DeletesCurrentLine()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        var state = new EditorState(doc);
        // Position on line 2
        state.Cursor.Position = doc.PositionToOffset(new DocumentPosition(2, 3));

        state.DeleteLine();

        Assert.AreEqual("Line 1\nLine 3", doc.GetText());
        Assert.AreEqual(2, doc.LineCount);
    }

    [TestMethod]
    public void DeleteLine_FirstLine_DeletesFirstLine()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3); // middle of line 1

        state.DeleteLine();

        Assert.AreEqual("Line 2\nLine 3", doc.GetText());
    }

    [TestMethod]
    public void DeleteLine_LastLine_DeletesLastLine()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        var state = new EditorState(doc);
        state.Cursor.Position = doc.PositionToOffset(new DocumentPosition(3, 3));

        state.DeleteLine();

        Assert.AreEqual("Line 1\nLine 2", doc.GetText());
    }

    [TestMethod]
    public void DeleteLine_OnlyLine_EmptiesDocument()
    {
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(3);

        state.DeleteLine();

        Assert.AreEqual("", doc.GetText());
        Assert.AreEqual(0, doc.Length);
    }

    [TestMethod]
    public void DeleteLine_ReadOnly_NoOp()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc) { IsReadOnly = true };
        state.Cursor.Position = new DocumentOffset(3);

        state.DeleteLine();

        Assert.AreEqual("Hello\nWorld", doc.GetText());
    }

    [TestMethod]
    public void DeleteLine_ClearsSelection()
    {
        var doc = new Hex1bDocument("Line 1\nLine 2\nLine 3");
        var state = new EditorState(doc);
        state.Cursor.Position = new DocumentOffset(10);
        state.Cursor.SelectionAnchor = new DocumentOffset(8);

        state.DeleteLine();

        Assert.IsFalse(state.Cursor.HasSelection);
    }

    // ── EnsureSelectionAnchor (DocumentCursor) ──────────────────

    [TestMethod]
    public void EnsureSelectionAnchor_SetsAnchorIfNull()
    {
        var cursor = new DocumentCursor { Position = new DocumentOffset(5) };
        Assert.IsNull(cursor.SelectionAnchor);

        cursor.EnsureSelectionAnchor();

        Assert.AreEqual(new DocumentOffset(5), cursor.SelectionAnchor);
    }

    [TestMethod]
    public void EnsureSelectionAnchor_DoesNotOverwriteExisting()
    {
        var cursor = new DocumentCursor
        {
            Position = new DocumentOffset(8),
            SelectionAnchor = new DocumentOffset(3)
        };

        cursor.EnsureSelectionAnchor();

        Assert.AreEqual(new DocumentOffset(3), cursor.SelectionAnchor); // unchanged
    }
}
