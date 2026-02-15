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

    [Fact]
    public async Task ScrollNode_InitialState_IsZero()
    {
        var node = new ScrollPanelNode();

        Assert.Equal(0, node.Offset);
        Assert.Equal(0, node.ContentSize);
        Assert.Equal(0, node.ViewportSize);
    }

    [Fact]
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
        Assert.True(node.ContentSize > node.ViewportSize);
    }

    [Fact]
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
        Assert.Equal(15, node.ContentSize - node.ViewportSize);
    }

    #endregion

    #region Measurement Tests - Vertical Scroll

    [Fact]
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
        Assert.True(size.Height <= 20);
        Assert.Equal(5, node.ContentSize);
    }

    [Fact]
    public async Task Measure_Vertical_ContentLargerThanViewport_UsesConstraints()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        Assert.Equal(20, size.Height);
        Assert.Equal(50, node.ContentSize);
    }

    [Fact]
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
        Assert.True(size.Width > 0);
    }

    [Fact]
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
        Assert.Equal(7, size.Width);
    }

    #endregion

    #region Measurement Tests - Horizontal Scroll

    [Fact]
    public async Task Measure_Horizontal_ContentWiderThanViewport_UsesConstraints()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.Equal(40, size.Width);
    }

    [Fact]
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
        Assert.True(size.Height >= 2);
    }

    #endregion

    #region Arrange Tests - Vertical Scroll

    [Fact]
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

        Assert.Equal(10, node.ViewportSize);
    }

    [Fact]
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
        Assert.Equal(-5, child.Bounds.Y);
    }

    [Fact]
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
        Assert.Equal(10, node.Offset);
    }

    #endregion

    #region Arrange Tests - Horizontal Scroll

    [Fact]
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
        Assert.Equal(30, node.ViewportSize);
    }

    [Fact]
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
        Assert.Equal(-10, child.Bounds.X);
    }

    #endregion

    #region Rendering Tests - Vertical Scrollbar

    [Fact]
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
            .WaitUntil(s => s.ContainsText("▉"), TimeSpan.FromSeconds(2), "scrollbar to render")
            .Build()
            .ApplyAsync(terminal);

        // Should contain scrollbar thumb character
        Assert.Contains("▉", terminal.CreateSnapshot().GetText());
    }

    [Fact]
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
            .WaitUntil(s => s.ContainsText("│") && s.ContainsText("▉"), TimeSpan.FromSeconds(2), "vertical scrollbar to render")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.True(snapshot.ContainsText("│"), "Should show track");
        Assert.True(snapshot.ContainsText("▉"), "Should show thumb");
        // No arrows in the new minimal style
        Assert.DoesNotContain("▲", snapshot.GetText());
        Assert.DoesNotContain("▼", snapshot.GetText());
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(2))
            // Scroll down 5 times to show Line 6, waiting for each scroll to render
            .Down()
            .WaitUntil(s => s.ContainsText("Line 2"), TimeSpan.FromSeconds(2), "Line 2 visible after 1st Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 3"), TimeSpan.FromSeconds(2), "Line 3 visible after 2nd Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 4"), TimeSpan.FromSeconds(2), "Line 4 visible after 3rd Down")
            .Down()
            .WaitUntil(s => s.ContainsText("Line 5"), TimeSpan.FromSeconds(2), "Line 5 visible after 4th Down")
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

    [Fact]
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
            .WaitUntil(s => s.ContainsText("■"), TimeSpan.FromSeconds(2), "horizontal scrollbar to render")
            .Build()
            .ApplyAsync(terminal);

        // Should contain scrollbar thumb character (horizontal uses ■, not ▉)
        Assert.Contains("■", terminal.CreateSnapshot().GetText());
    }

    [Fact]
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
            .WaitUntil(s => s.ContainsText("─") && s.ContainsText("■"), TimeSpan.FromSeconds(2), "horizontal scrollbar to render")
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

    [Fact]
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
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.Cyan));
    }

    [Fact]
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
        Assert.True(snapshot.HasForegroundColor(Hex1bColor.Yellow));
    }

    #endregion

    #region Focus Tests

    [Fact]
    public async Task IsFocusable_ReturnsTrue()
    {
        var node = new ScrollPanelNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public async Task GetFocusableNodes_IncludesSelfFirst()
    {
        var node = new ScrollPanelNode
        {
            Child = new ButtonNode { Label = "Button" }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Same(node, focusables[0]);
    }

    [Fact]
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

    [Fact]
    public async Task SetInitialFocus_FocusesSelf()
    {
        var button = new ButtonNode { Label = "Button" };
        var node = new ScrollPanelNode
        {
            Child = button
        };

        node.SetInitialFocus();

        Assert.True(node.IsFocused);
        Assert.False(button.IsFocused);
    }

    #endregion

    #region Input Handling - Vertical Scroll

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.Offset);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(4, node.Offset);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(9, node.Offset); // ViewportSize - 1
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.Offset);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(40, node.Offset); // ContentSize - ViewportSize = 50 - 10
    }

    [Fact]
    public async Task HandleInput_NotFocused_DoesNotScroll()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            IsFocused = false
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, node.Offset);
    }

    #endregion

    #region Input Handling - Horizontal Scroll

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, node.Offset);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(9, node.Offset);
    }

    [Fact]
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

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.Offset); // No scroll
    }

    #endregion

    #region OnScroll Event Tests

    [Fact]
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

        Assert.NotNull(receivedArgs);
        Assert.Equal(1, receivedArgs!.Offset);
        Assert.Equal(0, receivedArgs.PreviousOffset);
    }

    [Fact]
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

        Assert.NotNull(receivedArgs);
        Assert.Equal(21, receivedArgs!.Offset);
        Assert.Equal(20, receivedArgs.PreviousOffset);
        Assert.Equal(50, receivedArgs.ContentSize);
        Assert.Equal(10, receivedArgs.ViewportSize);
        Assert.Equal(40, receivedArgs.MaxOffset);
        Assert.True(receivedArgs.IsScrollable);
        Assert.False(receivedArgs.IsAtStart);
        Assert.False(receivedArgs.IsAtEnd);
    }

    [Fact]
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

        Assert.NotNull(receivedArgs);
        Assert.Equal(0, receivedArgs!.Offset);
        Assert.True(receivedArgs.IsAtStart);
        Assert.False(receivedArgs.IsAtEnd);
    }

    [Fact]
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

        Assert.NotNull(receivedArgs);
        Assert.Equal(40, receivedArgs!.Offset);
        Assert.False(receivedArgs.IsAtStart);
        Assert.True(receivedArgs.IsAtEnd);
    }

    #endregion

    #region ILayoutProvider Tests

    [Fact]
    public async Task ShouldRenderAt_WithinViewport_ReturnsTrue()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.True(node.ShouldRenderAt(10, 10)); // Within viewport
    }

    [Fact]
    public async Task ShouldRenderAt_OutsideViewport_ReturnsFalse()
    {
        var node = new ScrollPanelNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.False(node.ShouldRenderAt(10, 20)); // Below viewport (Y >= 15)
    }

    [Fact]
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

        Assert.True(clippedText.Length <= 10);
    }

    [Fact]
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

        Assert.Equal("", clippedText);
    }

    #endregion

    #region Integration Tests

    [Fact]
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
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(2))
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

    [Fact]
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
            .WaitUntil(s => s.ContainsText("▉"), TimeSpan.FromSeconds(2)) // Wait for scrollbar thumb
            .Down().Down().Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(3, lastOffset);
    }

    [Fact]
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
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(buttonClicked);
    }

    [Fact]
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
            .WaitUntil(s => s.ContainsText("Left Side") && s.ContainsText("Scrollable"), TimeSpan.FromSeconds(2), "splitter with scroll content to render")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        // WaitUntil already verified both Left Side and Scrollable content are visible
    }

    [Fact]
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
            .WaitUntil(s => s.ContainsText("■"), TimeSpan.FromSeconds(2)) // Wait for scroll thumb
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should have scrollbar with thin track and thumb (no arrows)
        Assert.Contains("─", snapshot.GetText());
        Assert.Contains("■", snapshot.GetText());  // Horizontal uses ■
    }

    [Fact]
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
            Assert.True(line.Length <= 30, $"Line too long: '{line}' ({line.Length} chars)");
        }
        
        // The scrollable content should be clipped - <<<END>>> should not be visible
        // because the full line is much longer than the 28-char inner width
        Assert.DoesNotContain("<<<END>>>", text);
    }
    
    #endregion
}
