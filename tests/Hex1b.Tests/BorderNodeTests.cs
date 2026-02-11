using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

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
    public async Task Measure_AddsBorderToChildSize()
    {
        var child = new TextBlockNode { Text = "Hello" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(Constraints.Unbounded);

        // Child is 5 wide, 1 tall. Border adds 2 to each dimension.
        Assert.Equal(7, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public async Task Measure_WithNoChild_ReturnsMinimalBorder()
    {
        var node = new BorderNode { Child = null };

        var size = node.Measure(Constraints.Unbounded);

        // Just the border: 2 wide, 2 tall
        Assert.Equal(2, size.Width);
        Assert.Equal(2, size.Height);
    }

    [Fact]
    public async Task Measure_RespectsConstraints()
    {
        var child = new TextBlockNode { Text = "This is a long text line" };
        var node = new BorderNode { Child = child };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.True(size.Width <= 15);
        Assert.True(size.Height <= 5);
    }

    [Fact]
    public async Task Measure_WithMultilineChild_CalculatesCorrectHeight()
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
    public async Task Measure_WithTightConstraints_RespectsMinimum()
    {
        var child = new TextBlockNode { Text = "Hi" };
        var node = new BorderNode { Child = child };

        // Very tight constraints
        var size = node.Measure(new Constraints(3, 3, 3, 3));

        Assert.Equal(3, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact]
    public void Measure_WithFixedSizeHints_RespectsHints()
    {
        var child = new TextBlockNode { Text = "Small" };
        var node = new BorderNode 
        { 
            Child = child,
            WidthHint = SizeHint.Fixed(82),
            HeightHint = SizeHint.Fixed(26)
        };

        // Even with unbounded constraints, should return the fixed size
        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(82, size.Width);
        Assert.Equal(26, size.Height);
    }

    [Fact]
    public void Measure_WithFixedSizeHints_ClampsToConstraints()
    {
        var child = new TextBlockNode { Text = "Small" };
        var node = new BorderNode 
        { 
            Child = child,
            WidthHint = SizeHint.Fixed(100),
            HeightHint = SizeHint.Fixed(50)
        };

        // Constraints are smaller than hints - should clamp
        var size = node.Measure(new Constraints(0, 80, 0, 24));

        Assert.Equal(80, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void Measure_WithPartialFixedHint_UsesChildForOther()
    {
        var child = new TextBlockNode { Text = "Hello" }; // 5 wide, 1 tall
        var node = new BorderNode 
        { 
            Child = child,
            WidthHint = SizeHint.Fixed(20),
            HeightHint = null // Should use child size + 2
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(20, size.Width);
        Assert.Equal(3, size.Height); // child height (1) + border (2)
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public async Task Arrange_PositionsChildInsideBorder()
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
    public async Task Arrange_SetsBounds()
    {
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        var bounds = new Rect(0, 0, 20, 5);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    [Fact]
    public async Task Arrange_WithMinimalBounds_DoesNotCrash()
    {
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        
        // Minimal 2x2 border with no inner space
        node.Measure(Constraints.Tight(2, 2));
        var ex = Record.Exception(() => node.Arrange(new Rect(0, 0, 2, 2)));
        
        Assert.Null(ex);
    }

    [Fact]
    public async Task Arrange_ChildGetsZeroSizeWhenBorderFillsSpace()
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
    public async Task Render_DrawsTopBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌") && s.ContainsText("─") && s.ContainsText("┐"),
                TimeSpan.FromSeconds(1), "top border with corners")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        // Should contain top-left corner
        Assert.Contains("┌", topLine);
        // Should contain horizontal line
        Assert.Contains("─", topLine);
        // Should contain top-right corner
        Assert.Contains("┐", topLine);
    }

    [Fact]
    public async Task Render_DrawsBottomBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("└") && s.ContainsText("┘"),
                TimeSpan.FromSeconds(1), "bottom border with corners")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var bottomLine = snapshot.GetLineTrimmed(4);
        // Should contain bottom-left corner
        Assert.Contains("└", bottomLine);
        // Should contain bottom-right corner
        Assert.Contains("┘", bottomLine);
    }

    [Fact]
    public async Task Render_DrawsVerticalBorders()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("│"),
                TimeSpan.FromSeconds(1), "vertical border")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Middle rows should have vertical borders
        var middleLine = snapshot.GetLineTrimmed(2);
        Assert.Contains("│", middleLine);
    }

    [Fact]
    public async Task Render_CompleteBorderBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" }
        };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌") && s.ContainsText("┘"),
                TimeSpan.FromSeconds(1), "complete border box")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Check complete border structure
        var line0 = snapshot.GetLineTrimmed(0);
        var line1 = snapshot.GetLineTrimmed(1);
        var line2 = snapshot.GetLineTrimmed(2);

        Assert.StartsWith("┌", line0);
        Assert.EndsWith("┐", line0);
        Assert.StartsWith("│", line1);
        Assert.StartsWith("└", line2);
        Assert.EndsWith("┘", line2);
    }

    #endregion

    #region Rendering Tests - Title

    [Fact]
    public async Task Render_WithTitle_ShowsTitleInTopBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Content" },
            Title = "My Title"
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("My Title"),
                TimeSpan.FromSeconds(1), "title in top border")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        Assert.Contains("My Title", topLine);
    }

    [Fact]
    public async Task Render_WithLongTitle_TruncatesTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = "This Is A Very Long Title"
        };

        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This I") && !s.ContainsText("Very Long Title"),
                TimeSpan.FromSeconds(1), "truncated title")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        // Title should be truncated to fit within border (innerWidth=8, title max=6 chars)
        Assert.DoesNotContain("Very Long Title", topLine);
        Assert.Contains("This I", topLine);  // First 6 chars of title
    }

    [Fact]
    public async Task Render_WithShortTitle_CentersTitleInBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = "T"
        };

        node.Measure(Constraints.Tight(30, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("─T") && s.ContainsText("T─"),
                TimeSpan.FromSeconds(1), "centered short title")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        // Title should be present
        Assert.Contains("T", topLine);
        // Should have horizontal lines on both sides
        Assert.Contains("─T", topLine);
        Assert.Contains("T─", topLine);
    }

    [Fact]
    public async Task Render_WithEmptyTitle_DrawsNormalBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = ""
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌────────┐"),
                TimeSpan.FromSeconds(1), "normal border without title")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        // Should be ┌────────┐ without title
        Assert.Equal("┌────────┐", topLine);
    }

    [Fact]
    public async Task Render_WithNullTitle_DrawsNormalBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "" },
            Title = null
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌────────┐"),
                TimeSpan.FromSeconds(1), "normal border with null title")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var topLine = snapshot.GetLineTrimmed(0);
        Assert.Equal("┌────────┐", topLine);
    }

    #endregion

    #region Rendering Tests - Child Content

    [Fact]
    public async Task Render_RendersChildContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hello" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 15, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2), "child content to render")
            .Build()
            .ApplyAsync(terminal);

        Assert.Contains("Hello", terminal.CreateSnapshot().GetScreenText());
    }

    [Fact]
    public async Task Render_ChildContentInsideBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Hi" }
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 10, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hi"), TimeSpan.FromSeconds(2), "child content to render")
            .Build()
            .ApplyAsync(terminal);

        // Child "Hi" should be somewhere in the screen between borders
        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("Hi", screenText);
        // First line should have top border
        var line0 = terminal.CreateSnapshot().GetLineTrimmed(0);
        Assert.Contains("┌", line0);
    }

    [Fact]
    public async Task Render_WithNoChild_DrawsEmptyBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode { Child = null };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 5, 3));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌") && s.ContainsText("┐") && s.ContainsText("└") && s.ContainsText("┘"),
                TimeSpan.FromSeconds(1), "empty border corners")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var screenText = snapshot.GetScreenText();
        Assert.Contains("┌", screenText);
        Assert.Contains("┐", screenText);
        Assert.Contains("└", screenText);
        Assert.Contains("┘", screenText);
    }

    #endregion

    #region Rendering Tests - Narrow Terminal

    [Fact]
    public async Task Render_InNarrowTerminal_StillDrawsBorder()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode
        {
            Child = new TextBlockNode { Text = "Short" }  // Use short content that fits
        };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 7, 3));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌") && s.ContainsText("┘"),
                TimeSpan.FromSeconds(1), "border in narrow terminal")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var screenText = snapshot.GetScreenText();
        Assert.Contains("┌", screenText);
        Assert.Contains("┐", screenText);
        Assert.Contains("└", screenText);
        Assert.Contains("┘", screenText);
    }

    [Fact]
    public async Task Render_MinimalBorder_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        var context = CreateContext(workload);
        var node = new BorderNode { Child = null };

        node.Measure(Constraints.Tight(10, 5));
        node.Arrange(new Rect(0, 0, 2, 2));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┌┐") && s.ContainsText("└┘"),
                TimeSpan.FromSeconds(1), "minimal 2x2 border")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var line0 = snapshot.GetLineTrimmed(0);
        var line1 = snapshot.GetLineTrimmed(1);
        
        // With width=2, we get: ┌┐ on top, └┘ on bottom
        Assert.Equal("┌┐", line0);
        Assert.Equal("└┘", line1);
    }

    #endregion

    #region Theming Tests

    [Fact]
    public async Task Render_WithCustomTheme_UsesThemeCharacters()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
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
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hi") && s.ContainsText("╔"), TimeSpan.FromSeconds(1))
            .Build()
            .ApplyAsync(terminal);

        var screenText = terminal.CreateSnapshot().GetScreenText();
        Assert.Contains("╔", screenText);
        Assert.Contains("╗", screenText);
        Assert.Contains("╚", screenText);
        Assert.Contains("╝", screenText);
        Assert.Contains("═", screenText);
        Assert.Contains("║", screenText);
    }

    [Fact]
    public async Task Render_DoubleLineBorderTheme_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("╔════╗") && s.ContainsText("╚════╝"),
                TimeSpan.FromSeconds(1), "double line border")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var line0 = snapshot.GetLineTrimmed(0);
        var line1 = snapshot.GetLineTrimmed(1);
        var line2 = snapshot.GetLineTrimmed(2);

        Assert.Equal("╔════╗", line0);
        Assert.StartsWith("║", line1);
        Assert.EndsWith("║", line1);
        Assert.Equal("╚════╝", line2);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public async Task GetFocusableNodes_ReturnsFocusableChildren()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var node = new BorderNode { Child = textBox };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Single(focusables);
        Assert.Contains(textBox, focusables);
    }

    [Fact]
    public async Task GetFocusableNodes_WithNonFocusableChild_ReturnsEmpty()
    {
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var node = new BorderNode { Child = textBlock };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public async Task GetFocusableNodes_WithNoChild_ReturnsEmpty()
    {
        var node = new BorderNode { Child = null };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Empty(focusables);
    }

    [Fact]
    public async Task GetFocusableNodes_WithNestedContainers_FindsAllFocusables()
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
    public async Task IsFocusable_ReturnsFalse()
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
    public async Task HandleInput_WithNoChild_ReturnsFalse()
    {
        var node = new BorderNode { Child = null };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'A', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public async Task HandleInput_WithNonFocusedChild_ReturnsFalse()
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("Hello World"))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("Hello World"));
        // Check for border characters
        Assert.True(snapshot.ContainsText("┌"));
        Assert.True(snapshot.ContainsText("┐"));
        Assert.True(snapshot.ContainsText("└"));
        Assert.True(snapshot.ContainsText("┘"));
    }

    [Fact]
    public async Task Integration_BorderWithTitle_ShowsTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("Content")).Title("My Panel")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("My Panel"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("My Panel"));
        Assert.True(snapshot.ContainsText("Content"));
    }

    [Fact]
    public async Task Integration_BorderWithVStack_RendersMultipleLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(v => [
                    v.Text("Line 1"),
                    v.Text("Line 2"),
                    v.Text("Line 3")
                ]).Title("List")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("Line 1"));
        Assert.True(snapshot.ContainsText("Line 2"));
        Assert.True(snapshot.ContainsText("Line 3"));
        Assert.True(snapshot.ContainsText("List"));
    }

    [Fact]
    public async Task Integration_BorderWithTextBox_HandlesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.TextBox(text).OnTextChanged(args => text = args.NewText)).Title("Input")
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 15).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.Border(ctx.Text("Nested")).Title("Inner")
                ).Title("Outer")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Nested"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("Outer"));
        Assert.True(snapshot.ContainsText("Inner"));
        Assert.True(snapshot.ContainsText("Nested"));
    }

    [Fact]
    public async Task Integration_BorderInNarrowTerminal_TruncatesGracefully()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(12, 5).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text("VeryLongContentText")).Title("VeryLongTitleText")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app - wait for border content to render
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .WaitUntil(s => s.ContainsText("┌"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Border characters should be present - use captured snapshot
        Assert.True(snapshot.ContainsText("┌"));
        Assert.True(snapshot.ContainsText("┐"));
    }

    [Fact]
    public async Task Integration_MultipleBordersInHStack_RenderSideBySide()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(50, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Border(ctx.Text("Left")).Title("L"),
                    h.Border(ctx.Text("Right")).Title("R")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("Left"));
        Assert.True(snapshot.ContainsText("Right"));
        Assert.True(snapshot.ContainsText("L"));
        Assert.True(snapshot.ContainsText("R"));
    }

    [Fact]
    public async Task Integration_BorderWithButton_HandlesClick()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();
        var clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Button("Click Me").OnClick(_ => { clicked = true; return Task.CompletedTask; })).Title("Actions")
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 10).Build();
        var clickedButton = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(v => [
                    v.Button("First").OnClick(_ => { clickedButton = "First"; return Task.CompletedTask; }),
                    v.Button("Second").OnClick(_ => { clickedButton = "Second"; return Task.CompletedTask; })
                ]).Title("Buttons")
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

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
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Content"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.True(snapshot.ContainsText("Header"));
        Assert.True(snapshot.ContainsText("Content"));
        // Border characters should be present
        Assert.True(snapshot.ContainsText("┌"));
    }

    [Fact]
    public async Task Integration_EmptyBorder_RendersMinimalBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(ctx.Text(""))
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Should still have complete border - use captured snapshot
        Assert.True(snapshot.ContainsText("┌"));
        Assert.True(snapshot.ContainsText("┐"));
        Assert.True(snapshot.ContainsText("└"));
        Assert.True(snapshot.ContainsText("┘"));
    }

    #endregion
    
    #region Border Background Bug Tests

    /// <summary>
    /// Regression test for picker dropdown border showing white background.
    /// The border should use the default (empty) global background, not inherit
    /// a modified theme from a sibling ThemePanelNode.
    /// </summary>
    [Fact]
    public async Task BorderNode_UsesGlobalBackground_WhenThemeIsDefault()
    {
        // Arrange
        var theme = Hex1bThemes.Default;
        var node = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 10, 5));
        
        // Act - get global background from theme
        var globalBg = theme.GetGlobalBackground();
        var globalBgAnsi = globalBg.IsDefault ? "" : globalBg.ToBackgroundAnsi();
        
        // Assert - default theme should have default background (empty ANSI code)
        Assert.True(globalBg.IsDefault, "Default theme should have IsDefault=true for BackgroundColor");
        Assert.Equal("", globalBgAnsi);
    }

    /// <summary>
    /// Verifies that when a theme is mutated with a white global background,
    /// the mutated theme returns the correct white ANSI code.
    /// </summary>
    [Fact]
    public async Task ThemeMutation_SetsGlobalBackground_ReturnsCorrectAnsi()
    {
        // Arrange - Clone the theme to allow mutation
        var baseTheme = Hex1bThemes.Default.Clone();
        
        // Simulate InfoBar's invert behavior: set global background to white
        var mutatedTheme = baseTheme
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.White);
        
        // Act
        var originalBg = Hex1bThemes.Default.GetGlobalBackground();
        var mutatedBg = mutatedTheme.GetGlobalBackground();
        
        // Assert
        Assert.True(originalBg.IsDefault, "Original theme should have default background");
        Assert.False(mutatedBg.IsDefault, "Mutated theme should NOT have default background");
        Assert.Equal(255, mutatedBg.R);
        Assert.Equal(255, mutatedBg.G);
        Assert.Equal(255, mutatedBg.B);
    }

    /// <summary>
    /// Tests that ThemePanelNode correctly restores the theme after rendering.
    /// This is critical for siblings that render after the ThemePanelNode.
    /// </summary>
    [Fact]
    public async Task ThemePanelNode_RestoresTheme_AfterRendering()
    {
        // Arrange
        var surface = new Hex1b.Surfaces.Surface(20, 10);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface, Hex1bThemes.Default);
        
        var child = new TextBlockNode { Text = "Panel Content" };
        var themePanel = new ThemePanelNode
        {
            ThemeMutator = t => t.Set(GlobalTheme.BackgroundColor, Hex1bColor.White),
            Child = child
        };
        themePanel.Measure(new Constraints(20, 20, 10, 10));
        themePanel.Arrange(new Rect(0, 0, 20, 10));
        
        var themeBefore = context.Theme;
        var bgBefore = themeBefore.GetGlobalBackground();
        
        // Act
        themePanel.Render(context);
        
        // Assert - theme should be restored after render
        var themeAfter = context.Theme;
        var bgAfter = themeAfter.GetGlobalBackground();
        
        Assert.True(bgBefore.IsDefault, "Theme before should have default background");
        Assert.True(bgAfter.IsDefault, "Theme after should be restored to default background");
        Assert.Same(themeBefore, themeAfter);
    }

    /// <summary>
    /// Tests the full scenario: rendering a VStack with InfoBar (white bg theme)
    /// followed by a Border (should use default bg theme).
    /// </summary>
    [Fact]
    public async Task VStack_WithInfoBarThenBorder_BorderHasDefaultBackground()
    {
        // This simulates the bug scenario:
        // VStack contains:
        //   1. InfoBar with inverted colors (sets white global background via ThemePanelNode)
        //   2. Border (should NOT inherit the white background)
        
        // Arrange
        var surface = new Hex1b.Surfaces.Surface(40, 20);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface, Hex1bThemes.Default);
        
        // Create InfoBar-like ThemePanelNode that sets white background
        var infoBarContent = new TextBlockNode { Text = "Menu Bar" };
        var infoBarThemePanel = new ThemePanelNode
        {
            ThemeMutator = t => t.Set(GlobalTheme.BackgroundColor, Hex1bColor.White),
            Child = infoBarContent
        };
        
        // Create a Border (like the picker dropdown)
        var borderContent = new TextBlockNode { Text = "Dropdown" };
        var border = new BorderNode { Child = borderContent };
        
        // Create VStack to hold both
        var vstack = new VStackNode();
        infoBarThemePanel.Measure(new Constraints(40, 40, 1, 1));
        infoBarThemePanel.Arrange(new Rect(0, 0, 40, 1));
        infoBarThemePanel.Parent = vstack;
        
        border.Measure(new Constraints(10, 10, 5, 5));
        border.Arrange(new Rect(0, 2, 10, 5));
        border.Parent = vstack;
        
        // Act - render in order (simulate VStack.Render)
        context.RenderChild(infoBarThemePanel);
        
        // Check theme AFTER infobar renders
        var bgAfterInfoBar = context.Theme.GetGlobalBackground();
        
        context.RenderChild(border);
        
        // Assert
        // After infobar, theme should be restored to default
        Assert.True(bgAfterInfoBar.IsDefault, 
            "Theme should be restored after ThemePanelNode renders, but global background is still modified!");
    }

    /// <summary>
    /// Tests the actual border rendering to verify no white background is in the output.
    /// This catches the case where the border incorrectly gets a background color.
    /// </summary>
    [Fact]
    public async Task BorderNode_Render_NoBackgroundColorWhenDefaultTheme()
    {
        // Arrange
        var surface = new Hex1b.Surfaces.Surface(15, 7);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface, Hex1bThemes.Default);
        
        var border = new BorderNode { Child = new TextBlockNode { Text = "Test" } };
        border.Measure(new Constraints(15, 15, 7, 7));
        border.Arrange(new Rect(0, 0, 10, 5));
        
        // Act
        border.Render(context);
        
        // Assert - Check the top-left corner cell's background
        var topLeftCell = surface[0, 0];
        Assert.True(topLeftCell.Background == null, 
            $"Top-left corner should have null background, but has RGB({topLeftCell.Background?.R},{topLeftCell.Background?.G},{topLeftCell.Background?.B})");
        
        // Check a border character cell (horizontal line)
        var topBorderCell = surface[1, 0];
        Assert.True(topBorderCell.Background == null, 
            $"Top border should have null background, but has RGB({topBorderCell.Background?.R},{topBorderCell.Background?.G},{topBorderCell.Background?.B})");
    }

    /// <summary>
    /// Tests that after a ThemePanelNode renders with white background,
    /// a sibling BorderNode does NOT have white background in its cells.
    /// </summary>
    [Fact]
    public async Task BorderNode_AfterThemePanelRender_NoWhiteBackground()
    {
        // Arrange
        var surface = new Hex1b.Surfaces.Surface(40, 20);
        var context = new Hex1b.Surfaces.SurfaceRenderContext(surface, Hex1bThemes.Default);
        
        // First, render a ThemePanelNode that sets white background (like InfoBar)
        var infoBarContent = new TextBlockNode { Text = "Menu" };
        var infoBarThemePanel = new ThemePanelNode
        {
            ThemeMutator = t => t.Set(GlobalTheme.BackgroundColor, Hex1bColor.White),
            Child = infoBarContent
        };
        infoBarThemePanel.Measure(new Constraints(40, 40, 1, 1));
        infoBarThemePanel.Arrange(new Rect(0, 0, 40, 1));
        infoBarThemePanel.Render(context);
        
        // Now render a BorderNode at a different position (like picker dropdown)
        var border = new BorderNode { Child = new TextBlockNode { Text = "Dropdown" } };
        border.Measure(new Constraints(12, 12, 5, 5));
        border.Arrange(new Rect(5, 5, 12, 5));
        border.Render(context);
        
        // Assert - Border cells should NOT have white background
        var borderTopLeft = surface[5, 5];
        var isWhite = borderTopLeft.Background != null && 
                      borderTopLeft.Background.Value.R == 255 && 
                      borderTopLeft.Background.Value.G == 255 && 
                      borderTopLeft.Background.Value.B == 255;
        
        Assert.False(isWhite, 
            $"Border top-left should NOT have white background, but has RGB({borderTopLeft.Background?.R},{borderTopLeft.Background?.G},{borderTopLeft.Background?.B})");
    }

    #endregion

    #region Theme Fallback Chain Tests

    [Fact]
    public void ThemeFallback_ExplicitValueWins()
    {
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.TopLine, "━");
        
        Assert.Equal("━", theme.Get(BorderTheme.TopLine));
    }

    [Fact]
    public void ThemeFallback_FallsBackToParentElement()
    {
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.HorizontalLine, "═");
        
        // TopLine not set → falls back to HorizontalLine
        Assert.Equal("═", theme.Get(BorderTheme.TopLine));
        Assert.Equal("═", theme.Get(BorderTheme.BottomLine));
    }

    [Fact]
    public void ThemeFallback_ExplicitOverridesTakesPrecedence()
    {
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.TopLine, "━");
        
        // TopLine explicitly set → uses it; BottomLine falls back
        Assert.Equal("━", theme.Get(BorderTheme.TopLine));
        Assert.Equal("═", theme.Get(BorderTheme.BottomLine));
    }

    [Fact]
    public void ThemeFallback_DefaultThemeUsesDefaults()
    {
        var theme = new Hex1bTheme("Test");
        
        // Nothing set → falls through chain to DefaultValue
        Assert.Equal("─", theme.Get(BorderTheme.TopLine));
        Assert.Equal("─", theme.Get(BorderTheme.BottomLine));
        Assert.Equal("│", theme.Get(BorderTheme.LeftLine));
        Assert.Equal("│", theme.Get(BorderTheme.RightLine));
    }

    [Fact]
    public void ThemeFallback_ColorChainResolvesCorrectly()
    {
        var red = Hex1bColor.FromRgb(255, 0, 0);
        var blue = Hex1bColor.FromRgb(0, 0, 255);
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.BorderColor, red)
            .Set(BorderTheme.TopBorderColor, blue);
        
        // TopBorderColor explicitly set → blue
        Assert.Equal(blue, theme.Get(BorderTheme.TopBorderColor));
        // BottomBorderColor → HorizontalBorderColor → BorderColor → red
        Assert.Equal(red, theme.Get(BorderTheme.BottomBorderColor));
        // LeftBorderColor → VerticalBorderColor → BorderColor → red
        Assert.Equal(red, theme.Get(BorderTheme.LeftBorderColor));
    }

    [Fact]
    public void ThemeFallback_AxisColorOverridesBase()
    {
        var red = Hex1bColor.FromRgb(255, 0, 0);
        var green = Hex1bColor.FromRgb(0, 255, 0);
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.BorderColor, red)
            .Set(BorderTheme.HorizontalBorderColor, green);
        
        // Top/Bottom → HorizontalBorderColor → green
        Assert.Equal(green, theme.Get(BorderTheme.TopBorderColor));
        Assert.Equal(green, theme.Get(BorderTheme.BottomBorderColor));
        // Left/Right → VerticalBorderColor → BorderColor → red
        Assert.Equal(red, theme.Get(BorderTheme.LeftBorderColor));
        Assert.Equal(red, theme.Get(BorderTheme.RightBorderColor));
    }

    [Fact]
    public void ThemeFallback_VerticalLineChain()
    {
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.VerticalLine, "║");
        
        Assert.Equal("║", theme.Get(BorderTheme.LeftLine));
        Assert.Equal("║", theme.Get(BorderTheme.RightLine));
    }

    [Fact]
    public void ThemeFallback_PerSideVerticalOverride()
    {
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.VerticalLine, "║")
            .Set(BorderTheme.LeftLine, "┃");
        
        Assert.Equal("┃", theme.Get(BorderTheme.LeftLine));
        Assert.Equal("║", theme.Get(BorderTheme.RightLine));
    }

    #endregion

    #region Per-Side Border Rendering Tests

    [Fact]
    public async Task Render_WithDifferentTopAndBottomLines_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.TopLine, "━")
            .Set(BorderTheme.BottomLine, "═");
        var context = new Hex1bRenderContext(workload, theme);

        var node = new BorderNode { Child = new TextBlockNode { Text = "" } };
        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("━") && s.ContainsText("═"),
                TimeSpan.FromSeconds(1), "different top/bottom lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var line0 = snapshot.GetLineTrimmed(0);
        var line2 = snapshot.GetLineTrimmed(2);

        Assert.Contains("━", line0);
        Assert.Contains("═", line2);
        // Top uses ━, bottom uses ═
        Assert.DoesNotContain("═", line0);
        Assert.DoesNotContain("━", line2);
    }

    [Fact]
    public async Task Render_WithDifferentLeftAndRightLines_DrawsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.LeftLine, "┃")
            .Set(BorderTheme.RightLine, "║");
        var context = new Hex1bRenderContext(workload, theme);

        var node = new BorderNode { Child = new TextBlockNode { Text = "" } };
        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("┃") && s.ContainsText("║"),
                TimeSpan.FromSeconds(1), "different left/right lines")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var line1 = snapshot.GetLineTrimmed(1);

        Assert.StartsWith("┃", line1);
        Assert.EndsWith("║", line1);
    }

    [Fact]
    public async Task Render_HorizontalLineFallback_AppliesToBothTopAndBottom()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
        var theme = new Hex1bTheme("Test")
            .Set(BorderTheme.HorizontalLine, "═")
            .Set(BorderTheme.TopLeftCorner, "╔")
            .Set(BorderTheme.TopRightCorner, "╗")
            .Set(BorderTheme.BottomLeftCorner, "╚")
            .Set(BorderTheme.BottomRightCorner, "╝");
        var context = new Hex1bRenderContext(workload, theme);

        var node = new BorderNode { Child = new TextBlockNode { Text = "" } };
        node.Measure(Constraints.Tight(15, 5));
        node.Arrange(new Rect(0, 0, 6, 3));
        node.Render(context);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("╔════╗") && s.ContainsText("╚════╝"),
                TimeSpan.FromSeconds(1), "horizontal line fallback")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        var line0 = snapshot.GetLineTrimmed(0);
        var line2 = snapshot.GetLineTrimmed(2);

        Assert.Equal("╔════╗", line0);
        Assert.Equal("╚════╝", line2);
    }

    #endregion
}
