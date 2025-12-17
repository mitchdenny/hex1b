using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBlockNode rendering using Hex1bTerminal.
/// </summary>
public class TextBlockNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new TextBlockNode { Text = "Hello World" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(11, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_ReturnsZeroWidth()
    {
        var node = new TextBlockNode { Text = "" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBlockNode { Text = "This is a very long text that exceeds constraints" };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
    }

    [Fact]
    public void Measure_RespectsMinWidthConstraint()
    {
        var node = new TextBlockNode { Text = "Hi" };

        var size = node.Measure(new Constraints(10, 20, 0, 5));

        Assert.Equal(10, size.Width);
    }

    #endregion

    #region Wrapping Tests

    [Fact]
    public void Measure_WithWrap_CalculatesMultipleLines()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World from Hex1b", 
            Overflow = TextOverflow.Wrap 
        };

        // Constrain to 10 chars wide - should wrap into 3 lines
        var size = node.Measure(new Constraints(0, 10, 0, int.MaxValue));

        // "Hello" (5), "World from" (10), "Hex1b" (5) - needs multiple lines
        Assert.True(size.Height > 1, $"Expected height > 1, got {size.Height}");
        Assert.True(size.Width <= 10, $"Expected width <= 10, got {size.Width}");
    }

    [Fact]
    public void Measure_WithWrap_SingleLineWhenFits()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello", 
            Overflow = TextOverflow.Wrap 
        };

        var size = node.Measure(new Constraints(0, 40, 0, int.MaxValue));

        Assert.Equal(1, size.Height);
        Assert.Equal(5, size.Width);
    }

    [Fact]
    public void Measure_WithWrap_UnboundedWidth_SingleLine()
    {
        var node = new TextBlockNode 
        { 
            Text = "This is a very long text that should not wrap", 
            Overflow = TextOverflow.Wrap 
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(1, size.Height);
        Assert.Equal(45, size.Width);
    }

    [Fact]
    public void Measure_WithWrap_PrefersWordBoundaries()
    {
        var node = new TextBlockNode
        {
            Text = "Hello world",
            Overflow = TextOverflow.Wrap
        };

        // Width 7 can't fit "Hello w" without splitting "world", so it should wrap at the space.
        var size = node.Measure(new Constraints(0, 7, 0, int.MaxValue));

        Assert.Equal(2, size.Height);
        Assert.True(size.Width <= 7, $"Expected width <= 7, got {size.Width}");
    }

    [Fact]
    public void Measure_WithEllipsis_RespectsMaxWidth()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World from Hex1b", 
            Overflow = TextOverflow.Ellipsis 
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_WithOverflow_IgnoresConstraint()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World", 
            Overflow = TextOverflow.Overflow 
        };

        // Default behavior - width is clamped but not reduced naturally
        var size = node.Measure(new Constraints(0, 5, 0, 5));

        Assert.Equal(5, size.Width);  // Clamped by Constrain()
        Assert.Equal(1, size.Height);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_WritesTextToTerminal()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Hello World" };

        node.Render(context);

        Assert.Equal("Hello World", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_EmptyText_WritesNothing()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "" };

        node.Render(context);

        Assert.Equal("", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_SpecialCharacters_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Hello → World ← Test" };

        node.Render(context);

        Assert.Equal("Hello → World ← Test", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_InNarrowTerminal_TextIsTruncatedByTerminalWidth()
    {
        // Terminal is only 10 chars wide - text will wrap/truncate at terminal boundary
        using var terminal = new Hex1bTerminal(10, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "This is a long text" };

        node.Render(context);

        // The first line should contain the first 10 characters
        Assert.Equal("This is a ", terminal.GetLine(0));
        // The rest wraps to the next line (terminal behavior, not widget)
        Assert.Equal("long text", terminal.GetLineTrimmed(1));
    }

    [Fact]
    public void Render_AtSpecificPosition_WritesAtCursorPosition()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBlockNode { Text = "Positioned" };

        context.SetCursorPosition(5, 3);
        node.Render(context);

        // Check that text appears at the right position
        var line = terminal.GetLine(3);
        Assert.Equal("     Positioned", line.TrimEnd());
    }

    [Fact]
    public void Render_VeryLongText_WrapsAtTerminalEdge()
    {
        using var terminal = new Hex1bTerminal(20, 5);
        var context = new Hex1bRenderContext(terminal);
        var longText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var node = new TextBlockNode { Text = longText };

        node.Render(context);

        // First 20 chars on line 0
        Assert.Equal("ABCDEFGHIJKLMNOPQRST", terminal.GetLine(0));
        // Remaining chars on line 1
        Assert.Equal("UVWXYZ", terminal.GetLineTrimmed(1));
    }

    #endregion

    #region Clipping Tests

    [Fact]
    public void Render_WithLayoutProvider_ClipsToClipRect()
    {
        using var terminal = new Hex1bTerminal(80, 10);
        var context = new Hex1bRenderContext(terminal);
        
        // Create a LayoutNode that will clip to a 10-char wide region
        var layoutNode = new Hex1b.Nodes.LayoutNode
        {
            ClipMode = Hex1b.Widgets.ClipMode.Clip
        };
        layoutNode.Arrange(new Rect(0, 0, 10, 5));
        
        // Set layout provider on context
        context.CurrentLayoutProvider = layoutNode;
        
        var node = new TextBlockNode { Text = "Hello World - This is long text" };
        node.Arrange(new Rect(0, 0, 10, 1));
        
        node.Render(context);
        
        // Text should be clipped to 10 characters
        Assert.Equal("Hello Worl", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithLayoutProvider_ClipsWhenStartingOutsideClipRect()
    {
        using var terminal = new Hex1bTerminal(80, 10);
        var context = new Hex1bRenderContext(terminal);
        
        // Layout clips from x=5 to x=15 (width 10)
        var layoutNode = new Hex1b.Nodes.LayoutNode
        {
            ClipMode = Hex1b.Widgets.ClipMode.Clip
        };
        layoutNode.Arrange(new Rect(5, 0, 10, 5));
        context.CurrentLayoutProvider = layoutNode;
        
        // Text starts at x=0, which is outside the clip rect (starts at x=5)
        var node = new TextBlockNode { Text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" };
        node.Arrange(new Rect(0, 0, 26, 1));
        
        node.Render(context);
        
        // Only chars from index 5-14 should appear (FGHIJKLMNO), at positions 5-14
        var line = terminal.GetLine(0);
        Assert.Equal("     FGHIJKLMNO", line.Substring(0, 15));
    }

    [Fact]
    public void Render_WithLayoutProviderOverflow_DoesNotClip()
    {
        using var terminal = new Hex1bTerminal(80, 10);
        var context = new Hex1bRenderContext(terminal);
        
        // Layout with Overflow mode - should not clip
        var layoutNode = new Hex1b.Nodes.LayoutNode
        {
            ClipMode = Hex1b.Widgets.ClipMode.Overflow
        };
        layoutNode.Arrange(new Rect(0, 0, 10, 5));
        context.CurrentLayoutProvider = layoutNode;
        
        var node = new TextBlockNode { Text = "Hello World" };
        node.Arrange(new Rect(0, 0, 20, 1));
        
        node.Render(context);
        
        // Full text should appear (no clipping)
        Assert.Equal("Hello World", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithoutLayoutProvider_DoesNotClip()
    {
        using var terminal = new Hex1bTerminal(80, 10);
        var context = new Hex1bRenderContext(terminal);
        
        // No layout provider
        var node = new TextBlockNode { Text = "Hello World" };
        
        node.Render(context);
        
        // Full text should appear
        Assert.Equal("Hello World", terminal.GetLineTrimmed(0));
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new TextBlockNode { Text = "Test" };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Focus and Input Tests

    [Fact]
    public void IsFocusable_ReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        Assert.False(node.IsFocusable);
    }

    [Fact]
    public void HandleInput_AlwaysReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_TextBlockWidget_RendersViaHex1bApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("Integration Test")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Integration Test"));
    }

    [Fact]
    public async Task Integration_MultipleTextBlocks_InVStack_RenderOnSeparateLines()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("First Line"),
                    v.Text("Second Line"),
                    v.Text("Third Line")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("First Line"));
        Assert.True(terminal.ContainsText("Second Line"));
        Assert.True(terminal.ContainsText("Third Line"));

        // Verify they appear at different positions
        var firstPositions = terminal.FindText("First Line");
        var secondPositions = terminal.FindText("Second Line");
        var thirdPositions = terminal.FindText("Third Line");

        Assert.Single(firstPositions);
        Assert.Single(secondPositions);
        Assert.Single(thirdPositions);

        // Each should be on a different line
        Assert.NotEqual(firstPositions[0].Line, secondPositions[0].Line);
        Assert.NotEqual(secondPositions[0].Line, thirdPositions[0].Line);
    }

    [Fact]
    public async Task Integration_TextBlock_WithStateChange_UpdatesOnReRender()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var counter = 0;

        using var app = new Hex1bApp<object>(
            new object(),
            ctx =>
            {
                counter++;
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        v.Text($"Counter: {counter}"),
                        v.Button("Increment", () => { /* counter increments on next render */ })
                    ])
                );
            },
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Press Enter to trigger button (causes re-render)
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        // After button press and re-render, counter should be 2
        Assert.True(terminal.ContainsText("Counter: 2"));
    }

    [Fact]
    public async Task Integration_TextBlock_InNarrowTerminal_RendersCorrectly()
    {
        // Very narrow terminal - 15 chars wide
        using var terminal = new Hex1bTerminal(15, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Short"),
                    v.Text("A longer text here")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // "Short" should fit on its line
        Assert.True(terminal.ContainsText("Short"));
        // Long text will wrap at terminal edge
        Assert.True(terminal.ContainsText("A longer text h"));
    }

    [Fact]
    public async Task Integration_TextBlock_WithDynamicState_ShowsCurrentValue()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var message = "Hello from State";

        using var app = new Hex1bApp<string>(
            message,
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Text(s => s)
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Hello from State"));
    }

    [Fact]
    public async Task Integration_TextBlock_EmptyString_DoesNotCrash()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should complete without error
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task Integration_TextBlock_UnicodeContent_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("日本語テスト 🎉 émojis")),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("日本語テスト"));
        Assert.True(terminal.ContainsText("🎉"));
    }

    #endregion
}
