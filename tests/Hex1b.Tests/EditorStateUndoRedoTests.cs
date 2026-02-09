using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for EditorState undo/redo: single and multi-cursor, typing coalescing,
/// cursor restoration, and cross-view sync.
/// NOTE: These tests depend on the EditHistory coalescing implementation and may
/// need updates if coalescing rules change.
/// </summary>
public class EditorStateUndoRedoTests
{
    private static EditorState CreateState(string text)
    {
        return new EditorState(new Hex1bDocument(text));
    }

    // ── Basic undo/redo ─────────────────────────────────────────

    [Fact]
    public void Undo_RevertsInsert()
    {
        var state = CreateState("");
        state.InsertText("Hello");

        state.Undo();

        Assert.Equal("", state.Document.GetText());
    }

    [Fact]
    public void Undo_RevertsSingleCharInserts_Coalesced()
    {
        var state = CreateState("");

        state.InsertText("a");
        state.InsertText("b");
        state.InsertText("c");

        // Single chars should be coalesced into one undo group
        state.Undo();

        Assert.Equal("", state.Document.GetText());
    }

    [Fact]
    public void Redo_ReappliesInsert()
    {
        var state = CreateState("");
        state.InsertText("Hello");

        state.Undo();
        state.Redo();

        Assert.Equal("Hello", state.Document.GetText());
    }

    [Fact]
    public void Undo_RevertsDelete()
    {
        var state = CreateState("Hello");
        state.Cursor.Position = new DocumentOffset(5);

        state.DeleteBackward();

        Assert.Equal("Hell", state.Document.GetText());

        state.Undo();

        Assert.Equal("Hello", state.Document.GetText());
    }

    [Fact]
    public void Undo_Redo_Undo_Roundtrip()
    {
        var state = CreateState("abc");
        state.Cursor.Position = new DocumentOffset(3);
        state.InsertText("d");

        Assert.Equal("abcd", state.Document.GetText());

        state.Undo();
        Assert.Equal("abc", state.Document.GetText());

        state.Redo();
        Assert.Equal("abcd", state.Document.GetText());

        state.Undo();
        Assert.Equal("abc", state.Document.GetText());
    }

    // ── Cursor restoration ──────────────────────────────────────

    [Fact]
    public void Undo_RestoresCursorPosition()
    {
        var state = CreateState("Hello");
        state.Cursor.Position = new DocumentOffset(5); // End

        state.InsertText(" World");
        Assert.Equal(11, state.Cursor.Position.Value);

        state.Undo();
        Assert.Equal(5, state.Cursor.Position.Value); // Restored
    }

    [Fact]
    public void Redo_RestoresCursorPosition()
    {
        var state = CreateState("Hello");
        state.Cursor.Position = new DocumentOffset(5);
        state.InsertText(" World");

        state.Undo();
        state.Redo();

        Assert.Equal(11, state.Cursor.Position.Value);
    }

    // ── Multi-operation undo ────────────────────────────────────

    [Fact]
    public void Undo_MultipleEdits_UndoesInOrder()
    {
        var state = CreateState("");

        state.InsertText("First");
        state.InsertText("\n"); // Newline breaks coalescing (not single-char coalescable)

        state.Undo(); // Undo the newline
        Assert.Equal("First", state.Document.GetText());

        state.Undo(); // Undo "First" (may be coalesced into one group)
        Assert.Equal("", state.Document.GetText());
    }

    [Fact]
    public void Undo_DeleteBackward_RestoresText()
    {
        var state = CreateState("abcdef");
        state.Cursor.Position = new DocumentOffset(3);

        state.DeleteBackward(); // Delete 'c'
        Assert.Equal("abdef", state.Document.GetText());

        state.Undo();
        Assert.Equal("abcdef", state.Document.GetText());
    }

    [Fact]
    public void Undo_DeleteForward_RestoresText()
    {
        var state = CreateState("abcdef");
        state.Cursor.Position = new DocumentOffset(3);

        state.DeleteForward(); // Delete 'd'
        Assert.Equal("abcef", state.Document.GetText());

        state.Undo();
        Assert.Equal("abcdef", state.Document.GetText());
    }

