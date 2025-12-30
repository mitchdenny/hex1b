using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Terminal.Automation;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Tests for TextBoxNode rendering and input handling.
/// </summary>
public class TextBoxNodeTests
{
    #region Measurement Tests

    [Fact]
    public void Measure_ReturnsCorrectSize()
    {
        var node = new TextBoxNode { Text = "hello" };

        var size = node.Measure(Constraints.Unbounded);

        // "[hello]" = 2 brackets + 5 chars = 7
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_HasMinWidth()
    {
        var node = new TextBoxNode { Text = "" };

        var size = node.Measure(Constraints.Unbounded);

        // "[ ]" = 2 brackets + 1 min char = 3
        Assert.Equal(3, size.Width);
    }

    [Fact]
    public void Measure_LongText_MeasuresFullWidth()
    {
        var node = new TextBoxNode { Text = "This is a very long text input" };

        var size = node.Measure(Constraints.Unbounded);

        // 30 chars + 2 brackets = 32
        Assert.Equal(32, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBoxNode { Text = "Long text here" };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
    }

    [Fact]
    public void Measure_WithEmoji_CalculatesDisplayWidth()
    {
        // "😀" is 2 cells wide
        var node = new TextBoxNode { Text = "😀" };

        var size = node.Measure(Constraints.Unbounded);

        // "[😀]" = 2 brackets + 2 display width for emoji = 4
        Assert.Equal(4, size.Width);
    }

    [Fact]
    public void Measure_WithCJK_CalculatesDisplayWidth()
    {
        // "中文" is 4 cells wide (2 + 2)
        var node = new TextBoxNode { Text = "中文" };

        var size = node.Measure(Constraints.Unbounded);

        // "[中文]" = 2 brackets + 4 display width = 6
        Assert.Equal(6, size.Width);
    }

    [Fact]
    public void Measure_MixedAsciiAndEmoji_CalculatesDisplayWidth()
    {
        // "Hi😀" = 2 + 2 = 4 cells
        var node = new TextBoxNode { Text = "Hi😀" };

        var size = node.Measure(Constraints.Unbounded);

        // "[Hi😀]" = 2 brackets + 4 display width = 6
        Assert.Equal(6, size.Width);
    }

    [Fact]
    public void Measure_FamilyEmoji_TreatedAsTwoColumns()
    {
        // "👨‍👩‍👧" is a ZWJ sequence but displays as one emoji (2 cells)
        var node = new TextBoxNode { Text = "👨‍👩‍👧" };

        var size = node.Measure(Constraints.Unbounded);

        // "[👨‍👩‍👧]" = 2 brackets + 2 display width = 4
        Assert.Equal(4, size.Width);
    }

    #endregion

    #region Rendering Tests - Unfocused State

    [Fact]
    public void Render_Unfocused_ShowsBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[test]", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Unfocused_EmptyText_ShowsEmptyBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[]", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Unfocused_LongText_RendersCompletely()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "This is a longer piece of text",
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[This is a longer piece of text]", terminal.CreateSnapshot().GetLineTrimmed(0));
    }

    #endregion

    #region Rendering Tests - Focused State with Cursor

    [Fact]
    public void Render_Focused_ShowsCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "abc",
            IsFocused = true
        };
        node.State.CursorPosition = 1;

        node.Render(context);

        // When focused, the cursor character should be highlighted with ANSI codes
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        // The text content should still be visible
        Assert.Contains("a", snapshot.GetLineTrimmed(0));
        Assert.Contains("b", snapshot.GetLineTrimmed(0));
        Assert.Contains("c", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_CursorAtStart_HighlightsFirstChar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "hello",
            IsFocused = true
        };
        node.State.CursorPosition = 0;

        node.Render(context);

        // Should have ANSI codes for cursor highlighting
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        Assert.Contains("hello", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_CursorAtEnd_ShowsCursorSpace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = true
        };
        node.State.CursorPosition = 4;

        node.Render(context);

