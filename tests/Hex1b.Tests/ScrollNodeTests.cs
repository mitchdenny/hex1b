using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Testing;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for ScrollNode layout, rendering, scrolling, and focus handling.
/// </summary>
public class ScrollNodeTests
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

    #region ScrollState Tests

    [Fact]
    public void ScrollState_InitialState_IsZero()
    {
        var state = new ScrollState();

        Assert.Equal(0, state.Offset);
        Assert.Equal(0, state.ContentSize);
        Assert.Equal(0, state.ViewportSize);
    }

    [Fact]
    public void ScrollState_IsScrollable_WhenContentExceedsViewport()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10 };

        Assert.True(state.IsScrollable);
    }

    [Fact]
    public void ScrollState_IsNotScrollable_WhenContentFitsViewport()
    {
        var state = new ScrollState { ContentSize = 5, ViewportSize = 10 };

        Assert.False(state.IsScrollable);
    }

    [Fact]
    public void ScrollState_MaxOffset_IsCorrect()
    {
        var state = new ScrollState { ContentSize = 25, ViewportSize = 10 };

        Assert.Equal(15, state.MaxOffset);
    }

    [Fact]
    public void ScrollState_ScrollDown_IncreasesOffset()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10, Offset = 0 };

        state.ScrollDown();

        Assert.Equal(1, state.Offset);
    }

    [Fact]
    public void ScrollState_ScrollUp_DecreasesOffset()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10, Offset = 5 };

        state.ScrollUp();

        Assert.Equal(4, state.Offset);
    }

    [Fact]
    public void ScrollState_ScrollDown_ClampsToMaxOffset()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10, Offset = 10 };

        state.ScrollDown(5);

        Assert.Equal(10, state.Offset); // MaxOffset is 10
    }

    [Fact]
    public void ScrollState_ScrollUp_ClampsToZero()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10, Offset = 2 };

        state.ScrollUp(5);

        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public void ScrollState_PageDown_ScrollsByViewportSize()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10, Offset = 0 };

        state.PageDown();

        Assert.Equal(9, state.Offset); // ViewportSize - 1
    }

    [Fact]
    public void ScrollState_PageUp_ScrollsByViewportSize()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10, Offset = 20 };

        state.PageUp();

        Assert.Equal(11, state.Offset); // 20 - (10 - 1)
    }

    [Fact]
    public void ScrollState_ScrollToStart_SetsOffsetToZero()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10, Offset = 25 };

        state.ScrollToStart();

        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public void ScrollState_ScrollToEnd_SetsOffsetToMaxOffset()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10, Offset = 0 };

        state.ScrollToEnd();

        Assert.Equal(40, state.Offset);
    }

    #endregion

    #region Measurement Tests - Vertical Scroll

    [Fact]
    public void Measure_Vertical_ContentSmallerThanViewport_ReturnsFitSize()
    {
        var node = new ScrollNode
        {
            Child = CreateTallContent(5),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Content is 5 lines, viewport allows 20, so no need for full space
        Assert.True(size.Height <= 20);
        Assert.Equal(5, node.State.ContentSize);
    }

    [Fact]
    public void Measure_Vertical_ContentLargerThanViewport_UsesConstraints()
    {
        var node = new ScrollNode
        {
            Child = CreateTallContent(50),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        Assert.Equal(20, size.Height);
        Assert.Equal(50, node.State.ContentSize);
    }

    [Fact]
    public void Measure_Vertical_IncludesScrollbarWidth()
    {
        var child = new TextBlockNode { Text = "Content" };
        var node = new ScrollNode
        {
            Child = child,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };
        node.State.ContentSize = 50; // Force scrollable

        var size = node.Measure(new Constraints(0, 30, 0, 20));

        // Width should be content width + scrollbar (1)
        Assert.True(size.Width > 7); // "Content" is 7 chars
    }

    [Fact]
    public void Measure_Vertical_NoScrollbar_DoesNotIncludeScrollbarWidth()
    {
        var child = new TextBlockNode { Text = "Content" };
        var node = new ScrollNode
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
    public void Measure_Horizontal_ContentWiderThanViewport_UsesConstraints()
    {
        var node = new ScrollNode
        {
            Child = CreateWideContent(20),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        var size = node.Measure(new Constraints(0, 40, 0, 10));

        Assert.Equal(40, size.Width);
    }

    [Fact]
    public void Measure_Horizontal_IncludesScrollbarHeight()
    {
        var child = new TextBlockNode { Text = "Content" };
        var node = new ScrollNode
        {
            Child = child,
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };
        node.State.ContentSize = 100; // Force scrollable

        var size = node.Measure(new Constraints(0, 20, 0, 10));

        // Height should include scrollbar (1)
        Assert.True(size.Height >= 2);
    }

    #endregion

    #region Arrange Tests - Vertical Scroll

    [Fact]
    public void Arrange_Vertical_SetsViewportSize()
    {
        var state = new ScrollState();
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        Assert.Equal(10, state.ViewportSize);
    }

    [Fact]
    public void Arrange_Vertical_ChildPositionedWithOffset()
    {
        var state = new ScrollState { Offset = 5 };
        var child = CreateTallContent(20);
        var node = new ScrollNode
        {
            Child = child,
            State = state,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        // Child should be positioned above the viewport by the offset
        Assert.Equal(-5, child.Bounds.Y);
    }

    [Fact]
    public void Arrange_Vertical_ClampsOffsetToMaxOffset()
    {
        var state = new ScrollState { Offset = 100 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        Assert.Equal(state.MaxOffset, state.Offset);
    }

    #endregion

    #region Arrange Tests - Horizontal Scroll

    [Fact]
    public void Arrange_Horizontal_SetsViewportSize()
    {
        var state = new ScrollState();
        var node = new ScrollNode
        {
            Child = CreateWideContent(20),
            State = state,
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        // Viewport width is 30 (minus scrollbar height doesn't affect width)
        Assert.Equal(30, state.ViewportSize);
    }

    [Fact]
    public void Arrange_Horizontal_ChildPositionedWithOffset()
    {
        var state = new ScrollState { Offset = 10 };
        var child = CreateWideContent(20);
        var node = new ScrollNode
        {
            Child = child,
            State = state,
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(new Constraints(0, 30, 0, 10));
        node.Arrange(new Rect(0, 0, 30, 10));

        // Child should be positioned to the left of the viewport by the offset
        Assert.Equal(-10, child.Bounds.X);
    }

    #endregion

    #region Rendering Tests - Vertical Scrollbar

    [Fact]
    public void Render_Vertical_ShowsScrollbar_WhenContentExceedsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        // Should contain scrollbar characters
        Assert.Contains("█", terminal.CreateSnapshot().GetText());
    }

    [Fact]
    public void Render_Vertical_ShowsArrows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = CreateContext(workload);
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.ContainsText("▲"), "Should show up arrow");
        Assert.True(snapshot.ContainsText("▼"), "Should show down arrow");
    }

    [Fact]
    public void Render_Vertical_NoScrollbar_WhenContentFitsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 20);
        var context = CreateContext(workload);
        var node = new ScrollNode
        {
            Child = CreateTallContent(5),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 20));
        node.Arrange(new Rect(0, 0, 40, 20));
        node.Render(context);

        // Content fits, so no scrollbar needed
        Assert.DoesNotContain("▲", terminal.CreateSnapshot().GetText());
        Assert.DoesNotContain("▼", terminal.CreateSnapshot().GetText());
    }

    [Fact]
    public async Task Render_Vertical_ClipsContentBeyondViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);

        await using var app = new Hex1bApp(
            ctx => ctx.VScroll(v => [
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
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

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
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var scrollState = new ScrollState { Offset = 5 };

        await using var app = new Hex1bApp(
            ctx => ctx.VScroll(v => [
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
            ], state: scrollState, showScrollbar: true),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 6"), TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var snapshot = terminal.CreateSnapshot();

        // Lines starting from offset 5 (Line 6) should be visible
        Assert.Contains("Line 6", snapshot.GetText());
        Assert.Contains("Line 7", snapshot.GetText());
        // Earlier lines should not be visible
        Assert.DoesNotContain("Line 5", snapshot.GetText());
        // Much later lines should also be clipped
        Assert.DoesNotContain("Line 12", snapshot.GetText());
    }

    #endregion

    #region Rendering Tests - Horizontal Scrollbar

    [Fact]
    public void Render_Horizontal_ShowsScrollbar_WhenContentExceedsViewport()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new ScrollNode
        {
            Child = CreateWideContent(10),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        // Should contain scrollbar characters
        Assert.Contains("█", terminal.CreateSnapshot().GetText());
    }

    [Fact]
    public void Render_Horizontal_ShowsArrows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = CreateContext(workload);
        var node = new ScrollNode
        {
            Child = CreateWideContent(10),
            Orientation = ScrollOrientation.Horizontal,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(20, 5));
        node.Arrange(new Rect(0, 0, 20, 5));
        node.Render(context);

        Assert.Contains("◀", terminal.CreateSnapshot().GetText());
        Assert.Contains("▶", terminal.CreateSnapshot().GetText());
    }

    #endregion

    #region Theming Tests

    [Fact]
    public void Render_WithCustomThumbColor_AppliesColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ScrollTheme.ThumbColor, Hex1bColor.Cyan);
        var context = CreateContext(workload, theme);
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        // Cyan foreground color should be applied
        Assert.True(terminal.CreateSnapshot().HasForegroundColor(Hex1bColor.Cyan));
    }

    [Fact]
    public void Render_WhenFocused_UsesFocusedThumbColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(ScrollTheme.FocusedThumbColor, Hex1bColor.Yellow);
        var context = CreateContext(workload, theme);
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical,
            ShowScrollbar = true,
            IsFocused = true
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        // Yellow foreground color should be applied
        Assert.True(terminal.CreateSnapshot().HasForegroundColor(Hex1bColor.Yellow));
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new ScrollNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_IncludesSelfFirst()
    {
        var node = new ScrollNode
        {
            Child = new ButtonNode { Label = "Button" }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Same(node, focusables[0]);
    }

    [Fact]
    public void GetFocusableNodes_IncludesChildFocusables()
    {
        var button = new ButtonNode { Label = "Button" };
        var node = new ScrollNode
        {
            Child = button
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(button, focusables);
    }

    [Fact]
    public void SetInitialFocus_FocusesSelf()
    {
        var button = new ButtonNode { Label = "Button" };
        var node = new ScrollNode
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
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, state.Offset);
    }

    [Fact]
    public async Task HandleInput_UpArrow_WhenFocused_ScrollsUp()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10, Offset = 5 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(4, state.Offset);
    }

    [Fact]
    public async Task HandleInput_PageDown_WhenFocused_ScrollsByViewportSize()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(50),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.PageDown, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(9, state.Offset);
    }

    [Fact]
    public async Task HandleInput_Home_WhenFocused_ScrollsToStart()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10, Offset = 25 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(50),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, state.Offset);
    }

    [Fact]
    public async Task HandleInput_End_WhenFocused_ScrollsToEnd()
    {
        var state = new ScrollState { ContentSize = 50, ViewportSize = 10 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(50),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(40, state.Offset);
    }

    [Fact]
    public async Task HandleInput_NotFocused_DoesNotScroll()
    {
        var state = new ScrollState { ContentSize = 20, ViewportSize = 10 };
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            State = state,
            Orientation = ScrollOrientation.Vertical,
            IsFocused = false
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 10));

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, state.Offset);
    }

    #endregion

    #region Input Handling - Horizontal Scroll

    [Fact]
    public async Task HandleInput_RightArrow_WhenFocused_ScrollsRight()
    {
        var state = new ScrollState { ContentSize = 100, ViewportSize = 30 };
        var node = new ScrollNode
        {
            Child = CreateWideContent(20),
            State = state,
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(1, state.Offset);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_WhenFocused_ScrollsLeft()
    {
        var state = new ScrollState { ContentSize = 100, ViewportSize = 30, Offset = 10 };
        var node = new ScrollNode
        {
            Child = CreateWideContent(20),
            State = state,
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(9, state.Offset);
    }

    [Fact]
    public async Task HandleInput_Horizontal_UpDownArrows_DoNotScroll()
    {
        var state = new ScrollState { ContentSize = 100, ViewportSize = 30 };
        var node = new ScrollNode
        {
            Child = CreateWideContent(20),
            State = state,
            Orientation = ScrollOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 10));

        // Up/down arrows match bindings but don't scroll horizontal
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, state.Offset); // No scroll
    }

    #endregion

    #region ILayoutProvider Tests

    [Fact]
    public void ShouldRenderAt_WithinViewport_ReturnsTrue()
    {
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.True(node.ShouldRenderAt(10, 10)); // Within viewport
    }

    [Fact]
    public void ShouldRenderAt_OutsideViewport_ReturnsFalse()
    {
        var node = new ScrollNode
        {
            Child = CreateTallContent(20),
            Orientation = ScrollOrientation.Vertical
        };
        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(5, 5, 40, 10));

        Assert.False(node.ShouldRenderAt(10, 20)); // Below viewport (Y >= 15)
    }

    [Fact]
    public void ClipString_ClipsHorizontally()
    {
        var node = new ScrollNode
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
    public void ClipString_OutsideVerticalBounds_ReturnsEmpty()
    {
        var node = new ScrollNode
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

        using var terminal = new Hex1bTerminal(workload, 40, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScroll(
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
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 1"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Contains("Line 1", terminal.CreateSnapshot().GetText());
        Assert.Contains("▲", terminal.CreateSnapshot().GetText());
        Assert.Contains("▼", terminal.CreateSnapshot().GetText());
    }

    [Fact]
    public async Task Integration_VScroll_ScrollsWithArrowKeys()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var state = new ScrollState();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScroll(
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
                    ],
                    state
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("▼"), TimeSpan.FromSeconds(2)) // Wait for down scroll indicator
            .Down().Down().Down()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal(3, state.Offset);
    }

    [Fact]
    public async Task Integration_VScrollWithButton_FocusNavigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var buttonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VScroll(
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
        await new Hex1bTestSequenceBuilder()
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

        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Text("Left Side"),
                    ctx.VScroll(
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
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left Side"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Contains("Left Side", terminal.CreateSnapshot().GetText());
        Assert.Contains("Scrollable", terminal.CreateSnapshot().GetText());
    }

    [Fact]
    public async Task Integration_HScroll_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 20, 5);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HScroll(
                    h => [
                        h.Text("Column1 "),
                        h.Text("Column2 "),
                        h.Text("Column3 "),
                        h.Text("Column4 "),
                        h.Text("Column5 "),
                    ]
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTestSequenceBuilder()
            .WaitUntil(s => s.ContainsText("◀"), TimeSpan.FromSeconds(2)) // Wait for scroll indicator
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        await runTask;

        Assert.Contains("◀", terminal.CreateSnapshot().GetText());
        Assert.Contains("▶", terminal.CreateSnapshot().GetText());
    }

    #endregion
}
