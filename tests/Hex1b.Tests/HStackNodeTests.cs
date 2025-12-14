using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for HStackNode layout, rendering, and focus management.
/// </summary>
public class HStackNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_SumsChildWidths()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAA" },
                new TextBlockNode { Text = "BBB" },
                new TextBlockNode { Text = "CCC" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(9, size.Width);
    }

    [Fact]
    public void Measure_TakesMaxHeight()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Short" },
                new TextBlockNode { Text = "Tall" },
                new TextBlockNode { Text = "Medium" }
            }
        };

        var size = node.Measure(Constraints.Unbounded);

        // All TextBlocks are 1 line tall
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyChildren_ReturnsZeroSize()
    {
        var node = new HStackNode { Children = new List<Hex1bNode>() };

        var size = node.Measure(Constraints.Unbounded);

        Assert.Equal(0, size.Width);
        Assert.Equal(0, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxConstraints()
    {
        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "FirstLongWord" },
                new TextBlockNode { Text = "SecondLongWord" }
            }
        };

        var size = node.Measure(new Constraints(0, 15, 0, 5));

        Assert.Equal(15, size.Width);
    }

    #endregion

    #region Arrange Tests

    [Fact]
    public void Arrange_PositionsChildrenHorizontally()
    {
        var child1 = new TextBlockNode { Text = "AAA" };
        var child2 = new TextBlockNode { Text = "BBB" };
        var node = new HStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(0, 0, 80, 24));

        Assert.Equal(0, child1.Bounds.X);
        Assert.Equal(3, child2.Bounds.X);
    }

    [Fact]
    public void Arrange_WithFillHints_DistributesWidth()
    {
        var child1 = new TextBlockNode { Text = "A" };
        var child2 = new TextBlockNode { Text = "B", WidthHint = SizeHint.Fill };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(20, 10));
        node.Arrange(new Rect(0, 0, 20, 10));

        // First child should be content-sized (1 char)
        Assert.Equal(1, child1.Bounds.Width);
        // Second child should fill remaining space
        Assert.Equal(19, child2.Bounds.Width);
    }

    [Fact]
    public void Arrange_WithFixedHints_UsesExactSize()
    {
        var child1 = new TextBlockNode { Text = "Fixed1", WidthHint = SizeHint.Fixed(10) };
        var child2 = new TextBlockNode { Text = "Fixed2", WidthHint = SizeHint.Fixed(15) };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2 }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        Assert.Equal(10, child1.Bounds.Width);
        Assert.Equal(15, child2.Bounds.Width);
    }

    [Fact]
    public void Arrange_WithMixedHints_DistributesCorrectly()
    {
        var child1 = new TextBlockNode { Text = "Fixed", WidthHint = SizeHint.Fixed(10) };
        var child2 = new TextBlockNode { Text = "Fill 1", WidthHint = SizeHint.Fill };
        var child3 = new TextBlockNode { Text = "Fill 2", WidthHint = SizeHint.Fill };
        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { child1, child2, child3 }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));

        Assert.Equal(10, child1.Bounds.Width);
        // Remaining 30 units split between 2 fill children
        Assert.Equal(15, child2.Bounds.Width);
        Assert.Equal(15, child3.Bounds.Width);
    }

    [Fact]
    public void Arrange_AtOffset_PositionsChildrenCorrectly()
    {
        var child1 = new TextBlockNode { Text = "AAA" };
        var child2 = new TextBlockNode { Text = "BBB" };
        var node = new HStackNode { Children = new List<Hex1bNode> { child1, child2 } };

        node.Measure(Constraints.Unbounded);
        node.Arrange(new Rect(5, 10, 30, 20));

        Assert.Equal(5, child1.Bounds.X);
        Assert.Equal(10, child1.Bounds.Y);
        Assert.Equal(8, child2.Bounds.X);
        Assert.Equal(10, child2.Bounds.Y);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void GetFocusableNodes_FindsAllFocusable()
    {
        var button1 = new ButtonNode { Label = "1" };
        var textBlock = new TextBlockNode { Text = "Not focusable" };
        var button2 = new ButtonNode { Label = "2" };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, textBlock, button2 }
        };

        var focusables = node.GetFocusableNodes().ToList();

        Assert.Equal(2, focusables.Count);
        Assert.Contains(button1, focusables);
        Assert.Contains(button2, focusables);
    }

    [Fact]
    public void HandleInput_Tab_MovesFocus()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = true };
        var button2 = new ButtonNode { Label = "2", IsFocused = false };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };
        node.InvalidateFocusCache();

        var result = InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);
    }

    [Fact]
    public void HandleInput_ShiftTab_MovesFocusBackward()
    {
        var button1 = new ButtonNode { Label = "1", IsFocused = true };
        var button2 = new ButtonNode { Label = "2", IsFocused = false };

        var node = new HStackNode
        {
            Children = new List<Hex1bNode> { button1, button2 }
        };
        node.InvalidateFocusCache();

        // Shift-tab from button1 should wrap to button2
        InputRouter.RouteInputToNode(node, new Hex1bKeyEvent(Hex1bKey.Tab, '\t', Hex1bModifiers.Shift));

        Assert.False(button1.IsFocused);
        Assert.True(button2.IsFocused);
    }

    [Fact]
    public void HandleInput_DispatchesToFocusedChild()
    {
        var clicked = false;
        var button = new ButtonNode { Label = "Click", IsFocused = true, OnClick = () => clicked = true };

        var node = new HStackNode { Children = new List<Hex1bNode> { button } };
        node.InvalidateFocusCache();

        // Use InputRouter to route input to the focused child
        InputRouter.RouteInput(node, new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));

        Assert.True(clicked);
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_RendersAllChildren()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "Left" },
                new TextBlockNode { Text = "Right" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        Assert.Contains("Left", terminal.GetScreenText());
        Assert.Contains("Right", terminal.GetScreenText());
    }

    [Fact]
    public void Render_ChildrenAppearOnSameLine()
    {
        using var terminal = new Hex1bTerminal(40, 10);
        var context = new Hex1bRenderContext(terminal);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAA" },
                new TextBlockNode { Text = "BBB" },
                new TextBlockNode { Text = "CCC" }
            }
        };

        node.Measure(Constraints.Tight(40, 10));
        node.Arrange(new Rect(0, 0, 40, 10));
        node.Render(context);

        var line = terminal.GetLineTrimmed(0);
        Assert.Contains("AAA", line);
        Assert.Contains("BBB", line);
        Assert.Contains("CCC", line);
    }

    [Fact]
    public void Render_InNarrowTerminal_TextWraps()
    {
        using var terminal = new Hex1bTerminal(8, 10);
        var context = new Hex1bRenderContext(terminal);

        var node = new HStackNode
        {
            Children = new List<Hex1bNode>
            {
                new TextBlockNode { Text = "AAAA" },
                new TextBlockNode { Text = "BBBB" }
            }
        };

        node.Measure(Constraints.Tight(8, 10));
        node.Arrange(new Rect(0, 0, 8, 10));
        node.Render(context);

        // Content wraps at terminal edge
        Assert.Contains("AAAA", terminal.GetLine(0));
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_HStack_RendersMultipleChildren()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Left"),
                    h.Text(" | "),
                    h.Text("Right")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Left"));
        Assert.True(terminal.ContainsText("|"));
        Assert.True(terminal.ContainsText("Right"));
    }

    [Fact]
    public async Task Integration_HStack_TabNavigatesThroughFocusables()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Button("Btn1", () => button1Clicked = true),
                    h.Text(" | "),
                    h.Button("Btn2", () => button2Clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Tab to second button and click
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.False(button1Clicked);
        Assert.True(button2Clicked);
    }

    [Fact]
    public async Task Integration_HStack_InNarrowTerminal_StillWorks()
    {
        using var terminal = new Hex1bTerminal(20, 10);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("A"),
                    h.Text("B"),
                    h.Text("C")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("A"));
        Assert.True(terminal.ContainsText("B"));
        Assert.True(terminal.ContainsText("C"));
    }

    [Fact]
    public async Task Integration_HStack_WithVStack_NestedLayouts()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.VStack(v => [
                        v.Text("Left Top"),
                        v.Text("Left Bottom")
                    ]),
                    h.VStack(v => [
                        v.Text("Right Top"),
                        v.Text("Right Bottom")
                    ])
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Left Top"));
        Assert.True(terminal.ContainsText("Left Bottom"));
        Assert.True(terminal.ContainsText("Right Top"));
        Assert.True(terminal.ContainsText("Right Bottom"));
    }

    [Fact]
    public async Task Integration_HStack_WithMixedContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "" };
        var clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("Label: "),
                    h.TextBox(textState),
                    h.Button("Submit", () => clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Type in textbox then tab to button and click
        terminal.SendKey(ConsoleKey.H, 'H', shift: true);
        terminal.SendKey(ConsoleKey.I, 'i');
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("Hi", textState.Text);
        Assert.True(clicked);
    }

    [Fact]
    public async Task Integration_HStack_EmptyStack_DoesNotCrash()
    {
        using var terminal = new Hex1bTerminal(80, 24);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => Array.Empty<Hex1bWidget>())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Should complete without error
        Assert.False(terminal.InAlternateScreen);
    }

    [Fact]
    public async Task Integration_HStack_LongContentInNarrowTerminal_Wraps()
    {
        using var terminal = new Hex1bTerminal(15, 5);

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    h.Text("VeryLongWord"),
                    h.Text("AndAnother")
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // Content wraps at terminal boundary
        Assert.True(terminal.ContainsText("VeryLongWord"));
    }

    [Fact]
    public async Task Integration_HStack_DynamicContent()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var items = new List<string> { "A", "B", "C" };

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => items.Select(item => h.Text($"[{item}]")).ToArray())
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("[A]"));
        Assert.True(terminal.ContainsText("[B]"));
        Assert.True(terminal.ContainsText("[C]"));
    }

    [Fact]
    public async Task Integration_TabFromSingleFocusableVStackInHStack_BubblesUpToHStack()
    {
        // Scenario: HStack with children that are VStacks, each containing only one focusable.
        // Tab should bubble up from the VStack to the HStack since there's nothing to cycle within.
        // This simulates the ResponsiveTodoExhibit Extra Wide layout.
        using var terminal = new Hex1bTerminal(80, 24);
        var rightButtonClicked = false;
        var listState = new ListState { Items = [new ListItem("1", "Item 1"), new ListItem("2", "Item 2")] };

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left VStack: only one focusable (List)
                    h.VStack(v => [v.Text("Header"), v.List(listState)]),
                    // Right VStack: only one focusable (Button)
                    h.VStack(v => [v.Text("Actions"), v.Button("Add", () => rightButtonClicked = true)])
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // List starts focused; Tab should bubble up to HStack and move to Button
        terminal.SendKey(ConsoleKey.Tab, '\t');
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click the button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(rightButtonClicked, "Tab should have moved focus from List in left VStack to Button in right VStack");
    }

    [Fact]
    public async Task Integration_VStackWithMultipleFocusables_HandleTabInternally()
    {
        // Scenario: VStack with 2 focusables should handle Tab itself, not bubble up
        using var terminal = new Hex1bTerminal(80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Button 1", () => button1Clicked = true),
                    v.Button("Button 2", () => button2Clicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Button 1 starts focused
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Button 1 -> Button 2
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click Button 2
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.False(button1Clicked);
        Assert.True(button2Clicked, "Tab should have moved focus from Button 1 to Button 2 within VStack");
    }

    [Fact]
    public async Task Integration_NestedVStackWithMultipleFocusables_TabEscapesAtBoundary()
    {
        // Scenario: VStack with 2 focusables nested inside an HStack.
        // Tab should cycle within the VStack, but escape at the last item.
        // This is the ResponsiveTodoExhibit "New" panel scenario.
        using var terminal = new Hex1bTerminal(80, 24);
        var listClicked = false;
        var addButtonClicked = false;
        var otherButtonClicked = false;
        var textBoxState = new TextBoxState();

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left: List
                    h.Button("List", () => listClicked = true),
                    // Middle: VStack with TextBox + Button (like "New" panel)
                    h.VStack(v => [
                        v.Text("New Task"),
                        v.TextBox(textBoxState),
                        v.Button("Add", () => addButtonClicked = true)
                    ]),
                    // Right: Another button
                    h.Button("Other", () => otherButtonClicked = true)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // List button starts focused
        terminal.SendKey(ConsoleKey.Tab, '\t'); // List -> TextBox (enters VStack)
        terminal.SendKey(ConsoleKey.Tab, '\t'); // TextBox -> Add (within VStack)
        terminal.SendKey(ConsoleKey.Tab, '\t'); // Add -> Other (escapes VStack at boundary!)
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click Other button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.False(listClicked);
        Assert.False(addButtonClicked);
        Assert.True(otherButtonClicked, "Tab at VStack boundary should escape to next HStack child");
    }

    [Fact]
    public async Task Integration_NestedVStackWithMultipleFocusables_ShiftTabEscapesAtBoundary()
    {
        // Scenario: Same as above but with Shift+Tab escaping at the first item.
        using var terminal = new Hex1bTerminal(80, 24);
        var listClicked = false;
        var textBoxState = new TextBoxState();

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.HStack(h => [
                    // Left: Button
                    h.Button("List", () => listClicked = true),
                    // Right: VStack with TextBox + Button
                    h.VStack(v => [
                        v.Text("New Task"),
                        v.TextBox(textBoxState),
                        v.Button("Add", () => { })
                    ])
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // List button starts focused
        terminal.SendKey(ConsoleKey.Tab, '\t'); // List -> TextBox (enters VStack)
        // Now Shift+Tab should go back to List (escape VStack at first boundary)
        terminal.SendKey(ConsoleKey.Tab, '\t', shift: true); // TextBox -> List (escapes VStack!)
        terminal.SendKey(ConsoleKey.Enter, '\r'); // Click List button
        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(listClicked, "Shift+Tab at first VStack item should escape back to previous HStack child");
    }

    #endregion
}