        // When cursor is at end, a space is shown as cursor placeholder
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    [Fact]
    public void Render_Focused_EmptyText_ShowsCursorSpace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = true
        };
        node.State.CursorPosition = 0;

        node.Render(context);

        // Should still have the brackets and ANSI codes for cursor
        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("[", snapshot.GetLineTrimmed(0));
        Assert.Contains("]", snapshot.GetLineTrimmed(0));
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    #endregion

    #region Rendering Tests - Selection

    [Fact]
    public void Render_WithSelection_HighlightsSelectedText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.SelectionAnchor = 0;
        node.State.CursorPosition = 5;

        node.Render(context);

        // Should have ANSI codes for selection highlighting
        var snapshot = terminal.CreateSnapshot();
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        // The text should still be present
        Assert.Contains("hello", snapshot.GetLineTrimmed(0));
        Assert.Contains("world", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithSelection_InMiddle_HighlightsCorrectPortion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 40, 5);
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode { Text = "abcdefgh", IsFocused = true };
        node.State.SelectionAnchor = 2;
        node.State.CursorPosition = 5;

        node.Render(context);

        // Should contain the full text
        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("abcdefgh", snapshot.GetLineTrimmed(0));
        // Should have selection ANSI codes
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public async Task HandleInput_WhenFocused_UpdatesState()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 5;

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("helloX", node.Text);
    }

    [Fact]
    public async Task HandleInput_WhenNotFocused_DoesNotHandle()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = false };

        var result = await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal("hello", node.Text);
    }

    [Fact]
    public async Task HandleInput_Backspace_DeletesCharacter()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 5;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("hell", node.Text);
        Assert.Equal(4, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_Delete_DeletesCharacterAhead()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 0;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("ello", node.Text);
        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_LeftArrow_MovesCursorLeft()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 3;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_RightArrow_MovesCursorRight()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 3;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(4, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_Home_MovesCursorToStart()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 3;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_End_MovesCursorToEnd()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 2;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(5, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_ShiftLeftArrow_CreatesSelection()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 3;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, node.State.CursorPosition);
        Assert.True(node.State.HasSelection);
        Assert.Equal(3, node.State.SelectionAnchor);
    }

    [Fact]
    public async Task HandleInput_CtrlA_SelectsAll()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 2;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.True(node.State.HasSelection);
        Assert.Equal(0, node.State.SelectionAnchor);
        Assert.Equal(5, node.State.CursorPosition);
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void IsFocusable_ReturnsTrue()
    {
        var node = new TextBoxNode();

        Assert.True(node.IsFocusable);
    }

    #endregion

    #region Mouse Click Tests

    [Fact]
    public void HandleMouseClick_PositionsCursorAtClickLocation()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=3, which is "ll" in "[hello]" (0='[', 1='h', 2='e', 3='l')
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(2, node.State.CursorPosition); // Position after 'he' (click on 'l')
    }

    [Fact]
    public void HandleMouseClick_AtStart_PositionsCursorAtZero()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=1, which is 'h' in "[hello]" (0='[', 1='h')
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 1, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(1, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.State.CursorPosition); // Click on first char positions at start
    }

    [Fact]
    public void HandleMouseClick_AtEnd_PositionsCursorAtEnd()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=6, which is ']' in "[hello]" (past the text)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 6, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(6, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(5, node.State.CursorPosition); // End of "hello"
    }

    [Fact]
    public void HandleMouseClick_OnBracket_PositionsCursorAtStart()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=0, which is '[' in "[hello]"
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 0, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(0, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public void HandleMouseClick_ClearsSelection()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.State.SelectAll(); // Select all text
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        Assert.True(node.State.HasSelection);

        // Click anywhere - should clear selection
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None, ClickCount: 1);
        node.HandleMouseClick(3, 0, mouseEvent);

        Assert.False(node.State.HasSelection);
    }

    [Fact]
    public void HandleMouseClick_DoubleClick_NotHandledByHandleMouseClick()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Double-click should NOT be handled by HandleMouseClick (it's handled by the binding)
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None, ClickCount: 2);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleMouseClick_RightClick_NotHandled()
    {
        var node = new TextBoxNode { Text = "hello" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Right-click should not be handled
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Right, MouseAction.Down, 3, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.NotHandled, result);
    }

    [Fact]
    public void HandleMouseClick_WithEmoji_PositionsCorrectly()
    {
        // "😀ab" - emoji is 2 cells wide
        var node = new TextBoxNode { Text = "😀ab" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=3 which is after the emoji (column 1-2), on 'a' (column 3)
        // "[😀ab]" - localX: 0='[', 1-2='😀', 3='a', 4='b', 5=']'
        var mouseEvent = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 3, 0, Hex1bModifiers.None, ClickCount: 1);
        var result = node.HandleMouseClick(3, 0, mouseEvent);

        Assert.Equal(InputResult.Handled, result);
        // After emoji (which is 2 chars in string), before 'a'
        Assert.Equal(2, node.State.CursorPosition);
    }

    [Fact]
    public void HandleMouseClick_OnWideChar_PositionsBasedOnMidpoint()
    {
        // Click on first half of emoji should position before it
        var node = new TextBoxNode { Text = "😀" };
        node.IsFocused = true;
        node.Arrange(new Rect(0, 0, 10, 1));

        // Click at localX=1 (first half of emoji) "[😀]"
        var mouseEvent1 = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 1, 0, Hex1bModifiers.None, ClickCount: 1);
        node.HandleMouseClick(1, 0, mouseEvent1);
        Assert.Equal(0, node.State.CursorPosition); // Before emoji

        // Click at localX=2 (second half of emoji)
        var mouseEvent2 = new Hex1bMouseEvent(MouseButton.Left, MouseAction.Down, 2, 0, Hex1bModifiers.None, ClickCount: 1);
        node.HandleMouseClick(2, 0, mouseEvent2);
        Assert.Equal(2, node.State.CursorPosition); // After emoji (emoji is 2 chars in string)
    }

    #endregion

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new TextBoxNode { Text = "test" };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_TextBox_RendersViaHex1bApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("Initial Text")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Initial Text"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Initial Text"));
    }

    [Fact]
    public async Task Integration_TextBox_ReceivesInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).OnTextChanged(args => capturedText = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hello", capturedText);
    }

    [Fact]
    public async Task Integration_TextBox_InNarrowTerminal_StillWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 15, 5);
        var capturedText = "Short";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).OnTextChanged(args => capturedText = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Short"), TimeSpan.FromSeconds(2))
            .End()
            .Type("X")
            .WaitUntil(s => s.ContainsText("ShortX"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("ShortX", capturedText);
    }

    [Fact]
    public async Task Integration_TextBox_TabBetweenMultiple()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text1 = "";
        var text2 = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text1).OnTextChanged(args => text1 = args.NewText),
                    v.TextBox(text2).OnTextChanged(args => text2 = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type in first box, tab to second, type in second
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("AB")
            .Tab()
            .Type("XY")
            .WaitUntil(s => s.ContainsText("XY"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("AB", text1);
        Assert.Equal("XY", text2);
    }

    [Fact]
    public async Task Integration_TextBox_BackspaceWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "test";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text).OnTextChanged(args => text = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("test"), TimeSpan.FromSeconds(2))
            .End()
            .Backspace()
            .Backspace()
            .WaitUntil(s => s.ContainsText("te") && !s.ContainsText("test"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("te", text);
    }

    [Fact]
    public async Task Integration_TextBox_CursorNavigationWorks()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "abc";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text).OnTextChanged(args => text = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Go to start, then right, then insert
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("abc"), TimeSpan.FromSeconds(2))
            .Home()
            .Right()
            .Type("X")
            .WaitUntil(s => s.ContainsText("aXbc"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("aXbc", text);
    }

    [Fact]
    public async Task Integration_TextBox_SpecialCharactersWork()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text).OnTextChanged(args => text = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("@!#")
            .WaitUntil(s => s.ContainsText("@!#"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("@!#", text);
    }

    [Fact]
    public async Task Integration_TextBox_LongTextInNarrowTerminal_Wraps()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 10, 5);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("LongTextHere")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[LongText"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The text box renders as "[LongTextHere]" which is 14 chars
        // In a 10-char wide terminal, it will wrap
        // Check that the text content is present (split across lines)
        Assert.True(terminal.CreateSnapshot().ContainsText("[LongText"));
    }

    #endregion

    #region Uncontrolled Mode Tests

    [Fact]
    public async Task Integration_TextBox_UncontrolledMode_PreservesStateAcrossRerenders()
    {
        // Regression test: TextBox with no state argument should preserve typed content
        // Previously, creating new TextBoxState() inline would reset state on each render
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox()  // No state argument - internally managed
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type some text
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The typed text should be visible in the terminal output
        Assert.True(terminal.CreateSnapshot().ContainsText("Hello"));
    }

    [Fact]
    public async Task Integration_TextBox_UncontrolledMode_MultipleTextBoxes_IndependentState()
    {
        // Each uncontrolled TextBox should have its own independent state
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(),  // First textbox
                    v.TextBox()   // Second textbox
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Type in first box, tab, type in second box
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("AA")
            .Tab()
            .Type("BB")
            .WaitUntil(s => s.ContainsText("BB"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Both texts should be visible
        Assert.True(terminal.CreateSnapshot().ContainsText("AA"));
        Assert.True(terminal.CreateSnapshot().ContainsText("BB"));
    }

    [Fact]
    public async Task Integration_TextBox_ControlledMode_StillWorks()
    {
        // Controlled mode with onTextChanged callback
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var text = "Initial";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(text).OnTextChanged(args => text = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Initial"), TimeSpan.FromSeconds(2))
            .End()
            .Type("X")
            .WaitUntil(s => s.ContainsText("InitialX"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The external state should be updated
        Assert.Equal("InitialX", text);
    }

    #endregion

    #region Run-First Pattern Integration Tests

    [Fact]
    public async Task RunFirst_Button_RendersAndExits()
    {
        // Test with Button (also focusable) to isolate the issue
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Button("Click Me")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Click Me"));
    }

    [Fact]
    public async Task RunFirst_TextBox_RendersWithoutInput()
    {
        // Simplest case: TextBox renders, no input, just Ctrl+C to exit
        // Wait for alternate screen instead of text to isolate rendering from input
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("Initial")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Initial"));
    }

    [Fact]
    public async Task RunFirst_TextBox_SingleCharacterInput()
    {
        // One character typed into TextBox
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("").OnTextChanged(args => capturedText = args.NewText)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.A)  // Single letter 'a'
            .WaitUntil(s => s.ContainsText("a"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("a", capturedText);
    }

    [Fact]
    public async Task RunFirst_TextBox_TypeMultipleChars()
    {
        // Type multiple chars using Type() method
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("").OnTextChanged(args => capturedText = args.NewText)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hello", capturedText);
    }

    [Fact]
    public async Task RunFirst_ButtonThenTextBox_TabToTextBox()
    {
        // Button has initial focus, then Tab to TextBox
        // This tests if the issue is TextBox having initial focus vs receiving focus later
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("OK"),
                    v.TextBox("").OnTextChanged(args => capturedText = args.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("OK"), TimeSpan.FromSeconds(2))
            .Tab()  // Move focus from Button to TextBox
            .Key(Hex1bKey.X)  // Type 'x' in TextBox
            .WaitUntil(s => s.ContainsText("x"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("x", capturedText);
    }

    [Fact]
    public async Task RunFirst_TwoButtons_TabBetweenThem()
    {
        // Two buttons, Tab between them, no TextBox involved
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var button1Clicked = false;
        var button2Clicked = false;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("First").OnClick(_ => button1Clicked = true),
                    v.Button("Second").OnClick(_ => button2Clicked = true)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(2))
            .Tab()  // Move from First to Second
            .Key(Hex1bKey.Enter)  // Activate Second button
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.False(button1Clicked);
        Assert.True(button2Clicked);
    }

    [Fact]
    public async Task RunFirst_ButtonThenTextBox_JustTabThenExit()
    {
        // Button first, Tab to TextBox, but don't type - just exit
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("OK"),
                    v.TextBox("pre-filled")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("OK"), TimeSpan.FromSeconds(2))
            .Tab()  // Move focus from Button to TextBox
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)  // Immediately exit
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("pre-filled"));
    }

    [Fact]
    public async Task RunFirst_TextBoxInVStack_NoFocusChange()
    {
        // TextBox in VStack, has initial focus, just exit without any interaction
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("test-value")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            // Wait for InAlternateScreen instead of specific text
            .WaitUntil(s => s.Terminal.InAlternateScreen, TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("test-value"));
    }

    [Fact]
    public async Task RunFirst_ButtonInVStack_Works()
    {
        // Button in VStack, has initial focus
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Click Me")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("Click Me"));
    }

    [Fact]
    public async Task RunFirst_TextBoxInVStack_RendersAndExits()
    {
        // TextBox in VStack, verify it renders and Ctrl+C exits properly
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = new Hex1bTerminal(workload, 80, 24);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("test")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("test"), TimeSpan.FromSeconds(2))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(terminal.CreateSnapshot().ContainsText("test"));
    }

    #endregion
}
