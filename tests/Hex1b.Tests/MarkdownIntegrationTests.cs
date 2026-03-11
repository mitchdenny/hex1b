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
        var source = "```csharp\nvar x = 1;\n```";

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 12).Build();

        using var app = new Hex1bApp(
            ctx => ctx.Markdown(source),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("var x = 1"), TimeSpan.FromSeconds(5),
                "code block rendered")
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
}
