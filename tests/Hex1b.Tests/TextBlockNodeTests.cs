using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBlockNode rendering using Hex1bTerminal.
/// </summary>
[TestClass]
public class TextBlockNodeTests
{
    #region Measurement Tests

    [TestMethod]
    public async Task Measure_ReturnsCorrectSize()
    {
        var node = new TextBlockNode { Text = "Hello World" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(11, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Measure_EmptyText_ReturnsZeroWidth()
    {
        var node = new TextBlockNode { Text = "" };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(0, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBlockNode { Text = "This is a very long text that exceeds constraints" };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.AreEqual(10, size.Width);
    }

    [TestMethod]
    public async Task Measure_RespectsMinWidthConstraint()
    {
        var node = new TextBlockNode { Text = "Hi" };

        var size = node.Measure(new Constraints(10, 20, 0, 5));

        Assert.AreEqual(10, size.Width);
    }

    #endregion

    #region Wrapping Tests

    [TestMethod]
    public async Task Measure_WithWrap_CalculatesMultipleLines()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World from Hex1b", 
            Overflow = TextOverflow.Wrap 
        };

        // Constrain to 10 chars wide - should wrap into 3 lines
        var size = node.Measure(new Constraints(0, 10, 0, int.MaxValue));

        // "Hello" (5), "World from" (10), "Hex1b" (5) - needs multiple lines
        Assert.IsTrue(size.Height > 1, $"Expected height > 1, got {size.Height}");
        Assert.IsTrue(size.Width <= 10, $"Expected width <= 10, got {size.Width}");
    }

    [TestMethod]
    public async Task Measure_WithWrap_SingleLineWhenFits()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello", 
            Overflow = TextOverflow.Wrap 
        };

        var size = node.Measure(new Constraints(0, 40, 0, int.MaxValue));

        Assert.AreEqual(1, size.Height);
        Assert.AreEqual(5, size.Width);
    }

