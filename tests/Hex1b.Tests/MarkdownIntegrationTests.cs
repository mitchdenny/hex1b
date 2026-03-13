using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Markdown;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class MarkdownIntegrationTests
{
    [Fact]
    public async Task Markdown_RendersHeading()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("# Hello World"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5),
                "heading rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_RendersParagraph()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("This is a paragraph of text."),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This is a paragraph"), TimeSpan.FromSeconds(5),
                "paragraph rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_RendersFencedCode()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("```csharp\nvar x = 1;\n```"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("var"), TimeSpan.FromSeconds(5),
                "code block rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_FencedCode_MultiLine_ShowsAllLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("```\nline1\nline2\nline3\n```"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("line1") && s.ContainsText("line3"),
                TimeSpan.FromSeconds(5), "multi-line code block rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_FencedCode_HasBorderWithLanguageTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("```python\nprint('hi')\n```"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("python") && s.ContainsText("print"),
                TimeSpan.FromSeconds(5), "code block with language title rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_IndentedCode_RendersAsEditor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Some text\n\n    indented code\n    more code\n\nAfter"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("indented") && s.ContainsText("After"),
                TimeSpan.FromSeconds(5), "indented code block rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_RendersBlockQuote()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("> This is a quote"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This is a quote"), TimeSpan.FromSeconds(5),
                "blockquote rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_BlockQuoteWraps_WithBarOnEveryLine()
    {
        // Use a narrow terminal so the block quote text must wrap
        var longQuote = "> This is a longer block quote that should wrap across multiple lines in a narrow terminal";

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(25, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown(longQuote),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                // Verify that "│ " appears on multiple lines (wrapping works)
                var text = s.GetText();
                var lines = text.Split('\n');
                var barLines = lines.Count(l => l.TrimEnd().Contains("│"));
                return barLines >= 2;
            }, TimeSpan.FromSeconds(5),
                "blockquote wraps with bar on multiple lines")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_RendersList()
    {
        var source = "- Alpha\n- Beta\n- Gamma";

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown(source),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alpha") && s.ContainsText("Gamma"),
                TimeSpan.FromSeconds(5), "list rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_RendersComplexDocument()
    {
        var source = """
            # Title

            A paragraph with content.

            ```
            code block
            ```

            > A block quote

            - Item 1
            - Item 2

            ---

            Final text.
            """;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 24).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(ctx.Markdown(source)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Title") && s.ContainsText("paragraph"),
                TimeSpan.FromSeconds(5), "document rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_HeadingThenParagraph_HasBlankLineBetween()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("# Title\nParagraph text"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var text = s.GetText();
                var lines = text.Split('\n');
                var headingLine = Array.FindIndex(lines, l => l.Contains("Title"));
                var paragraphLine = Array.FindIndex(lines, l => l.Contains("Paragraph"));
                return headingLine >= 0 && paragraphLine == headingLine + 2;
            }, TimeSpan.FromSeconds(5), "heading then paragraph with gap")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ParagraphThenList_HasBlankLineBetween()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Some text\n\n- Item one"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var text = s.GetText();
                var lines = text.Split('\n');
                var textLine = Array.FindIndex(lines, l => l.Contains("Some text"));
                var itemLine = Array.FindIndex(lines, l => l.Contains("Item one"));
                // There should be a blank line between paragraph and list
                return textLine >= 0 && itemLine >= 0 && itemLine == textLine + 2;
            }, TimeSpan.FromSeconds(5), "paragraph then list with gap")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_CodeBlockHasSpacingAround()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Before\n\n```\ncode\n```\n\nAfter"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var text = s.GetText();
                var lines = text.Split('\n');
                var beforeLine = Array.FindIndex(lines, l => l.Contains("Before"));
                var afterLine = Array.FindIndex(lines, l => l.Contains("After"));
                // code block (border = 3 rows) + 1 blank before + 1 blank after = 5 rows between
                return beforeLine >= 0 && afterLine >= 0 && afterLine > beforeLine + 3;
            }, TimeSpan.FromSeconds(5), "code block has spacing around it")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ImageOnlyParagraph_WithLoader_RendersKgpImage()
    {
        // Create a 2x2 red RGBA image (16 bytes)
        var rgba = new byte[] {
            255, 0, 0, 255,  255, 0, 0, 255,
            255, 0, 0, 255,  255, 0, 0, 255
        };

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("![Red square](test.png)")
                .OnImageLoad((uri, alt) =>
                    Task.FromResult<Hex1b.Markdown.MarkdownImageData?>(
                        new Hex1b.Markdown.MarkdownImageData(rgba, 2, 2))),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // In headless mode (no KGP), the fallback text [Red square] should appear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Red square"),
                TimeSpan.FromSeconds(5), "image fallback text rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ImageOnlyParagraph_LoaderReturnsNull_FallsBackToText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("![Alt text](missing.png)")
                .OnImageLoad((uri, alt) =>
                    Task.FromResult<Hex1b.Markdown.MarkdownImageData?>(null)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Alt text"),
                TimeSpan.FromSeconds(5), "image fallback when loader returns null")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ImageInMixedParagraph_NoLoader_RendersAsInlineText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        // Image mixed with text — should stay inline, not become KgpImage
        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Here is an image ![photo](pic.png) in a sentence"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("photo") && s.ContainsText("sentence"),
                TimeSpan.FromSeconds(5), "inline image renders as text in paragraph")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ImageOnlyParagraph_NoLoader_RendersAsText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        // No OnImageLoad — should render as text fallback
        using var app = new Hex1bApp(
            ctx => ctx.Markdown("![My image](photo.png)"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("My image"),
                TimeSpan.FromSeconds(5), "image without loader renders as text")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ImageLoader_ReceivesCorrectUri()
    {
        Uri? capturedUri = null;
        string? capturedAlt = null;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("![Sunset](./images/sunset.png)")
                .OnImageLoad((uri, alt) =>
                {
                    capturedUri = uri;
                    capturedAlt = alt;
                    return Task.FromResult<Hex1b.Markdown.MarkdownImageData?>(null);
                }),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Sunset"),
                TimeSpan.FromSeconds(5), "image loader was called")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.NotNull(capturedUri);
        Assert.Equal("./images/sunset.png", capturedUri!.OriginalString);
        Assert.Equal("Sunset", capturedAlt);
    }

    [Fact]
    public async Task Markdown_InScrollPanel_Scrollable()
    {
        // Generate enough content to exceed viewport
        var lines = Enumerable.Range(1, 30).Select(i => $"- Line {i}");
        var source = string.Join("\n", lines);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(ctx.Markdown(source)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(5),
                "list rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_WithOnBlockHandler_OverridesHeading()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("# Original")
                .OnBlock<HeadingBlock>((mctx, block) =>
                    new TextBlockWidget($"CUSTOM: {block.Text}")),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("CUSTOM: Original"), TimeSpan.FromSeconds(5),
                "custom heading rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_WithOnBlockHandler_DefaultChaining()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("# Hello")
                .OnBlock<HeadingBlock>((mctx, block) =>
                    new VStackWidget([
                        new TextBlockWidget("WRAPPER"),
                        mctx.Default(block)
                    ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("WRAPPER") && s.ContainsText("Hello"),
                TimeSpan.FromSeconds(5), "wrapped heading rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_InBorder_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => new BorderWidget(ctx.Markdown("# Bordered\n\nContent here")),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bordered") && s.ContainsText("Content here"),
                TimeSpan.FromSeconds(5), "bordered markdown rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    // ==========================================================================
    // Phase 2: Styled inline rendering integration tests
    // ==========================================================================

    [Fact]
    public async Task Markdown_BoldText_RendersBold()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Hello **bold** world"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Hello"))
                    return false;
                // Find the 'b' in 'bold' and verify it has Bold attribute
                // "Hello " = 6 chars, then "bold" starts at column 6
                var cell = s.GetCell(6, 0);
                return cell.Character == "b"
                    && (cell.Attributes & CellAttributes.Bold) != 0;
            }, TimeSpan.FromSeconds(5), "bold text rendered with attribute")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_ItalicText_RendersItalic()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Hello *italic* world"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Hello"))
                    return false;
                // "Hello " = 6 chars, "italic" starts at col 6
                var cell = s.GetCell(6, 0);
                return cell.Character == "i"
                    && (cell.Attributes & CellAttributes.Italic) != 0;
            }, TimeSpan.FromSeconds(5), "italic text rendered with attribute")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_InlineCode_HasBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Use `code` here"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Use"))
                    return false;
                // "Use " = 4 chars, "code" starts at col 4
                var cell = s.GetCell(4, 0);
                return cell.Character == "c" && cell.Background != null;
            }, TimeSpan.FromSeconds(5), "inline code rendered with background")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_MixedInlines_AllStyled()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Normal **bold** *italic* `code` end"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Normal"))
                    return false;

                // "Normal " = 7 chars
                var normalCell = s.GetCell(0, 0);
                var boldCell = s.GetCell(7, 0);     // 'b' of 'bold'
                var italicCell = s.GetCell(12, 0);  // 'i' of 'italic' (after "Normal bold ")
                var codeCell = s.GetCell(19, 0);    // 'c' of 'code' (after "Normal bold italic ")

                return normalCell.Character == "N"
                    && (normalCell.Attributes & CellAttributes.Bold) == 0
                    && boldCell.Character == "b"
                    && (boldCell.Attributes & CellAttributes.Bold) != 0
                    && italicCell.Character == "i"
                    && (italicCell.Attributes & CellAttributes.Italic) != 0
                    && codeCell.Character == "c"
                    && codeCell.Background != null;
            }, TimeSpan.FromSeconds(5), "mixed inlines all styled correctly")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_HeadingWithBold_ComposesStyles()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("# Heading Text"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Heading"))
                    return false;
                // h1 has "▌ " prefix (▌ is 1 col + space = 2 cols)
                // Then "Heading" starts at col 2
                // h1 should have Bold attribute and foreground color
                var cell = s.GetCell(2, 0);
                return cell.Character == "H"
                    && (cell.Attributes & CellAttributes.Bold) != 0
                    && cell.Foreground != null;
            }, TimeSpan.FromSeconds(5), "heading styled with bold and color")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_Link_HasHyperlinkData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Visit [example](https://example.com) now"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                if (!s.ContainsText("Visit"))
                    return false;
                // "Visit " = 6 chars, "example" starts at col 6
                var cell = s.GetCell(6, 0);
                return cell.Character == "e"
                    && cell.TrackedHyperlink != null
                    && cell.TrackedHyperlink.Data.Uri == "https://example.com";
            }, TimeSpan.FromSeconds(5), "link has clickable hyperlink data")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    // ==========================================================================
    // List Wrapping Tests
    // ==========================================================================

    [Fact]
    public async Task Markdown_BulletList_WrapsWithHangingIndent()
    {
        // Terminal is 30 chars wide — "• " takes 2 chars, text wraps at 28
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("- This is a long list item that should wrap to multiple lines"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This") && s.ContainsText("long"),
                TimeSpan.FromSeconds(5), "list rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Line 1 should start with "• " marker
        Assert.Contains("This", screenText);

        var lines = screenText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, $"Expected at least 2 lines for wrapped text, got {lines.Length}. Screen:\n{screenText}");

        // Second line starts with spaces (hanging indent)
        var secondLine = lines[1];
        Assert.True(secondLine.StartsWith("  "), $"Continuation line should be indented. Got: '{secondLine}'");
    }

    [Fact]
    public async Task Markdown_OrderedList_WrapsWithHangingIndent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("1. This is a long ordered list item that should wrap correctly"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("1.") && s.ContainsText("long"),
                TimeSpan.FromSeconds(5), "list rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var lines = screenText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2, $"Expected at least 2 lines. Screen:\n{screenText}");

        // First line starts with "1. "
        Assert.StartsWith("1.", lines[0].TrimEnd());

        // Second line indented by 3 chars (length of "1. ")
        Assert.True(lines[1].StartsWith("   "), $"Continuation should indent 3 chars. Got: '{lines[1]}'");
    }

    // ==========================================================================
    // Focus Navigation Integration Tests
    // ==========================================================================

    [Fact]
    public async Task Markdown_FocusableLinks_TabMovesToFirstLink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Click [here](https://example.com) for info")
                .Focusable(children: true),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(5),
                "markdown rendered")
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(100))
            .WaitUntil(s =>
            {
                // "Click " = 6 chars, "here" starts at col 6
                var cell = s.GetCell(6, 0);
                // When focused, the link should have bold attribute (reverse video adds bold)
                return cell.Character == "h" && cell.Attributes.HasFlag(CellAttributes.Bold);
            }, TimeSpan.FromSeconds(5), "link to be focused with highlight")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_FocusableLinks_TabCyclesThroughLinks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("See [first](https://a.com) and [second](https://b.com)")
                .Focusable(children: true),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // EnsureFocus auto-focuses the first link, so wait for that
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var positions = s.FindText("first");
                if (positions.Count == 0) return false;
                var cell = s.GetCell(positions[0].Column, positions[0].Line);
                return cell.Attributes.HasFlag(CellAttributes.Bold);
            }, TimeSpan.FromSeconds(5), "first link auto-focused via EnsureFocus")
            // Tab to move focus to the second link
            .Tab()
            .WaitUntil(s =>
            {
                var positions = s.FindText("second");
                if (positions.Count == 0) return false;
                var cell = s.GetCell(positions[0].Column, positions[0].Line);
                return cell.Attributes.HasFlag(CellAttributes.Bold);
            }, TimeSpan.FromSeconds(5), "second link focused after Tab")
            // Tab again to wrap back to first link
            .Tab()
            .WaitUntil(s =>
            {
                var positions = s.FindText("first");
                if (positions.Count == 0) return false;
                var cell = s.GetCell(positions[0].Column, positions[0].Line);
                if (!cell.Attributes.HasFlag(CellAttributes.Bold)) return false;
                // Also verify second link lost focus
                var secondPositions = s.FindText("second");
                if (secondPositions.Count == 0) return false;
                var secondCell = s.GetCell(secondPositions[0].Column, secondPositions[0].Line);
                return !secondCell.Attributes.HasFlag(CellAttributes.Bold);
            }, TimeSpan.FromSeconds(5), "first link re-focused after wrap-around Tab")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_FocusableLinks_EnterActivatesLink()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        string? activatedUrl = null;
        string? activatedText = null;

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Click [here](https://example.com) now")
                .Focusable(children: true)
                .OnLinkActivated(args =>
                {
                    activatedUrl = args.Url;
                    activatedText = args.Text;
                    args.Handled = true; // prevent default browser open
                }),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(5),
                "markdown rendered")
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("https://example.com", activatedUrl);
        Assert.Equal("here", activatedText);
    }

    [Fact]
    public async Task Markdown_FocusableLinks_OnLinkActivatedReceivesCorrectKind()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        Events.MarkdownLinkKind? receivedKind = null;

        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Go to [docs](https://docs.example.com)")
                .Focusable(children: true)
                .OnLinkActivated(args =>
                {
                    receivedKind = args.Kind;
                    args.Handled = true;
                }),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Go to"), TimeSpan.FromSeconds(5),
                "markdown rendered")
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(200))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(Events.MarkdownLinkKind.External, receivedKind);
    }

    [Fact]
    public async Task Markdown_NotFocusable_TabDoesNotFocusLinks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        // Without Focusable(children: true), links should not be focusable
        using var app = new Hex1bApp(
            ctx => ctx.Markdown("Click [here](https://example.com) now"),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click"), TimeSpan.FromSeconds(5),
                "markdown rendered")
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            .WaitUntil(s =>
            {
                // "Click " = 6, "here" at col 6
                // Link should NOT have bold (focus highlight)
                var cell = s.GetCell(6, 0);
                return cell.Character == "h" && !cell.Attributes.HasFlag(CellAttributes.Bold);
            }, TimeSpan.FromSeconds(2), "link not focused")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Theory]
    [InlineData("Getting Started", "getting-started")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Spaces  Around  ", "--spaces--around--")]
    [InlineData("ALL CAPS HEADING", "all-caps-heading")]
    [InlineData("Special!@#Characters$%", "specialcharacters")]
    [InlineData("hyphen-ated", "hyphen-ated")]
    [InlineData("Mix 123 Numbers", "mix-123-numbers")]
    [InlineData("", "")]
    public void GenerateSlug_ProducesGitHubStyleSlug(string input, string expected)
    {
        var slug = MarkdownWidgetRenderer.GenerateSlug(input);
        Assert.Equal(expected, slug);
    }

    [Fact]
    public async Task Markdown_NestedList_RendersWithAlternateBullets()
    {
        var md = "- Item 1\n  - Nested A\n  - Nested B\n- Item 2";

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown(md),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        Hex1bTerminalSnapshot? snapshot = null;
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(5), "nested list rendered")
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        snapshot = terminal.CreateSnapshot();

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Top-level items use "•", nested items use "◦"
        Assert.NotNull(snapshot);
        Assert.True(snapshot.ContainsText("•"), "Top-level bullet (•) should be present");
        Assert.True(snapshot.ContainsText("◦"), "Nested bullet (◦) should be present");
        Assert.True(snapshot.ContainsText("Nested A"), "Nested item A should be present");
        Assert.True(snapshot.ContainsText("Item 2"), "Second top-level item should be present");
    }

    [Fact]
    public async Task Markdown_IntraDocumentLink_ScrollsToHeading()
    {
        // Build a tall document: link at top, target heading far below
        var lines = new List<string>
        {
            "# Top",
            "",
            "[Go to target](#target-heading)",
            ""
        };

        // Add enough filler to push the target heading well off screen
        for (int i = 0; i < 30; i++)
        {
            lines.Add($"Filler paragraph {i}.");
            lines.Add("");
        }

        lines.Add("## Target Heading");
        lines.Add("");
        lines.Add("You made it here.");

        var source = string.Join("\n", lines);

        string? activatedUrl = null;
        Events.MarkdownLinkKind? activatedKind = null;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(
                ctx.Markdown(source).Focusable(children: true)
                    .OnLinkActivated(args =>
                    {
                        activatedUrl = args.Url;
                        activatedKind = args.Kind;
                        // Do NOT set Handled — let default ScrollToHeading run
                    })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            // Wait for the markdown to render
            .WaitUntil(s => s.ContainsText("Go to target"), TimeSpan.FromSeconds(5),
                "markdown rendered")
            // Tab to move focus to the link (skip past heading which is now focusable)
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(200))
            // Activate the intra-document link
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(500))
            // Check if activation happened by verifying URL was captured
            .WaitUntil(s => activatedUrl != null, TimeSpan.FromSeconds(5),
                "link activated callback fired")
            // Now wait for the target heading to scroll into view
            .WaitUntil(s => s.ContainsText("Target Heading"), TimeSpan.FromSeconds(5),
                "target heading visible after intra-document link activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("#target-heading", activatedUrl);
        Assert.Equal(Events.MarkdownLinkKind.IntraDocument, activatedKind);
    }

    [Fact]
    public async Task Markdown_IntraDocumentLink_ScrollsToHeading_WhenAlreadyScrolled()
    {
        var lines = new List<string>
        {
            "## Target Heading",
            "",
            "You made it here.",
            ""
        };

        for (int i = 0; i < 30; i++)
        {
            lines.Add($"Filler paragraph {i}.");
            lines.Add("");
        }

        lines.Add("[Jump to top](#target-heading)");
        lines.Add("");

        for (int i = 0; i < 10; i++)
        {
            lines.Add($"Trailing filler {i}.");
            lines.Add("");
        }

        var source = string.Join("\n", lines);

        string? activatedUrl = null;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(
                ctx.Markdown(source).Focusable(children: true)
                    .OnLinkActivated(args =>
                    {
                        activatedUrl = args.Url;
                    })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Target Heading"), TimeSpan.FromSeconds(5),
                "initial render")
            // Tab past heading, then to the link
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            .WaitUntil(s => s.ContainsText("Jump to top"), TimeSpan.FromSeconds(5),
                "link scrolled into view")
            // Activate the link
            .Key(Hex1bKey.Enter)
            .Wait(TimeSpan.FromMilliseconds(500))
            // Verify callback fired
            .WaitUntil(s => activatedUrl != null, TimeSpan.FromSeconds(5),
                "link activated callback fired")
            // The target heading should scroll into view
            .WaitUntil(s => s.ContainsText("Target Heading"), TimeSpan.FromSeconds(5),
                "target heading visible after scrolled intra-doc link activation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_FocusedLink_ArrowKeysScrollDocument()
    {
        var lines = new List<string> { "## Top", "" };
        for (int i = 0; i < 40; i++)
        {
            lines.Add($"Line {i}.");
            lines.Add("");
        }

        lines.Add("[Link](#top)");
        lines.Add("");
        lines.Add("## Bottom");

        var source = string.Join("\n", lines);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(
                ctx.Markdown(source).Focusable(children: true)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(5), "initial render")
            // Tab to focus the heading
            .Tab()
            .Wait(TimeSpan.FromMilliseconds(300))
            // Down arrows scroll incrementally
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(100))
            .Key(Hex1bKey.DownArrow)
            .Wait(TimeSpan.FromMilliseconds(300))
            // "Top" should no longer be visible (scrolled past)
            .WaitUntil(s => !s.ContainsText("Top"), TimeSpan.FromSeconds(2),
                "scrolled down by arrows")
            // End should jump to the bottom
            .Key(Hex1bKey.End)
            .Wait(TimeSpan.FromMilliseconds(500))
            .WaitUntil(s => s.ContainsText("Bottom"), TimeSpan.FromSeconds(5),
                "scrolled to end")
            // Home should jump back to the top
            .Key(Hex1bKey.Home)
            .Wait(TimeSpan.FromMilliseconds(500))
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(5),
                "scrolled to start")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_Table_RendersWithGridLines()
    {
        var markdown = "| Name | Value |\n|------|-------|\n| Foo  | 42    |\n| Bar  | 99    |";

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown(markdown),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Foo"), TimeSpan.FromSeconds(5),
                "table rendered with data")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var text = snapshot.GetScreenText();

        // Verify table structure is present
        Assert.Contains("Name", text);
        Assert.Contains("Value", text);
        Assert.Contains("Foo", text);
        Assert.Contains("42", text);
        Assert.Contains("Bar", text);
        Assert.Contains("99", text);

        // Verify gridline characters are rendered
        Assert.Contains("┌", text);
        Assert.Contains("┐", text);
        Assert.Contains("└", text);
        Assert.Contains("┘", text);
        Assert.Contains("─", text);
        Assert.Contains("│", text);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_AcceptsIHex1bDocument()
    {
        var doc = new Hex1bDocument("# Document Heading\n\nParagraph from document.");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            (Func<RootContext, Hex1bWidget>)(ctx => ctx.Markdown(doc)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Document Heading") && s.ContainsText("Paragraph from document"),
                TimeSpan.FromSeconds(5), "document heading and paragraph rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_DocumentUpdatesReflected()
    {
        var doc = new Hex1bDocument("# Before");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            (Func<RootContext, Hex1bWidget>)(ctx => ctx.Markdown(doc)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Before"), TimeSpan.FromSeconds(5),
                "initial content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Modify the document
        doc.Apply(new ReplaceOperation(
            new DocumentRange(
                new DocumentOffset(0),
                new DocumentOffset(doc.Length)),
            "# After"));

        // Send a key to trigger a rebuild cycle
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(s => s.ContainsText("After"), TimeSpan.FromSeconds(5),
                "updated content rendered")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_MouseScrollOverCodeBlock_ScrollsParentPanel()
    {
        // Build markdown with enough content to require scrolling, including a code block.
        var lines = new List<string>
        {
            "# Top",
            "",
            "```csharp",
            "var x = 1;",
            "var y = 2;",
            "var z = 3;",
            "```",
            ""
        };

        // Add lines to push "Bottom" well below the fold
        for (int i = 0; i < 30; i++)
            lines.Add($"Filler line {i}.");

        lines.Add("");
        lines.Add("## Bottom");

        var source = string.Join("\n", lines);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(ctx.Markdown(source)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(5), "initial render")
            // Position mouse over where the code block renders (roughly row 3-5)
            .MouseMoveTo(10, 3)
            // Scroll down with the mouse wheel over the code block area
            .ScrollDown(10)
            .Wait(TimeSpan.FromMilliseconds(500))
            // After scrolling, "Top" should no longer be visible and filler should be
            .WaitUntil(s => !s.ContainsText("Top"), TimeSpan.FromSeconds(3),
                "parent panel scrolled past Top")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Markdown_MouseScrollOverFocusableLink_ScrollsParentPanel()
    {
        var lines = new List<string>
        {
            "# Top",
            "",
            "Click [this link](https://example.com) to visit.",
            ""
        };

        for (int i = 0; i < 30; i++)
            lines.Add($"Filler line {i}.");

        lines.Add("");
        lines.Add("## Bottom");

        var source = string.Join("\n", lines);

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(ctx.Markdown(source).Focusable(true)),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(5), "initial render")
            // Position mouse over the link text (row 2, within the link)
            .MouseMoveTo(10, 2)
            // Scroll down with the mouse wheel over the link
            .ScrollDown(10)
            .Wait(TimeSpan.FromMilliseconds(500))
            .WaitUntil(s => !s.ContainsText("Top"), TimeSpan.FromSeconds(3),
                "parent panel scrolled past Top")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }
}
