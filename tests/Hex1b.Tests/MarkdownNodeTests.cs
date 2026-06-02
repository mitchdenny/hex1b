using Hex1b.Layout;
using Hex1b.Markdown;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class MarkdownNodeTests
{
    // --- Measure ---

    [TestMethod]
    public void Measure_EmptySource_ReturnsZeroSize()
    {
        var node = new MarkdownNode { Source = "" };
        node.BuildWidgetTree(); // Trigger parse
        var size = node.Measure(new Constraints(0, 80, 0, 24));
        Assert.AreEqual(0, size.Height);
    }

    // --- Default Renderers ---

    [TestMethod]
    public void BuildWidgetTree_Heading_ProducesVStackWithTextBlock()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        TestSeq.Single(vstack.Children);
    }

    [TestMethod]
    public void BuildWidgetTree_Paragraph_ProducesMarkdownTextBlock()
    {
        var node = new MarkdownNode { Source = "Hello world" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        var mdTextBlock = TestSeq.IsType<MarkdownTextBlockWidget>(TestSeq.Single(vstack.Children));
        // Verify the inlines contain the text
        var textInline = TestSeq.IsType<TextInline>(TestSeq.Single(mdTextBlock.Inlines));
        Assert.AreEqual("Hello world", textInline.Text);
    }

    [TestMethod]
    public void BuildWidgetTree_FencedCode_ProducesBorder()
    {
        var node = new MarkdownNode { Source = "```\ncode\n```" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        TestSeq.IsType<BorderWidget>(TestSeq.Single(vstack.Children));
    }

    [TestMethod]
    public void BuildWidgetTree_BlockQuote_ProducesMarkdownTextBlock()
    {
        var node = new MarkdownNode { Source = "> Quote text" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        TestSeq.IsType<MarkdownTextBlockWidget>(TestSeq.Single(vstack.Children));
    }

    [TestMethod]
    public void BuildWidgetTree_UnorderedList_ProducesNestedVStack()
    {
        var node = new MarkdownNode { Source = "- Item 1\n- Item 2" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        var listStack = TestSeq.IsType<VStackWidget>(TestSeq.Single(vstack.Children));
        Assert.AreEqual(2, listStack.Children.Count);
    }

    [TestMethod]
    public void BuildWidgetTree_ThematicBreak_ProducesSeparator()
    {
        var node = new MarkdownNode { Source = "---" };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        TestSeq.IsType<SeparatorWidget>(TestSeq.Single(vstack.Children));
    }

    [TestMethod]
    public void BuildWidgetTree_EmptySource_ProducesEmptyTextBlock()
    {
        var node = new MarkdownNode { Source = "" };
        var widget = node.BuildWidgetTree();

        TestSeq.IsType<TextBlockWidget>(widget);
    }

    // --- Caching ---

    [TestMethod]
    public void BuildWidgetTree_SameSource_DoesNotReparse()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget1 = node.BuildWidgetTree();
        var widget2 = node.BuildWidgetTree();

        // Both should produce structurally equivalent widgets
        TestSeq.IsType<VStackWidget>(widget1);
        TestSeq.IsType<VStackWidget>(widget2);
    }

    [TestMethod]
    public void BuildWidgetTree_ChangedSource_Reparses()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget1 = node.BuildWidgetTree();
        TestSeq.IsType<VStackWidget>(widget1);

        node.Source = "New paragraph";
        var widget2 = node.BuildWidgetTree();
        var vstack = TestSeq.IsType<VStackWidget>(widget2);
        var mdTextBlock = TestSeq.IsType<MarkdownTextBlockWidget>(TestSeq.Single(vstack.Children));
        var textInline = TestSeq.IsType<TextInline>(TestSeq.Single(mdTextBlock.Inlines));
        Assert.AreEqual("New paragraph", textInline.Text);
    }

    // --- OnBlock Handler ---

    [TestMethod]
    public void BuildWidgetTree_WithOnBlockHandler_OverridesDefault()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        node.BlockHandlers = node.BlockHandlers.Add(
            (typeof(HeadingBlock),
             new Func<MarkdownBlockContext, HeadingBlock, Hex1bWidget>(
                 (ctx, block) => new TextBlockWidget($"CUSTOM: {block.Text}"))));

        var widget = node.BuildWidgetTree();
        var vstack = TestSeq.IsType<VStackWidget>(widget);
        var text = TestSeq.IsType<TextBlockWidget>(TestSeq.Single(vstack.Children));
        Assert.AreEqual("CUSTOM: Hello", text.Text);
    }

    [TestMethod]
    public void BuildWidgetTree_WithOnBlockHandler_DefaultChaining()
    {
        var node = new MarkdownNode { Source = "# Hello" };

        // Register a handler that wraps the default
        node.BlockHandlers = node.BlockHandlers.Add(
            (typeof(HeadingBlock),
             new Func<MarkdownBlockContext, HeadingBlock, Hex1bWidget>(
                 (ctx, block) =>
                 {
                     var defaultWidget = ctx.Default(block);
                     return new VStackWidget([
                         new TextBlockWidget("BEFORE"),
                         defaultWidget,
                         new TextBlockWidget("AFTER")
                     ]);
                 })));

        var widget = node.BuildWidgetTree();
        var outerVStack = TestSeq.IsType<VStackWidget>(widget);
        var wrappedVStack = TestSeq.IsType<VStackWidget>(TestSeq.Single(outerVStack.Children));
        Assert.AreEqual(3, wrappedVStack.Children.Count);

        var before = TestSeq.IsType<TextBlockWidget>(wrappedVStack.Children[0]);
        Assert.AreEqual("BEFORE", before.Text);

        var after = TestSeq.IsType<TextBlockWidget>(wrappedVStack.Children[2]);
        Assert.AreEqual("AFTER", after.Text);
    }

    [TestMethod]
    public void BuildWidgetTree_MultipleHandlers_LastRegisteredCalledFirst()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var callOrder = new List<string>();

        // Handler A (registered first, called second via Default)
        node.BlockHandlers = node.BlockHandlers.Add(
            (typeof(HeadingBlock),
             new Func<MarkdownBlockContext, HeadingBlock, Hex1bWidget>(
                 (ctx, block) =>
                 {
                     callOrder.Add("A");
                     return new TextBlockWidget("A");
                 })));

        // Handler B (registered last, called first)
        node.BlockHandlers = node.BlockHandlers.Add(
            (typeof(HeadingBlock),
             new Func<MarkdownBlockContext, HeadingBlock, Hex1bWidget>(
                 (ctx, block) =>
                 {
                     callOrder.Add("B");
                     return ctx.Default(block); // chains to A
                 })));

        node.BuildWidgetTree();

        TestSeq.AreEqual(["B", "A"], callOrder);
    }

    // --- Widget Record ---

    [TestMethod]
    public void MarkdownWidget_OnBlock_ReturnsNewInstance()
    {
        var widget = new MarkdownWidget("# Hello");
        var modified = widget.OnBlock<HeadingBlock>((ctx, block) =>
            new TextBlockWidget("custom"));

        Assert.AreNotSame(widget, modified);
        Assert.IsEmpty(widget.BlockHandlers);
        TestSeq.Single(modified.BlockHandlers);
    }

    [TestMethod]
    public void MarkdownWidget_OnBlock_ChainMultipleTypes()
    {
        var widget = new MarkdownWidget("# Hello")
            .OnBlock<HeadingBlock>((ctx, block) => new TextBlockWidget("h"))
            .OnBlock<ParagraphBlock>((ctx, block) => new TextBlockWidget("p"));

        Assert.AreEqual(2, widget.BlockHandlers.Count);
    }

    // --- Complex Documents ---

    [TestMethod]
    public void BuildWidgetTree_ComplexDocument_ProducesCorrectStructure()
    {
        var source = """
            # Title

            A paragraph.

            ```
            code
            ```

            > Quote

            - Item 1
            - Item 2

            ---
            """;

        var node = new MarkdownNode { Source = source };
        var widget = node.BuildWidgetTree();

        var vstack = TestSeq.IsType<VStackWidget>(widget);
        Assert.IsTrue(vstack.Children.Count >= 6, $"Expected ≥6 children, got {vstack.Children.Count}");
    }
}
