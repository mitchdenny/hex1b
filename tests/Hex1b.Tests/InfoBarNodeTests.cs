using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for InfoBarNode layout, rendering, and theming.
/// </summary>
public class InfoBarNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    [Fact]
    public async Task Measure_ReturnsHeightOfOne()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_WidthIsAtLeastSectionTextLength()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Hello"), new InfoBarSection("World")]
        };

        var size = node.Measure(Constraints.Unbounded);

        // "Hello" (5) + "World" (5) = 10
        Assert.True(size.Width >= 10);
    }

    [Fact]
    public async Task Measure_FillsAvailableWidth()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Short")]
        };

        var size = node.Measure(new Constraints(0, 80, 0, 24));

        // Should fill to max width
        Assert.Equal(80, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_WithNoSections_ReturnsMinimalSize()
    {
        var node = new InfoBarNode { Sections = [] };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Arrange_SetsBounds()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Test")]
        };
        var bounds = new Rect(0, 23, 80, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public async Task Render_DisplaysSectionText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = CreateContext(workload);
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Ready")]
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(1), "Ready text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Contains("Ready", snapshot.GetScreenText());
    }

    [Fact]
    public async Task Render_DisplaysMultipleSections()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 5).Build();
        var context = CreateContext(workload);
        var node = new InfoBarNode
        {
            Sections = [
                new InfoBarSection("Mode: Normal"),
                new InfoBarSection(" | "),
                new InfoBarSection("Line 1, Col 15")
            ]
        };

        node.Measure(Constraints.Tight(60, 1));
        node.Arrange(new Rect(0, 0, 60, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mode: Normal") && s.ContainsText("Line 1, Col 15"), TimeSpan.FromSeconds(1), "all info bar sections to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var screenText = snapshot.GetScreenText();
        Assert.Contains("Mode: Normal", screenText);
        Assert.Contains("|", screenText);
        Assert.Contains("Line 1, Col 15", screenText);
    }

    [Fact]
    public async Task Render_WithInvertColors_SwapsForegroundAndBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var theme = new Hex1bTheme("Test")
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.Black);
        var context = CreateContext(workload, theme);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Inverted")],
            InvertColors = true
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Inverted"), TimeSpan.FromSeconds(1), "Inverted text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // With inversion: foreground becomes background (White -> foreground uses Black)
        // and background becomes foreground (Black -> background uses White)
        // So we should see white background and black foreground
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 255, 255)));
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(0, 0, 0)));
    }

    [Fact]
    public async Task Render_WithInvertColorsFalse_UsesNormalColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var theme = new Hex1bTheme("Test")
            .Set(InfoBarTheme.ForegroundColor, Hex1bColor.Green)
            .Set(InfoBarTheme.BackgroundColor, Hex1bColor.Blue);
        var context = CreateContext(workload, theme);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Normal")],
            InvertColors = false
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Normal"), TimeSpan.FromSeconds(1), "Normal text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Should use the specified colors directly
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(0, 255, 0))); // Green foreground
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(0, 0, 255))); // Blue background
    }

    [Fact]
    public async Task Render_SectionWithCustomColors_OverridesBarColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = CreateContext(workload);

        var node = new InfoBarNode
        {
            Sections = [
                new InfoBarSection("Normal"),
                new InfoBarSection("Error!", Hex1bColor.Red, Hex1bColor.Yellow)
            ],
            InvertColors = true
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Error!"), TimeSpan.FromSeconds(1), "Error! text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Error section should have its own colors
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.FromRgb(255, 0, 0))); // Red foreground
        Assert.True(snapshot.HasBackgroundColor(Hex1bColor.FromRgb(255, 255, 0))); // Yellow background
    }

    [Fact]
    public async Task Render_TruncatesTextExceedingBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("This text is way too long for the bar")]
        };

        node.Measure(Constraints.Tight(20, 1));
        node.Arrange(new Rect(0, 0, 20, 1));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This text is way too"), TimeSpan.FromSeconds(1), "truncated text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Text should be truncated to fit
        var screenText = snapshot.GetScreenText();
        Assert.DoesNotContain("for the bar", screenText);
    }

    [Fact]
    public async Task IsFocusable_ReturnsFalse()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public async Task GetFocusableNodes_ReturnsEmpty()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public async Task HandleInput_ReturnsFalse()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_InfoBar_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.InfoBar("Ready")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Ready"));
    }

    [Fact]
    public async Task Integration_InfoBarWithMultipleSections_RendersAll()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new InfoBarWidget([
                    new InfoBarSection("Mode: Insert"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("UTF-8"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("Ln 42, Col 7")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mode: Insert"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Mode: Insert"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Mode: Insert"));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("UTF-8"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("UTF-8"));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Ln 42, Col 7"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Ln 42, Col 7"));
    }

    [Fact]
    public async Task Integration_InfoBarInVStack_PositionedCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Main Content"),
                    v.InfoBar("Status Bar")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Status Bar"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Main Content"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Main Content"));
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Status Bar"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Status Bar"));
    }

    [Fact]
    public async Task Integration_InfoBarWithThemedColors_AppliesCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var theme = Hex1bThemes.Default.Clone()
            .Set(InfoBarTheme.ForegroundColor, Hex1bColor.Cyan)
            .Set(InfoBarTheme.BackgroundColor, Hex1bColor.DarkGray);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new InfoBarWidget([new InfoBarSection("Themed")], InvertColors: false)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);
        Assert.True(terminal.CreateSnapshot().ContainsText("Themed"));
        Assert.True(terminal.CreateSnapshot().HasForegroundColor(Hex1bColor.FromRgb(0, 255, 255))); // Cyan
        Assert.True(terminal.CreateSnapshot().HasBackgroundColor(Hex1bColor.FromRgb(64, 64, 64))); // DarkGray
    }

    #endregion
}
