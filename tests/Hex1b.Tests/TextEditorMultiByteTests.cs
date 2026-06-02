using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests that the text editor works correctly with multi-byte and multi-char-unit
/// Unicode characters (surrogate pairs, combining marks, etc.).
/// Verifies grapheme-wise cursor movement and correct rendering without U+FFFD corruption.
/// </summary>
[TestClass]
public class TextEditorMultiByteTests
{
    [TestMethod]
    public void MoveCursor_Right_SkipsOverSurrogatePair()
    {
        // 😀 is U+1F600 — a surrogate pair (2 C# chars)
        var doc = new Hex1bDocument("A😀B");
        var state = new EditorState(doc);

        // Cursor starts at 0 ('A')
        Assert.AreEqual(0, state.Cursor.Position.Value);

        // Move right: should land after 'A', before 😀
        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(1, state.Cursor.Position.Value);

        // Move right: should skip the entire surrogate pair (2 C# chars), land after 😀
        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(3, state.Cursor.Position.Value); // 1 + 2 (surrogate pair length)

        // Move right: should land after 'B'
        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(4, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void MoveCursor_Left_SkipsOverSurrogatePair()
    {
        var doc = new Hex1bDocument("A😀B");
        var state = new EditorState(doc);

        // Start at end
        state.Cursor.Position = new DocumentOffset(doc.Length); // 4
        Assert.AreEqual(4, state.Cursor.Position.Value);

        // Move left: land before 'B'
        state.MoveCursor(CursorDirection.Left);
        Assert.AreEqual(3, state.Cursor.Position.Value);

        // Move left: skip entire surrogate pair, land before 😀
        state.MoveCursor(CursorDirection.Left);
        Assert.AreEqual(1, state.Cursor.Position.Value);

        // Move left: land at start
        state.MoveCursor(CursorDirection.Left);
        Assert.AreEqual(0, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void MoveCursor_Right_HandlesAccentedCharacters()
    {
        // 'é' (U+00E9) is a single C# char (2 UTF-8 bytes but 1 UTF-16 code unit)
        var doc = new Hex1bDocument("café");
        var state = new EditorState(doc);

        state.MoveCursor(CursorDirection.Right); // c
        Assert.AreEqual(1, state.Cursor.Position.Value);
        state.MoveCursor(CursorDirection.Right); // a
        Assert.AreEqual(2, state.Cursor.Position.Value);
        state.MoveCursor(CursorDirection.Right); // f
        Assert.AreEqual(3, state.Cursor.Position.Value);
        state.MoveCursor(CursorDirection.Right); // é
        Assert.AreEqual(4, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void MoveCursor_Right_HandlesMultipleEmoji()
    {
        // Two emoji, each a surrogate pair (2 C# chars each)
        var doc = new Hex1bDocument("🚀🎉");
        var state = new EditorState(doc);

        Assert.AreEqual(0, state.Cursor.Position.Value);

        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(2, state.Cursor.Position.Value); // past first emoji

        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(4, state.Cursor.Position.Value); // past second emoji
    }

    [TestMethod]
    public void MoveCursor_DoesNotLandInsideSurrogatePair()
    {
        // "X😀Y" — moving right from X should never land at position 2 (inside the surrogate pair)
        var doc = new Hex1bDocument("X😀Y");
        var state = new EditorState(doc);

        var positions = new List<int>();
        for (int i = 0; i <= doc.Length; i++)
        {
            positions.Add(state.Cursor.Position.Value);
            state.MoveCursor(CursorDirection.Right);
        }

        // Valid cursor positions: 0 (before X), 1 (before 😀), 3 (before Y), 4 (end)
        // Position 2 (inside surrogate pair) should NEVER appear
        Assert.DoesNotContain(2, positions);
        Assert.Contains(0, positions);
        Assert.Contains(1, positions);
        Assert.Contains(3, positions);
        Assert.Contains(4, positions);
    }

    [TestMethod]
    public void SelectRight_OverSurrogatePair_SelectsWholeCharacter()
    {
        var doc = new Hex1bDocument("A😀B");
        var state = new EditorState(doc);

        // Move to position 1 (before 😀)
        state.MoveCursor(CursorDirection.Right);
        Assert.AreEqual(1, state.Cursor.Position.Value);

        // Select right — should select the entire emoji
        state.MoveCursor(CursorDirection.Right, extend: true);
        Assert.AreEqual(3, state.Cursor.Position.Value);
        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(1, state.Cursor.SelectionStart.Value);
        Assert.AreEqual(3, state.Cursor.SelectionEnd.Value);

        // The selected text should be the emoji, not a broken surrogate
        var selectedText = doc.GetText(new DocumentRange(state.Cursor.SelectionStart, state.Cursor.SelectionEnd));
        Assert.AreEqual("😀", selectedText);
    }

    [TestMethod]
    public void HexEditorNavigation_AfterViewSwitch_WorksCorrectly()
    {
        // Simulates switching from text to hex view — ByteCursorOffset should be null
        // and hex renderer should derive byte position from char position
        var doc = new Hex1bDocument("AB");
        var state = new EditorState(doc);

        // Move right in text mode (clears ByteCursorOffset)
        state.MoveCursor(CursorDirection.Right);
        Assert.IsNull(state.ByteCursorOffset);
        Assert.AreEqual(1, state.Cursor.Position.Value);

        // Now simulate hex navigation — HandleNavigation should work
        var hexRenderer = new HexEditorViewRenderer();
        var handled = hexRenderer.HandleNavigation(CursorDirection.Right, state, extend: false, 80);
        Assert.IsTrue(handled);

        // ByteCursorOffset should now be set
        Assert.IsNotNull(state.ByteCursorOffset);
    }
}
