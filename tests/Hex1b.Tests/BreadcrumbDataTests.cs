using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for <see cref="BreadcrumbData"/>, <see cref="BreadcrumbSymbol"/>,
/// and their integration with <see cref="IEditorSession"/> on <see cref="EditorNode"/>.
/// </summary>
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

    [Fact]
    public void SetBreadcrumbs_WithData_StoresAndReturnsBreadcrumbs()
    {
        var session = CreateSession();
        var data = new BreadcrumbData([
            new BreadcrumbSymbol("Program", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(10, 1))
        ]);

        session.SetBreadcrumbs(data);

        Assert.NotNull(session.Breadcrumbs);
        Assert.Single(session.Breadcrumbs!.Symbols);
        Assert.Equal("Program", session.Breadcrumbs.Symbols[0].Name);
    }

    [Fact]
    public void SetBreadcrumbs_Null_ClearsBreadcrumbs()
    {
        var session = CreateSession();
        session.SetBreadcrumbs(new BreadcrumbData([
            new BreadcrumbSymbol("Temp", BreadcrumbSymbolKind.Class,
                new DocumentPosition(1, 1), new DocumentPosition(5, 1))
        ]));

        session.SetBreadcrumbs(null);

        Assert.Null(session.Breadcrumbs);
    }

    [Fact]
    public void Breadcrumbs_Default_ReturnsNull()
    {
        var session = CreateSession();

        Assert.Null(session.Breadcrumbs);
    }

    [Fact]
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

        Assert.NotNull(session.Breadcrumbs);
        Assert.Single(session.Breadcrumbs!.Symbols);
        Assert.Equal("Second", session.Breadcrumbs.Symbols[0].Name);
    }

    // ── BreadcrumbSymbol construction ────────────────────────

    [Fact]
    public void BreadcrumbSymbol_WithChildren_CreatesHierarchy()
    {
        var children = new List<BreadcrumbSymbol>
        {
            new("InnerMethod", BreadcrumbSymbolKind.Method,
                new DocumentPosition(3, 5), new DocumentPosition(8, 5))
        };

        var parent = new BreadcrumbSymbol("OuterClass", BreadcrumbSymbolKind.Class,
            new DocumentPosition(1, 1), new DocumentPosition(10, 1), children);

        Assert.NotNull(parent.Children);
        Assert.Single(parent.Children!);
        Assert.Equal("InnerMethod", parent.Children[0].Name);
        Assert.Equal(BreadcrumbSymbolKind.Method, parent.Children[0].Kind);
    }

    [Fact]
    public void BreadcrumbSymbol_WithoutChildren_HasNullChildren()
    {
        var symbol = new BreadcrumbSymbol("Standalone", BreadcrumbSymbolKind.Function,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.Null(symbol.Children);
    }

    [Fact]
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

        Assert.Equal("MyService", cls.Name);
        Assert.Single(cls.Children!);
        Assert.Equal("Execute", cls.Children![0].Name);
        Assert.Single(cls.Children[0].Children!);
        Assert.Equal("if-block", cls.Children[0].Children![0].Name);
    }

    // ── BreadcrumbSymbolKind ─────────────────────────────────

    [Theory]
    [InlineData(BreadcrumbSymbolKind.File)]
    [InlineData(BreadcrumbSymbolKind.Module)]
    [InlineData(BreadcrumbSymbolKind.Namespace)]
    [InlineData(BreadcrumbSymbolKind.Package)]
    [InlineData(BreadcrumbSymbolKind.Class)]
    [InlineData(BreadcrumbSymbolKind.Method)]
    [InlineData(BreadcrumbSymbolKind.Property)]
    [InlineData(BreadcrumbSymbolKind.Field)]
    [InlineData(BreadcrumbSymbolKind.Constructor)]
    [InlineData(BreadcrumbSymbolKind.Enum)]
    [InlineData(BreadcrumbSymbolKind.Interface)]
    [InlineData(BreadcrumbSymbolKind.Function)]
    [InlineData(BreadcrumbSymbolKind.Variable)]
    [InlineData(BreadcrumbSymbolKind.Constant)]
    [InlineData(BreadcrumbSymbolKind.String)]
    [InlineData(BreadcrumbSymbolKind.Number)]
    [InlineData(BreadcrumbSymbolKind.Boolean)]
    [InlineData(BreadcrumbSymbolKind.Array)]
    [InlineData(BreadcrumbSymbolKind.Object)]
    [InlineData(BreadcrumbSymbolKind.Key)]
    [InlineData(BreadcrumbSymbolKind.Null)]
    [InlineData(BreadcrumbSymbolKind.EnumMember)]
    [InlineData(BreadcrumbSymbolKind.Struct)]
    [InlineData(BreadcrumbSymbolKind.Event)]
    [InlineData(BreadcrumbSymbolKind.Operator)]
    [InlineData(BreadcrumbSymbolKind.TypeParameter)]
    public void BreadcrumbSymbolKind_AllValues_AreValidForSymbol(BreadcrumbSymbolKind kind)
    {
        var symbol = new BreadcrumbSymbol("test", kind,
            new DocumentPosition(1, 1), new DocumentPosition(1, 5));

        Assert.Equal(kind, symbol.Kind);
    }

    // ── BreadcrumbData construction ──────────────────────────

    [Fact]
    public void BreadcrumbData_EmptySymbolList_IsValid()
    {
        var data = new BreadcrumbData([]);

        Assert.NotNull(data.Symbols);
        Assert.Empty(data.Symbols);
    }

    [Fact]
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

        Assert.Equal(3, data.Symbols.Count);
        Assert.Equal("ClassA", data.Symbols[0].Name);
        Assert.Equal("ClassB", data.Symbols[1].Name);
        Assert.Equal("EnumC", data.Symbols[2].Name);
    }

    // ── Record equality ──────────────────────────────────────

    [Fact]
    public void BreadcrumbSymbol_RecordEquality_EqualWhenSameValues()
    {
        var a = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));
        var b = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.Equal(a, b);
    }

    [Fact]
    public void BreadcrumbSymbol_RecordEquality_NotEqualWhenDifferentKind()
    {
        var a = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Method,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));
        var b = new BreadcrumbSymbol("Foo", BreadcrumbSymbolKind.Property,
            new DocumentPosition(1, 1), new DocumentPosition(5, 1));

        Assert.NotEqual(a, b);
    }
}
