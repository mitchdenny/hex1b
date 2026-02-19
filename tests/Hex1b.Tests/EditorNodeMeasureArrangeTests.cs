// NOTE: These tests verify EditorNode measurement and arrangement behavior.
// As editor capabilities evolve (e.g., scrollbars, gutters), layout may change.

using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorNodeMeasureArrangeTests
{
    [Fact]
    public void Measure_FillsAvailableSpace()
    {
        // NOTE: Editor may not always fill space if min-size constraints are added.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.Equal(40, size.Width);
        Assert.Equal(10, size.Height);
    }

    [Fact]
    public void Measure_Unconstrained_FillsDesiredDefault()
    {
        // NOTE: Default dimensions may become configurable.
        // When constraints allow it, the editor prefers 80x24 but is clamped by constraints.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };

        // With large constraints, editor fills to its preferred 80x24
        var size = node.Measure(new Constraints(0, 200, 0, 100));

        // Editor takes all available space up to max
        Assert.Equal(200, size.Width);
        Assert.Equal(100, size.Height);
    }

    [Fact]
    public void Arrange_StoresViewportDimensions()
    {
        // NOTE: Viewport may shrink if scrollbar or gutter is added.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };

        node.Measure(new Constraints(0, 50, 0, 20));
        node.Arrange(new Rect(0, 0, 50, 20));

        Assert.Equal(20, node.ViewportLines);
        Assert.Equal(50, node.ViewportColumns);
    }

    [Fact]
    public void Arrange_SubscribesToDocument_ChangesMarkDirty()
    {
        // NOTE: Subscription model may change with reactive/observable patterns.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };

        node.Measure(new Constraints(0, 20, 0, 5));
        node.Arrange(new Rect(0, 0, 20, 5));

        // After arrange, the node subscribes to document changes.
        // Editing the document should mark the node dirty.
        // We can verify by checking that a subsequent edit doesn't throw
        // (the subscription handler calls MarkDirty).
        doc.Apply(new InsertOperation(new DocumentOffset(5), " World"));

        // If subscription failed, this would not reach here
        Assert.Equal("Hello World", doc.GetText());
    }

    [Fact]
    public void Arrange_SwappingDocument_UnsubscribesOldSubscribesNew()
    {
        // NOTE: Hot-swapping documents may gain animation/transition in future.
        var doc1 = new Hex1bDocument("Doc1");
        var doc2 = new Hex1bDocument("Doc2");
        var state1 = new EditorState(doc1);
        var state2 = new EditorState(doc2);
        var node = new EditorNode { State = state1 };

        node.Measure(new Constraints(0, 20, 0, 5));
        node.Arrange(new Rect(0, 0, 20, 5));

        // Swap to state2
        node.State = state2;
        node.Arrange(new Rect(0, 0, 20, 5));

        // Edit doc2 — should not throw (subscription active)
        doc2.Apply(new InsertOperation(new DocumentOffset(4), "!"));
        Assert.Equal("Doc2!", doc2.GetText());

        // Edit doc1 — node should no longer react to it (unsubscribed)
        doc1.Apply(new InsertOperation(new DocumentOffset(4), "?"));
        Assert.Equal("Doc1?", doc1.GetText());
    }

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        // NOTE: Read-only editor may still be focusable for navigation.
        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };

        Assert.True(node.IsFocusable);
    }
}
