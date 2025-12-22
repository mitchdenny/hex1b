using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Tests for PanelNode layout, rendering, and input handling.
/// </summary>
public class PanelNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload)
    {
        return new Hex1bRenderContext(workload);
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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Panel Content" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("Panel Content", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_WithBackgroundColor_FillsBackground()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.Blue);
        var context = new Hex1bRenderContext(workload, theme);

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);

        // Should contain background color ANSI escape code
        Assert.Contains("\x1b[48;2;", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public void Render_WithForegroundColor_AppliesColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.ForegroundColor, Hex1bColor.Green);
        var context = new Hex1bRenderContext(workload, theme);

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 3));
        node.Render(context);

        // Should contain foreground color ANSI escape code
        Assert.Contains("\x1b[38;2;", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public void Render_WithDefaultColors_RendersNormally()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);  // Uses default theme

        var node = new PanelNode
        {
            Child = new TextBlockNode { Text = "Content" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("Content", terminal.CreateSnapshot().GetScreenText());
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
    public async Task HandleInput_PassesToChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };
        var node = new PanelNode { Child = button };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), focusRing, routerState);

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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var theme = new Hex1bTheme("Test")
            .Set(PanelTheme.BackgroundColor, Hex1bColor.DarkGray);
        var context = new Hex1bRenderContext(workload, theme);

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

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("Box", screenText);
        Assert.Contains("Nested", screenText);
        Assert.Contains("┌", screenText);
    }

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_PanelWithTextBlock_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Text("Panel Content"))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Panel Content"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Panel Content", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithVStack_RendersChildren()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(v => [
                    v.Text("Line 1"),
                    v.Text("Line 2"),
                    v.Text("Line 3")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Line 1", terminal.CreateSnapshot().RawOutput);
        Assert.Contains("Line 2", terminal.CreateSnapshot().RawOutput);
        Assert.Contains("Line 3", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithButton_HandlesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Button("Click Me").OnClick(_ => { clicked = true; return Task.CompletedTask; }))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_PanelWithTextBox_HandlesInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.TextBox(text).OnTextChanged(args => text = args.NewText))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello Panel")
            .WaitUntil(s => s.ContainsText("Hello Panel"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Equal("Hello Panel", text);
    }

    [Fact]
    public async Task Integration_PanelInsideBorder_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Panel(ctx.Text("Panel Inside Border")), "Container")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Panel Inside Border"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Panel Inside Border", terminal.CreateSnapshot().RawOutput);
        Assert.Contains("┌", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithCustomTheme_AppliesColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(PanelTheme.BackgroundColor, Hex1bColor.FromRgb(50, 50, 100));

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Text("Themed"))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Themed"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        // Should contain background color ANSI code
        Assert.Contains("\x1b[48;2;50;50;100m", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_NestedPanels_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.Panel(ctx.Text("Deep Nested")))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Deep Nested"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Deep Nested", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_PanelInVStack_RendersWithSiblings()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Panel(ctx.Text("Panel Content")),
                    v.Text("Footer")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Header"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        Assert.Contains("Header", terminal.CreateSnapshot().RawOutput);
        Assert.Contains("Panel Content", terminal.CreateSnapshot().RawOutput);
        Assert.Contains("Footer", terminal.CreateSnapshot().RawOutput);
    }

    [Fact]
    public async Task Integration_PanelWithList_HandlesNavigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Panel(ctx.List(items))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync();
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal);
        await runTask;

        // Verify second item is selected via rendered output
        Assert.Contains("> Item 2", terminal.CreateSnapshot().RawOutput);
    }

    #endregion
}
