using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for PanelNode layout, rendering, and input handling.
/// </summary>
public class PanelNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal)
    {
        return new Hex1bRenderContext(terminal);
    }

    [Fact]
    public void Measure_ReturnsChildSize()
    {
        var child = new TextBlockNode { Text = "Hello World" };
        var node = new PanelNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // Panel doesn't add any size - just passes through child size
        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsZero()
    {
        var node = new PanelNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var child = new TextBlockNode { Text = "This is a long text" };
        var node = new PanelNode { Child = child };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.True(size.Width <= 10);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Arrange_ChildGetsFullBounds()
    {
        var child = new TextBlockNode { Text = "Test" };
        var node = new PanelNode { Child = child };
        var bounds = new Rect(5, 3, 20, 10);

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(bounds);

        // Child should have exact same bounds as panel
        Assert.Equal(bounds, child.Bounds);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new PanelNode { Child = new TextBlockNode { Text = "Test" } };
        var bounds = new Rect(0, 0, 20, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Render_RendersChildContent()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);
        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Panel Content" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("Panel Content", terminal.GetScreenText());
    }

    [Fact]
    public void Render_WithBackgroundColor_FillsBackground()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.Blue);
        var context = new Hex1bRenderContext(terminal, theme);

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);

        // Should contain background color ANSI escape code
        Assert.Contains("\x1b[48;2;", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithForegroundColor_AppliesColor()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.ForegroundColor, Hex1bColor.Green);
        var context = new Hex1bRenderContext(terminal, theme);

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);

        // Should contain foreground color ANSI escape code
        Assert.Contains("\x1b[38;2;", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithDefaultColors_RendersNormally()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = CreateContext(terminal);  // Uses default theme

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Content" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("Content", terminal.GetScreenText());
    }

    [Fact]
    public void GetFocusableNodes_ReturnsFocusableChildren()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new PanelNode { Child = button };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(button, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonFocusableChild_ReturnsEmpty()
    {
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var node = new PanelNode { Child = textBlock };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNoChild_ReturnsEmpty()
    {
        var node = new PanelNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void HandleInput_PassesToChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = () => clicked = true
        };
        var node = new PanelNode { Child = button };

        // Use InputRouter to route input to the focused child
        var result = InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_WithNoChild_ReturnsFalse()
    {
        var node = new PanelNode { Child = null };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new PanelNode();

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_WithNestedContainers_FindsAllFocusables()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var button = new ButtonNode { Label = "OK" };
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox, button }
        };
        var node = new PanelNode { Child = vstack };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(textBox, focusables);
        Assert.Contains(button, focusables);
    }

    [Fact]
    public void NestedPanelAndBorder_WorkTogether()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.DarkGray);
        var context = new Hex1bRenderContext(terminal, theme);

        var node = new BorderNode
        {
            Child = new PanelNode
            {
                Child = new TextBlockNode { Text = "Nested" }
            },
            Title = "Box"
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        var screenText = terminal.GetScreenText();
        Assert.Contains("Box", screenText);
        Assert.Contains("Nested", screenText);
        Assert.Contains("┌", screenText);
    }

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_PanelWithTextBlock_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(30, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Text("Panel Content"))
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Panel Content", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithVStack_RendersChildren()
    {
        using var terminal = new Hex1bTerminal(30, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(v => [
                    v.Text("Line 1"),
                    v.Text("Line 2"),
                    v.Text("Line 3")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Line 1", terminal.RawOutput);
        Assert.Contains("Line 2", terminal.RawOutput);
        Assert.Contains("Line 3", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithButton_HandlesFocus()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Button("Click Me", () => clicked = true))
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_PanelWithTextBox_HandlesInput()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var textBoxState = new TextBoxState();

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.TextBox(textBoxState))
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.TypeText("Hello Panel");
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Equal("Hello Panel", textBoxState.Text);
    }

    [Fact]
    public async Task Integration_PanelInsideBorder_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Panel(ctx.Text("Panel Inside Border")), "Container")
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Panel Inside Border", terminal.RawOutput);
        Assert.Contains("Container", terminal.RawOutput);
        Assert.Contains("┌", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithCustomTheme_AppliesColors()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 100));

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Text("Themed"))
            ),
            new Hex1bAppOptions { Terminal = terminal, Theme = theme }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should contain background color ANSI code
        Assert.Contains("\x1b[48;2;50;50;100m", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_NestedPanels_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(30, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Panel(ctx.Text("Deep Nested")))
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Deep Nested", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_PanelInVStack_RendersWithSiblings()
    {
        using var terminal = new Hex1bTerminal(40, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Panel(ctx.Text("Panel Content")),
                    v.Text("Footer")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Header", terminal.RawOutput);
        Assert.Contains("Panel Content", terminal.RawOutput);
        Assert.Contains("Footer", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithList_HandlesNavigation()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var listState = new ListState
        {
            Items = [new ListItem("1", "Item 1"), new ListItem("2", "Item 2")],
            SelectedIndex = 0
        };

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.List(listState))
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.DownArrow);
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Equal(1, listState.SelectedIndex);
    }

    #endregion
}
