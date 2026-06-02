using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for ScrollPanelNode layout, rendering, scrolling, and focus handling.
/// </summary>
[TestClass]
public class ScrollPanelNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    private static VStackNode CreateTallContent(int lineCount)
    {
        var children = new List<Hex1bNode>();
        for (int i = 0; i < lineCount; i++)
        {
            children.Add(new TextBlockNode { Text = $"Line {i + 1}" });
        }
        return new VStackNode { Children = children };
    }

    private static HStackNode CreateWideContent(int columnCount)
    {
        var children = new List<Hex1bNode>();
        for (int i = 0; i < columnCount; i++)
        {
            children.Add(new TextBlockNode { Text = $"Col{i + 1} " });
        }
        return new HStackNode { Children = children };
    }

    #region ScrollPanelNode Internal State Tests

    [TestMethod]
    public async Task ScrollNode_InitialState_IsZero()
    {
        var node = new ScrollPanelNode();

        Assert.AreEqual(0, node.Offset);
        Assert.AreEqual(0, node.ContentSize);
        Assert.AreEqual(0, node.ViewportSize);
    }

    [TestMethod]
    public async Task ScrollNode_IsScrollable_WhenContentExceedsViewport()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        // After arrange, ContentSize and ViewportSize should be set
        Assert.IsTrue(node.ContentSize > node.ViewportSize);
    }

    [TestMethod]
    public async Task ScrollNode_MaxOffset_IsCorrect()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(25),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        // MaxOffset = ContentSize - ViewportSize = 25 - 10 = 15
        Assert.AreEqual(15, node.ContentSize - node.ViewportSize);
    }

    #endregion

    #region Measurement Tests - Vertical Scroll

    [TestMethod]
    public async Task Measure_Vertical_ContentSmallerThanViewport_ReturnsFitSize()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(5),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Content is 5 lines, viewport allows 20, so no need for full space
        Assert.IsTrue(size.Height <= 20);
        Assert.AreEqual(5, node.ContentSize);
    }

    [TestMethod]
    public async Task Measure_Vertical_ContentLargerThanViewport_UsesConstraints()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        Assert.AreEqual(20, size.Height);
        Assert.AreEqual(50, node.ContentSize);
    }

    [TestMethod]
    public async Task Measure_Vertical_IncludesScrollbarWidth()
    {
        var child = new TextBlockNode { Text = "Content" };
        var node = new ScrollPanelNode
        {
            Child = child,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };
        // Set internal state to force scrollable
        node.Measure(Constraints.Tight(30, 20));
        node.Arrange(new Rect(0, 0, 30, 20));

        // Create a new node with content that exceeds viewport
        var scrollableNode = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = scrollableNode.Measure(new Constraints(0, 30, 0, 20));

        // Width should include space for scrollbar
        Assert.IsTrue(size.Width > 0);
    }

    [TestMethod]
    public async Task Measure_Vertical_NoScrollbar_DoesNotIncludeScrollbarWidth()
    {
        var child = new TextBlockNode { Text = "Content" };
        var node = new ScrollPanelNode
        {
            Child = child,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = false
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Width should be just content width
        Assert.AreEqual(7, size.Width);
    }

    #endregion

    #region Measurement Tests - Horizontal Scroll

    [TestMethod]
    public async Task Measure_Horizontal_ContentWiderThanViewport_UsesConstraints()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.AreEqual(40, size.Width);
    }

    [TestMethod]
    public async Task Measure_Horizontal_IncludesScrollbarHeight()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(50),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 20, 0, 10));

        // Height should include scrollbar (1)
        Assert.IsTrue(size.Height >= 2);
    }

    #endregion

    #region Arrange Tests - Vertical Scroll

    [TestMethod]
    public async Task Arrange_Vertical_SetsViewportSize()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        Assert.AreEqual(10, node.ViewportSize);
    }

    [TestMethod]
    public async Task Arrange_Vertical_ChildPositionedWithOffset()
    {
        var child = CreateTallContent(20);
        var node = new ScrollPanelNode
        {
            Child = child,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        
        // Scroll down 5 lines
        node.SetOffset(5);
        node.Arrange(new Rect(0, 0, 30, 10));

        // Child should be positioned above the viewport by the offset
        Assert.AreEqual(-5, child.Bounds.Y);
    }

    [TestMethod]
    public async Task Arrange_Vertical_ClampsOffsetToMaxOffset()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        
        // Try to set offset beyond max
        node.SetOffset(100);

        // Should be clamped to max (20 - 10 = 10)
        Assert.AreEqual(10, node.Offset);
    }

    #endregion

    #region Arrange Tests - Horizontal Scroll

    [TestMethod]
    public async Task Arrange_Horizontal_SetsViewportSize()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        // Viewport width is 30 (minus scrollbar height doesn't affect width)
        Assert.AreEqual(30, node.ViewportSize);
    }

    [TestMethod]
    public async Task Arrange_Horizontal_ChildPositionedWithOffset()
    {
        var child = CreateWideContent(20);
        var node = new ScrollPanelNode
        {
            Child = child,
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        
        // Scroll right 10 units
        node.SetOffset(10);
        node.Arrange(new Rect(0, 0, 30, 10));

        // Child should be positioned to the left of the viewport by the offset
        Assert.AreEqual(-10, child.Bounds.X);
    }

    #endregion

    #region Rendering Tests - Vertical Scrollbar

    [TestMethod]
    public async Task Render_Vertical_ShowsScrollbar_WhenContentExceedsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = CreateContext(workload);
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▉"), TimeSpan.FromSeconds(5), "scrollbar to render")
            .Build()
            .ApplyAsync(terminal);

        // Should contain scrollbar thumb character
        Assert.Contains("▉", terminal.CreateSnapshot().GetText());
    }

    [TestMethod]
    public async Task Render_Vertical_ShowsThumbAndTrack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var context = CreateContext(workload);
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("│") && s.ContainsText("▉"), TimeSpan.FromSeconds(5), "vertical scrollbar to render")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(snapshot.ContainsText("│"), "Should show track");
        Assert.IsTrue(snapshot.ContainsText("▉"), "Should show thumb");
        // No arrows in the new minimal style
        Assert.DoesNotContain("▲", snapshot.GetText());
        Assert.DoesNotContain("▼", snapshot.GetText());
    }

    [TestMethod]
    public async Task Render_Vertical_NoScrollbar_WhenContentFitsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 20).Build();
        var context = CreateContext(workload);
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(5),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 20));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1") && !s.ContainsText("▉"),
                TimeSpan.FromSeconds(1), "content fits without scrollbar")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Content fits, so no scrollbar needed
        Assert.DoesNotContain("▉", snapshot.GetText());
    }

    [TestMethod]
    public async Task Render_Vertical_ClipsContentBeyondViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();

        await using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(v => [
                v.Text("Line 1"),
                v.Text("Line 2"),
                v.Text("Line 3"),
                v.Text("Line 4"),
                v.Text("Line 5"),
                v.Text("Line 6"),
                v.Text("Line 7"),
                v.Text("Line 8"),
                v.Text("Line 9"),
                v.Text("Line 10"),
            ], showScrollbar: true),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1") && s.ContainsText("Line 2") && !s.ContainsText("Line 6"),
                TimeSpan.FromSeconds(2), "Lines 1-2 visible, Line 6 clipped")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // First lines should be visible, later lines should be clipped
        Assert.Contains("Line 1", snapshot.GetText());
        Assert.Contains("Line 2", snapshot.GetText());
        // Line 6+ should definitely be clipped (viewport is 5 rows)
        Assert.DoesNotContain("Line 6", snapshot.GetText());
    }

    [TestMethod]
    public async Task Render_Vertical_WhenScrolled_ShowsOffsetContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();

        await using var app = new Hex1bApp(
            ctx => ctx.VScrollPanel(v => [
                v.Text("Line 1"),
                v.Text("Line 2"),
                v.Text("Line 3"),
                v.Text("Line 4"),
                v.Text("Line 5"),
                v.Text("Line 6"),
                v.Text("Line 7"),
                v.Text("Line 8"),
                v.Text("Line 9"),
                v.Text("Line 10"),
                v.Text("Line 11"),
                v.Text("Line 12"),
                v.Text("Line 13"),
                v.Text("Line 14"),
                v.Text("Line 15"),
            ], showScrollbar: true),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Scroll down and capture BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(5))
            // Scroll down 5 times to show Line 6, waiting for each scroll to render
            .Down()
            .WaitUntil(s => s.ContainsText("Line 2"), TimeSpan.FromSeconds(5), "Line 2 visible after 1st Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(5), "Line 3 visible after 2nd Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 4"), TimeSpan.FromSeconds(5), "Line 4 visible after 3rd Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 5"), TimeSpan.FromSeconds(5), "Line 5 visible after 4th Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 6") && s.ContainsText("Line 7"),
                TimeSpan.FromSeconds(2), "Lines 6 and 7 visible after 5th Down")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Lines starting from offset should include Line 6 and 7
        Assert.Contains("Line 6", snapshot.GetText());
        Assert.Contains("Line 7", snapshot.GetText());
    }

    #endregion

    #region Rendering Tests - Horizontal Scrollbar

    [TestMethod]
    public async Task Render_Horizontal_ShowsScrollbar_WhenContentExceedsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(10),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("■"), TimeSpan.FromSeconds(5), "horizontal scrollbar to render")
            .Build()
            .ApplyAsync(terminal);

        // Should contain scrollbar thumb character (horizontal uses ■, not ▉)
        Assert.Contains("■", terminal.CreateSnapshot().GetText());
    }

    [TestMethod]
    public async Task Render_Horizontal_ShowsThumbAndTrack()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var context = CreateContext(workload);
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(10),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("─") && s.ContainsText("■"), TimeSpan.FromSeconds(5), "horizontal scrollbar to render")
            .Build()
            .ApplyAsync(terminal);

        Assert.Contains("─", terminal.CreateSnapshot().GetText());
        Assert.Contains("■", terminal.CreateSnapshot().GetText());  // Horizontal uses ■, not ▉
        // No arrows in the new minimal style
        Assert.DoesNotContain("◀", terminal.CreateSnapshot().GetText());
        Assert.DoesNotContain("▶", terminal.CreateSnapshot().GetText());
    }

    #endregion

    #region Theming Tests

    [TestMethod]
    public async Task Render_WithCustomThumbColor_AppliesColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var theme = Hex1bThemes.Default.Clone()
            .Set(ScrollTheme.ThumbColor, Hex1bColor.Cyan);
        var context = CreateContext(workload, theme);
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.HasForegroundColor(Hex1bColor.Cyan),
                TimeSpan.FromSeconds(1), "Cyan foreground color")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Cyan foreground color should be applied
        Assert.IsTrue(snapshot.HasForegroundColor(Hex1bColor.Cyan));
    }

    [TestMethod]
    public async Task Render_WhenFocused_UsesFocusedThumbColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var theme = Hex1bThemes.Default.Clone()
            .Set(ScrollTheme.FocusedThumbColor, Hex1bColor.Yellow);
        var context = CreateContext(workload, theme);
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true,
            IsFocused = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.HasForegroundColor(Hex1bColor.Yellow),
                TimeSpan.FromSeconds(1), "Yellow foreground color")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Yellow foreground color should be applied
        Assert.IsTrue(snapshot.HasForegroundColor(Hex1bColor.Yellow));
    }

    #endregion

    #region Focus Tests

    [TestMethod]
    public async Task IsFocusable_ReturnsTrue()
    {
        var node = new ScrollPanelNode();

        Assert.IsTrue(node.IsFocusable);
    }

    [TestMethod]
    public async Task GetFocusableNodes_IncludesSelfFirst()
    {
        var node = new ScrollPanelNode
        {
            Child = new ButtonNode { Label = "Button" }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.AreSame(node, focusables[0]);
    }

    [TestMethod]
    public async Task GetFocusableNodes_IncludesChildFocusables()
    {
        var button = new ButtonNode { Label = "Button" };
        var node = new ScrollPanelNode
        {
            Child = button
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(button, focusables);
    }

    [TestMethod]
    public async Task SetInitialFocus_FocusesSelf()
    {
        var button = new ButtonNode { Label = "Button" };
        var node = new ScrollPanelNode
        {
            Child = button
        };

        node.SetInitialFocus();

        Assert.IsTrue(node.IsFocused);
        Assert.IsFalse(button.IsFocused);
    }

    #endregion

    #region Input Handling - Vertical Scroll

    [TestMethod]
    public async Task HandleInput_DownArrow_WhenFocused_ScrollsDown()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, node.Offset);
    }

    [TestMethod]
    public async Task HandleInput_UpArrow_WhenFocused_ScrollsUp()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.SetOffset(5); // Start at offset 5

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(4, node.Offset);
    }

    [TestMethod]
    public async Task HandleInput_PageDown_WhenFocused_ScrollsByViewportSize()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(9, node.Offset); // ViewportSize - 1
    }

    [TestMethod]
    public async Task HandleInput_Home_WhenFocused_ScrollsToStart()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.SetOffset(25);

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(0, node.Offset);
    }

    [TestMethod]
    public async Task HandleInput_End_WhenFocused_ScrollsToEnd()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(40, node.Offset); // ContentSize - ViewportSize = 50 - 10
    }

    [TestMethod]
    public async Task HandleInput_DownArrow_ScrollsRegardlessOfPanelFocus()
    {
        // The ScrollPanelNode no longer guards its scroll handlers with an IsFocused check.
        // Real routing already restricts which nodes receive bindings (the focus path), so the
        // guard was redundant in normal flows and harmful for global / bubble-up scenarios.
        // This test documents the new behavior: invoking the binding directly scrolls the panel.
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = false
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, node.Offset);
    }

    #endregion

    #region Input Handling - Horizontal Scroll

    [TestMethod]
    public async Task HandleInput_RightArrow_WhenFocused_ScrollsRight()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(1, node.Offset);
    }

    [TestMethod]
    public async Task HandleInput_LeftArrow_WhenFocused_ScrollsLeft()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));
        node.SetOffset(10);

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(9, node.Offset);
    }

    [TestMethod]
    public async Task HandleInput_Horizontal_UpDownArrows_DoNotScroll()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));

        // Up/down arrows match bindings but don't scroll horizontal
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(0, node.Offset); // No scroll
    }

    #endregion

    #region OnScroll Event Tests

    [TestMethod]
    public async Task HandleInput_DownArrow_FiresScrollEvent()
    {
        ScrollChangedEventArgs? receivedArgs = null;
        var widget = new ScrollPanelWidget(new VStackWidget([new TextBlockWidget("Line 1")]))
            .OnScroll(args => receivedArgs = args);

        // Use reconciliation to properly set up the node
        var context = ReconcileContext.CreateRoot();
        var node = (ScrollPanelNode)widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.Child = CreateTallContent(20);  // Replace child for this test
        node.IsFocused = true;
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.IsNotNull(receivedArgs);
        Assert.AreEqual(1, receivedArgs!.Offset);
        Assert.AreEqual(0, receivedArgs.PreviousOffset);
    }

    [TestMethod]
    public async Task ScrollEvent_ProvidesCorrectStateInfo()
    {
        ScrollChangedEventArgs? receivedArgs = null;
        var widget = new ScrollPanelWidget(new VStackWidget([new TextBlockWidget("Line 1")]))
            .OnScroll(args => receivedArgs = args);

        // Use reconciliation to properly set up the node
        var context = ReconcileContext.CreateRoot();
        var node = (ScrollPanelNode)widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.Child = CreateTallContent(50);  // Replace child for this test
        node.IsFocused = true;
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.SetOffset(20);

        // Scroll down one more
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.IsNotNull(receivedArgs);
        Assert.AreEqual(21, receivedArgs!.Offset);
        Assert.AreEqual(20, receivedArgs.PreviousOffset);
        Assert.AreEqual(50, receivedArgs.ContentSize);
        Assert.AreEqual(10, receivedArgs.ViewportSize);
        Assert.AreEqual(40, receivedArgs.MaxOffset);
        Assert.IsTrue(receivedArgs.IsScrollable);
        Assert.IsFalse(receivedArgs.IsAtStart);
        Assert.IsFalse(receivedArgs.IsAtEnd);
    }

    [TestMethod]
    public async Task ScrollEvent_IsAtStart_WhenAtTop()
    {
        ScrollChangedEventArgs? receivedArgs = null;
        var widget = new ScrollPanelWidget(new VStackWidget([new TextBlockWidget("Line 1")]))
            .OnScroll(args => receivedArgs = args);

        // Use reconciliation to properly set up the node
        var context = ReconcileContext.CreateRoot();
        var node = (ScrollPanelNode)widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.Child = CreateTallContent(50);  // Replace child for this test
        node.IsFocused = true;
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.SetOffset(1);

        // Scroll up to start
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.IsNotNull(receivedArgs);
        Assert.AreEqual(0, receivedArgs!.Offset);
        Assert.IsTrue(receivedArgs.IsAtStart);
        Assert.IsFalse(receivedArgs.IsAtEnd);
    }

    [TestMethod]
    public async Task ScrollEvent_IsAtEnd_WhenAtBottom()
    {
        ScrollChangedEventArgs? receivedArgs = null;
        var widget = new ScrollPanelWidget(new VStackWidget([new TextBlockWidget("Line 1")]))
            .OnScroll(args => receivedArgs = args);

        // Use reconciliation to properly set up the node
        var context = ReconcileContext.CreateRoot();
        var node = (ScrollPanelNode)widget.ReconcileAsync(null, context).GetAwaiter().GetResult();
        node.Child = CreateTallContent(50);  // Replace child for this test
        node.IsFocused = true;
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.SetOffset(39);

        // Scroll down to end
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.IsNotNull(receivedArgs);
        Assert.AreEqual(40, receivedArgs!.Offset);
        Assert.IsFalse(receivedArgs.IsAtStart);
        Assert.IsTrue(receivedArgs.IsAtEnd);
    }

    #endregion

    #region ILayoutProvider Tests

    [TestMethod]
    public async Task ShouldRenderAt_WithinViewport_ReturnsTrue()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.IsTrue(node.ShouldRenderAt(10, 10)); // Within viewport
    }

    [TestMethod]
    public async Task ShouldRenderAt_OutsideViewport_ReturnsFalse()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.IsFalse(node.ShouldRenderAt(10, 20)); // Below viewport (Y >= 15)
    }

    [TestMethod]
    public async Task ClipString_ClipsHorizontally()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(10, 10));
        node.Arrange(new Rect(0, 0, 10, 10));

        var (adjustedX, clippedText) = node.ClipString(0, 0, "This is a very long line of text");

        Assert.IsTrue(clippedText.Length <= 10);
    }

    [TestMethod]
    public async Task ClipString_OutsideVerticalBounds_ReturnsEmpty()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        var (_, clippedText) = node.ClipString(0, 15, "Text outside viewport");

        Assert.AreEqual("", clippedText);
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public async Task Integration_VScroll_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        await using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScrollPanel(
                    v => [
                        v.Text("Line 1"),
                        v.Text("Line 2"),
                        v.Text("Line 3"),
                        v.Text("Line 4"),
                        v.Text("Line 5"),
                        v.Text("Line 6"),
                        v.Text("Line 7"),
                        v.Text("Line 8"),
                        v.Text("Line 9"),
                        v.Text("Line 10"),
                        v.Text("Line 11"),
                        v.Text("Line 12"),
                    ]
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Contains("Line 1", snapshot.GetText());
        // Should have scrollbar with thin track and thumb (no arrows)
        Assert.Contains("│", snapshot.GetText());
        Assert.Contains("▉", snapshot.GetText());
    }

    [TestMethod]
    public async Task Integration_VScroll_ScrollsWithArrowKeys()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        int lastOffset = 0;

        await using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScrollPanel(
                    v => [
                        v.Text("Line 1"),
                        v.Text("Line 2"),
                        v.Text("Line 3"),
                        v.Text("Line 4"),
                        v.Text("Line 5"),
                        v.Text("Line 6"),
                        v.Text("Line 7"),
                        v.Text("Line 8"),
                        v.Text("Line 9"),
                        v.Text("Line 10"),
                    ]
                ).OnScroll(args => lastOffset = args.Offset)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▉"), TimeSpan.FromSeconds(5)) // Wait for scrollbar thumb
            .Down().Down().Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.AreEqual(3, lastOffset);
    }

    [TestMethod]
    public async Task Integration_VScrollWithButton_FocusNavigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var buttonClicked = false;

        await using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScrollPanel(
                    v => [
                        v.Text("Content"),
                        v.Button("Click Me").OnClick(_ => { buttonClicked = true; return Task.CompletedTask; }),
                    ]
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // Tab from scroll widget to button, then press enter
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(5))
            .Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.IsTrue(buttonClicked);
    }

    [TestMethod]
    public async Task Integration_VScrollInsideSplitter_Works()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(60, 15).Build();

        await using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Text("Left Side"),
                    ctx.VScrollPanel(
                        v => [
                            v.Text("Scrollable line 1"),
                            v.Text("Scrollable line 2"),
                            v.Text("Scrollable line 3"),
                            v.Text("Scrollable line 4"),
                            v.Text("Scrollable line 5"),
                            v.Text("Scrollable line 6"),
                            v.Text("Scrollable line 7"),
                            v.Text("Scrollable line 8"),
                            v.Text("Scrollable line 9"),
                            v.Text("Scrollable line 10"),
                        ]
                    ),
                    leftWidth: 15
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left Side") && s.ContainsText("Scrollable"), TimeSpan.FromSeconds(5), "splitter with scroll content to render")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        // WaitUntil already verified both Left Side and Scrollable content are visible
    }

    [TestMethod]
    public async Task Integration_HScroll_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        await using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HScrollPanel(
                    h => [
                        h.Text("Column1 "),
                        h.Text("Column2 "),
                        h.Text("Column3 "),
                        h.Text("Column4 "),
                        h.Text("Column5 "),
                    ]
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload } // Using default Surface mode
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting (horizontal scrollbar uses ■, not ▉)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("■"), TimeSpan.FromSeconds(5)) // Wait for scroll thumb
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should have scrollbar with thin track and thumb (no arrows)
        Assert.Contains("─", snapshot.GetText());
        Assert.Contains("■", snapshot.GetText());  // Horizontal uses ■
    }

    [TestMethod]
    public async Task Integration_HScroll_InsideBorder_ClipsCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        // Terminal is 30 wide, border takes 2 (left+right), so inner content is 28
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(30, 8).Build();

        await using var app = new Hex1bApp(
            ctx => ctx.Border(
                ctx.VStack(v => [
                    v.Text("Header"),
                    v.HScrollPanel(
                        h => [
                            // Long line that exceeds the viewport
                            h.Text("<<<START>>> | Col1 | Col2 | Col3 | Col4 | Col5 | <<<END>>>")
                        ]
                    ),
                    v.Text("Footer"),
                ])
            ).Title("Test"),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Test") && s.ContainsText("Header") && s.ContainsText("Footer"),
                TimeSpan.FromSeconds(2), "border with title, header, and footer")
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var text = snapshot.GetText();
        var lines = text.Split('\n');
        
        // Verify border characters are intact
        Assert.Contains("Test", text); // Title in border
        Assert.Contains("Header", text);
        Assert.Contains("Footer", text);
        
        // Find the scroll line and verify it doesn't overflow the border
        // The border's right edge should be column 29 (0-indexed), content area is columns 1-28
        // Content should not appear in column 0 or after column 28
        foreach (var line in lines)
        {
            // Each line should be at most 30 characters (terminal width)
            Assert.IsTrue(line.Length <= 30, $"Line too long: '{line}' ({line.Length} chars)");
        }
        
        // The scrollable content should be clipped - <<<END>>> should not be visible
        // because the full line is much longer than the 28-char inner width
        Assert.DoesNotContain("<<<END>>>", text);
    }
    
    #endregion

    #region Issue #296 — Global page actions and public scroll API

    /// <summary>
    /// Minimal sibling-focusable node used to simulate "focus is on a TextBox in another subtree"
    /// without dragging in the full TextBox machinery.
    /// </summary>
    private sealed class StubFocusableNode : Hex1bNode
    {
        public override bool IsFocusable => true;
        private bool _isFocused;
        public override bool IsFocused { get => _isFocused; set => _isFocused = value; }

        protected override Size MeasureCore(Constraints constraints) => new(10, 1);
        public override void Render(Hex1bRenderContext context) { }
    }

    /// <summary>
    /// Container that aggregates focusable descendants for FocusRing rebuilding.
    /// </summary>
    private sealed class StubContainerNode : Hex1bNode
    {
        public List<Hex1bNode> Children { get; } = new();

        protected override Size MeasureCore(Constraints constraints) => new(80, 24);
        public override void Render(Hex1bRenderContext context) { }
        public override IEnumerable<Hex1bNode> GetChildren() => Children;

        public override IEnumerable<Hex1bNode> GetFocusableNodes()
        {
            foreach (var child in Children)
            {
                foreach (var f in child.GetFocusableNodes())
                {
                    yield return f;
                }
            }
        }
    }

    [TestMethod]
    public async Task GlobalPageDown_ScrollsPanel_WhenFocusIsOnSibling()
    {
        // Arrange: ScrollPanel with a global PageDown binding, plus a sibling focusable that owns focus.
        var scrollPanel = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            // Simulate the user's InputBindings: register a global PageDown that triggers PageDownAction.
            BindingsConfigurator = b => b.Key(Hex1bKey.PageDown).Global().Triggers(ScrollPanelWidget.PageDownAction)
        };
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));

        var sibling = new StubFocusableNode { IsFocused = true };

        var root = new StubContainerNode();
        root.Children.Add(scrollPanel);
        root.Children.Add(sibling);
        scrollPanel.Parent = root;
        sibling.Parent = root;

        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        focusRing.Focus(sibling);

        // Sanity: focus is on the sibling, not the panel.
        Assert.IsFalse(scrollPanel.IsFocused);
        Assert.IsTrue(sibling.IsFocused);

        var state = new InputRouterState();

        // Act
        var result = await InputRouter.RouteInputAsync(
            root,
            new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None),
            focusRing,
            state,
            null,
            TestContext.Current.CancellationToken);

        // Assert: global binding fired, panel scrolled by ViewportSize - 1.
        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(9, scrollPanel.Offset);
    }

    [TestMethod]
    public async Task GlobalPageUp_ScrollsPanel_WhenFocusIsOnSibling()
    {
        var scrollPanel = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            BindingsConfigurator = b => b.Key(Hex1bKey.PageUp).Global().Triggers(ScrollPanelWidget.PageUpAction)
        };
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));
        scrollPanel.Offset = 30; // start in the middle so PageUp has room to scroll

        var sibling = new StubFocusableNode { IsFocused = true };

        var root = new StubContainerNode();
        root.Children.Add(scrollPanel);
        root.Children.Add(sibling);
        scrollPanel.Parent = root;
        sibling.Parent = root;

        var focusRing = new FocusRing();
        focusRing.Rebuild(root);
        focusRing.Focus(sibling);

        var state = new InputRouterState();

        var result = await InputRouter.RouteInputAsync(
            root,
            new Hex1bKeyEvent(Hex1bKey.PageUp, '\0', Hex1bModifiers.None),
            focusRing,
            state,
            null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.AreEqual(21, scrollPanel.Offset); // 30 - (10 - 1)
    }

    [TestMethod]
    public async Task PageDown_BubblesUp_FromFocusableDescendant_ToScrollPanelAncestor()
    {
        // Regression for the second symptom of issue #296: when a focusable descendant of a
        // ScrollPanel has focus and presses PageDown, the binding should bubble up to the panel
        // and scroll. Before the fix, the IsFocused guard silently swallowed the key.
        var inner = new StubFocusableNode { IsFocused = true };
        var scrollPanel = new ScrollPanelNode
        {
            Child = inner,
            Orientation = ScrollOrientation.Vertical
        };
        inner.Parent = scrollPanel;
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));

        // Add some scrollable content size by faking content via a tall sibling structure: instead,
        // just give the panel a tall child and re-measure.
        scrollPanel.Child = CreateTallContent(50);
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));

        // Re-attach focus to a real focusable inside the panel for routing.
        // CreateTallContent returns a VStackNode of TextBlockNodes — none focusable. Add one.
        var focusableChild = new StubFocusableNode { IsFocused = true };
        scrollPanel.Child = focusableChild;
        focusableChild.Parent = scrollPanel;
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));

        // The panel needs ContentSize > ViewportSize to have a non-zero MaxOffset. Force it.
        // Easiest path: build a vertical stack with a focusable header and many text blocks.
        var children = new List<Hex1bNode> { focusableChild };
        for (int i = 0; i < 50; i++) children.Add(new TextBlockNode { Text = $"line {i}" });
        scrollPanel.Child = new VStackNode { Children = children };
        focusableChild.Parent = scrollPanel.Child;
        ((VStackNode)scrollPanel.Child).Parent = scrollPanel;
        scrollPanel.Measure(Constraints.Unbounded);
        scrollPanel.Arrange(new Rect(0, 0, 40, 10));

        var focusRing = new FocusRing();
        focusRing.Rebuild(scrollPanel);
        focusRing.Focus(focusableChild);

        Assert.IsFalse(scrollPanel.IsFocused, "Panel should not own focus directly when descendant is focused");
        Assert.IsTrue(focusableChild.IsFocused);
        Assert.IsTrue(scrollPanel.MaxOffset > 0, "Panel must be scrollable for this test to be meaningful");

        var state = new InputRouterState();

        var result = await InputRouter.RouteInputAsync(
            scrollPanel,
            new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None),
            focusRing,
            state,
            null,
            TestContext.Current.CancellationToken);

        Assert.AreEqual(InputResult.Handled, result);
        Assert.IsTrue(scrollPanel.Offset > 0, $"Expected panel to scroll via bubble-up, but Offset is {scrollPanel.Offset}");
    }

    [TestMethod]
    public void OffsetSetter_ClampsToValidRange()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        node.Offset = -100;
        Assert.AreEqual(0, node.Offset);

        node.Offset = 9999;
        Assert.AreEqual(node.MaxOffset, node.Offset);

        node.Offset = 5;
        Assert.AreEqual(5, node.Offset);
    }

    [TestMethod]
    public void ScrollByPage_AdvancesByViewportMinusOne()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        node.ScrollByPage(1);
        Assert.AreEqual(9, node.Offset);  // ViewportSize (10) - 1

        node.ScrollByPage(1);
        Assert.AreEqual(18, node.Offset);

        node.ScrollByPage(-1);
        Assert.AreEqual(9, node.Offset);

        node.ScrollByPage(2);
        Assert.AreEqual(27, node.Offset); // direction can be larger magnitudes
    }

    [TestMethod]
    public void ScrollBy_AppliesRelativeOffset()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        node.ScrollBy(5);
        Assert.AreEqual(5, node.Offset);

        node.ScrollBy(-2);
        Assert.AreEqual(3, node.Offset);

        // Large positive scroll clamps to MaxOffset; the public API is overflow-safe.
        node.ScrollBy(int.MaxValue);
        Assert.AreEqual(node.MaxOffset, node.Offset);

        node.ScrollBy(int.MinValue);
        Assert.AreEqual(0, node.Offset);
    }

    [TestMethod]
    public void ScrollToTop_SetsOffsetToZero()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Offset = 25;

        node.ScrollToTop();

        Assert.AreEqual(0, node.Offset);
    }

    [TestMethod]
    public void ScrollToBottom_SetsOffsetToMaxOffset()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Offset = 5;

        node.ScrollToBottom();

        Assert.AreEqual(node.MaxOffset, node.Offset);
        Assert.AreEqual(40, node.Offset); // 50 content - 10 viewport
    }

    [TestMethod]
    public async Task ProgrammaticScroll_DoesNotFire_OnScrollEvent()
    {
        // Public scroll API has no InputBindingActionContext to attach to the event args.
        // Documented behavior: programmatic mutations do not fire OnScroll.
        ScrollChangedEventArgs? received = null;
        var widget = new ScrollPanelWidget(new VStackWidget([new TextBlockWidget("Line 1")]))
            .OnScroll(e => received = e);

        var context = ReconcileContext.CreateRoot();
        var node = (ScrollPanelNode)await widget.ReconcileAsync(null, context);
        node.Child = CreateTallContent(50);
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        node.Offset = 5;
        node.ScrollBy(3);
        node.ScrollByPage(1);
        node.ScrollToTop();
        node.ScrollToBottom();

        Assert.IsNull(received);
    }

    #endregion
}
