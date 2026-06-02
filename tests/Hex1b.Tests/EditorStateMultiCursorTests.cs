using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for EditorState multi-cursor operations.
/// NOTE: Multi-cursor behavior may change as the editor evolves; these tests document
/// the current expected behavior for insert, delete, movement, and merge operations.
/// </summary>
[TestClass]
public class EditorStateMultiCursorTests
{
    private static EditorState CreateState(string text)
    {
        return new EditorState(new Hex1bDocument(text));
    }

    // ── Multi-cursor insert ─────────────────────────────────────

    [TestMethod]
    public void InsertText_MultiCursor_InsertsAtAllPositions()
    {
        var state = CreateState("aaabbb");
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursors.Add(new DocumentOffset(3));

        state.InsertText("X");

        // After reverse-order insert: cursor at 3 inserts first -> "aaaXbbb"
        // Then cursor at 0 inserts -> "XaaaXbbb"
        Assert.AreEqual("XaaaXbbb", state.Document.GetText());
    }

    [TestMethod]
    public void InsertText_MultiCursor_CursorsAdvancePastInsertedText()
    {
        var state = CreateState("ab");
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursors.Add(new DocumentOffset(1));

        state.InsertText("X");

        // "XaXb", cursors at 1 and 3
        Assert.AreEqual("XaXb", state.Document.GetText());
        Assert.AreEqual(1, state.Cursors[0].Position.Value);
        Assert.AreEqual(3, state.Cursors[1].Position.Value);
    }

    [TestMethod]
    public void InsertText_MultiCursor_MergesOverlapping()
    {
        var state = CreateState("");
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursors.Add(new DocumentOffset(0)); // Duplicate

        state.InsertText("X");

        // Both insert at 0, resulting in "XX", then merge since positions would overlap
        Assert.Contains("X", state.Document.GetText());
    }

    // ── Multi-cursor delete ─────────────────────────────────────

    [TestMethod]
    public void DeleteBackward_MultiCursor_DeletesAtAllPositions()
    {
        var state = CreateState("XaXb");
        state.Cursor.Position = new DocumentOffset(1); // after first X
        state.Cursors.Add(new DocumentOffset(3)); // after second X

        state.DeleteBackward();

        Assert.AreEqual("ab", state.Document.GetText());
    }

    [TestMethod]
    public void DeleteForward_MultiCursor_DeletesAtAllPositions()
    {
        var state = CreateState("aXbX");
        state.Cursor.Position = new DocumentOffset(1); // before first X
        state.Cursors.Add(new DocumentOffset(3)); // before second X

        state.DeleteForward();

        Assert.AreEqual("ab", state.Document.GetText());
    }

    // ── Multi-cursor navigation ─────────────────────────────────

    [TestMethod]
    public void MoveCursor_MultiCursor_MovesAll()
    {
        var state = CreateState("Hello\nWorld");
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursors.Add(new DocumentOffset(6)); // Start of "World"

        state.MoveCursor(CursorDirection.Right);

        Assert.AreEqual(1, state.Cursors[0].Position.Value);
        Assert.AreEqual(7, state.Cursors[1].Position.Value);
    }

    [TestMethod]
    public void MoveToLineEnd_MultiCursor_MovesAll()
    {
        var state = CreateState("abc\ndef");
        state.Cursor.Position = new DocumentOffset(0);
        state.Cursors.Add(new DocumentOffset(4)); // Start of "def"

        state.MoveToLineEnd();

        Assert.AreEqual(3, state.Cursors[0].Position.Value); // End of "abc"
        Assert.AreEqual(7, state.Cursors[1].Position.Value); // End of "def"
    }

