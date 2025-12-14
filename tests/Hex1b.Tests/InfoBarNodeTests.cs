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
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(terminal, theme);
    }

    [Fact]
    public void Measure_ReturnsHeightOfOne()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WidthIsAtLeastSectionTextLength()
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
    public void Measure_FillsAvailableWidth()
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
    public void Measure_WithNoSections_ReturnsMinimalSize()
    {
        var node = new InfoBarNode { Sections = [] };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Arrange_SetsBounds()
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
    public void Render_DisplaysSectionText()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Ready")]
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);

        Assert.Contains("Ready", terminal.GetScreenText());
    }

    [Fact]
    public void Render_DisplaysMultipleSections()
    {
        using var terminal = new Hex1bTerminal(60, 5);
        var context = CreateContext(terminal);
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

        var screenText = terminal.GetScreenText();
        Assert.Contains("Mode: Normal", screenText);
        Assert.Contains("|", screenText);
        Assert.Contains("Line 1, Col 15", screenText);
    }

    [Fact]
    public void Render_WithInvertColors_SwapsForegroundAndBackground()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var theme = new Hex1bTheme("Test")
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.Black);
        var context = CreateContext(terminal, theme);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Inverted")],
            InvertColors = true
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);

        // With inversion: foreground becomes background (White -> foreground uses Black)
        // and background becomes foreground (Black -> background uses White)
        // So we should see white background ANSI code
        Assert.Contains("\x1b[48;2;255;255;255m", terminal.RawOutput);
        Assert.Contains("\x1b[38;2;0;0;0m", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithInvertColorsFalse_UsesNormalColors()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var theme = new Hex1bTheme("Test")
            .Set(InfoBarTheme.ForegroundColor, Hex1bColor.Green)
            .Set(InfoBarTheme.BackgroundColor, Hex1bColor.Blue);
        var context = CreateContext(terminal, theme);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Normal")],
            InvertColors = false
        };

        node.Measure(Constraints.Tight(40, 1));
        node.Arrange(new Rect(0, 0, 40, 1));
        node.Render(context);

        // Should use the specified colors directly
        Assert.Contains("\x1b[38;2;0;255;0m", terminal.RawOutput); // Green foreground
        Assert.Contains("\x1b[48;2;0;0;255m", terminal.RawOutput); // Blue background
    }

    [Fact]
    public void Render_SectionWithCustomColors_OverridesBarColors()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = CreateContext(terminal);

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

        // Error section should have its own colors
        Assert.Contains("\x1b[38;2;255;0;0m", terminal.RawOutput); // Red foreground
        Assert.Contains("\x1b[48;2;255;255;0m", terminal.RawOutput); // Yellow background
    }

    [Fact]
    public void Render_TruncatesTextExceedingBounds()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);

        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("This text is way too long for the bar")]
        };

        node.Measure(Constraints.Tight(20, 1));
        node.Arrange(new Rect(0, 0, 20, 1));
        node.Render(context);

        // Text should be truncated to fit
        var screenText = terminal.GetScreenText();
        Assert.DoesNotContain("for the bar", screenText);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_ReturnsEmpty()
    {
        var node = new InfoBarNode
        {
            Sections = [new InfoBarSection("Status")]
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void HandleInput_ReturnsFalse()
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
        using var terminal = new Hex1bTerminal(60, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.InfoBar("Ready")
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Ready", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_InfoBarWithMultipleSections_RendersAll()
    {
        using var terminal = new Hex1bTerminal(80, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new InfoBarWidget([
                    new InfoBarSection("Mode: Insert"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("UTF-8"),
                    new InfoBarSection(" | "),
                    new InfoBarSection("Ln 42, Col 7")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Mode: Insert", terminal.RawOutput);
        Assert.Contains("UTF-8", terminal.RawOutput);
        Assert.Contains("Ln 42, Col 7", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_InfoBarInVStack_PositionedCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 5);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Main Content"),
                    v.InfoBar("Status Bar")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Main Content", terminal.RawOutput);
        Assert.Contains("Status Bar", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_InfoBarWithThemedColors_AppliesCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var theme = Hex1bThemes.Default.Clone()
            .Set(InfoBarTheme.ForegroundColor, Hex1bColor.Cyan)
            .Set(InfoBarTheme.BackgroundColor, Hex1bColor.DarkGray);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                new InfoBarWidget([new InfoBarSection("Themed")], InvertColors: false)
            ),
            new Hex1bAppOptions { Terminal = terminal, Theme = theme }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Themed", terminal.RawOutput);
        Assert.Contains("\x1b[38;2;0;255;255m", terminal.RawOutput); // Cyan
        Assert.Contains("\x1b[48;2;64;64;64m", terminal.RawOutput); // DarkGray
    }

    #endregion
}
