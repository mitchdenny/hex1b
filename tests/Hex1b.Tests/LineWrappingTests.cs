using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class LineWrappingTests
{
    [Fact]
    public void WrapLine_ShortLine_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello", 20);

        Assert.Single(result);
        Assert.Equal("Hello", result[0]);
    }

    [Fact]
    public void WrapLine_LongLine_WrapsAtWordBoundary()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello world this is a test", 12);

        Assert.True(result.Count >= 2);
        Assert.Equal("Hello world", result[0]);
        Assert.StartsWith("this", result[1]);
    }

    [Fact]
    public void WrapLine_NoSpaces_HardBreaks()
    {
        var result = TextEditorViewRenderer.WrapLine("ABCDEFGHIJKLMNO", 5);

        Assert.Equal(3, result.Count);
        Assert.Equal("ABCDE", result[0]);
        Assert.Equal("FGHIJ", result[1]);
        Assert.Equal("KLMNO", result[2]);
    }

    [Fact]
    public void WrapLine_MultipleWraps()
    {
        // 30 chars, viewport 10 => 3 segments
        var result = TextEditorViewRenderer.WrapLine("aaaa bbbb cccc dddd eeee ffff", 10);

        Assert.True(result.Count >= 3);
    }

    [Fact]
    public void WrapLine_ExactWidth_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("12345", 5);

        Assert.Single(result);
        Assert.Equal("12345", result[0]);
    }

    [Fact]
    public void WrapLine_EmptyLine_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("", 10);

        Assert.Single(result);
        Assert.Equal("", result[0]);
    }

    [Fact]
    public void WrapLine_ZeroWidth_ReturnsSingleSegment()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello", 0);

        Assert.Single(result);
        Assert.Equal("Hello", result[0]);
    }

    [Fact]
    public void EditorWidget_WordWrap_SetsProperty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap();
        Assert.True(widget.WordWrapValue);
    }

    [Fact]
    public void EditorWidget_WordWrap_DefaultFalse()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);
        Assert.False(widget.WordWrapValue);
    }

    [Fact]
    public void EditorWidget_WordWrap_CanDisable()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap(true).WordWrap(false);
        Assert.False(widget.WordWrapValue);
    }

    [Fact]
    public void EditorNode_WordWrap_DefaultFalse()
    {
        var node = new EditorNode();
        Assert.False(node.WordWrap);
    }

    [Fact]
    public void EditorNode_WordWrap_CanBeSet()
    {
        var node = new EditorNode();
        node.WordWrap = true;
        Assert.True(node.WordWrap);
    }

    [Fact]
    public async Task Reconcile_WordWrap_SetsNodeProperty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap();

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.True(node.WordWrap);
    }

    [Fact]
    public async Task Reconcile_NoWordWrap_NodePropertyFalse()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.False(node.WordWrap);
    }
}