    [TestMethod]
    public async Task Measure_WithWrap_UnboundedWidth_SingleLine()
    {
        var node = new TextBlockNode 
        { 
            Text = "This is a very long text that should not wrap", 
            Overflow = TextOverflow.Wrap 
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.AreEqual(1, size.Height);
        Assert.AreEqual(45, size.Width);
    }

    [TestMethod]
    public async Task Measure_WithWrap_PrefersWordBoundaries()
    {
        var node = new TextBlockNode
        {
            Text = "Hello world",
            Overflow = TextOverflow.Wrap
        };

        // Width 7 can't fit "Hello w" without splitting "world", so it should wrap at the space.
        var size = node.Measure(new Constraints(0, 7, 0, int.MaxValue));

        Assert.AreEqual(2, size.Height);
        Assert.IsTrue(size.Width <= 7, $"Expected width <= 7, got {size.Width}");
    }

    [TestMethod]
    public async Task Measure_WithEllipsis_RespectsMaxWidth()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World from Hex1b", 
            Overflow = TextOverflow.Ellipsis 
        };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.AreEqual(10, size.Width);
        Assert.AreEqual(1, size.Height);
    }

    [TestMethod]
    public async Task Measure_WithOverflow_IgnoresConstraint()
    {
        var node = new TextBlockNode 
        { 
            Text = "Hello World", 
            Overflow = TextOverflow.Truncate 
        };

        // Default behavior - width is clamped but not reduced naturally
        var size = node.Measure(new Constraints(0, 5, 0, 5));

        Assert.AreEqual(5, size.Width);  // Clamped by Constrain()
        Assert.AreEqual(1, size.Height);
    }

    #endregion

    #region Rendering Tests

    [TestMethod]
    public async Task Render_WritesTextToTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Hello World" };

        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "Hello World text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.AreEqual("Hello World", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_EmptyText_WritesNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "" };

        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        Assert.AreEqual("", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_SpecialCharacters_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Hello → World ← Test" };

        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello → World ← Test"), TimeSpan.FromSeconds(5), "special characters text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.AreEqual("Hello → World ← Test", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_InNarrowTerminal_TextIsTruncatedByTerminalWidth()
    {
        // Terminal is only 10 chars wide - text will wrap/truncate at terminal boundary
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "This is a long text" };

        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("This is a") && s.ContainsText("long text"), TimeSpan.FromSeconds(5), "wrapped text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // The first line should contain the first 10 characters
        Assert.AreEqual("This is a ", snapshot.GetLine(0));
        // The rest wraps to the next line (terminal behavior, not widget)
        Assert.AreEqual("long text", snapshot.GetLineTrimmed(1));
    }

    [TestMethod]
    public async Task Render_AtSpecificPosition_WritesAtCursorPosition()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBlockNode { Text = "Positioned" };

        context.SetCursorPosition(5, 3);
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Positioned"), TimeSpan.FromSeconds(5), "Positioned text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Check that text appears at the right position
        var line = snapshot.GetLine(3);
        Assert.AreEqual("     Positioned", line.TrimEnd());
    }

    [TestMethod]
    public async Task Render_VeryLongText_WrapsAtTerminalEdge()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var longText = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var node = new TextBlockNode { Text = longText };

        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ABCDEFGHIJKLMNOPQRST") && s.ContainsText("UVWXYZ"), TimeSpan.FromSeconds(5), "wrapped alphabet to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // First 20 chars on line 0
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRST", snapshot.GetLine(0));
        // Remaining chars on line 1
        Assert.AreEqual("UVWXYZ", snapshot.GetLineTrimmed(1));
    }

    #endregion

    #region Clipping Tests

    [TestMethod]
    public async Task Render_WithLayoutProvider_ClipsToClipRect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello Worl"), TimeSpan.FromSeconds(5), "clipped text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Text should be clipped to 10 characters
        Assert.AreEqual("Hello Worl", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_WithLayoutProvider_ClipsWhenStartingOutsideClipRect()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("FGHIJKLMNO"), TimeSpan.FromSeconds(5), "clipped alphabet section to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Only chars from index 5-14 should appear (FGHIJKLMNO), at positions 5-14
        var line = snapshot.GetLine(0);
        Assert.AreEqual("     FGHIJKLMNO", line.Substring(0, 15));
    }

    [TestMethod]
    public async Task Render_WithLayoutProviderOverflow_DoesNotClip()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "full Hello World text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Full text should appear (no clipping)
        Assert.AreEqual("Hello World", snapshot.GetLineTrimmed(0));
    }

    [TestMethod]
    public async Task Render_WithoutLayoutProvider_DoesNotClip()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var context = new Hex1bRenderContext(workload);
        
        // No layout provider
        var node = new TextBlockNode { Text = "Hello World" };
        
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello World"), TimeSpan.FromSeconds(5), "Hello World text to appear")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Full text should appear
        Assert.AreEqual("Hello World", snapshot.GetLineTrimmed(0));
    }

    #endregion

    #region Layout Tests

    [TestMethod]
    public async Task Arrange_SetsBounds()
    {
        var node = new TextBlockNode { Text = "Test" };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.AreEqual(bounds, node.Bounds);
    }

    #endregion

    #region Focus and Input Tests

    [TestMethod]
    public async Task IsFocusable_ReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        Assert.IsFalse(node.IsFocusable);
    }

    [TestMethod]
    public async Task HandleInput_AlwaysReturnsFalse()
    {
        var node = new TextBlockNode { Text = "Test" };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));

        Assert.AreEqual(InputResult.NotHandled, result);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [TestMethod]
    public async Task Integration_TextBlockWidget_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("Integration Test")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Integration Test"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(snapshot.ContainsText("Integration Test"));
    }

    [TestMethod]
    public async Task Integration_MultipleTextBlocks_InVStack_RenderOnSeparateLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

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
        
        // Capture snapshot BEFORE exiting the app (while alternate screen is active)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Third Line"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use the captured snapshot for assertions (app has exited, alternate screen restored)
        Assert.IsTrue(snapshot.ContainsText("First Line"));
        Assert.IsTrue(snapshot.ContainsText("Second Line"));
        Assert.IsTrue(snapshot.ContainsText("Third Line"));

        // Verify they appear at different positions
        var firstPositions = snapshot.FindText("First Line");
        var secondPositions = snapshot.FindText("Second Line");
        var thirdPositions = snapshot.FindText("Third Line");

        TestSeq.Single(firstPositions);
        TestSeq.Single(secondPositions);
        TestSeq.Single(thirdPositions);

        // Each should be on a different line
        Assert.AreNotEqual(firstPositions[0].Line, secondPositions[0].Line);
        Assert.AreNotEqual(secondPositions[0].Line, thirdPositions[0].Line);
    }

    [TestMethod]
    public async Task Integration_TextBlock_WithStateChange_UpdatesOnReRender()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Counter: 1"), TimeSpan.FromSeconds(5))
            .Enter() // Click the button (should already have focus as only focusable widget)
            .WaitUntil(s => s.ContainsText("Counter: 2"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The button was clicked, counter was incremented. May be higher than 2 due to Ctrl+C triggering exit render.
        Assert.IsTrue(counter >= 2, $"Expected counter >= 2 but was {counter}");
    }

    [TestMethod]
    public async Task Integration_TextBlock_InNarrowTerminal_RendersCorrectly()
    {
        // Very narrow terminal - 15 chars wide
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();

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
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Short"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        // "Short" should fit on its line
        Assert.IsTrue(snapshot.ContainsText("Short"));
        // Long text will wrap at terminal edge
        Assert.IsTrue(snapshot.ContainsText("A longer text h"));
    }

    [TestMethod]
    public async Task Integration_TextBlock_WithDynamicState_ShowsCurrentValue()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var message = "Hello from State";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Text(message)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello from State"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.IsTrue(snapshot.ContainsText("Hello from State"));
    }

    [TestMethod]
    public async Task Integration_TextBlock_EmptyString_DoesNotCrash()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

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

    [TestMethod]
    public async Task Integration_TextBlock_UnicodeContent_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Text("日本語テスト 🎉 émojis")),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting the app
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("日本語テスト"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        await runTask;

        // Use captured snapshot for assertions
        Assert.IsTrue(snapshot.ContainsText("日本語テスト"));
        Assert.IsTrue(snapshot.ContainsText("🎉"));
    }

    #endregion
}
