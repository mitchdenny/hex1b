using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="BreadcrumbData"/>, <see cref="BreadcrumbSymbol"/>,
/// and their integration with <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
[TestClass]
public class BreadcrumbDataTests
{
    private static IEditorSession CreateSession(string text = "test")
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state };
        return (IEditorSession)node;
    }

    // ── IEditorSession integration ───────────────────────────

    [TestMethod]
    public void SetBreadcrumbs_WithData_StoresAndReturnsBreadcrumbs()
    {
        var session = CreateSession();
        var data = new BreadcrumbData([
            new BreadcrumbSymbol("Program", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(10, 1))
        ]);

        session.SetBreadcrumbs(data);

        Assert.IsNotNull(session.Breadcrumbs);
        TestSeq.Single(session.Breadcrumbs!.Symbols);
        Assert.AreEqual("Program", session.Breadcrumbs.Symbols[0].Name);
    }

    [TestMethod]
    public void SetBreadcrumbs_Null_ClearsBreadcrumbs()
    {
        var session = CreateSession();
        session.SetBreadcrumbs(new BreadcrumbData([
            new BreadcrumbSymbol("Temp", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(5, 1))
        ]));

        session.SetBreadcrumbs(null);

        Assert.IsNull(session.Breadcrumbs);
    }

    [TestMethod]
    public void Breadcrumbs_Default_ReturnsNull()
    {
        var session = CreateSession();

        Assert.IsNull(session.Breadcrumbs);
    }

    [TestMethod]
    public void SetBreadcrumbs_CalledTwice_ReplacesData()
    {
        var session = CreateSession();
        var first = new BreadcrumbData([
            new BreadcrumbSymbol("First", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(5, 1))
        ]);
        var second = new BreadcrumbData([
            new BreadcrumbSymbol("Second", BreadcrumbSymbolKind.Method,
                new DocumentPosition(2, 1), new DocumentPosition(4, 1))
        ]);

        session.SetBreadcrumbs(first);
        session.SetBreadcrumbs(second);

        Assert.IsNotNull(session.Breadcrumbs);
        TestSeq.Single(session.Breadcrumbs!.Symbols);
        Assert.AreEqual("Second", session.Breadcrumbs.Symbols[0].Name);
    }

    // ── BreadcrumbSymbol construction ────────────────────────

    [TestMethod]
    public void BreadcrumbSymbol_WithChildren_CreatesHierarchy()
    {
        var children = new List<BreadcrumbSymbol>
        {
            new("InnerMethod", BreadcrumbSymbolKind.Method,
                new DocumentPosition(3, 5), new DocumentPosition(8, 5))
        };

        var parent = new BreadcrumbSymbol("OuterClass", BreadcrumbSymbolKind.Class,
            new DocumentPosition(1, 1), new DocumentPosition(10, 1), children);

        Assert.IsNotNull(parent.Children);
        TestSeq.Single(parent.Children!);
        Assert.AreEqual("InnerMethod", parent.Children[0].Name);
        Assert.AreEqual(BreadcrumbSymbolKind.Method, parent.Children[0].Kind);
    }

    [TestMethod]
    public void BreadcrumbSymbol_WithoutChildren_HasNullChildren()
    {
        var symbol = new BreadcrumbSymbol("Standalone", BreadcrumbSymbolKind.Function,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.IsNull(symbol.Children);
    }

    [TestMethod]
    public void BreadcrumbSymbol_NestedHierarchy_ClassMethodBlock()
    {
        var block = new BreadcrumbSymbol("if-block", BreadcrumbSymbolKind.Object,
            new DocumentPosition(5, 9), new DocumentPosition(7, 9));

        var method = new BreadcrumbSymbol("Execute", BreadcrumbSymbolKind.Method,
            new DocumentPosition(3, 5), new DocumentPosition(9, 5),
            [block]);

        var cls = new BreadcrumbSymbol("MyService", BreadcrumbSymbolKind.Class,
            new DocumentPosition(1, 1), new DocumentPosition(11, 1),
            [method]);

        Assert.AreEqual("MyService", cls.Name);
        TestSeq.Single(cls.Children!);
        Assert.AreEqual("Execute", cls.Children![0].Name);
        TestSeq.Single(cls.Children[0].Children!);
        Assert.AreEqual("if-block", cls.Children[0].Children![0].Name);
    }

    // ── BreadcrumbSymbolKind ─────────────────────────────────

    [TestMethod]
    [DataRow(BreadcrumbSymbolKind.File)]
    [DataRow(BreadcrumbSymbolKind.Module)]
    [DataRow(BreadcrumbSymbolKind.Namespace)]
    [DataRow(BreadcrumbSymbolKind.Package)]
    [DataRow(BreadcrumbSymbolKind.Class)]
    [DataRow(BreadcrumbSymbolKind.Method)]
    [DataRow(BreadcrumbSymbolKind.Property)]
    [DataRow(BreadcrumbSymbolKind.Field)]
    [DataRow(BreadcrumbSymbolKind.Constructor)]
    [DataRow(BreadcrumbSymbolKind.Enum)]
    [DataRow(BreadcrumbSymbolKind.Interface)]
    [DataRow(BreadcrumbSymbolKind.Function)]
    [DataRow(BreadcrumbSymbolKind.Variable)]
    [DataRow(BreadcrumbSymbolKind.Constant)]
    [DataRow(BreadcrumbSymbolKind.String)]
    [DataRow(BreadcrumbSymbolKind.Number)]
    [DataRow(BreadcrumbSymbolKind.Boolean)]
    [DataRow(BreadcrumbSymbolKind.Array)]
    [DataRow(BreadcrumbSymbolKind.Object)]
    [DataRow(BreadcrumbSymbolKind.Key)]
    [DataRow(BreadcrumbSymbolKind.Null)]
    [DataRow(BreadcrumbSymbolKind.EnumMember)]
    [DataRow(BreadcrumbSymbolKind.Struct)]
    [DataRow(BreadcrumbSymbolKind.Event)]
    [DataRow(BreadcrumbSymbolKind.Operator)]
    [DataRow(BreadcrumbSymbolKind.TypeParameter)]
    public void BreadcrumbSymbolKind_AllValues_AreValidForSymbol(BreadcrumbSymbolKind kind)
    {
        var symbol = new BreadcrumbSymbol("test", kind,
            new DocumentPosition(1, 1), new DocumentPosition(1, 5));

        Assert.AreEqual(kind, symbol.Kind);
    }

    // ── BreadcrumbData construction ──────────────────────────

    [TestMethod]
    public void BreadcrumbData_EmptySymbolList_IsValid()
    {
        var data = new BreadcrumbData([]);

        Assert.IsNotNull(data.Symbols);
        Assert.IsEmpty(data.Symbols);
    }

    [TestMethod]
    public void BreadcrumbData_MultipleTopLevelSymbols_PreservesAll()
    {
        var data = new BreadcrumbData([
            new BreadcrumbSymbol("ClassA", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(10, 1)),
            new BreadcrumbSymbol("ClassB", BreadcrumbSymbolKind.Class,
                new DocumentPosition(12, 1), new DocumentPosition(20, 1)),
            new BreadcrumbSymbol("EnumC", BreadcrumbSymbolKind.Enum,
                new DocumentPosition(22, 1), new DocumentPosition(25, 1))
        ]);

        Assert.AreEqual(3, data.Symbols.Count);
        Assert.AreEqual("ClassA", data.Symbols[0].Name);
        Assert.AreEqual("ClassB", data.Symbols[1].Name);
        Assert.AreEqual("EnumC", data.Symbols[2].Name);
    }

    // ── Record equality ──────────────────────────────────────

    [TestMethod]
    public void BreadcrumbSymbol_RecordEquality_EqualWhenSameValues()
    {
        var a = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));
        var b = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void BreadcrumbSymbol_RecordEquality_NotEqualWhenDifferentKind()
    {
        var a = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));
        var b = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Property,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.AreNotEqual(a, b);
    }
}
