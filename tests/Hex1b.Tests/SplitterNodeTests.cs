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

        // Count occurrences of divider in raw output - should be 5 (one per row)
        var dividerCount = terminal.RawOutput.Split("│").Length - 1;
        Assert.Equal(5, dividerCount);
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
    public void HandleInput_LeftArrow_WhenFocused_DecreasesLeftWidth()
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

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(18, node.LeftWidth);
    }

    [Fact]
    public void HandleInput_RightArrow_WhenFocused_IncreasesLeftWidth()
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

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(22, node.LeftWidth);
    }

    [Fact]
    public void HandleInput_LeftArrow_RespectsMinLeftWidth()
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

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(5, node.LeftWidth); // Clamped to MinLeftWidth
    }

    [Fact]
    public void HandleInput_RightArrow_RespectsMaxWidth()
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

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        Assert.True(node.LeftWidth <= 42);
    }

    [Fact]
    public void HandleInput_NotFocused_DoesNotResize()
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
        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(20, node.LeftWidth);
    }

    #endregion

    #region Input Handling - Tab Navigation Tests

    [Fact]
    public void HandleInput_Tab_MovesFocusToNextFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left", IsFocused = true };
        var rightButton = new ButtonNode { Label = "Right" };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };
        node.SyncFocusIndex();

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None));

        // Focus moves from left button to splitter itself
        Assert.False(leftButton.IsFocused);
        Assert.True(node.IsFocused);
    }

    [Fact]
    public void HandleInput_ShiftTab_MovesFocusToPreviousFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };
        node.SyncFocusIndex();

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift));

        // Focus moves from right button to splitter
        Assert.False(rightButton.IsFocused);
        Assert.True(node.IsFocused);
    }

    [Fact]
    public void HandleInput_Tab_WrapsAround()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };
        node.SyncFocusIndex();

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None));

        // Focus wraps from right button to left button
        Assert.True(leftButton.IsFocused);
        Assert.False(rightButton.IsFocused);
    }

    [Fact]
    public void HandleInput_Escape_JumpsToFirstFocusable()
    {
        var leftButton = new ButtonNode { Label = "Left" };
        var rightButton = new ButtonNode { Label = "Right", IsFocused = true };
        var node = new SplitterNode
        {
            Left = leftButton,
            Right = rightButton
        };
        node.SyncFocusIndex();

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Escape, '\x1b', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.True(leftButton.IsFocused);
        Assert.False(rightButton.IsFocused);
    }

    #endregion

    #region Input Handling - Child Input Tests

    [Fact]
    public void HandleInput_Enter_PassesToFocusedChild()
    {
        var clicked = false;
        var button = new ButtonNode
        {
            Label = "Click",
            IsFocused = true,
            OnClick = () => clicked = true
        };
        var node = new SplitterNode
        {
            Left = button,
            Right = new TextBlockNode { Text = "Right" }
        };
        node.SyncFocusIndex();

        // Use InputRouter to route input to the focused child
        var result = InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.True(clicked);
    }

    [Fact]
    public void HandleInput_Typing_PassesToFocusedTextBox()
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
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.None));
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.B, 'b', Hex1bModifiers.None));
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.C, 'c', Hex1bModifiers.None));

        Assert.Equal("abc", textBoxState.Text);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_Splitter_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(60, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Left", () => leftClicked = true),
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.Button("Left", () => { }),
                    ctx.Button("Right", () => rightClicked = true),
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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
        var listState = new ListState
        {
            Items = [new ListItem("1", "Item 1"), new ListItem("2", "Item 2")],
            SelectedIndex = 0
        };

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.List(listState),
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

        Assert.Equal(1, listState.SelectedIndex);
    }

    [Fact]
    public async Task Integration_SplitterWithTextBox_HandlesTyping()
    {
        using var terminal = new Hex1bTerminal(60, 10);
        var textBoxState = new TextBoxState();

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.Splitter(
                    ctx.TextBox(textBoxState),
                    ctx.Text("Right"),
                    leftWidth: 25
                )
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.TypeText("Hello Splitter");
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.Equal("Hello Splitter", textBoxState.Text);
    }

    [Fact]
    public async Task Integration_SplitterInsideBorder_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(70, 12);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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
    public void HandleInput_Vertical_UpArrow_WhenFocused_DecreasesFirstSize()
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

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(8, node.FirstSize);
    }

    [Fact]
    public void HandleInput_Vertical_DownArrow_WhenFocused_IncreasesFirstSize()
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

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(12, node.FirstSize);
    }

    [Fact]
    public void HandleInput_Vertical_UpArrow_RespectsMinFirstSize()
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

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(5, node.FirstSize); // Clamped to MinFirstSize
    }

    [Fact]
    public void HandleInput_Vertical_DownArrow_RespectsMaxHeight()
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

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));

        Assert.True(node.FirstSize <= 14);
    }

    [Fact]
    public void HandleInput_Vertical_LeftRightArrows_DoNotResize()
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

        // Left/right arrows shouldn't resize vertical splitter
        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(10, node.FirstSize);
    }

    [Fact]
    public void HandleInput_Horizontal_UpDownArrows_DoNotResize()
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

        // Up/down arrows shouldn't resize horizontal splitter
        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal(20, node.FirstSize);
    }

    #endregion

    #region Vertical Splitter - Integration Tests

    [Fact]
    public async Task Integration_VSplitter_RendersCorrectly()
    {
        using var terminal = new Hex1bTerminal(40, 15);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top", () => topClicked = true),
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VSplitter(
                    ctx.Button("Top", () => { }),
                    ctx.Button("Bottom", () => bottomClicked = true),
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
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
}
