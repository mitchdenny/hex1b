using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
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
            Overflow = TextOverflow.Truncate 
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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Hello World" };

        node.Render(context);

        Assert.Equal("Hello World", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_EmptyText_WritesNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "" };

        node.Render(context);

        Assert.Equal("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_SpecialCharacters_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Hello → World ← Test" };

        node.Render(context);

        Assert.Equal("Hello → World ← Test", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_InNarrowTerminal_TextIsTruncatedByTerminalWidth()
    {
        // Terminal is only 10 chars wide - text will wrap/truncate at terminal boundary
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "This is a long text" };

        node.Render(context);

        // The first line should contain the first 10 characters
        Assert.Equal("This is a ", terminal.CreateSnapshot().GetLine(0));
        // The rest wraps to the next line (terminal behavior, not widget)
        Assert.Equal("long text", terminal.CreateSnapshot().GetLineTrimmed(1));
    }

    [Fact]
    public void Render_AtSpecificPosition_WritesAtCursorPosition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Positioned" };

        context.SetCursorPosition(5, 3);
        node.Render(context);

        // Check that text appears at the right position
        var line = terminal.CreateSnapshot().GetLine(3);
        Assert.Equal("     Positioned", line.TrimEnd());
    }

    [Fact]
    public void Render_VeryLongText_WrapsAtTerminalEdge()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = new Hex1bRenderContext(workload);
        var longText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var node = new TextBlockNode { Text = longText };

        node.Render(context);

        // First 20 chars on line 0
        Assert.Equal("ABCDEFGHIJKLMNOPQRST", terminal.CreateSnapshot().GetLine(0));
        // Remaining chars on line 1
        Assert.Equal("UVWXYZ", terminal.CreateSnapshot().GetLineTrimmed(1));
    }

    #endregion

    #region Clipping Tests

    [Fact]
    public void Render_WithLayoutProvider_ClipsToClipRect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var context = new Hex1bRenderContext(workload);
        
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
        Assert.Equal("Hello Worl", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithLayoutProvider_ClipsWhenStartingOutsideClipRect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var context = new Hex1bRenderContext(workload);
        
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
        var line = terminal.CreateSnapshot().GetLine(0);
        Assert.Equal("     FGHIJKLMNO", line.Substring(0, 15));
    }

    [Fact]
    public void Render_WithLayoutProviderOverflow_DoesNotClip()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var context = new Hex1bRenderContext(workload);
        
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
        Assert.Equal("Hello World", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithoutLayoutProvider_DoesNotClip()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 10);
        var context = new Hex1bRenderContext(workload);
        
        // No layout provider
        var node = new TextBlockNode { Text = "Hello World" };
        
        node.Render(context);
        
        // Full text should appear
        Assert.Equal("Hello World", terminal.CreateSnapshot().GetLineTrimmed(0));
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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("Integration Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Integration Test"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Integration Test"));
    }

    [Fact]
    public async Task Integration_MultipleTextBlocks_InVStack_RenderOnSeparateLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("First Line"),
                    v.Text("Second Line"),
                    v.Text("Third Line")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third Line"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("First Line"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Second Line"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Third Line"));

        // Verify they appear at different positions
        var firstPositions = terminal.CreateSnapshot().FindText("First Line");
        var secondPositions = terminal.CreateSnapshot().FindText("Second Line");
        var thirdPositions = terminal.CreateSnapshot().FindText("Third Line");

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
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var counter = 0;

        using var app = new Hex1bApp(
            ctx =>
            {
                counter++;
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        v.Text($"Counter: {counter}"),
                        v.Button("Increment").OnClick(_ => Task.CompletedTask /* counter increments on next render */)
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // Wait for initial render, Enter to click button (it should have focus), wait for re-render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Counter: 1"), TimeSpan.FromSeconds(2))
            .Enter() // Click the button (should already have focus as only focusable widget)
            .WaitUntil(s => s.ContainsText("Counter: 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The button was clicked, counter was incremented. May be higher than 2 due to Ctrl+C triggering exit render.
        Assert.True(counter >= 2, $"Expected counter >= 2 but was {counter}");
    }

    [Fact]
    public async Task Integration_TextBlock_InNarrowTerminal_RendersCorrectly()
    {
        // Very narrow terminal - 15 chars wide
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Text("Short"),
                    v.Text("A longer text here")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Short"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // "Short" should fit on its line
        Assert.True(terminal.CreateSnapshot().ContainsText("Short"));
        // Long text will wrap at terminal edge
        Assert.True(terminal.CreateSnapshot().ContainsText("A longer text h"));
    }

    [Fact]
    public async Task Integration_TextBlock_WithDynamicState_ShowsCurrentValue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var message = "Hello from State";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Text(message)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello from State"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Hello from State"));
    }

    [Fact]
    public async Task Integration_TextBlock_EmptyString_DoesNotCrash()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // No specific text to wait for, just give it time to render then exit
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(50))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should complete without error (test passes if no exception thrown)
    }

    [Fact]
    public async Task Integration_TextBlock_UnicodeContent_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("日本語テスト 🎉 émojis")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("日本語テスト"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("日本語テスト"));
        Assert.True(terminal.CreateSnapshot().ContainsText("🎉"));
    }

    #endregion
}
