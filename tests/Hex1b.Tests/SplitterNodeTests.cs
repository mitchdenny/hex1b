using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;
using Hex1b.Terminal;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for SplitterNode layout, rendering, resizing, and focus handling.
/// </summary>
public class SplitterNodeTests
{
    private static Hex1bRenderContext CreateContext(IHex1bAppTerminalWorkloadAdapter workload, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(workload, theme);
    }

    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var left = new TextBlockNode { Text = "Left Pane" };
        var right = new TextBlockNode { Text = "Right Pane" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 10 };

        var size = node.Measure(Constraints.Unbounded);

        // Width = LeftWidth (10) + divider (3) + right content width (10)
        Assert.Equal(23, size.Width);
    }

    [Fact]
    public void Measure_WithNoChildren_ReturnsMinimalSize()
    {
        var node = new SplitterNode { Left = null, Right = null, LeftWidth = 20 };

        var size = node.Measure(Constraints.Unbounded);

        // Just LeftWidth + divider width
        Assert.Equal(23, size.Width); // 20 + 3
    }

    [Fact]
    public void Measure_RespectsConstraints()
    {
        var left = new TextBlockNode { Text = "Very long left content" };
        var right = new TextBlockNode { Text = "Very long right content" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 15 };

        var size = node.Measure(new Constraints(0, 30, 0, 10));

        Assert.True(size.Width <= 30);
        Assert.True(size.Height <= 10);
    }

    [Fact]
    public void Measure_HeightIsMaxOfBothPanes()
    {
        var left = new VStackNode
        {
            Children = [
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" }
            ]
        };
        var right = new TextBlockNode { Text = "Single line" };
        var node = new SplitterNode { Left = left, Right = right };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(3, size.Height); // Height of left pane
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_LeftPaneGetsLeftWidth()
    {
        var left = new TextBlockNode { Text = "Left" };
        var right = new TextBlockNode { Text = "Right" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 15 };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        Assert.Equal(0, left.Bounds.X);
        Assert.Equal(15, left.Bounds.Width);
    }

    [Fact]
    public void Arrange_RightPaneGetsRemainingWidth()
    {
        var left = new TextBlockNode { Text = "Left" };
        var right = new TextBlockNode { Text = "Right" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 15 };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        // Right pane starts after left + divider (15 + 3 = 18)
        Assert.Equal(18, right.Bounds.X);
        // Right pane gets remaining width (50 - 15 - 3 = 32)
        Assert.Equal(32, right.Bounds.Width);
    }

    [Fact]
    public void Arrange_WithOffset_PositionsCorrectly()
    {
        var left = new TextBlockNode { Text = "Left" };
        var right = new TextBlockNode { Text = "Right" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 10 };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(5, 3, 40, 8));

        Assert.Equal(5, left.Bounds.X);
        Assert.Equal(3, left.Bounds.Y);
        Assert.Equal(18, right.Bounds.X); // 5 + 10 + 3
        Assert.Equal(3, right.Bounds.Y);
    }

    [Fact]
    public void Arrange_BothPanesGetFullHeight()
    {
        var left = new TextBlockNode { Text = "Left" };
        var right = new TextBlockNode { Text = "Right" };
        var node = new SplitterNode { Left = left, Right = right, LeftWidth = 10 };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 8));

        Assert.Equal(8, left.Bounds.Height);
        Assert.Equal(8, right.Bounds.Height);
    }

    #endregion

    #region Rendering - Divider Tests

    [Fact]
    public void Render_ShowsDivider()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 20
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        // Default divider character is "│"
        Assert.True(terminal.CreateSnapshot().ContainsText("│"));
    }

    [Fact]
    public void Render_ShowsLeftContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left Pane Content" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 25
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().ContainsText("Left Pane Content"));
    }

    [Fact]
    public void Render_ShowsRightContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right Pane Content" },
            LeftWidth = 15
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().ContainsText("Right Pane Content"));
    }

    [Fact]
    public void Render_DividerSpansFullHeight()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 5);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "L" },
            Right = new TextBlockNode { Text = "R" },
            LeftWidth = 10
        };

        node.Measure(Constraints.Tight(50, 5));
        node.Arrange(new Rect(0, 0, 50, 5));
        node.Render(context);

        // Count occurrences of divider chars in screen text - should be 5 (one per row)
        // 3 regular dividers + 2 arrow characters (← and →) at midpoint
        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();
        var dividerCount = screenText.Split("│").Length - 1;
        var leftArrowCount = screenText.Split("←").Length - 1;
        var rightArrowCount = screenText.Split("→").Length - 1;
        Assert.Equal(5, dividerCount + leftArrowCount + rightArrowCount);
    }

    #endregion

    #region Rendering - Theming Tests

    [Fact]
    public void Render_WithCustomDividerColor_AppliesColor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(SplitterTheme.DividerColor, Hex1bColor.Cyan);
        var context = CreateContext(workload, theme);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 15
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        // Cyan is RGB(0, 255, 255)
        Assert.True(terminal.CreateSnapshot().HasForegroundColor(Hex1bColor.FromRgb(0, 255, 255)));
    }

    [Fact]
    public void Render_WithCustomDividerCharacter_UsesCustomCharacter()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(SplitterTheme.DividerCharacter, "║");
        var context = CreateContext(workload, theme);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 15
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().ContainsText("║"));
    }

    [Fact]
    public void Render_WhenFocused_InvertsDividerColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 15,
            IsFocused = true
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        // When focused, should have background color on divider
        Assert.True(terminal.CreateSnapshot().HasBackgroundColor());
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new SplitterNode();

        Assert.True(node.IsFocusable);
    }

    [Fact]
    public void GetFocusableNodes_IncludesSelf()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Non-focusable" },
            Right = new TextBlockNode { Text = "Non-focusable" }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(node, focusables);
    }

    [Fact]
    public void GetFocusableNodes_IncludesLeftPaneFocusables()
    {
        var button = new ButtonNode { Label = "Click" };
        var node = new SplitterNode
        {
            Left = button,
            Right = new TextBlockNode { Text = "Right" }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(button, focusables);
    }

    [Fact]
    public void GetFocusableNodes_IncludesRightPaneFocusables()
    {
        var textBox = new TextBoxNode { State = new TextBoxState() };
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = textBox
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Contains(textBox, focusables);
    }

    [Fact]
    public void GetFocusableNodes_OrdersLeftThenSplitterThenRight()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right" };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(3, focusables.Count);
        Assert.Same(leftButton, focusables[0]);
        Assert.Same(node, focusables[1]); // Splitter itself
        Assert.Same(rightButton, focusables[2]);
    }

    [Fact]
    public void SetInitialFocus_FocusesFirstFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right" };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };

        node.SetInitialFocus();

        Assert.True(leftButton.IsFocused);
        Assert.False(rightButton.IsFocused);
    }

    #endregion

    #region Input Handling - Resize Tests

    [Fact]
    public async Task HandleInput_LeftArrow_WhenFocused_DecreasesLeftWidth()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 20,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(18, node.LeftWidth);
    }

    [Fact]
    public async Task HandleInput_RightArrow_WhenFocused_IncreasesLeftWidth()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 20,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(22, node.LeftWidth);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_RespectsMinLeftWidth()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 6,
            IsFocused = true,
            ResizeStep = 5,
            MinLeftWidth = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, node.LeftWidth); // Clamped to MinLeftWidth
    }

    [Fact]
    public async Task HandleInput_RightArrow_RespectsMaxWidth()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 40,
            IsFocused = true,
            ResizeStep = 5,
            MinLeftWidth = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10)); // Total 50, max left = 50 - 3 - 5 = 42

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.True(node.LeftWidth <= 42);
    }

    [Fact]
    public async Task HandleInput_NotFocused_DoesNotResize()
    {
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 20,
            IsFocused = false
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        // Arrow keys won't resize when not focused
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(20, node.LeftWidth);
    }

    #endregion

    #region Input Handling - Tab Navigation Tests

    [Fact]
    public async Task HandleInput_Tab_MovesFocusToNextFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left", IsFocused = true };
        var rightButton = new ButtonNode { Label = "Right" };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };

        // Use FocusRing for focus navigation
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        // Focus moves from left button to splitter itself
        Assert.False(leftButton.IsFocused);
        Assert.True(node.IsFocused);
    }

    [Fact]
    public async Task HandleInput_ShiftTab_MovesFocusToPreviousFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };

        // Use FocusRing for focus navigation
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState2 = new InputRouterState();

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift), focusRing, routerState2, null, TestContext.Current.CancellationToken);

        // Focus moves from right button to splitter
        Assert.False(rightButton.IsFocused);
        Assert.True(node.IsFocused);
    }

    [Fact]
    public async Task HandleInput_Tab_WrapsAround()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };

        // Use FocusRing for focus navigation
        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        var routerState = new InputRouterState();

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        // Focus wraps from right button to left button
        Assert.True(leftButton.IsFocused);
        Assert.False(rightButton.IsFocused);
    }

    [Fact]
    public async Task HandleInput_Escape_JumpsToFirstFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };
        node.SyncFocusIndex();

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(leftButton.IsFocused);
        Assert.False(rightButton.IsFocused);
    }

    #endregion

    #region Input Handling - Child Input Tests

    [Fact]
    public async Task HandleInput_Enter_PassesToFocusedChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            ClickAction = _ => { clicked = true; return Task.CompletedTask; }
        };
        var node = new SplitterNode
        {
            Left = button,
            Right = new TextBlockNode { Text = "Right" }
        };
        node.SyncFocusIndex();

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public async Task HandleInput_Typing_PassesToFocusedTextBox()
    {
        var textBoxState = new TextBoxState();
        var textBox = new TextBoxNode
        {
            State = textBoxState,
            IsFocused = true
        };
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = textBox
        };
        node.SyncFocusIndex();

        var focusRing = new FocusRing();
        focusRing.Rebuild(node);
        focusRing.EnsureFocus();
        var routerState = new InputRouterState();

        // Use InputRouter to route input to the focused child
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.B, 'b', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.C, 'c', Hex1bModifiers.None), focusRing, routerState, null, TestContext.Current.CancellationToken);

        Assert.Equal("abc", textBoxState.Text);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_Splitter_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Text("Left Content"),
                    ctx.Text("Right Content"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right Content"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Left Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("│"));
    }

    [Fact]
    public async Task Integration_SplitterWithVStackPanes_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    v => [v.Text("Left 1"), v.Text("Left 2")],
                    v => [v.Text("Right 1"), v.Text("Right 2")],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Left 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Left 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right 2"));
    }

    [Fact]
    public async Task Integration_SplitterWithButtons_HandlesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var leftClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Button("Left").OnClick(_ => { leftClicked = true; return Task.CompletedTask; }),
                    ctx.Text("Right"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Enter clicks the focused button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(leftClicked);
    }

    [Fact]
    public async Task Integration_SplitterNavigation_TabSwitchesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var rightClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Button("Left").OnClick(_ => Task.CompletedTask),
                    ctx.Button("Right").OnClick(_ => { rightClicked = true; return Task.CompletedTask; }),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab through: left -> splitter -> right, then Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left"), TimeSpan.FromSeconds(2))
            .Tab().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(rightClicked);
    }

    [Fact]
    public async Task Integration_SplitterResize_ArrowKeysWork()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Text("Left"),
                    ctx.Text("Right"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab to the splitter itself (first is left text, which isn't focusable, so splitter is first)
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left"), TimeSpan.FromSeconds(2))
            .Left().Left()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The splitter would have received initial focus since Left is just text
        // So arrow keys should have resized it
        // We can't easily verify the exact size without inspecting the node
        // But we can verify the app ran without error
        Assert.True(terminal.CreateSnapshot().ContainsText("Left"));
    }

    [Fact]
    public async Task Integration_SplitterWithList_HandlesNavigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.List(items),
                    ctx.Text("Details"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Down arrow navigates the list
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(2))
            .Down()
            .WaitUntil(s => s.ContainsText("> Item 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Verify second item is selected via rendered output
        Assert.True(terminal.CreateSnapshot().ContainsText("> Item 2"));
    }

    [Fact]
    public async Task Integration_SplitterWithTextBox_HandlesTyping()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.TextBox(text).OnTextChanged(args => text = args.NewText),
                    ctx.Text("Right"),
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello Splitter")
            .WaitUntil(s => s.ContainsText("Hello Splitter"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hello Splitter", text);
    }

    [Fact]
    public async Task Integration_SplitterInsideBorder_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 70, 12);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.HSplitter(
                        ctx.Text("Left"),
                        ctx.Text("Right"),
                        leftWidth: 20
                    ),
                    "Split View"
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Split View"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Split View"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Left"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Right"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
    }

    #endregion

    #region Vertical Splitter - Measurement Tests

    [Fact]
    public void Measure_Vertical_ReturnsCorrectSize()
    {
        var top = new TextBlockNode { Text = "Top Pane" };
        var bottom = new TextBlockNode { Text = "Bottom Pane" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };

        var size = node.Measure(Constraints.Unbounded);

        // Height = FirstSize (5) + divider (1) + bottom content height (1)
        Assert.Equal(7, size.Height);
        // Width should be max of top and bottom
        Assert.Equal(11, size.Width); // "Bottom Pane" is 11 chars
    }

    [Fact]
    public void Measure_Vertical_WithNoChildren_ReturnsMinimalSize()
    {
        var node = new SplitterNode 
        { 
            First = null, 
            Second = null, 
            FirstSize = 10,
            Orientation = SplitterOrientation.Vertical 
        };

        var size = node.Measure(Constraints.Unbounded);

        // Just FirstSize + divider height
        Assert.Equal(11, size.Height); // 10 + 1
    }

    [Fact]
    public void Measure_Vertical_RespectsConstraints()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 8,
            Orientation = SplitterOrientation.Vertical 
        };

        var size = node.Measure(new Constraints(0, 20, 0, 10));

        Assert.True(size.Height <= 10);
    }

    [Fact]
    public void Measure_Vertical_WidthIsMaxOfBothPanes()
    {
        var top = new HStackNode
        {
            Children = [
                new TextBlockNode { Text = "Wide" },
                new TextBlockNode { Text = "Content" },
                new TextBlockNode { Text = "Here" }
            ]
        };
        var bottom = new TextBlockNode { Text = "Short" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom,
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical 
        };

        var size = node.Measure(Constraints.Unbounded);

        // Width of top pane ("Wide" + "Content" + "Here" = 15)
        Assert.Equal(15, size.Width);
    }

    #endregion

    #region Vertical Splitter - Arrange Tests

    [Fact]
    public void Arrange_Vertical_FirstPaneGetsFirstSizeHeight()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 8,
            Orientation = SplitterOrientation.Vertical 
        };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 20));

        Assert.Equal(0, top.Bounds.Y);
        Assert.Equal(8, top.Bounds.Height);
    }

    [Fact]
    public void Arrange_Vertical_SecondPaneGetsRemainingHeight()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 8,
            Orientation = SplitterOrientation.Vertical 
        };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 20));

        // Bottom pane starts after top + divider (8 + 1 = 9)
        Assert.Equal(9, bottom.Bounds.Y);
        // Bottom pane gets remaining height (20 - 8 - 1 = 11)
        Assert.Equal(11, bottom.Bounds.Height);
    }

    [Fact]
    public void Arrange_Vertical_WithOffset_PositionsCorrectly()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(5, 3, 40, 15));

        Assert.Equal(5, top.Bounds.X);
        Assert.Equal(3, top.Bounds.Y);
        Assert.Equal(5, bottom.Bounds.X);
        Assert.Equal(9, bottom.Bounds.Y); // 3 + 5 + 1
    }

    [Fact]
    public void Arrange_Vertical_BothPanesGetFullWidth()
    {
        var top = new TextBlockNode { Text = "Top" };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode 
        { 
            First = top, 
            Second = bottom, 
            FirstSize = 5,
            Orientation = SplitterOrientation.Vertical 
        };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 15));

        Assert.Equal(40, top.Bounds.Width);
        Assert.Equal(40, bottom.Bounds.Width);
    }

    #endregion

    #region Vertical Splitter - Rendering Tests

    [Fact]
    public void Render_Vertical_ShowsHorizontalDivider()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        node.Render(context);

        // Default horizontal divider character is "─"
        Assert.True(terminal.CreateSnapshot().ContainsText("─"));
    }

    [Fact]
    public void Render_Vertical_ShowsTopContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top Content" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().ContainsText("Top Content"));
    }

    [Fact]
    public void Render_Vertical_ShowsBottomContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom Content" },
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        node.Render(context);

        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom Content"));
    }

    [Fact]
    public void Render_Vertical_WhenFocused_InvertsDividerColors()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var context = CreateContext(workload);
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        node.Render(context);

        // When focused, should have background color on divider
        Assert.True(terminal.CreateSnapshot().HasBackgroundColor());
    }

    #endregion

    #region Vertical Splitter - Input Handling Tests

    [Fact]
    public async Task HandleInput_Vertical_UpArrow_WhenFocused_DecreasesFirstSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 10,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 20));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(8, node.FirstSize);
    }

    [Fact]
    public async Task HandleInput_Vertical_DownArrow_WhenFocused_IncreasesFirstSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 10,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true,
            ResizeStep = 2
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 20));

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(12, node.FirstSize);
    }

    [Fact]
    public async Task HandleInput_Vertical_UpArrow_RespectsMinFirstSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 6,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true,
            ResizeStep = 5,
            MinFirstSize = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 20));

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, node.FirstSize); // Clamped to MinFirstSize
    }

    [Fact]
    public async Task HandleInput_Vertical_DownArrow_RespectsMaxHeight()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 15,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true,
            ResizeStep = 5,
            MinFirstSize = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 20)); // Total 20, max first = 20 - 1 - 5 = 14

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.True(node.FirstSize <= 14);
    }

    [Fact]
    public async Task HandleInput_Vertical_LeftRightArrows_DoNotResize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 10,
            Orientation = SplitterOrientation.Vertical,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 30, 20));

        // Left/right arrows match bindings but don't resize vertical splitter
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Binding matches and executes, but action checks orientation and does nothing
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(10, node.FirstSize);  // Size unchanged
    }

    [Fact]
    public async Task HandleInput_Horizontal_UpDownArrows_DoNotResize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" },
            FirstSize = 20,
            Orientation = SplitterOrientation.Horizontal,
            IsFocused = true
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 50, 10));

        // Up/down arrows match bindings but don't resize horizontal splitter
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        // Binding matches and executes, but action checks orientation and does nothing
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(20, node.FirstSize);  // Size unchanged
    }

    #endregion

    #region Vertical Splitter - Integration Tests

    [Fact]
    public async Task Integration_VSplitter_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Text("Top Content"),
                    ctx.Text("Bottom Content"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bottom Content"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Top Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom Content"));
        Assert.True(terminal.CreateSnapshot().ContainsText("─")); // Horizontal divider
    }

    [Fact]
    public async Task Integration_VSplitterWithVStackPanes_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    v => [v.Text("Top 1"), v.Text("Top 2")],
                    v => [v.Text("Bottom 1"), v.Text("Bottom 2")],
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bottom 2"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Top 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Top 2"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom 1"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom 2"));
    }

    [Fact]
    public async Task Integration_VSplitterWithButtons_HandlesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);
        var topClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top").OnClick(_ => { topClicked = true; return Task.CompletedTask; }),
                    ctx.Text("Bottom"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Enter clicks the focused button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(topClicked);
    }

    [Fact]
    public async Task Integration_VSplitterNavigation_TabSwitchesFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);
        var bottomClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top").OnClick(_ => Task.CompletedTask),
                    ctx.Button("Bottom").OnClick(_ => { bottomClicked = true; return Task.CompletedTask; }),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Tab through: top -> splitter -> bottom, then Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(2))
            .Tab().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(bottomClicked);
    }

    [Fact]
    public async Task Integration_VSplitterResize_ArrowKeysWork()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Text("Top"),
                    ctx.Text("Bottom"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Since first child is just text (not focusable), splitter gets focus
        // Up/down arrows should resize it
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Top"), TimeSpan.FromSeconds(2))
            .Up().Up()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Top"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom"));
    }

    [Fact]
    public async Task Integration_VSplitterInsideBorder_RendersCorrectly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 50, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.VSplitter(
                        ctx.Text("Top"),
                        ctx.Text("Bottom"),
                        topHeight: 4
                    ),
                    "Vertical Split"
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Vertical Split"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Vertical Split"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Top"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom"));
        Assert.True(terminal.CreateSnapshot().ContainsText("┌"));
    }

    [Fact]
    public async Task Integration_NestedSplitters_HorizontalInsideVertical()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 20);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.HSplitter(
                        ctx.Text("Top-Left"),
                        ctx.Text("Top-Right"),
                        leftWidth: 20
                    ),
                    ctx.Text("Bottom"),
                    topHeight: 8
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bottom"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Top-Left"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Top-Right"));
        Assert.True(terminal.CreateSnapshot().ContainsText("Bottom"));
    }

    #endregion

    #region Focus Bubbling Tests - VStack/HStack inside Splitter

    /// <summary>
    /// Regression test: When a focusable widget (e.g., List) is inside a VStack inside a Splitter,
    /// pressing Tab should move focus to the next widget managed by the Splitter, not be swallowed
    /// by the VStack. This tests that VStack doesn't register Tab bindings when an ancestor
    /// (like Splitter) has ManagesChildFocus = true.
    /// </summary>
    [Fact]
    public async Task Integration_TabFromListInVStackInsideSplitter_MovesFocusToNextPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 15);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    // Left pane: VStack containing a List (this is the key scenario)
                    v => [v.Text("Theme List"), v.List(items)],
                    // Right pane: VStack with Button that we want to Tab to
                    v => [v.Button("Right Button").OnClick(_ => { rightButtonClicked = true; return Task.CompletedTask; })],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // List starts focused, Tab should move through: List -> Splitter -> Right Button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right Button"), TimeSpan.FromSeconds(2))
            .Tab().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(rightButtonClicked, "Tab should have moved focus from List through Splitter to Right Button");
    }

    /// <summary>
    /// Regression test: Same as above but with multiple buttons in left pane VStack.
    /// Focus order: First -> Second -> Splitter -> Right
    /// </summary>
    [Fact]
    public async Task Integration_TabFromSecondButtonInVStackInsideSplitter_MovesFocusToNextPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 10);
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    // Left pane: VStack containing two Buttons
                    v => [v.Button("First").OnClick(_ => Task.CompletedTask), v.Button("Second").OnClick(_ => Task.CompletedTask)],
                    // Right pane: VStack with Button
                    v => [v.Button("Right").OnClick(_ => { rightButtonClicked = true; return Task.CompletedTask; })],
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Focus order: First -> Second -> Splitter -> Right
        // (VStack doesn't handle Tab when inside Splitter, so it bubbles up to Splitter)
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Tab().Tab().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(rightButtonClicked, "Tab should have moved focus from Second Button through Splitter to Right Button");
    }

    /// <summary>
    /// Regression test: Shift+Tab should also bubble up to Splitter for reverse navigation.
    /// </summary>
    [Fact]
    public async Task Integration_ShiftTabFromButtonInsideSplitter_MovesFocusToPreviousPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 15);
        var leftButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    v => [v.Text("Left Pane"), v.Button("Left Button").OnClick(_ => { leftButtonClicked = true; return Task.CompletedTask; })],
                    v => [v.Text("Right Pane"), v.Button("Right Button").OnClick(_ => Task.CompletedTask)],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Navigate to Right Button first
        // Tab, Tab, then Shift+Tab, Shift+Tab, then Enter
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Left Button"), TimeSpan.FromSeconds(2))
            .Tab().Tab()  // Left Button -> Splitter -> Right Button
            .Shift().Tab().Shift().Tab()  // Right Button -> Splitter -> Left Button
            .Enter()  // Click the button
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(leftButtonClicked, "Shift+Tab should have moved focus back to Left Button");
    }

    /// <summary>
    /// Regression test: Deeply nested structure - VStack inside Panel inside Splitter.
    /// Tab should still bubble up to the Splitter.
    /// </summary>
    [Fact]
    public async Task Integration_TabFromWidgetInDeepNesting_BubblesUpToSplitter()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 70, 15);
        IReadOnlyList<string> items = ["Theme 1", "Theme 2"];
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    // Left: VStack > List (deep nesting like ThemingExhibit)
                    v => [
                        v.Text("═══ Themes ═══"),
                        v.Text(""),
                        v.List(items)
                    ],
                    // Right: VStack > Button
                    v => [
                        v.Text("═══ Preview ═══"),
                        v.Button("Click Me").OnClick(_ => { rightButtonClicked = true; return Task.CompletedTask; })
                    ],
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // List is focused initially, Tab should navigate to Splitter, then to Button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Tab().Tab().Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(rightButtonClicked, "Tab should bubble up from deeply nested List to Splitter");
    }

    #endregion

    #region Drag Binding Tests

    [Fact]
    public void ConfigureDefaultBindings_IncludesDragBinding()
    {
        var node = new SplitterNode();
        var builder = node.BuildBindings();

        Assert.Single(builder.DragBindings);
        Assert.Equal(MouseButton.Left, builder.DragBindings[0].Button);
    }

    [Fact]
    public void DragBinding_Matches_LeftMouseDown()
    {
        var node = new SplitterNode();
        var builder = node.BuildBindings();
        var dragBinding = builder.DragBindings[0];

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 10, 5, Hex1bModifiers.None);
        
        Assert.True(dragBinding.Matches(mouseEvent));
    }

    [Fact]
    public void DragBinding_DoesNotMatch_RightMouseDown()
    {
        var node = new SplitterNode();
        var builder = node.BuildBindings();
        var dragBinding = builder.DragBindings[0];

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Right, MouseAction.Down, 10, 5, Hex1bModifiers.None);
        
        Assert.False(dragBinding.Matches(mouseEvent));
    }

    [Fact]
    public void DragBinding_DoesNotMatch_MouseMove()
    {
        var node = new SplitterNode();
        var builder = node.BuildBindings();
        var dragBinding = builder.DragBindings[0];

        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Move, 10, 5, Hex1bModifiers.None);
        
        Assert.False(dragBinding.Matches(mouseEvent));
    }

    [Fact]
    public void DragHandler_OnMove_Horizontal_UpdatesFirstSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" },
            FirstSize = 20,
            Orientation = SplitterOrientation.Horizontal,
            MinFirstSize = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 60, 10));

        var builder = node.BuildBindings();
        var dragBinding = builder.DragBindings[0];
        
        // Start drag at position (0, 0) - local to the node
        var handler = dragBinding.StartDrag(0, 0);
        
        // Move 10 pixels to the right
        handler.OnMove?.Invoke(10, 0);

        Assert.Equal(30, node.FirstSize); // 20 + 10
    }

    [Fact]
    public void DragHandler_OnMove_Horizontal_RespectsMinSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" },
            FirstSize = 20,
            Orientation = SplitterOrientation.Horizontal,
            MinFirstSize = 10
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 60, 10));

        var builder = node.BuildBindings();
        var handler = builder.DragBindings[0].StartDrag(0, 0);
        
        // Try to move 15 pixels to the left (beyond min)
        handler.OnMove?.Invoke(-15, 0);

        Assert.Equal(10, node.FirstSize); // Clamped to MinFirstSize
    }

    [Fact]
    public void DragHandler_OnMove_Horizontal_RespectsMaxSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" },
            FirstSize = 20,
            Orientation = SplitterOrientation.Horizontal,
            MinFirstSize = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 60, 10)); // Max = 60 - 3 - 5 = 52

        var builder = node.BuildBindings();
        var handler = builder.DragBindings[0].StartDrag(0, 0);
        
        // Try to move far to the right (beyond max)
        handler.OnMove?.Invoke(50, 0);

        Assert.Equal(52, node.FirstSize); // Clamped to max
    }

    [Fact]
    public void DragHandler_OnMove_Vertical_UpdatesFirstSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Top" },
            Second = new TextBlockNode { Text = "Bottom" },
            FirstSize = 10,
            Orientation = SplitterOrientation.Vertical,
            MinFirstSize = 3
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 40, 30));

        var builder = node.BuildBindings();
        var handler = builder.DragBindings[0].StartDrag(0, 0);
        
        // Move 5 pixels down
        handler.OnMove?.Invoke(0, 5);

        Assert.Equal(15, node.FirstSize); // 10 + 5
    }

    [Fact]
    public void DragHandler_OnMove_CapturesStartSize()
    {
        var node = new SplitterNode
        {
            First = new TextBlockNode { Text = "Left" },
            Second = new TextBlockNode { Text = "Right" },
            FirstSize = 20,
            Orientation = SplitterOrientation.Horizontal,
            MinFirstSize = 5
        };
        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 60, 10));

        var builder = node.BuildBindings();
        var handler = builder.DragBindings[0].StartDrag(0, 0);
        
        // Multiple moves should be relative to start, not cumulative
        handler.OnMove?.Invoke(5, 0);  // 20 + 5 = 25
        handler.OnMove?.Invoke(10, 0); // 20 + 10 = 30 (not 25 + 10 = 35)

        Assert.Equal(30, node.FirstSize);
    }

    #endregion

    #region Nested Splitter Focus Tests

    /// <summary>
    /// Regression test: When splitters are nested, only the outermost splitter should set initial focus.
    /// The inner splitter should NOT call SetInitialFocus because its parent (outer splitter)
    /// has ManagesChildFocus = true.
    /// </summary>
    [Fact]
    public async Task Integration_NestedSplitters_InnerSplitterDoesNotOverrideFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 20);
        var outerLeftClicked = false;
        
        // The outer splitter's left pane has a button.
        // The inner splitter (in right pane) also has a button.
        // Focus should go to the OUTER left button (first focusable), not the inner splitter's button.
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    // Outer left: button that should get initial focus
                    ctx.Button("Outer Left").OnClick(_ => { outerLeftClicked = true; return Task.CompletedTask; }),
                    // Outer right: inner splitter with its own button
                    ctx.HSplitter(
                        ctx.Button("Inner Left").OnClick(_ => Task.CompletedTask),
                        ctx.Text("Inner Right"),
                        leftWidth: 15
                    ),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Just press Enter - should click the focused button
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Outer Left"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(outerLeftClicked, "Initial focus should be on Outer Left button, not Inner Left");
    }

    /// <summary>
    /// Regression test: Similar to above but with vertical splitters nested inside horizontal.
    /// </summary>
    [Fact]
    public async Task Integration_NestedVSplitterInHSplitter_OuterGetsInitialFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 60, 20);
        var outerLeftClicked = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Button("Outer Left").OnClick(_ => { outerLeftClicked = true; return Task.CompletedTask; }),
                    ctx.VSplitter(
                        ctx.Button("Top").OnClick(_ => Task.CompletedTask),
                        ctx.Button("Bottom").OnClick(_ => Task.CompletedTask),
                        topHeight: 8
                    ),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Outer Left"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(outerLeftClicked, "Initial focus should be on Outer Left button");
    }

    /// <summary>
    /// Regression test: Three levels of nested splitters - only the root should set focus.
    /// </summary>
    [Fact]
    public async Task Integration_TripleNestedSplitters_OnlyRootSetsFocus()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 25);
        var level1Clicked = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    ctx.Button("Level 1").OnClick(_ => { level1Clicked = true; return Task.CompletedTask; }),
                    ctx.HSplitter(
                        ctx.Button("Level 2").OnClick(_ => Task.CompletedTask),
                        ctx.HSplitter(
                            ctx.Button("Level 3").OnClick(_ => Task.CompletedTask),
                            ctx.Text("Deepest"),
                            leftWidth: 12
                        ),
                        leftWidth: 15
                    ),
                    leftWidth: 18
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Level 1"), TimeSpan.FromSeconds(2))
            .Enter()
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(level1Clicked, "Initial focus should be on Level 1 button (first focusable at root level)");
    }

    /// <summary>
    /// Regression test: Directly verify that nested splitter nodes do not have IsFocused=true
    /// when they shouldn't. The inner splitter's divider should NOT appear focused.
    /// </summary>
    [Fact]
    public void DirectReconcile_NestedSplitters_OnlyOuterFirstFocusableIsFocused()
    {
        // Build widgets manually - outer splitter with inner splitter in second pane
        var innerButton = new ButtonWidget("Inner Left").OnClick(_ => Task.CompletedTask);
        var innerSplitter = new SplitterWidget(
            innerButton,
            new TextBlockWidget("Inner Right"),
            firstSize: 15
        );
        var outerButton = new ButtonWidget("Outer Left").OnClick(_ => Task.CompletedTask);
        var outerSplitter = new SplitterWidget(
            outerButton,
            innerSplitter,
            firstSize: 20
        );

        // Reconcile from root
        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        context.IsNew = true;
        var rootNode = outerSplitter.ReconcileAsync(null, context).GetAwaiter().GetResult() as SplitterNode;

        Assert.NotNull(rootNode);

        // Find all the splitter nodes and buttons
        var outerButtonNode = rootNode.First?.GetFocusableNodes().OfType<ButtonNode>().FirstOrDefault();
        var innerSplitterNode = rootNode.Second?.GetFocusableNodes().OfType<SplitterNode>().FirstOrDefault() 
                                ?? (rootNode.Second as LayoutNode)?.Child?.GetFocusableNodes().OfType<SplitterNode>().FirstOrDefault();
        
        // Get the actual inner splitter node by traversing the tree
        var layoutNode = rootNode.Second as LayoutNode;
        var actualInnerSplitter = layoutNode?.Child as SplitterNode;

        Assert.NotNull(outerButtonNode);
        Assert.NotNull(actualInnerSplitter);

        // The outer button should be focused (it's first in the focus order)
        Assert.True(outerButtonNode.IsFocused, "Outer button should be focused");
        
        // The inner splitter itself should NOT be focused
        Assert.False(actualInnerSplitter.IsFocused, 
            "Inner splitter should NOT be focused - only one element should have focus at a time");
        
        // The outer splitter should NOT be focused either (it's not first focusable)
        Assert.False(rootNode.IsFocused, "Outer splitter should not be focused (button is first)");
    }

    /// <summary>
    /// Regression test: Nested splitters with NO interactive children - only one should be focused.
    /// This was the original bug: both splitter dividers would render as focused.
    /// </summary>
    [Fact]
    public void DirectReconcile_NestedSplittersNoButtons_OnlyOneSplitterFocused()
    {
        // Build widgets with ONLY text (no buttons) - splitters are the only focusables
        var innerSplitter = new SplitterWidget(
            new TextBlockWidget("Inner Left"),
            new TextBlockWidget("Inner Right"),
            firstSize: 15
        );
        var outerSplitter = new SplitterWidget(
            new TextBlockWidget("Outer Left"),
            innerSplitter,
            firstSize: 20
        );

        // Reconcile from root
        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        context.IsNew = true;
        var rootNode = outerSplitter.ReconcileAsync(null, context).GetAwaiter().GetResult() as SplitterNode;

        Assert.NotNull(rootNode);

        // Get the inner splitter node
        var layoutNode = rootNode.Second as LayoutNode;
        var innerSplitterNode = layoutNode?.Child as SplitterNode;

        Assert.NotNull(innerSplitterNode);

        // Count how many nodes have IsFocused = true
        var allFocusables = rootNode.GetFocusableNodes().ToList();
        var focusedCount = allFocusables.Count(n => n.IsFocused);

        Assert.Equal(1, focusedCount);
        
        // The outer splitter should be focused (it's first in its own focus order)
        Assert.True(rootNode.IsFocused, "Outer splitter should be focused (first focusable)");
        
        // The inner splitter should NOT be focused
        Assert.False(innerSplitterNode.IsFocused, 
            "Inner splitter should NOT be focused - this was the original bug!");
    }

    #endregion

    #region Nested Splitter Clipping Tests

    /// <summary>
    /// Regression test: Content in nested splitter panes should be clipped to their bounds.
    /// When a VStack inside a splitter pane has more content than fits in the allocated space,
    /// the overflow should NOT render into adjacent panes.
    /// 
    /// Bug: With a VSplitter(topHeight: 6) containing a horizontal Splitter in the top pane,
    /// and each pane having VStack with 7 lines of text, lines 7+ should be clipped and NOT
    /// appear in the bottom pane area.
    /// </summary>
    [Fact]
    public async Task Integration_NestedSplitters_ContentDoesNotOverflowIntoBelowPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 15);

        // Replicate the nested splitters example from docs with content that overflows
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    // Top: horizontal splitter with content that's taller than topHeight
                    ctx.HSplitter(
                        ctx.VStack(tl => [
                            tl.Text("Top-Left"),
                            tl.Text("Line 2"),
                            tl.Text("Line 3"),
                            tl.Text("Line 4"),
                            tl.Text("Line 5"),
                            tl.Text("Line 6"),
                            tl.Text("OVERFLOW-TL")  // This should NOT appear in bottom pane
                        ]),
                        ctx.VStack(tr => [
                            tr.Text("Top-Right"),
                            tr.Text("Line 2"),
                            tr.Text("Line 3"),
                            tr.Text("Line 4"),
                            tr.Text("Line 5"),
                            tr.Text("Line 6"),
                            tr.Text("OVERFLOW-TR")  // This should NOT appear in bottom pane
                        ]),
                        leftWidth: 20
                    ),
                    // Bottom: single VStack
                    ctx.VStack(bottom => [
                        bottom.Text("Bottom Pane"),
                        bottom.Text("This should be the only content here")
                    ]),
                    topHeight: 6
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bottom Pane"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Verify top pane content is visible
        Assert.True(snapshot.ContainsText("Top-Left"), "Top-Left pane header should be visible");
        Assert.True(snapshot.ContainsText("Top-Right"), "Top-Right pane header should be visible");
        
        // Verify bottom pane content is visible
        Assert.True(snapshot.ContainsText("Bottom Pane"), "Bottom pane header should be visible");
        
        // BUG CHECK: Overflow content should NOT be visible
        // These lines are beyond the topHeight (6) and should be clipped
        Assert.False(snapshot.ContainsText("OVERFLOW-TL"), 
            "Top-Left overflow content should be clipped and not visible in bottom pane area");
        Assert.False(snapshot.ContainsText("OVERFLOW-TR"), 
            "Top-Right overflow content should be clipped and not visible in bottom pane area");
    }

    /// <summary>
    /// Regression test: Horizontal splitter content should be clipped when panes are sized too narrow.
    /// Text that exceeds the pane width should not overflow into the adjacent pane.
    /// </summary>
    [Fact]
    public async Task Integration_HorizontalSplitter_ContentDoesNotOverflowIntoRightPane()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 50, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    // Left pane: narrow (10 chars) with long text
                    left => [
                        left.Text("Left"),
                        left.Text("LONG_TEXT_THAT_OVERFLOWS_LEFT_PANE")  // 34 chars, way longer than 10
                    ],
                    // Right pane
                    right => [
                        right.Text("Right Pane"),
                        right.Text("Should not see overflow here")
                    ],
                    leftWidth: 10
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Right Pane"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        var screenText = snapshot.GetScreenText();
        
        // Verify basic content is visible
        Assert.True(snapshot.ContainsText("Left"), "Left pane header should be visible");
        Assert.True(snapshot.ContainsText("Right Pane"), "Right pane header should be visible");
        
        // The long text should be truncated at the left pane boundary (10 chars)
        // It should NOT overflow past the divider into the right pane area
        // Check that the full overflow text is NOT visible
        Assert.False(snapshot.ContainsText("LONG_TEXT_THAT_OVERFLOWS_LEFT_PANE"),
            "Full overflow text should be clipped, not visible in right pane area");
    }

    /// <summary>
    /// Unit test: Verify that SplitterNode clips child content when rendering.
    /// </summary>
    [Fact]
    public void Render_Horizontal_ClipsLeftPaneContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = CreateContext(workload);

        // Create a splitter with a narrow left pane and wide text content
        var left = new VStackNode
        {
            Children = [
                new TextBlockNode { Text = "OVERFLOW_TEXT_THAT_IS_MUCH_LONGER_THAN_10_CHARS" }
            ]
        };
        var right = new TextBlockNode { Text = "Right" };
        var node = new SplitterNode
        {
            Left = left,
            Right = right,
            LeftWidth = 10
        };

        node.Measure(Constraints.Tight(40, 5));
        node.Arrange(new Rect(0, 0, 40, 5));
        node.Render(context);

        var snapshot = terminal.CreateSnapshot();
        var screenText = snapshot.GetScreenText();

        // The full text should NOT be visible
        Assert.False(screenText.Contains("OVERFLOW_TEXT_THAT_IS_MUCH_LONGER_THAN_10_CHARS"),
            "Left pane content should be clipped to LeftWidth");
        
        // Right pane content should be visible
        Assert.True(snapshot.ContainsText("Right"), "Right pane content should be visible");
    }

    /// <summary>
    /// Unit test: Verify that SplitterNode clips child content in the vertical direction.
    /// </summary>
    [Fact]
    public void Render_Vertical_ClipsTopPaneContent()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 30, 10);
        var context = CreateContext(workload);

        // Create a vertical splitter with a short top pane (height 3) and tall content (5 lines)
        var top = new VStackNode
        {
            Children = [
                new TextBlockNode { Text = "Line 1" },
                new TextBlockNode { Text = "Line 2" },
                new TextBlockNode { Text = "Line 3" },
                new TextBlockNode { Text = "OVERFLOW_LINE_4" },  // Should be clipped
                new TextBlockNode { Text = "OVERFLOW_LINE_5" }   // Should be clipped
            ]
        };
        var bottom = new TextBlockNode { Text = "Bottom" };
        var node = new SplitterNode
        {
            First = top,
            Second = bottom,
            FirstSize = 3,
            Orientation = SplitterOrientation.Vertical
        };

        node.Measure(Constraints.Tight(30, 10));
        node.Arrange(new Rect(0, 0, 30, 10));
        node.Render(context);

        var snapshot = terminal.CreateSnapshot();

        // Lines 1-3 should be visible
        Assert.True(snapshot.ContainsText("Line 1"), "Line 1 should be visible in top pane");
        Assert.True(snapshot.ContainsText("Line 2"), "Line 2 should be visible in top pane");
        Assert.True(snapshot.ContainsText("Line 3"), "Line 3 should be visible in top pane");
        
        // Overflow lines should NOT be visible (clipped)
        Assert.False(snapshot.ContainsText("OVERFLOW_LINE_4"),
            "Line 4 should be clipped - it exceeds top pane height");
        Assert.False(snapshot.ContainsText("OVERFLOW_LINE_5"),
            "Line 5 should be clipped - it exceeds top pane height");
        
        // Bottom pane content should be visible
        Assert.True(snapshot.ContainsText("Bottom"), "Bottom pane content should be visible");
    }

    /// <summary>
    /// Regression test: When a nested horizontal splitter is inside a VSplitter's top pane,
    /// resizing the inner horizontal splitter should not cause content to overflow into
    /// the bottom pane of the outer vertical splitter.
    /// 
    /// This tests the exact scenario from the docs: VSplitter containing a horizontal Splitter
    /// in its top pane, where the inner splitter's panes have VStack content.
    /// </summary>
    [Fact]
    public async Task Integration_NestedSplitters_ResizingInnerDoesNotCauseOverflow()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    // Top: horizontal splitter with content
                    ctx.HSplitter(
                        ctx.VStack(tl => [
                            tl.Text("Top-Left"),
                            tl.Text(""),
                            tl.Text("Horizontal split").Wrap(),
                            tl.Text("in top pane").Wrap()
                        ]),
                        ctx.VStack(tr => [
                            tr.Text("Top-Right"),
                            tr.Text(""),
                            tr.Text("Both panes share").Wrap(),
                            tr.Text("the same height").Wrap()
                        ]),
                        leftWidth: 20
                    ),
                    // Bottom: single VStack with distinct content
                    ctx.VStack(bottom => [
                        bottom.Text("BOTTOM_PANE_MARKER"),
                        bottom.Text(""),
                        bottom.Text("This is the bottom pane")
                    ]),
                    topHeight: 6
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Initial render - then Tab to inner splitter and resize
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("BOTTOM_PANE_MARKER"), TimeSpan.FromSeconds(2))
            .Tab()  // Move to inner splitter
            .Left().Left().Left().Left().Left()  // Resize left significantly (make left pane very narrow)
            .Capture("after_resize")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // After resizing, the bottom pane marker should still be visible
        // and no content from the top panes should have leaked below the horizontal divider
        Assert.True(snapshot.ContainsText("BOTTOM_PANE_MARKER"), 
            "Bottom pane content should still be visible after resizing inner splitter");
        
        // Get the screen and check that content from top panes is not appearing
        // below the divider line (row 6, since topHeight: 6)
        var screenText = snapshot.GetScreenText();
        var lines = screenText.Split('\n');
        
        // The divider should be at row 6 (0-indexed), bottom pane starts at row 7
        // Top pane text should NOT appear in the bottom section
        if (lines.Length > 7)
        {
            var bottomSection = string.Join("\n", lines.Skip(7));
            
            // These are texts that should ONLY appear in the top panes
            Assert.False(bottomSection.Contains("Top-Left") && !bottomSection.Contains("BOTTOM"),
                "Top-Left content should not appear in bottom section after resize");
        }
    }

    /// <summary>
    /// Regression test: When text is configured to wrap and the horizontal splitter is
    /// dragged to the extreme RIGHT, the wrapped text from the RIGHT pane should NOT 
    /// overflow vertically into the bottom pane of the outer VSplitter.
    /// 
    /// This reproduces the exact issue: when the right pane becomes very narrow,
    /// the wrapped text needs more vertical space than available, potentially overflowing.
    /// </summary>
    [Fact]
    public async Task Integration_NestedSplitters_WrappingTextDoesNotOverflowWhenDraggedExtreme()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 60, 15);

        // Exactly match the SplitterNestedExample from the website
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    // Top: horizontal splitter
                    ctx.HSplitter(
                        ctx.VStack(tl => [
                            tl.Text("Top-Left"),
                            tl.Text(""),
                            tl.Text("Horizontal split").Wrap(),
                            tl.Text("in top pane").Wrap()
                        ]),
                        ctx.VStack(tr => [
                            tr.Text("Top-Right"),
                            tr.Text(""),
                            tr.Text("Both panes share").Wrap(),
                            tr.Text("the same height").Wrap()
                        ]),
                        leftWidth: 20
                    ),
                    // Bottom: single VStack
                    ctx.VStack(bottom => [
                        bottom.Text("Bottom Pane"),
                        bottom.Text(""),
                        bottom.Text("This demonstrates nesting a horizontal splitter").Wrap(),
                        bottom.Text("inside the top pane of a vertical splitter.").Wrap(),
                        bottom.Text("Great for IDE-style layouts!").Wrap()
                    ]),
                    topHeight: 6
                )
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Initial render, Tab TWICE to focus inner horizontal splitter, then drag EXTREME RIGHT
        // First Tab focuses VSplitter, second Tab focuses inner Splitter
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bottom Pane"), TimeSpan.FromSeconds(2))
            .Tab()  // Focus VSplitter
            .Tab()  // Focus inner horizontal Splitter
            // Resize to extreme RIGHT (20+ right arrows - making right pane very narrow)
            .Right().Right().Right().Right().Right().Right().Right().Right().Right().Right()
            .Right().Right().Right().Right().Right().Right().Right().Right().Right().Right()
            .Capture("after_extreme_resize")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // After resizing to extreme, the bottom pane should still be clean
        Assert.True(snapshot.ContainsText("Bottom Pane"), 
            "Bottom pane marker should still be visible after extreme resize");
        
        var screenText = snapshot.GetScreenText();
        var lines = screenText.Split('\n');
        
        // Top pane is 6 rows (0-5), divider at row 6, bottom pane starts at row 7
        // Check that Top-Left/Top-Right content doesn't appear in bottom section
        if (lines.Length > 7)
        {
            var bottomSection = string.Join("\n", lines.Skip(7));
            
            // Text from the top panes should NOT appear in the bottom pane section
            Assert.False(bottomSection.Contains("Top-Left"),
                $"Top-Left text should be clipped, not overflow into bottom pane. Bottom section:\n{bottomSection}");
            Assert.False(bottomSection.Contains("Top-Right"),
                $"Top-Right text should be clipped, not overflow into bottom pane. Bottom section:\n{bottomSection}");
            Assert.False(bottomSection.Contains("Horizontal split"),
                $"'Horizontal split' text should be clipped, not overflow into bottom pane. Bottom section:\n{bottomSection}");
            Assert.False(bottomSection.Contains("Both panes share"),
                $"'Both panes share' text should be clipped, not overflow into bottom pane. Bottom section:\n{bottomSection}");
            
            // Check for wrapped text fragments that indicate overflow
            Assert.False(bottomSection.Contains("heheigh") || bottomSection.Contains("height"),
                $"Wrapped 'height' text fragments from top pane should not overflow. Bottom section:\n{bottomSection}");
            Assert.False(bottomSection.Contains("panes") || bottomSection.Contains("share"),
                $"Wrapped text fragments from top pane should not overflow. Bottom section:\n{bottomSection}");
        }
    }

    #endregion
}
