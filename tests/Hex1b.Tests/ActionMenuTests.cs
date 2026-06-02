using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="ActionMenu"/>, <see cref="ActionMenuItem"/>,
/// and their integration with <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
[TestClass]
public class ActionMenuTests
{
    private static IEditorSession CreateSession(string text = "test")
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        return (IEditorSession)node;
    }

    // ── ActionMenu construction ──────────────────────────────

    [TestMethod]
    public void ActionMenu_WithItems_StoresAnchorAndItems()
    {
        var anchor = new DocumentPosition(5, 10);
        var menu = new ActionMenu(anchor, [
            new ActionMenuItem("Extract Method", "extract-method"),
            new ActionMenuItem("Inline Variable", "inline-var")
        ]);

        Assert.AreEqual(anchor, menu.Anchor);
        Assert.AreEqual(2, menu.Items.Count);
        Assert.AreEqual("Extract Method", menu.Items[0].Label);
        Assert.AreEqual("extract-method", menu.Items[0].Id);
        Assert.AreEqual("Inline Variable", menu.Items[1].Label);
        Assert.AreEqual("inline-var", menu.Items[1].Id);
    }

    [TestMethod]
    public void ActionMenu_WithTitle_StoresTitle()
    {
        var menu = new ActionMenu(new DocumentPosition(1, 1), [
            new ActionMenuItem("Fix", "fix-1")
        ]) { Title = "Quick Fixes" };

        Assert.AreEqual("Quick Fixes", menu.Title);
    }

    [TestMethod]
    public void ActionMenu_WithoutTitle_IsNull()
    {
        var menu = new ActionMenu(new DocumentPosition(1, 1), [
            new ActionMenuItem("Fix", "fix-1")
        ]);

        Assert.IsNull(menu.Title);
    }

    [TestMethod]
    public void ActionMenu_EmptyItems_IsValid()
    {
        var menu = new ActionMenu(new DocumentPosition(1, 1), []);

        Assert.IsEmpty(menu.Items);
    }

    // ── ActionMenuItem construction ──────────────────────────

    [TestMethod]
    public void ActionMenuItem_WithDetail_StoresValue()
    {
        var item = new ActionMenuItem("Extract Method", "extract-method")
        {
            Detail = "Extract selected code into a new method"
        };

        Assert.AreEqual("Extract selected code into a new method", item.Detail);
    }

    [TestMethod]
    public void ActionMenuItem_WithoutDetail_IsNull()
    {
        var item = new ActionMenuItem("Rename", "rename");

        Assert.IsNull(item.Detail);
    }

    [TestMethod]
    public void ActionMenuItem_WithIsPreferred_StoresTrue()
    {
        var item = new ActionMenuItem("Fix All", "fix-all") { IsPreferred = true };

        Assert.IsTrue(item.IsPreferred);
    }

    [TestMethod]
    public void ActionMenuItem_DefaultIsPreferred_IsFalse()
    {
        var item = new ActionMenuItem("Fix All", "fix-all");

        Assert.IsFalse(item.IsPreferred);
    }

    [TestMethod]
    public void ActionMenuItem_FullyPopulated_PreservesAllProperties()
    {
        var item = new ActionMenuItem("Add Import", "add-import")
        {
            Detail = "using System.Linq;",
            IsPreferred = true
        };

        Assert.AreEqual("Add Import", item.Label);
        Assert.AreEqual("add-import", item.Id);
        Assert.AreEqual("using System.Linq;", item.Detail);
        Assert.IsTrue(item.IsPreferred);
    }

    // ── IEditorSession integration ───────────────────────────

    [TestMethod]
    public async Task ShowActionMenuAsync_ReturnsNull_CurrentStubBehavior()
    {
        var session = CreateSession();
        var menu = new ActionMenu(new DocumentPosition(1, 1), [
            new ActionMenuItem("Action A", "a"),
            new ActionMenuItem("Action B", "b")
        ]);

        var result = await session.ShowActionMenuAsync(menu);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task ShowActionMenuAsync_WithTitle_ReturnsNull()
    {
        var session = CreateSession();
        var menu = new ActionMenu(new DocumentPosition(3, 5), [
            new ActionMenuItem("Fix", "fix-1")
        ]) { Title = "Code Actions" };

        var result = await session.ShowActionMenuAsync(menu);

        Assert.IsNull(result);
    }

    // ── Record equality ──────────────────────────────────────

    [TestMethod]
    public void ActionMenu_RecordEquality_EqualWhenSameListReference()
    {
        var items = new List<ActionMenuItem> { new("Fix", "fix-1") };

        var a = new ActionMenu(new DocumentPosition(1, 1), items) { Title = "Actions" };
        var b = new ActionMenu(new DocumentPosition(1, 1), items) { Title = "Actions" };

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void ActionMenu_RecordEquality_NotEqualWhenDifferentAnchor()
    {
        var items = new List<ActionMenuItem> { new("Fix", "fix-1") };

        var a = new ActionMenu(new DocumentPosition(1, 1), items);
        var b = new ActionMenu(new DocumentPosition(2, 3), items);

        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void ActionMenuItem_RecordEquality_EqualWhenSameValues()
    {
        var a = new ActionMenuItem("Rename", "rename") { Detail = "Rename symbol", IsPreferred = true };
        var b = new ActionMenuItem("Rename", "rename") { Detail = "Rename symbol", IsPreferred = true };

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void ActionMenuItem_RecordEquality_NotEqualWhenDifferentId()
    {
        var a = new ActionMenuItem("Fix", "fix-1");
        var b = new ActionMenuItem("Fix", "fix-2");

        Assert.AreNotEqual(a, b);
    }
}
