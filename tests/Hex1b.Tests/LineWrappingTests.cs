using Hex1b.Documents;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class LineWrappingTests
{
    [TestMethod]
    public void WrapLine_ShortLine_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello", 20);

        TestSeq.Single(result);
        Assert.AreEqual("Hello", result[0]);
    }

    [TestMethod]
    public void WrapLine_LongLine_WrapsAtWordBoundary()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello world this is a test", 12);

        Assert.IsTrue(result.Count >= 2);
        Assert.AreEqual("Hello world", result[0]);
        Assert.StartsWith("this", result[1]);
    }

    [TestMethod]
    public void WrapLine_NoSpaces_HardBreaks()
    {
        var result = TextEditorViewRenderer.WrapLine("ABCDEFGHIJKLMNO", 5);

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("ABCDE", result[0]);
        Assert.AreEqual("FGHIJ", result[1]);
        Assert.AreEqual("KLMNO", result[2]);
    }

    [TestMethod]
    public void WrapLine_MultipleWraps()
    {
        // 30 chars, viewport 10 => 3 segments
        var result = TextEditorViewRenderer.WrapLine("aaaa bbbb cccc dddd eeee ffff", 10);

        Assert.IsTrue(result.Count >= 3);
    }

    [TestMethod]
    public void WrapLine_ExactWidth_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("12345", 5);

        TestSeq.Single(result);
        Assert.AreEqual("12345", result[0]);
    }

    [TestMethod]
    public void WrapLine_EmptyLine_NoWrap()
    {
        var result = TextEditorViewRenderer.WrapLine("", 10);

        TestSeq.Single(result);
        Assert.AreEqual("", result[0]);
    }

    [TestMethod]
    public void WrapLine_ZeroWidth_ReturnsSingleSegment()
    {
        var result = TextEditorViewRenderer.WrapLine("Hello", 0);

        TestSeq.Single(result);
        Assert.AreEqual("Hello", result[0]);
    }

    [TestMethod]
    public void EditorWidget_WordWrap_SetsProperty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap();
        Assert.IsTrue(widget.WordWrapValue);
    }

    [TestMethod]
    public void EditorWidget_WordWrap_DefaultFalse()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);
        Assert.IsFalse(widget.WordWrapValue);
    }

    [TestMethod]
    public void EditorWidget_WordWrap_CanDisable()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap(true).WordWrap(false);
        Assert.IsFalse(widget.WordWrapValue);
    }

    [TestMethod]
    public void EditorNode_WordWrap_DefaultFalse()
    {
        var node = new EditorNode();
        Assert.IsFalse(node.WordWrap);
    }

    [TestMethod]
    public void EditorNode_WordWrap_CanBeSet()
    {
        var node = new EditorNode();
        node.WordWrap = true;
        Assert.IsTrue(node.WordWrap);
    }

    [TestMethod]
    public async Task Reconcile_WordWrap_SetsNodeProperty()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state).WordWrap();

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.IsTrue(node.WordWrap);
    }

    [TestMethod]
    public async Task Reconcile_NoWordWrap_NodePropertyFalse()
    {
        var doc = new Hex1bDocument("test");
        var state = new EditorState(doc);
        var widget = new EditorWidget(state);

        var context = ReconcileContext.CreateRoot();
        var node = (EditorNode)await widget.ReconcileAsync(null, context);

        Assert.IsFalse(node.WordWrap);
    }
}