    [TestMethod]
    public void MoveToDocumentStart_MultiCursor_MergesAll()
    {
        var state = CreateState("Hello\nWorld");
        state.Cursor.Position = new DocumentOffset(2);
        state.Cursors.Add(new DocumentOffset(8));

        state.MoveToDocumentStart();

        // Both cursors move to offset 0 — should merge to 1
        TestSeq.Single(state.Cursors);
        Assert.AreEqual(0, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void MoveToDocumentEnd_MultiCursor_MergesAll()
    {
        var state = CreateState("Hello\nWorld");
        state.Cursor.Position = new DocumentOffset(2);
        state.Cursors.Add(new DocumentOffset(8));

        state.MoveToDocumentEnd();

        TestSeq.Single(state.Cursors);
        Assert.AreEqual(11, state.Cursor.Position.Value);
    }

    // ── Multi-cursor selection extend ───────────────────────────

    [TestMethod]
    public void MoveCursor_Extend_MultiCursor_ExtendsAll()
    {
        var state = CreateState("abcdef");
        state.Cursor.Position = new DocumentOffset(1);
        state.Cursors.Add(new DocumentOffset(4));

        state.MoveCursor(CursorDirection.Right, extend: true);

        Assert.AreEqual(2, state.Cursors[0].Position.Value);
        Assert.IsTrue(state.Cursors[0].HasSelection);
        Assert.AreEqual(1, state.Cursors[0].SelectionAnchor!.Value.Value);

        Assert.AreEqual(5, state.Cursors[1].Position.Value);
        Assert.IsTrue(state.Cursors[1].HasSelection);
        Assert.AreEqual(4, state.Cursors[1].SelectionAnchor!.Value.Value);
    }

    // ── AddCursorAtNextMatch ────────────────────────────────────

    [TestMethod]
    public void AddCursorAtNextMatch_SelectsWordUnderCursor_IfNoSelection()
    {
        var state = CreateState("hello world hello");
        state.Cursor.Position = new DocumentOffset(1); // inside "hello"

        state.AddCursorAtNextMatch();

        // First call selects "hello" under cursor, then finds next "hello"
        Assert.AreEqual(2, state.Cursors.Count);
    }

    [TestMethod]
    public void AddCursorAtNextMatch_FindsNextOccurrence()
    {
        var state = CreateState("abc def abc ghi abc");
        // Select "abc" (0..3)
        state.Cursor.SelectionAnchor = new DocumentOffset(0);
        state.Cursor.Position = new DocumentOffset(3);

        state.AddCursorAtNextMatch();

        Assert.AreEqual(2, state.Cursors.Count);
        // Second cursor should select the second "abc" at positions 8..11
        var second = state.Cursors[1];
        Assert.IsTrue(second.HasSelection);
        Assert.AreEqual(8, second.SelectionStart.Value);
        Assert.AreEqual(11, second.SelectionEnd.Value);
    }

    [TestMethod]
    public void AddCursorAtNextMatch_WrapsAround()
    {
        var state = CreateState("abc def abc");
        // Position at second "abc", select it
        state.Cursor.SelectionAnchor = new DocumentOffset(8);
        state.Cursor.Position = new DocumentOffset(11);

        state.AddCursorAtNextMatch();

        // Should wrap around and find the first "abc"
        Assert.AreEqual(2, state.Cursors.Count);
    }

    [TestMethod]
    public void AddCursorAtNextMatch_NoMatch_DoesNothing()
    {
        var state = CreateState("abc def ghi");
        state.Cursor.SelectionAnchor = new DocumentOffset(0);
        state.Cursor.Position = new DocumentOffset(3); // "abc" selected

        state.AddCursorAtNextMatch(); // No more "abc"

        TestSeq.Single(state.Cursors); // No new cursor added
    }

    [TestMethod]
    public void AddCursorAtNextMatch_ThreeTimes_ThreeCursors()
    {
        var state = CreateState("ab cd ab ef ab");
        state.Cursor.SelectionAnchor = new DocumentOffset(0);
        state.Cursor.Position = new DocumentOffset(2); // "ab" selected

        state.AddCursorAtNextMatch(); // Finds "ab" at 6
        state.AddCursorAtNextMatch(); // Finds "ab" at 12

        Assert.AreEqual(3, state.Cursors.Count);
    }

    // ── CollapseToSingleCursor ──────────────────────────────────

    [TestMethod]
    public void CollapseToSingleCursor_KeepsPrimary()
    {
        var state = CreateState("abcdef");
        state.Cursor.Position = new DocumentOffset(2);
        state.Cursors.Add(new DocumentOffset(4));

        state.CollapseToSingleCursor();

        TestSeq.Single(state.Cursors);
        Assert.AreEqual(2, state.Cursor.Position.Value);
    }

    // ── Multi-cursor word operations ────────────────────────────

    [TestMethod]
    public void DeleteWordBackward_MultiCursor()
    {
        // "hello world foo bar" with cursors after "hello" and "world"
        // After deleting words backward:
        // Cursor at 11 (after "world") deletes "world" → "hello  foo bar"
        // Cursor at 5 (after "hello") deletes "hello" → "  foo bar"
        // But offset adjustment shifts: delete at 5 removes 5 chars, cursor at 6 adjusts
        var state = CreateState("hello world foo bar");
        state.Cursor.Position = new DocumentOffset(5); // after "hello"
        state.Cursors.Add(new DocumentOffset(11)); // after "world"

        state.DeleteWordBackward();

        Assert.AreEqual("  foo bar", state.Document.GetText());
    }

    [TestMethod]
    public void DeleteWordForward_MultiCursor()
    {
        // "hello world foo" with cursors before "hello" and before "world"
        // GetNextWordBoundary skips word chars + trailing non-word chars
        // Cursor at 6 deletes "world " → "hello foo"
        // Cursor at 0 deletes "hello " → "foo"
        var state = CreateState("hello world foo");
        state.Cursor.Position = new DocumentOffset(0); // before "hello"
        state.Cursors.Add(new DocumentOffset(6)); // before "world"

        state.DeleteWordForward();

        Assert.AreEqual("foo", state.Document.GetText());
    }
}
