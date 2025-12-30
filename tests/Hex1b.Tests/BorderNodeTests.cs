using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for BorderNode layout, rendering, theming, and input handling.
/// Tests both direct node operations and integration with Hex1bApp using fluent API.
/// </summary>
public class BorderNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload)
    {
        return new Hex1bRenderContext(workload);
    }

    #region Measurement Tests

    [Fact]
    public void Measure_AddsBorderToChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // Child is 5 wide, 1 tall. Border adds 2 to each dimension.
        Assert.Equal(7, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_WithNoChild_ReturnsMinimalBorder()
    {
        var node = new BorderNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        // Just the border: 2 wide, 2 tall
        Assert.Equal(2, size.Width);
        Assert.Equal(2, size.Height);
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var child = new TextBlockNode { Text = "This is a long text line" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.True(size.Width <= 15);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public void Measure_WithMultilineChild_CalculatesCorrectHeight()
    {
        // Simulate a VStack with multiple children
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" }
            }
        };
        var node = new BorderNode { Child = vstack };

        var size = node.Measure(Constraints.Unbounded);

        // VStack: 6 chars wide, 3 tall + 2 for border = 8x5
        Assert.Equal(8, size.Width);
        Assert.Equal(5, size.Height);
    }

    [Fact]
    public void Measure_WithTightConstraints_RespectsMinimum()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = new BorderNode { Child = child };

        // Very tight constraints
        var size = node.Measure(new Constraints(3, 3, 3, 3));

        Assert.Equal(3, size.Width);
        Assert.Equal(3, size.Height);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_PositionsChildInsideBorder()
    {
        var child = new TextBlockNode { Text = "Test" };
        var node = new BorderNode { Child = child };

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(new Rect(5, 3, 20, 10));

        // Child should be offset by 1 in each direction
        Assert.Equal(6, child.Bounds.X);
        Assert.Equal(4, child.Bounds.Y);
        // Child should be 2 smaller in each dimension
        Assert.Equal(18, child.Bounds.Width);
        Assert.Equal(8, child.Bounds.Height);
    }

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        var bounds = new Rect(0, 0, 20, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public void Arrange_WithMinimalBounds_DoesNotCrash()
    {
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        
        // Minimal 2x2 border with no inner space
        node.Measure(Constraints.Tight(2, 2));
        var ex = Record.Exception(() => node.Arrange(new Rect(0, 0, 2, 2)));
        
        Assert.Null(ex);
    }

    [Fact]
    public void Arrange_ChildGetsZeroSizeWhenBorderFillsSpace()
    {
        var child = new TextBlockNode { Text = "Test" };
        var node = new BorderNode { Child = child };

        node.Measure(Constraints.Tight(2, 2));
        node.Arrange(new Rect(0, 0, 2, 2));

        // Child should have zero width and height
        Assert.Equal(0, child.Bounds.Width);
        Assert.Equal(0, child.Bounds.Height);
    }

    #endregion

    #region Rendering Tests - Border Characters

    [Fact]
    public void Render_DrawsTopBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        // Should contain top-left corner
        Assert.Contains("┌", topLine);
        // Should contain horizontal line
        Assert.Contains("─", topLine);
        // Should contain top-right corner
        Assert.Contains("┐", topLine);
    }

    [Fact]
    public void Render_DrawsBottomBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var bottomLine = terminal.CreateSnapshot().GetLineTrimmed(4);
        // Should contain bottom-left corner
        Assert.Contains("└", bottomLine);
        // Should contain bottom-right corner
        Assert.Contains("┘", bottomLine);
    }

    [Fact]
    public void Render_DrawsVerticalBorders()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        // Middle rows should have vertical borders
        var middleLine = terminal.CreateSnapshot().GetLineTrimmed(2);
        Assert.Contains("│", middleLine);
    }

    [Fact]
    public void Render_CompleteBorderBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" }
        };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);

        // Check complete border structure
        var line0 = terminal.CreateSnapshot().GetLineTrimmed(0);
        var line1 = terminal.CreateSnapshot().GetLineTrimmed(1);
        var line2 = terminal.CreateSnapshot().GetLineTrimmed(2);

        Assert.StartsWith("┌", line0);
        Assert.EndsWith("┐", line0);
        Assert.StartsWith("│", line1);
        Assert.StartsWith("└", line2);
        Assert.EndsWith("┘", line2);
    }

    #endregion

    #region Rendering Tests - Title

    [Fact]
    public void Render_WithTitle_ShowsTitleInTopBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Content" },
            Title = "My Title"
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("My Title", topLine);
    }

    [Fact]
    public void Render_WithLongTitle_TruncatesTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = "This Is A Very Long Title"
        };

        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        // Title should be truncated to fit within border (innerWidth=8, title max=6 chars)
        Assert.DoesNotContain("Very Long Title", topLine);
        Assert.Contains("This I", topLine);  // First 6 chars of title
    }

    [Fact]
    public void Render_WithShortTitle_CentersTitleInBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = "T"
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        // Title should be present
        Assert.Contains("T", topLine);
        // Should have horizontal lines on both sides
        Assert.Contains("─T", topLine);
        Assert.Contains("T─", topLine);
    }

    [Fact]
    public void Render_WithEmptyTitle_DrawsNormalBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = ""
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        // Should be ┌────────┐ without title
        Assert.Equal("┌────────┐", topLine);
    }

    [Fact]
    public void Render_WithNullTitle_DrawsNormalBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = null
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var topLine = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Equal("┌────────┐", topLine);
    }

    #endregion

    #region Rendering Tests - Child Content

    [Fact]
    public void Render_RendersChildContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hello" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 15, 5));
        node.Render(context);

        Assert.Contains("Hello", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public void Render_ChildContentInsideBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        // Child "Hi" should be somewhere in the screen between borders
        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("Hi", screenText);
        // First line should have top border
        var line0 = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("┌", line0);
    }

    [Fact]
    public void Render_WithNoChild_DrawsEmptyBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new BorderNode { Child = null };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 5, 3));
        node.Render(context);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("┌", screenText);
        Assert.Contains("┐", screenText);
        Assert.Contains("└", screenText);
        Assert.Contains("┘", screenText);
    }

    #endregion

    #region Rendering Tests - Narrow Terminal

    [Fact]
    public void Render_InNarrowTerminal_StillDrawsBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Short" }  // Use short content that fits
        };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 7, 3));
        node.Render(context);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("┌", screenText);
        Assert.Contains("┐", screenText);
        Assert.Contains("└", screenText);
        Assert.Contains("┘", screenText);
    }

    [Fact]
    public void Render_MinimalBorder_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var context = CreateContext(workload);
        var node = new BorderNode { Child = null };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 2, 2));
        node.Render(context);

        var line0 = terminal.CreateSnapshot().GetLineTrimmed(0);
        var line1 = terminal.CreateSnapshot().GetLineTrimmed(1);
        
        // With width=2, we get: ┌┐ on top, └┘ on bottom
        Assert.Equal("┌┐", line0);
        Assert.Equal("└┘", line1);
    }

    #endregion

    #region Theming Tests

    [Fact]
    public void Render_WithCustomTheme_UsesThemeCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.VerticalLine, "║");
        var context = new Hex1bRenderContext(workload, theme);

        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("╔", screenText);
        Assert.Contains("╗", screenText);
        Assert.Contains("╚", screenText);
        Assert.Contains("╝", screenText);
        Assert.Contains("═", screenText);
        Assert.Contains("║", screenText);
    }

    [Fact]
    public void Render_DoubleLineBorderTheme_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 5);
        var theme = new Hex1bTheme("DoubleLine")
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.VerticalLine, "║");
        var context = new Hex1bRenderContext(workload, theme);

        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" }
        };

        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);

        var line0 = terminal.CreateSnapshot().GetLineTrimmed(0);
        var line1 = terminal.CreateSnapshot().GetLineTrimmed(1);
        var line2 = terminal.CreateSnapshot().GetLineTrimmed(2);

        Assert.Equal("╔════╗", line0);
        Assert.StartsWith("║", line1);
        Assert.EndsWith("║", line1);
        Assert.Equal("╚════╝", line2);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_ReturnsFocusableChildren()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var node = new BorderNode { Child = textBox };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(textBox, focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNonFocusableChild_ReturnsEmpty()
    {
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var node = new BorderNode { Child = textBlock };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNoChild_ReturnsEmpty()
    {
        var node = new BorderNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public void GetFocusableNodes_WithNestedContainers_FindsAllFocusables()
    {
        var textBox1 = new TextBoxNode { State = new TextBoxState() };
        var textBox2 = new TextBoxNode { State = new TextBoxState() };
        var vstack = new VStackNode
        {
            Children = new List<Hex1bNode> { textBox1, textBox2 }
        };
        var node = new BorderNode { Child = vstack };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(textBox1, focusables);
        Assert.Contains(textBox2, focusables);
    }

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new BorderNode();

        Assert.False(node.IsFocusable);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_PassesToChild()
    {
        var state = new TextBoxState { Text = "test", CursorPosition = 4 };
        var textBox = new TextBoxNode { State = state, IsFocused = true };
        var node = new BorderNode { Child = textBox };

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();

        // Use InputRouter to dispatch input through the node tree
        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("testA", state.Text);
    }

    [Fact]
    public void HandleInput_WithNoChild_ReturnsFalse()
    {
        var node = new BorderNode { Child = null };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleInput_WithNonFocusedChild_ReturnsFalse()
    {
        var state = new TextBoxState { Text = "test" };
        var textBox = new TextBoxNode { State = state, IsFocused = false };
        var node = new BorderNode { Child = textBox };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        // TextBox should not handle input when not focused
        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal("test", state.Text);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_BorderWithTextBlock_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("Hello World"))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Hello World"));
        // Check for border characters
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┐"));
        Assert.True(terminal.CreateSnapshot().ContainsText("└"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┘"));
    }

    [Fact]
    public async Task Integration_BorderWithTitle_ShowsTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("Content"), title: "My Panel")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("My Panel"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Check for title and content
        Assert.True(terminal.CreateSnapshot().ContainsText("My Panel"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Content"));
    }

    [Fact]
    public async Task Integration_BorderWithVStack_RendersMultipleLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(v => [
                    v.Text("Line 1"),
                    v.Text("Line 2"),
                    v.Text("Line 3")
                ], title: "List")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Line 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Line 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Line 3"));
        Assert.True(terminal.CreateSnapshot().ContainsText("List"));
    }

    [Fact]
    public async Task Integration_BorderWithTextBox_HandlesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.TextBox(text).OnTextChanged(args => text = args.NewText), title: "Input")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type into the textbox then exit
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hello", text);
    }

    [Fact]
    public async Task Integration_NestedBorders_RenderCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.Border(ctx.Text("Nested"), title: "Inner"),
                    title: "Outer"
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Nested"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Outer"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Inner"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Nested"));
    }

    [Fact]
    public async Task Integration_BorderInNarrowTerminal_TruncatesGracefully()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 12, 5);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("VeryLongContentText"), title: "VeryLongTitleText")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Border characters should be present
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┐"));
    }

    [Fact]
    public async Task Integration_MultipleBordersInHStack_RenderSideBySide()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Border(ctx.Text("Left"), title: "L"),
                    h.Border(ctx.Text("Right"), title: "R")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Left"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right"));
        Assert.True(terminal.CreateSnapshot().ContainsText("L"));
        Assert.True(terminal.CreateSnapshot().ContainsText("R"));
    }

    [Fact]
    public async Task Integration_BorderWithButton_HandlesClick()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Button("Click Me").OnClick(_ => { clicked = true; return Task.CompletedTask; }), title: "Actions")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Press enter to click the button then exit
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_BorderWithMultipleButtons_NavigatesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var clickedButton = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(v => [
                    v.Button("First").OnClick(_ => { clickedButton = "First"; return Task.CompletedTask; }),
                    v.Button("Second").OnClick(_ => { clickedButton = "Second"; return Task.CompletedTask; })
                ], title: "Buttons")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to second button, then click it
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Tab()
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Second", clickedButton);
    }

    [Fact]
    public async Task Integration_BorderAtOffset_RendersAtCorrectPosition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.Border(ctx.Text("Content"))
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Header"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Content"));
        // Border characters should be present
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
    }

    [Fact]
    public async Task Integration_EmptyBorder_RendersMinimalBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text(""))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should still have complete border
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┐"));
        Assert.True(terminal.CreateSnapshot().ContainsText("└"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┘"));
    }

    #endregion
}
