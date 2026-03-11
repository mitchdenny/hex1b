using System.Collections.Immutable;
using System.Text;
using Hex1b.Widgets;

namespace Hex1b.Markdown;

/// <summary>
/// Renders a <see cref="MarkdownDocument"/> AST into a Hex1b widget tree.
/// Resolves the handler middleware chain for each block type, falling through
/// to built-in default renderers.
/// </summary>
internal static class MarkdownWidgetRenderer
{
    private static readonly WidgetContext<MarkdownWidget> s_ctx = new();

    /// <summary>
    /// Render a parsed markdown document into a widget tree.
    /// </summary>
    public static Hex1bWidget Render(
        MarkdownDocument document,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers)
    {
        var widgets = new List<Hex1bWidget>();

        foreach (var block in document.Blocks)
        {
            var widget = RenderBlock(block, blockHandlers);
            widgets.Add(widget);
        }

        if (widgets.Count == 0)
            return new TextBlockWidget("");

        return new VStackWidget(widgets);
    }

    /// <summary>
    /// Render a single block by resolving the handler chain.
    /// </summary>
    internal static Hex1bWidget RenderBlock(
        MarkdownBlock block,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers)
    {
        var blockType = block.GetType();

        // Collect handlers for this block type (in registration order)
        var matchingHandlers = new List<Delegate>();
        foreach (var (type, handler) in blockHandlers)
        {
            if (type.IsAssignableFrom(blockType))
                matchingHandlers.Add(handler);
        }

        // Build the chain: last registered = first called
        // The innermost handler is the built-in default
        Func<MarkdownBlock, Hex1bWidget> currentDefault = b => RenderBlockDefault(b, blockHandlers);

        // Wrap from first-registered to last-registered so last is outermost
        foreach (var handler in matchingHandlers)
        {
            var captured = currentDefault;
            var capturedHandler = handler;
            currentDefault = b =>
            {
                var ctx = new MarkdownBlockContext(captured, s_ctx);
                // Invoke the typed handler via dynamic dispatch
                return (Hex1bWidget)capturedHandler.DynamicInvoke(ctx, b)!;
            };
        }

        return currentDefault(block);
    }

    /// <summary>
    /// Built-in default renderers for all block types.
    /// </summary>
    private static Hex1bWidget RenderBlockDefault(
        MarkdownBlock block,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            FencedCodeBlock fencedCode => RenderFencedCode(fencedCode),
            IndentedCodeBlock indentedCode => RenderIndentedCode(indentedCode),
            BlockQuoteBlock blockQuote => RenderBlockQuote(blockQuote, blockHandlers),
            ListBlock list => RenderList(list, blockHandlers),
            ThematicBreakBlock => RenderThematicBreak(),
            _ => new TextBlockWidget(block.ToString() ?? "")
        };
    }

    private static Hex1bWidget RenderHeading(HeadingBlock heading)
    {
        // Phase 1: render as plain text with heading prefix marker
        var prefix = heading.Level switch
        {
            1 => "▌ ",
            2 => "▎ ",
            _ => ""
        };

        var text = $"{prefix}{heading.Text}";
        var widget = new TextBlockWidget(text) { Overflow = TextOverflow.Wrap };

        // Apply bold for h1/h2 using ANSI (theme colors applied in Phase 2)
        if (heading.Level <= 2)
        {
            text = $"\x1b[1m{prefix}{heading.Text}\x1b[22m";
            widget = new TextBlockWidget(text) { Overflow = TextOverflow.Wrap };
        }

        return widget;
    }

    private static Hex1bWidget RenderParagraph(ParagraphBlock paragraph)
    {
        var text = RenderInlinesToPlainText(paragraph.Inlines);
        return new TextBlockWidget(text) { Overflow = TextOverflow.Wrap };
    }

    private static Hex1bWidget RenderFencedCode(FencedCodeBlock code)
    {
        var content = new TextBlockWidget(code.Content);
        var border = new BorderWidget(content);
        return string.IsNullOrEmpty(code.Language) ? border : border.Title(code.Language);
    }

    private static Hex1bWidget RenderIndentedCode(IndentedCodeBlock code)
    {
        var content = new TextBlockWidget(code.Content);
        return new PaddingWidget(4, 0, 0, 0, content);
    }

    private static Hex1bWidget RenderBlockQuote(
        BlockQuoteBlock blockQuote,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers)
    {
        var children = new List<Hex1bWidget>();
        foreach (var child in blockQuote.Children)
        {
            children.Add(RenderBlock(child, blockHandlers));
        }

        var innerContent = children.Count == 1
            ? children[0]
            : new VStackWidget(children);

        // Render as: "│ " + content
        return new HStackWidget([
            new TextBlockWidget("│ "),
            innerContent
        ]);
    }

    private static Hex1bWidget RenderList(
        ListBlock list,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers)
    {
        var items = new List<Hex1bWidget>();
        for (int i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            var marker = list.IsOrdered
                ? $"{list.StartNumber + i}. "
                : "• ";

            var itemChildren = new List<Hex1bWidget>();
            foreach (var child in item.Children)
            {
                itemChildren.Add(RenderBlock(child, blockHandlers));
            }

            var content = itemChildren.Count == 1
                ? itemChildren[0]
                : new VStackWidget(itemChildren);

            items.Add(new HStackWidget([
                new TextBlockWidget(marker),
                content
            ]));
        }

        return new VStackWidget(items);
    }

    private static Hex1bWidget RenderThematicBreak()
    {
        return new SeparatorWidget();
    }

    /// <summary>
    /// Phase 1: render inlines as plain text (emphasis/code markers stripped).
    /// Phase 2 will add ANSI styling.
    /// </summary>
    internal static string RenderInlinesToPlainText(IReadOnlyList<MarkdownInline> inlines)
    {
        var sb = new StringBuilder();
        RenderInlinesCore(inlines, sb);
        return sb.ToString();
    }

    private static void RenderInlinesCore(IReadOnlyList<MarkdownInline> inlines, StringBuilder sb)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    sb.Append(text.Text);
                    break;
                case EmphasisInline emphasis:
                    RenderInlinesCore(emphasis.Children, sb);
                    break;
                case CodeInline code:
                    sb.Append(code.Code);
                    break;
                case LinkInline link:
                    sb.Append(link.Text);
                    break;
                case ImageInline image:
                    sb.Append($"[{image.AltText}]");
                    break;
                case LineBreakInline lineBreak:
                    sb.Append(lineBreak.IsHard ? '\n' : ' ');
                    break;
            }
        }
    }
}
