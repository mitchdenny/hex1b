// NOTE: These tests verify EditorNode scroll offset management and EnsureCursorVisible.
// As editor capabilities evolve (e.g., smooth scrolling, scroll margins), expected
// behavior may change.

using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorNodeScrollTests
{
    private static EditorNode CreateNode(string text, int width, int height, bool focused = true)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = focused };

        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));

        return node;
    }

    [Fact]
    public void ScrollOffset_AfterArrange_StartsAt1()
    {
        // NOTE: Initial scroll may change if we support restoring scroll position.
        var node = CreateNode("Hello\nWorld", 20, 5);
        Assert.Equal(1, node.ScrollOffset);
    }

    [Fact]
    public void ScrollOffset_SetToZero_ClampsTo1()
    {
        // NOTE: Clamping behavior may change with virtual scroll support.
        var node = CreateNode("Hello\nWorld", 20, 5);
        node.ScrollOffset = 0;
        Assert.Equal(1, node.ScrollOffset);
    }

    [Fact]
    public void ScrollOffset_SetToNegative_ClampsTo1()
    {
        // NOTE: Clamping behavior may change with virtual scroll support.
        var node = CreateNode("Hello\nWorld", 20, 5);
        node.ScrollOffset = -5;
        Assert.Equal(1, node.ScrollOffset);
    }

    [Fact]
    public void ScrollOffset_SetBeyondLineCount_ClampsToLineCount()
    {
        // NOTE: May change if we allow scrolling past end of document.
        var node = CreateNode("A\nB\nC", 20, 5); // 3 lines
        node.ScrollOffset = 100;
        Assert.Equal(3, node.ScrollOffset);
    }

    [Fact]
    public void EnsureCursorVisible_CursorBelowViewport_ScrollsDown()
    {
        // NOTE: Scroll margin (keeping cursor N lines from edge) may be added.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i}"));
        var node = CreateNode(lines, 20, 5);

        // Move cursor to line 10 (well below 5-line viewport)
        for (var i = 0; i < 9; i++)
            node.State.MoveCursor(CursorDirection.Down);

        // Notify the node that cursor changed (in real usage, input handlers do this)
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 5));

        // Cursor is on line 10; viewport should scroll so line 10 is visible
        Assert.True(node.ScrollOffset <= 10, $"ScrollOffset {node.ScrollOffset} should be <= 10");
        Assert.True(node.ScrollOffset + node.ViewportLines > 10,
            $"ScrollOffset {node.ScrollOffset} + viewport {node.ViewportLines} should show line 10");
    }

    [Fact]
    public void ScrollOffset_SetManually_DoesNotFlickBackToCursor()
    {
        // When scroll is set via scrollbar or wheel (no cursor change), Arrange must
        // NOT snap back to the cursor position. This is the "flick-back" fix.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i}"));
        var node = CreateNode(lines, 20, 5);

        // Scroll to line 15, but cursor is still on line 1
        node.ScrollOffset = 15;

        // Re-arrange should NOT snap back — no cursor change occurred
        node.Arrange(new Rect(0, 0, 20, 5));

        Assert.Equal(15, node.ScrollOffset);
    }

    [Fact]
    public void EnsureCursorVisible_CursorWithinViewport_NoScrollChange()
    {
        // NOTE: Scroll behavior may change with smooth scrolling.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i}"));
        var node = CreateNode(lines, 20, 10);

        // Cursor at line 1, viewport shows lines 1-10 → no scroll needed
        Assert.Equal(1, node.ScrollOffset);

        // Move cursor to line 5 (still within viewport)
        for (var i = 0; i < 4; i++)
            node.State.MoveCursor(CursorDirection.Down);

        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 10));

        // Should still be at scroll offset 1
        Assert.Equal(1, node.ScrollOffset);
    }

    [Fact]
    public void PageDown_ScrollsViewport()
    {
        // NOTE: PageDown behavior may gain half-page option.
        var lines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line{i}"));
        var node = CreateNode(lines, 20, 10);

        // PageDown moves cursor by viewport lines
        node.State.MovePageDown(node.ViewportLines);
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 10));

        // Cursor should be on line 11 (started at line 1, moved down by viewport=10);
        // MovePageDown uses the viewport lines count
        var cursorLine = node.State.Document.OffsetToPosition(node.State.Cursor.Position).Line;
        Assert.True(cursorLine > 1, $"Cursor should have moved past line 1, is on line {cursorLine}");
        Assert.True(node.ScrollOffset <= cursorLine);
        Assert.True(node.ScrollOffset + node.ViewportLines > cursorLine);
    }

    [Fact]
    public void PageUp_ScrollsViewportBack()
    {
        // NOTE: PageUp behavior may gain half-page option.
        var lines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line{i}"));
        var node = CreateNode(lines, 20, 10);

        // Go to line 20, then page up
        for (var i = 0; i < 19; i++)
            node.State.MoveCursor(CursorDirection.Down);
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 10));

        node.State.MovePageUp(node.ViewportLines);
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 10));

        var cursorLine = node.State.Document.OffsetToPosition(node.State.Cursor.Position).Line;
        Assert.True(node.ScrollOffset <= cursorLine);
        Assert.True(node.ScrollOffset + node.ViewportLines > cursorLine);
    }

    [Fact]
    public void TwoEditors_SameDoc_IndependentScroll()
    {
        // NOTE: Linked scroll mode may be added for diff views.
        var doc = new Hex1bDocument(string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line{i}")));
        var state1 = new EditorState(doc);
        var state2 = new EditorState(doc);

        var node1 = new EditorNode { State = state1, IsFocused = true };
        var node2 = new EditorNode { State = state2, IsFocused = false };

        node1.Measure(new Constraints(0, 20, 0, 10));
        node1.Arrange(new Rect(0, 0, 20, 10));

        node2.Measure(new Constraints(0, 20, 0, 10));
        node2.Arrange(new Rect(0, 0, 20, 10));

        // Scroll node1 to line 20
        node1.ScrollOffset = 20;

        // node2 should still be at line 1
        Assert.Equal(20, node1.ScrollOffset);
        Assert.Equal(1, node2.ScrollOffset);
    }

    [Fact]
    public void ViewportLines_MatchArrangedHeight()
    {
        // NOTE: May change if scrollbar or status bar consumes viewport lines.
        var node = CreateNode("Hello", 30, 15);
        Assert.Equal(15, node.ViewportLines);
        Assert.Equal(30, node.ViewportColumns);
    }
}
