using System.Collections.Immutable;
using System.Text;
using Hex1b.Events;
using Hex1b.Theming;
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
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers,
        bool focusableChildren = false,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler = null,
        MarkdownWidget? sourceWidget = null)
    {
        var widgets = new List<Hex1bWidget>();

        foreach (var block in document.Blocks)
        {
            var widget = RenderBlock(block, blockHandlers, focusableChildren, linkActivatedHandler, sourceWidget);
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
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers,
        bool focusableChildren = false,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler = null,
        MarkdownWidget? sourceWidget = null,
        int listDepth = 0)
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
        Func<MarkdownBlock, Hex1bWidget> currentDefault = b =>
            RenderBlockDefault(b, blockHandlers, focusableChildren, linkActivatedHandler, sourceWidget, listDepth);

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
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers,
        bool focusableChildren,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler,
        MarkdownWidget? sourceWidget,
        int listDepth = 0)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading, focusableChildren, linkActivatedHandler, sourceWidget),
            ParagraphBlock paragraph => RenderParagraph(paragraph, focusableChildren, linkActivatedHandler, sourceWidget),
            FencedCodeBlock fencedCode => RenderFencedCode(fencedCode),
            IndentedCodeBlock indentedCode => RenderIndentedCode(indentedCode),
            BlockQuoteBlock blockQuote => RenderBlockQuote(
                blockQuote, blockHandlers, focusableChildren, linkActivatedHandler, sourceWidget),
            ListBlock list => RenderList(
                list, blockHandlers, focusableChildren, linkActivatedHandler, sourceWidget, listDepth),
            ThematicBreakBlock => RenderThematicBreak(),
            _ => new TextBlockWidget(block.ToString() ?? "")
        };
    }

    private static Hex1bWidget RenderHeading(
        HeadingBlock heading,
        bool focusableChildren,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler,
        MarkdownWidget? sourceWidget)
    {
        // Prepend heading marker as a text inline
        var prefix = heading.Level switch
        {
            1 => "▌ ",
            2 => "▎ ",
            _ => ""
        };

        var inlines = new List<MarkdownInline>();
        if (prefix.Length > 0)
            inlines.Add(new TextInline(prefix));
        inlines.AddRange(heading.Inlines);

        // h1/h2 get bold base attributes
        var baseAttrs = heading.Level <= 2 ? CellAttributes.Bold : CellAttributes.None;

        // Heading color from theme defaults
        var headingFg = heading.Level switch
        {
            1 => Hex1bColor.FromRgb(100, 200, 255),
            2 => Hex1bColor.FromRgb(130, 210, 240),
            3 => Hex1bColor.FromRgb(160, 200, 220),
            4 => Hex1bColor.FromRgb(180, 190, 210),
            5 => Hex1bColor.FromRgb(180, 180, 200),
            6 => Hex1bColor.FromRgb(160, 160, 180),
            _ => (Hex1bColor?)null
        };

        return new MarkdownTextBlockWidget(inlines)
        {
            BaseAttributes = baseAttrs,
            BaseForeground = headingFg,
            FocusableLinks = focusableChildren,
            LinkActivatedHandler = linkActivatedHandler,
            SourceWidget = sourceWidget,
            AnchorId = GenerateSlug(heading.Text)
        };
    }

    /// <summary>
    /// Generates a GitHub-style heading slug from the given text.
    /// Lowercases, replaces spaces with hyphens, and strips non-alphanumeric
    /// characters (except hyphens).
    /// </summary>
    internal static string GenerateSlug(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
            else if (ch == ' ' || ch == '-')
                sb.Append('-');
            // else: strip
        }

        return sb.ToString();
    }

    private static Hex1bWidget RenderParagraph(
        ParagraphBlock paragraph,
        bool focusableChildren,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler,
        MarkdownWidget? sourceWidget)
    {
        return new MarkdownTextBlockWidget(paragraph.Inlines)
        {
            FocusableLinks = focusableChildren,
            LinkActivatedHandler = linkActivatedHandler,
            SourceWidget = sourceWidget
        };
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
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers,
        bool focusableChildren,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler,
        MarkdownWidget? sourceWidget)
    {
        const string prefix = "│ ";
        const int prefixWidth = 2;

        var blockWidgets = new List<Hex1bWidget>();
        foreach (var child in blockQuote.Children)
        {
            if (child is ParagraphBlock paragraph)
            {
                // Prepend "│ " to the paragraph inlines and use hanging indent
                // with continuation prefix so every line starts with "│ ".
                var prefixedInlines = new List<MarkdownInline>();
                prefixedInlines.Add(new TextInline(prefix));
                prefixedInlines.AddRange(paragraph.Inlines);

                blockWidgets.Add(new MarkdownTextBlockWidget(prefixedInlines)
                {
                    HangingIndent = prefixWidth,
                    ContinuationPrefix = prefix,
                    FocusableLinks = focusableChildren,
                    LinkActivatedHandler = linkActivatedHandler,
                    SourceWidget = sourceWidget
                });
            }
            else
            {
                // Non-paragraph block (code block, nested list, nested block quote, etc.)
                // — render normally and indent with "│ " prefix via padding.
                var childWidget = RenderBlock(child, blockHandlers, focusableChildren,
                    linkActivatedHandler, sourceWidget);
                blockWidgets.Add(new HStackWidget([
                    new TextBlockWidget(prefix),
                    childWidget
                ]));
            }
        }

        return blockWidgets.Count == 1
            ? blockWidgets[0]
            : new VStackWidget(blockWidgets);
    }

    private static readonly string[] UnorderedBullets = ["• ", "◦ ", "▪ "];

    private static Hex1bWidget RenderList(
        ListBlock list,
        ImmutableList<(Type BlockType, Delegate Handler)> blockHandlers,
        bool focusableChildren,
        Func<MarkdownLinkActivatedEventArgs, Task>? linkActivatedHandler,
        MarkdownWidget? sourceWidget,
        int listDepth = 0)
    {
        var items = new List<Hex1bWidget>();
        for (int i = 0; i < list.Items.Count; i++)
        {
            var item = list.Items[i];
            var marker = list.IsOrdered
                ? $"{list.StartNumber + i}. "
                : UnorderedBullets[listDepth % UnorderedBullets.Length];

            // Task list checkbox
            if (item.IsChecked is bool isChecked)
            {
                marker += isChecked ? "☑ " : "☐ ";
            }
            var markerWidth = DisplayWidth.GetStringWidth(marker);

            // For list items whose first child is a paragraph, prepend the marker
            // to the paragraph inlines and use hanging indent so continuation
            // lines align with the text, not the marker.
            if (item.Children.Count >= 1 && item.Children[0] is ParagraphBlock firstParagraph)
            {
                var prefixedInlines = new List<MarkdownInline>();
                prefixedInlines.Add(new TextInline(marker));
                prefixedInlines.AddRange(firstParagraph.Inlines);

                var firstWidget = new MarkdownTextBlockWidget(prefixedInlines)
                {
                    HangingIndent = markerWidth,
                    FocusableLinks = focusableChildren,
                    LinkActivatedHandler = linkActivatedHandler,
                    SourceWidget = sourceWidget
                };

                if (item.Children.Count == 1)
                {
                    items.Add(firstWidget);
                }
                else
                {
                    // Multiple blocks in the list item: first paragraph + rest
                    var blockWidgets = new List<Hex1bWidget> { firstWidget };
                    for (int j = 1; j < item.Children.Count; j++)
                    {
                        var childWidget = RenderBlock(item.Children[j], blockHandlers,
                            focusableChildren, linkActivatedHandler, sourceWidget, listDepth + 1);
                        // Indent continuation blocks to align with the text
                        blockWidgets.Add(new PaddingWidget(markerWidth, 0, 0, 0, childWidget));
                    }
                    items.Add(new VStackWidget(blockWidgets));
                }
            }
            else
            {
                // Non-paragraph list item (e.g., nested list only) — use padding
                var itemChildren = new List<Hex1bWidget>();
                foreach (var child in item.Children)
                {
                    itemChildren.Add(RenderBlock(child, blockHandlers, focusableChildren,
                        linkActivatedHandler, sourceWidget, listDepth + 1));
                }
                var content = itemChildren.Count == 1
                    ? itemChildren[0]
                    : new VStackWidget(itemChildren);
                items.Add(new PaddingWidget(markerWidth, 0, 0, 0,
                    new VStackWidget([
                        new MarkdownTextBlockWidget([new TextInline(marker)]) { HangingIndent = markerWidth },
                        content
                    ])));
            }
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