    [Fact]
    public void Undo_DeleteWordBackward_RestoresWord()
    {
        var state = CreateState("hello world");
        state.Cursor.Position = new DocumentOffset(5); // after "hello"

        state.DeleteWordBackward();
        Assert.Equal(" world", state.Document.GetText());

        state.Undo();
        Assert.Equal("hello world", state.Document.GetText());
    }

    [Fact]
    public void Undo_DeleteLine_RestoresLine()
    {
        var state = CreateState("line1\nline2\nline3");
        state.Cursor.Position = new DocumentOffset(7); // in "line2"

        state.DeleteLine();

        state.Undo();
        Assert.Equal("line1\nline2\nline3", state.Document.GetText());
    }

    // ── New edit after undo clears redo ──────────────────────────

    [Fact]
    public void NewEdit_AfterUndo_ClearsRedo()
    {
        var state = CreateState("");
        state.InsertText("Hello");
        state.Undo();

        Assert.True(state.History.CanRedo);

        state.InsertText("World");

        Assert.False(state.History.CanRedo);
        Assert.Equal("World", state.Document.GetText());
    }

    // ── Undo with no history ────────────────────────────────────

    [Fact]
    public void Undo_WithNoHistory_DoesNothing()
    {
        var state = CreateState("Hello");
        state.Undo(); // Should not throw
        Assert.Equal("Hello", state.Document.GetText());
    }

    [Fact]
    public void Redo_WithNoHistory_DoesNothing()
    {
        var state = CreateState("Hello");
        state.Redo(); // Should not throw
        Assert.Equal("Hello", state.Document.GetText());
    }

    // ── Selection + undo ────────────────────────────────────────

    [Fact]
    public void Undo_ReplaceSelection_RestoresOriginalText()
    {
        var state = CreateState("Hello World");
        state.Cursor.SelectionAnchor = new DocumentOffset(6);
        state.Cursor.Position = new DocumentOffset(11); // Select "World"

        state.InsertText("Hex1b"); // Replace selection
        Assert.Equal("Hello Hex1b", state.Document.GetText());

        state.Undo();
        Assert.Equal("Hello World", state.Document.GetText());
    }

    // ── Read-only ───────────────────────────────────────────────

    [Fact]
    public void ReadOnly_DoesNotAddToHistory()
    {
        var state = CreateState("Hello");
        state.IsReadOnly = true;

        state.InsertText("X");

        Assert.False(state.History.CanUndo);
        Assert.Equal("Hello", state.Document.GetText());
    }

    // ── Cross-view undo ─────────────────────────────────────────

    [Fact]
    public void Undo_AffectsSharedDocument()
    {
        var doc = new Hex1bDocument("Hello");
        var state1 = new EditorState(doc);
        var state2 = new EditorState(doc);

        // Edit through state1
        state1.Cursor.Position = new DocumentOffset(5);
        state1.InsertText(" World");
        Assert.Equal("Hello World", doc.GetText());

        // Undo through state1 — document should revert
        state1.Undo();
        Assert.Equal("Hello", doc.GetText());

        // state2 sees the same document
        Assert.Equal("Hello", state2.Document.GetText());
    }

    // ── Stress: many undo/redo ──────────────────────────────────

    [Fact]
    public void Stress_ManyUndoRedo_MaintainsConsistency()
    {
        var state = CreateState("");

        // Type 50 lines
        for (var i = 0; i < 50; i++)
        {
            state.InsertText($"Line {i}\n");
        }

        var afterText = state.Document.GetText();

        // Undo everything
        while (state.History.CanUndo)
        {
            state.Undo();
        }

        Assert.Equal("", state.Document.GetText());

        // Redo everything
        while (state.History.CanRedo)
        {
            state.Redo();
        }

        Assert.Equal(afterText, state.Document.GetText());
    }
}
