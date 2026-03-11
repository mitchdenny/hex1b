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
}
