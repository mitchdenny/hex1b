using Hex1b.Layout;
using Hex1b.Markdown;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class MarkdownNodeTests
{
    // --- Measure ---

    [Fact]
    public void Measure_EmptySource_ReturnsZeroSize()
    {
        var node = new MarkdownNode { Source = "" };
        node.BuildWidgetTree(); // Trigger parse
        var size = node.Measure(new Constraints(0, 80, 0, 24));
        Assert.Equal(0, size.Height);
    }

    // --- Default Renderers ---

    [Fact]
    public void BuildWidgetTree_Heading_ProducesVStackWithTextBlock()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        Assert.Single(vstack.Children);
    }

    [Fact]
    public void BuildWidgetTree_Paragraph_ProducesMarkdownTextBlock()
    {
        var node = new MarkdownNode { Source = "Hello world" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        var mdTextBlock = Assert.IsType<MarkdownTextBlockWidget>(Assert.Single(vstack.Children));
        // Verify the inlines contain the text
        var textInline = Assert.IsType<TextInline>(Assert.Single(mdTextBlock.Inlines));
        Assert.Equal("Hello world", textInline.Text);
    }

    [Fact]
    public void BuildWidgetTree_FencedCode_ProducesBorder()
    {
        var node = new MarkdownNode { Source = "```\ncode\n```" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        Assert.IsType<BorderWidget>(Assert.Single(vstack.Children));
    }

    [Fact]
    public void BuildWidgetTree_BlockQuote_ProducesMarkdownTextBlock()
    {
        var node = new MarkdownNode { Source = "> Quote text" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        Assert.IsType<MarkdownTextBlockWidget>(Assert.Single(vstack.Children));
    }

    [Fact]
    public void BuildWidgetTree_UnorderedList_ProducesNestedVStack()
    {
        var node = new MarkdownNode { Source = "- Item 1\n- Item 2" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        var listStack = Assert.IsType<VStackWidget>(Assert.Single(vstack.Children));
        Assert.Equal(2, listStack.Children.Count);
    }

    [Fact]
    public void BuildWidgetTree_ThematicBreak_ProducesSeparator()
    {
        var node = new MarkdownNode { Source = "---" };
        var widget = node.BuildWidgetTree();

        var vstack = Assert.IsType<VStackWidget>(widget);
        Assert.IsType<SeparatorWidget>(Assert.Single(vstack.Children));
    }

    [Fact]
    public void BuildWidgetTree_EmptySource_ProducesEmptyTextBlock()
    {
        var node = new MarkdownNode { Source = "" };
        var widget = node.BuildWidgetTree();

        Assert.IsType<TextBlockWidget>(widget);
    }

    // --- Caching ---

    [Fact]
    public void BuildWidgetTree_SameSource_DoesNotReparse()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget1 = node.BuildWidgetTree();
        var widget2 = node.BuildWidgetTree();

        // Both should produce structurally equivalent widgets
        Assert.IsType<VStackWidget>(widget1);
        Assert.IsType<VStackWidget>(widget2);
    }

    [Fact]
    public void BuildWidgetTree_ChangedSource_Reparses()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        var widget1 = node.BuildWidgetTree();
        Assert.IsType<VStackWidget>(widget1);

        node.Source = "New paragraph";
        var widget2 = node.BuildWidgetTree();
        var vstack = Assert.IsType<VStackWidget>(widget2);
        var mdTextBlock = Assert.IsType<MarkdownTextBlockWidget>(Assert.Single(vstack.Children));
        var textInline = Assert.IsType<TextInline>(Assert.Single(mdTextBlock.Inlines));
        Assert.Equal("New paragraph", textInline.Text);
    }

    // --- OnBlock Handler ---

    [Fact]
    public void BuildWidgetTree_WithOnBlockHandler_OverridesDefault()
    {
        var node = new MarkdownNode { Source = "# Hello" };
        node.BlockHandlers = node.BlockHandlers.Add(
            (typeof(HeadingBlock),
             new Func<MarkdownBlockContext, HeadingBlock, Hex1bWidget>(
                 (ctx, block) => new TextBlockWidget($"CUSTOM: {block.Text}"))));

        var widget = node.BuildWidgetTree();
        var vstack = Assert.IsType<VStackWidget>(widget);
        var text = Assert.IsType<TextBlockWidget>(Assert.Single(vstack.Children));
        Assert.Equal("CUSTOM: Hello", text.Text);
    }

    [Fact]
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
        var outerVStack = Assert.IsType<VStackWidget>(widget);
        var wrappedVStack = Assert.IsType<VStackWidget>(Assert.Single(outerVStack.Children));
        Assert.Equal(3, wrappedVStack.Children.Count);

        var before = Assert.IsType<TextBlockWidget>(wrappedVStack.Children[0]);
        Assert.Equal("BEFORE", before.Text);

        var after = Assert.IsType<TextBlockWidget>(wrappedVStack.Children[2]);
        Assert.Equal("AFTER", after.Text);
    }

    [Fact]
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

        Assert.Equal(["B", "A"], callOrder);
    }

    // --- Widget Record ---

    [Fact]
    public void MarkdownWidget_OnBlock_ReturnsNewInstance()
    {
        var widget = new MarkdownWidget("# Hello");
        var modified = widget.OnBlock<HeadingBlock>((ctx, block) =>
            new TextBlockWidget("custom"));

        Assert.NotSame(widget, modified);
        Assert.Empty(widget.BlockHandlers);
        Assert.Single(modified.BlockHandlers);
    }

    [Fact]
    public void MarkdownWidget_OnBlock_ChainMultipleTypes()
    {
        var widget = new MarkdownWidget("# Hello")
            .OnBlock<HeadingBlock>((ctx, block) => new TextBlockWidget("h"))
            .OnBlock<ParagraphBlock>((ctx, block) => new TextBlockWidget("p"));

        Assert.Equal(2, widget.BlockHandlers.Count);
    }

    // --- Complex Documents ---

    [Fact]
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

        var vstack = Assert.IsType<VStackWidget>(widget);
        Assert.True(vstack.Children.Count >= 6, $"Expected ≥6 children, got {vstack.Children.Count}");
    }
}
