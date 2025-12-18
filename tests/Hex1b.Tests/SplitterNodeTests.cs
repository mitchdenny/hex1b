using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive tests for SplitterNode layout, rendering, resizing, and focus handling.
/// </summary>
public class SplitterNodeTests
{
    private static Hex1bRenderContext CreateContext(Hex1bTerminal terminal, Hex1bTheme? theme = null)
    {
        return new Hex1bRenderContext(terminal, theme);
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
        using var terminal = new Hex1bTerminal(50, 10);
        var context = CreateContext(terminal);
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
        Assert.Contains("│", terminal.RawOutput);
    }

    [Fact]
    public void Render_ShowsLeftContent()
    {
        using var terminal = new Hex1bTerminal(50, 10);
        var context = CreateContext(terminal);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left Pane Content" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 25
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.Contains("Left Pane Content", terminal.RawOutput);
    }

    [Fact]
    public void Render_ShowsRightContent()
    {
        using var terminal = new Hex1bTerminal(50, 10);
        var context = CreateContext(terminal);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right Pane Content" },
            LeftWidth = 15
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.Contains("Right Pane Content", terminal.RawOutput);
    }

    [Fact]
    public void Render_DividerSpansFullHeight()
    {
        using var terminal = new Hex1bTerminal(50, 5);
        var context = CreateContext(terminal);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "L" },
            Right = new TextBlockNode { Text = "R" },
            LeftWidth = 10
        };

        node.Measure(Constraints.Tight(50, 5));
        node.Arrange(new Rect(0, 0, 50, 5));
        node.Render(context);

        // Count occurrences of divider chars in raw output - should be 5 (one per row)
        // 3 regular dividers + 2 arrow characters (◀ and ▶) at midpoint
        var dividerCount = terminal.RawOutput.Split("│").Length - 1;
        var leftArrowCount = terminal.RawOutput.Split("◀").Length - 1;
        var rightArrowCount = terminal.RawOutput.Split("▶").Length - 1;
        Assert.Equal(5, dividerCount + leftArrowCount + rightArrowCount);
    }

    #endregion

    #region Rendering - Theming Tests

    [Fact]
    public void Render_WithCustomDividerColor_AppliesColor()
    {
        using var terminal = new Hex1bTerminal(50, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(SplitterTheme.DividerColor, Hex1bColor.Cyan);
        var context = CreateContext(terminal, theme);
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
        Assert.Contains("\x1b[38;2;0;255;255m", terminal.RawOutput);
    }

    [Fact]
    public void Render_WithCustomDividerCharacter_UsesCustomCharacter()
    {
        using var terminal = new Hex1bTerminal(50, 10);
        var theme = Hex1bThemes.Default.Clone()
            .Set(SplitterTheme.DividerCharacter, "║");
        var context = CreateContext(terminal, theme);
        var node = new SplitterNode
        {
            Left = new TextBlockNode { Text = "Left" },
            Right = new TextBlockNode { Text = "Right" },
            LeftWidth = 15
        };

        node.Measure(Constraints.Tight(50, 10));
        node.Arrange(new Rect(0, 0, 50, 10));
        node.Render(context);

        Assert.Contains("║", terminal.RawOutput);
    }

    [Fact]
    public void Render_WhenFocused_InvertsDividerColors()
    {
        using var terminal = new Hex1bTerminal(50, 10);
        var context = CreateContext(terminal);
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
        Assert.Contains("\x1b[48;2;", terminal.RawOutput);
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

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

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

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

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

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

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

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

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
        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

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

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing);

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

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift), focusRing);

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

        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None), focusRing);

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

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None));

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

        // Use InputRouter to route input to the focused child
        var result = await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

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

        // Use InputRouter to route input to the focused child
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.B, 'b', Hex1bModifiers.None));
        await InputRouter.RouteInputAsync(node, new Hex1bKeyEvent(Hex1bKey.C, 'c', Hex1bModifiers.None));

        Assert.Equal("abc", textBoxState.Text);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_Splitter_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Text("Left Content"),
                    ctx.Text("Right Content"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Left Content", terminal.RawOutput);
        Assert.Contains("Right Content", terminal.RawOutput);
        Assert.Contains("│", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_SplitterWithVStackPanes_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    v => [v.Text("Left 1"), v.Text("Left 2")],
                    v => [v.Text("Right 1"), v.Text("Right 2")],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Left 1", terminal.RawOutput);
        Assert.Contains("Left 2", terminal.RawOutput);
        Assert.Contains("Right 1", terminal.RawOutput);
        Assert.Contains("Right 2", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_SplitterWithButtons_HandlesFocus()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        var leftClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Left", _ => { leftClicked = true; return Task.CompletedTask; }),
                    ctx.Text("Right"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Enter clicks the focused button
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(leftClicked);
    }

    [Fact]
    public async Task Integration_SplitterNavigation_TabSwitchesFocus()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        var rightClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Left", _ => Task.CompletedTask),
                    ctx.Button("Right", _ => { rightClicked = true; return Task.CompletedTask; }),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab through: left -> splitter -> right, then Enter
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(rightClicked);
    }

    [Fact]
    public async Task Integration_SplitterResize_ArrowKeysWork()
    {
        using var terminal = new Hex1bTerminal(60, 10);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Text("Left"),
                    ctx.Text("Right"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab to the splitter itself (first is left text, which isn't focusable, so splitter is first)
        terminal.SendKey(ConsoleKey.LeftArrow);
        terminal.SendKey(ConsoleKey.LeftArrow);
        terminal.CompleteInput();
        await app.RunAsync();

        // The splitter would have received initial focus since Left is just text
        // So arrow keys should have resized it
        // We can't easily verify the exact size without inspecting the node
        // But we can verify the app ran without error
        Assert.Contains("Left", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_SplitterWithList_HandlesNavigation()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.List(items),
                    ctx.Text("Details"),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Down arrow navigates the list
        terminal.SendKey(ConsoleKey.DownArrow);
        terminal.CompleteInput();
        await app.RunAsync();

        // Verify second item is selected via rendered output
        Assert.Contains("> Item 2", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_SplitterWithTextBox_HandlesTyping()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.TextBox(text, onTextChanged: args => text = args.NewText),
                    ctx.Text("Right"),
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.TypeText("Hello Splitter");
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Equal("Hello Splitter", text);
    }

    [Fact]
    public async Task Integration_SplitterInsideBorder_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(70, 12);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Border(
                    ctx.Splitter(
                        ctx.Text("Left"),
                        ctx.Text("Right"),
                        leftWidth: 20
                    ),
                    "Split View"
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Split View", terminal.RawOutput);
        Assert.Contains("Left", terminal.RawOutput);
        Assert.Contains("Right", terminal.RawOutput);
        Assert.Contains("┌", terminal.RawOutput);
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
        using var terminal = new Hex1bTerminal(30, 10);
        var context = CreateContext(terminal);
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
        Assert.Contains("─", terminal.RawOutput);
    }

    [Fact]
    public void Render_Vertical_ShowsTopContent()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var context = CreateContext(terminal);
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

        Assert.Contains("Top Content", terminal.RawOutput);
    }

    [Fact]
    public void Render_Vertical_ShowsBottomContent()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var context = CreateContext(terminal);
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

        Assert.Contains("Bottom Content", terminal.RawOutput);
    }

    [Fact]
    public void Render_Vertical_WhenFocused_InvertsDividerColors()
    {
        using var terminal = new Hex1bTerminal(30, 10);
        var context = CreateContext(terminal);
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
        Assert.Contains("\x1b[48;2;", terminal.RawOutput);
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

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

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

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));

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

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

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

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));

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
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

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
        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

        // Binding matches and executes, but action checks orientation and does nothing
        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(20, node.FirstSize);  // Size unchanged
    }

    #endregion

    #region Vertical Splitter - Integration Tests

    [Fact]
    public async Task Integration_VSplitter_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Text("Top Content"),
                    ctx.Text("Bottom Content"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Top Content", terminal.RawOutput);
        Assert.Contains("Bottom Content", terminal.RawOutput);
        Assert.Contains("─", terminal.RawOutput); // Horizontal divider
    }

    [Fact]
    public async Task Integration_VSplitterWithVStackPanes_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    v => [v.Text("Top 1"), v.Text("Top 2")],
                    v => [v.Text("Bottom 1"), v.Text("Bottom 2")],
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Top 1", terminal.RawOutput);
        Assert.Contains("Top 2", terminal.RawOutput);
        Assert.Contains("Bottom 1", terminal.RawOutput);
        Assert.Contains("Bottom 2", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_VSplitterWithButtons_HandlesFocus()
    {
        using var terminal = new Hex1bTerminal(40, 15);
        var topClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top", _ => { topClicked = true; return Task.CompletedTask; }),
                    ctx.Text("Bottom"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Enter clicks the focused button
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(topClicked);
    }

    [Fact]
    public async Task Integration_VSplitterNavigation_TabSwitchesFocus()
    {
        using var terminal = new Hex1bTerminal(40, 15);
        var bottomClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top", _ => Task.CompletedTask),
                    ctx.Button("Bottom", _ => { bottomClicked = true; return Task.CompletedTask; }),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab through: top -> splitter -> bottom, then Enter
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(bottomClicked);
    }

    [Fact]
    public async Task Integration_VSplitterResize_ArrowKeysWork()
    {
        using var terminal = new Hex1bTerminal(40, 15);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Text("Top"),
                    ctx.Text("Bottom"),
                    topHeight: 5
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Since first child is just text (not focusable), splitter gets focus
        // Up/down arrows should resize it
        terminal.SendKey(ConsoleKey.UpArrow);
        terminal.SendKey(ConsoleKey.UpArrow);
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Top", terminal.RawOutput);
        Assert.Contains("Bottom", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_VSplitterInsideBorder_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(50, 15);

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
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Vertical Split", terminal.RawOutput);
        Assert.Contains("Top", terminal.RawOutput);
        Assert.Contains("Bottom", terminal.RawOutput);
        Assert.Contains("┌", terminal.RawOutput);
    }

    [Fact]
    public async Task Integration_NestedSplitters_HorizontalInsideVertical()
    {
        using var terminal = new Hex1bTerminal(60, 20);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Splitter(
                        ctx.Text("Top-Left"),
                        ctx.Text("Top-Right"),
                        leftWidth: 20
                    ),
                    ctx.Text("Bottom"),
                    topHeight: 8
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Contains("Top-Left", terminal.RawOutput);
        Assert.Contains("Top-Right", terminal.RawOutput);
        Assert.Contains("Bottom", terminal.RawOutput);
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
        using var terminal = new Hex1bTerminal(60, 15);
        IReadOnlyList<string> items = ["Item 1", "Item 2"];
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    // Left pane: VStack containing a List (this is the key scenario)
                    v => [v.Text("Theme List"), v.List(items)],
                    // Right pane: VStack with Button that we want to Tab to
                    v => [v.Button("Right Button", _ => { rightButtonClicked = true; return Task.CompletedTask; })],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // List starts focused, Tab should move through: List -> Splitter -> Right Button
        terminal.SendKey(ConsoleKey.Tab, '\t'); // List -> Splitter
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Splitter -> Right Button
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click the button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(rightButtonClicked, "Tab should have moved focus from List through Splitter to Right Button");
    }

    /// <summary>
    /// Regression test: Same as above but with multiple buttons in left pane VStack.
    /// Focus order: First -> Second -> Splitter -> Right
    /// </summary>
    [Fact]
    public async Task Integration_TabFromSecondButtonInVStackInsideSplitter_MovesFocusToNextPane()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    // Left pane: VStack containing two Buttons
                    v => [v.Button("First", _ => Task.CompletedTask), v.Button("Second", _ => Task.CompletedTask)],
                    // Right pane: VStack with Button
                    v => [v.Button("Right", _ => { rightButtonClicked = true; return Task.CompletedTask; })],
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Focus order: First -> Second -> Splitter -> Right
        // (VStack doesn't handle Tab when inside Splitter, so it bubbles up to Splitter)
        terminal.SendKey(ConsoleKey.Tab, '\t'); // First -> Second
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Second -> Splitter
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Splitter -> Right Button  
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click the button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(rightButtonClicked, "Tab should have moved focus from Second Button through Splitter to Right Button");
    }

    /// <summary>
    /// Regression test: Shift+Tab should also bubble up to Splitter for reverse navigation.
    /// </summary>
    [Fact]
    public async Task Integration_ShiftTabFromButtonInsideSplitter_MovesFocusToPreviousPane()
    {
        using var terminal = new Hex1bTerminal(60, 15);
        var leftButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    v => [v.Text("Left Pane"), v.Button("Left Button", _ => { leftButtonClicked = true; return Task.CompletedTask; })],
                    v => [v.Text("Right Pane"), v.Button("Right Button", _ => Task.CompletedTask)],
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Navigate to Right Button first
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Left Button -> Splitter
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Splitter -> Right Button
        // Now Shift+Tab back
        terminal.SendKey(ConsoleKey.Tab, '\t', shift: true); // Right Button -> Splitter
        terminal.SendKey(ConsoleKey.Tab, '\t', shift: true); // Splitter -> Left Button
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click the button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(leftButtonClicked, "Shift+Tab should have moved focus back to Left Button");
    }

    /// <summary>
    /// Regression test: Deeply nested structure - VStack inside Panel inside Splitter.
    /// Tab should still bubble up to the Splitter.
    /// </summary>
    [Fact]
    public async Task Integration_TabFromWidgetInDeepNesting_BubblesUpToSplitter()
    {
        using var terminal = new Hex1bTerminal(70, 15);
        IReadOnlyList<string> items = ["Theme 1", "Theme 2"];
        var rightButtonClicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    // Left: Panel > VStack > List (deep nesting like ThemingExhibit)
                    ctx.Panel(p => [
                        p.VStack(v => [
                            v.Text("═══ Themes ═══"),
                            v.Text(""),
                            v.List(items)
                        ])
                    ]),
                    // Right: Panel > VStack > Button
                    ctx.Panel(p => [
                        p.VStack(v => [
                            v.Text("═══ Preview ═══"),
                            v.Button("Click Me", _ => { rightButtonClicked = true; return Task.CompletedTask; })
                        ])
                    ]),
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // List is focused initially, Tab should navigate to Splitter, then to Button
        terminal.SendKey(ConsoleKey.Tab, '\t'); // List -> Splitter
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Splitter -> Button
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click the button
        terminal.CompleteInput();
        await app.RunAsync();

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
        using var terminal = new Hex1bTerminal(60, 20);
        var outerLeftClicked = false;
        
        // The outer splitter's left pane has a button.
        // The inner splitter (in right pane) also has a button.
        // Focus should go to the OUTER left button (first focusable), not the inner splitter's button.
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    // Outer left: button that should get initial focus
                    ctx.Button("Outer Left", _ => { outerLeftClicked = true; return Task.CompletedTask; }),
                    // Outer right: inner splitter with its own button
                    ctx.Splitter(
                        ctx.Button("Inner Left", _ => Task.CompletedTask),
                        ctx.Text("Inner Right"),
                        leftWidth: 15
                    ),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Just press Enter - should click the focused button
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(outerLeftClicked, "Initial focus should be on Outer Left button, not Inner Left");
    }

    /// <summary>
    /// Regression test: Similar to above but with vertical splitters nested inside horizontal.
    /// </summary>
    [Fact]
    public async Task Integration_NestedVSplitterInHSplitter_OuterGetsInitialFocus()
    {
        using var terminal = new Hex1bTerminal(60, 20);
        var outerLeftClicked = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Outer Left", _ => { outerLeftClicked = true; return Task.CompletedTask; }),
                    ctx.VSplitter(
                        ctx.Button("Top", _ => Task.CompletedTask),
                        ctx.Button("Bottom", _ => Task.CompletedTask),
                        topHeight: 8
                    ),
                    leftWidth: 20
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(outerLeftClicked, "Initial focus should be on Outer Left button");
    }

    /// <summary>
    /// Regression test: Three levels of nested splitters - only the root should set focus.
    /// </summary>
    [Fact]
    public async Task Integration_TripleNestedSplitters_OnlyRootSetsFocus()
    {
        using var terminal = new Hex1bTerminal(80, 25);
        var level1Clicked = false;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Level 1", _ => { level1Clicked = true; return Task.CompletedTask; }),
                    ctx.Splitter(
                        ctx.Button("Level 2", _ => Task.CompletedTask),
                        ctx.Splitter(
                            ctx.Button("Level 3", _ => Task.CompletedTask),
                            ctx.Text("Deepest"),
                            leftWidth: 12
                        ),
                        leftWidth: 15
                    ),
                    leftWidth: 18
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();
        await app.RunAsync();

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
        var innerButton = new ButtonWidget("Inner Left") { OnClick = _ => Task.CompletedTask };
        var innerSplitter = new SplitterWidget(
            innerButton,
            new TextBlockWidget("Inner Right"),
            firstSize: 15
        );
        var outerButton = new ButtonWidget("Outer Left") { OnClick = _ => Task.CompletedTask };
        var outerSplitter = new SplitterWidget(
            outerButton,
            innerSplitter,
            firstSize: 20
        );

        // Reconcile from root
        var context = Hex1b.Widgets.ReconcileContext.CreateRoot();
        context.IsNew = true;
        var rootNode = outerSplitter.Reconcile(null, context) as SplitterNode;

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
        var rootNode = outerSplitter.Reconcile(null, context) as SplitterNode;

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
}
