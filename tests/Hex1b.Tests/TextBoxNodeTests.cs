using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
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
    public async Task Measure_ReturnsCorrectSize()
    {
        var node = new TextBoxNode { Text = "hello" };

        var size = node.Measure(Constraints.Unbounded);

        // "[hello]" = 2 brackets + 5 chars = 7
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_EmptyText_HasMinWidth()
    {
        var node = new TextBoxNode { Text = "" };

        var size = node.Measure(Constraints.Unbounded);

        // "[ ]" = 2 brackets + 1 min char = 3
        Assert.Equal(3, size.Width);
    }

    [Fact]
    public async Task Measure_LongText_MeasuresFullWidth()
    {
        var node = new TextBoxNode { Text = "This is a very long text input" };

        var size = node.Measure(Constraints.Unbounded);

        // 30 chars + 2 brackets = 32
        Assert.Equal(32, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public async Task Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBoxNode { Text = "Long text here" };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
    }

    [Fact]
    public async Task Measure_WithEmoji_CalculatesDisplayWidth()
    {
        // "😀" is 2 cells wide
        var node = new TextBoxNode { Text = "😀" };

        var size = node.Measure(Constraints.Unbounded);

        // "[😀]" = 2 brackets + 2 display width for emoji = 4
        Assert.Equal(4, size.Width);
    }

    [Fact]
    public async Task Measure_WithCJK_CalculatesDisplayWidth()
    {
        // "中文" is 4 cells wide (2 + 2)
        var node = new TextBoxNode { Text = "中文" };

        var size = node.Measure(Constraints.Unbounded);

        // "[中文]" = 2 brackets + 4 display width = 6
        Assert.Equal(6, size.Width);
    }

    [Fact]
    public async Task Measure_MixedAsciiAndEmoji_CalculatesDisplayWidth()
    {
        // "Hi😀" = 2 + 2 = 4 cells
        var node = new TextBoxNode { Text = "Hi😀" };

        var size = node.Measure(Constraints.Unbounded);

        // "[Hi😀]" = 2 brackets + 4 display width = 6
        Assert.Equal(6, size.Width);
    }

    [Fact]
    public async Task Measure_FamilyEmoji_TreatedAsTwoColumns()
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
    public async Task Render_Unfocused_ShowsBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = false
        };

        node.Render(context);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[test]"), TimeSpan.FromSeconds(5), "bracketed text visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Contains("[test]", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task Render_Unfocused_EmptyText_ShowsEmptyBrackets()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = false
        };

        node.Render(context);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[]"), TimeSpan.FromSeconds(5), "empty brackets visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Contains("[]", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task Render_Unfocused_LongText_RendersCompletely()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "This is a longer piece of text",
            IsFocused = false
        };

        node.Render(context);
        
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[This is a longer piece of text]"), TimeSpan.FromSeconds(5), "long text visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Contains("[This is a longer piece of text]", snapshot.GetLineTrimmed(0));
    }

    #endregion

    #region Rendering Tests - Focused State with Cursor

    [Fact]
    public async Task Render_Focused_ShowsCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "abc",
            IsFocused = true
        };
        node.State.CursorPosition = 1;

        node.Render(context);
        
        // Wait for text content and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("a") && s.ContainsText("b") && s.ContainsText("c"), 
                TimeSpan.FromSeconds(2), "text content visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // When focused, the cursor character should be highlighted with ANSI codes
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        // The text content should still be visible
        Assert.Contains("a", snapshot.GetLineTrimmed(0));
        Assert.Contains("b", snapshot.GetLineTrimmed(0));
        Assert.Contains("c", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task Render_Focused_CursorAtStart_HighlightsFirstChar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "hello",
            IsFocused = true
        };
        node.State.CursorPosition = 0;

        node.Render(context);
        
        // Wait for text content and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5), "text content visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Should have ANSI codes for cursor highlighting
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        Assert.Contains("hello", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task Render_Focused_CursorAtEnd_ShowsCursorSpace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "test",
            IsFocused = true
        };
        node.State.CursorPosition = 4;

        node.Render(context);
        
        // Wait for text content and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("test"), TimeSpan.FromSeconds(5), "text content visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // When cursor is at end, a space is shown as cursor placeholder
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    [Fact]
    public async Task Render_Focused_EmptyText_ShowsCursorSpace()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode
        {
            Text = "",
            IsFocused = true
        };
        node.State.CursorPosition = 0;

        node.Render(context);
        
        // Wait for brackets to appear and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[") && s.ContainsText("]"), TimeSpan.FromSeconds(5), "brackets visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Should still have the brackets and ANSI codes for cursor
        Assert.Contains("[", snapshot.GetLineTrimmed(0));
        Assert.Contains("]", snapshot.GetLineTrimmed(0));
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
    }

    #endregion

    #region Rendering Tests - Selection

    [Fact]
    public async Task Render_WithSelection_HighlightsSelectedText()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.SelectionAnchor = 0;
        node.State.CursorPosition = 5;

        node.Render(context);
        
        // Wait for text content and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello") && s.ContainsText("world"), 
                TimeSpan.FromSeconds(2), "text content visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Should have ANSI codes for selection highlighting
        Assert.True(snapshot.HasForegroundColor() || snapshot.HasBackgroundColor() || snapshot.HasAttribute(CellAttributes.Reverse));
        // The text should still be present
        Assert.Contains("hello", snapshot.GetLineTrimmed(0));
        Assert.Contains("world", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task Render_WithSelection_InMiddle_HighlightsCorrectPortion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        var context = new Hex1bRenderContext(workload);
        var node = new TextBoxNode { Text = "abcdefgh", IsFocused = true };
        node.State.SelectionAnchor = 2;
        node.State.CursorPosition = 5;

        node.Render(context);
        
        // Wait for text content and capture snapshot atomically
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("abcdefgh"), TimeSpan.FromSeconds(5), "text content visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Should contain the full text
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
    public async Task HandleInput_CtrlLeftArrow_MovesToPreviousWord()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 11; // End of "world"

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(6, node.State.CursorPosition); // Start of "world"
    }

    [Fact]
    public async Task HandleInput_CtrlRightArrow_MovesToNextWord()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 0; // Start of "hello"

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(6, node.State.CursorPosition); // Start of "world"
    }

    [Fact]
    public async Task HandleInput_CtrlLeftArrow_AtStart_StaysAtStart()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 0;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlRightArrow_AtEnd_StaysAtEnd()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 11;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(11, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlLeftArrow_ClearsSelectionFirst()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.SelectionAnchor = 6;
        node.State.CursorPosition = 11; // "world" selected

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.False(node.State.HasSelection);
        Assert.Equal(0, node.State.CursorPosition); // Moved to start of "hello" (from selection start)
    }

    [Fact]
    public async Task HandleInput_CtrlBackspace_DeletesPreviousWord()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 11; // End of "world"

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("hello ", node.Text);
        Assert.Equal(6, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlDelete_DeletesNextWord()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.CursorPosition = 0;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("world", node.Text);
        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlBackspace_AtStart_DoesNothing()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 0;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("hello", node.Text);
        Assert.Equal(0, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlDelete_AtEnd_DoesNothing()
    {
        var node = new TextBoxNode { Text = "hello", IsFocused = true };
        node.State.CursorPosition = 5;

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("hello", node.Text);
        Assert.Equal(5, node.State.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlBackspace_WithSelection_DeletesSelection()
    {
        var node = new TextBoxNode { Text = "hello world", IsFocused = true };
        node.State.SelectionAnchor = 6;
        node.State.CursorPosition = 11; // "world" selected

        await InputRouter.RouteInputToNodeAsync(node, new Hex1bKeyEvent(Hex1bKey.Backspace, '\0', Hex1bModifiers.Control), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("hello ", node.Text);
        Assert.False(node.State.HasSelection);
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
    public async Task IsFocusable_ReturnsTrue()
    {
        var node = new TextBoxNode();

        Assert.True(node.IsFocusable);
    }

    #endregion

    #region Mouse Click Tests

    [Fact]
    public async Task HandleMouseClick_PositionsCursorAtClickLocation()
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
    public async Task HandleMouseClick_AtStart_PositionsCursorAtZero()
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
    public async Task HandleMouseClick_AtEnd_PositionsCursorAtEnd()
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
    public async Task HandleMouseClick_OnBracket_PositionsCursorAtStart()
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
    public async Task HandleMouseClick_ClearsSelection()
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
    public async Task HandleMouseClick_DoubleClick_NotHandledByHandleMouseClick()
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
    public async Task HandleMouseClick_RightClick_NotHandled()
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
    public async Task HandleMouseClick_WithEmoji_PositionsCorrectly()
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
    public async Task HandleMouseClick_OnWideChar_PositionsBasedOnMidpoint()
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
    public async Task Arrange_SetsBounds()
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("Initial Text")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Initial Text"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Initial Text"));
    }

    [Fact]
    public async Task Integration_TextBox_ReceivesInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 5).Build();
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
            .WaitUntil(s => s.ContainsText("Short"), TimeSpan.FromSeconds(5))
            .End()
            .Type("X")
            .WaitUntil(s => s.ContainsText("ShortX"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("AB")
            .Tab()
            .Type("XY")
            .WaitUntil(s => s.ContainsText("XY"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("test"), TimeSpan.FromSeconds(5))
            .End()
            .Backspace()
            .Backspace()
            .WaitUntil(s => s.ContainsText("te") && !s.ContainsText("test"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("abc"), TimeSpan.FromSeconds(5))
            .Home()
            .Right()
            .Type("X")
            .WaitUntil(s => s.ContainsText("aXbc"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("@!#")
            .WaitUntil(s => s.ContainsText("@!#"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("@!#", text);
    }

    [Fact]
    public async Task Integration_TextBox_LongTextInNarrowTerminal_ScrollsToShowCursor()
    {
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 5).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("LongTextHere")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // The text box has "LongTextHere" (12 chars) in a 10-col terminal.
        // Bracket mode viewport = 10 - 2 = 8 chars.
        // Cursor starts at end (position 12), so viewport scrolls to show the end.
        // Visible text should include the tail of the string.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("extHere"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // With viewport scrolling, the end of the text is visible with cursor
        Assert.True(snapshot.ContainsText("extHere"));
    }

    #endregion

    #region Uncontrolled Mode Tests

    [Fact]
    public async Task Integration_TextBox_UncontrolledMode_PreservesStateAcrossRerenders()
    {
        // Regression test: TextBox with no state argument should preserve typed content
        // Previously, creating new TextBoxState() inline would reset state on each render
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

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
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The typed text should be visible in the terminal output
        Assert.True(snapshot.ContainsText("Hello"));
    }

    [Fact]
    public async Task Integration_TextBox_UncontrolledMode_MultipleTextBoxes_IndependentState()
    {
        // Each uncontrolled TextBox should have its own independent state
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

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
        
        // Capture snapshot BEFORE exiting
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("AA")
            .Tab()
            .Type("BB")
            .WaitUntil(s => s.ContainsText("BB"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Both texts should be visible
        Assert.True(snapshot.ContainsText("AA"));
        Assert.True(snapshot.ContainsText("BB"));
    }

    [Fact]
    public async Task Integration_TextBox_ControlledMode_StillWorks()
    {
        // Controlled mode with onTextChanged callback
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("Initial"), TimeSpan.FromSeconds(5))
            .End()
            .Type("X")
            .WaitUntil(s => s.ContainsText("InitialX"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Button("Click Me")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Click Me"));
    }

    [Fact]
    public async Task RunFirst_TextBox_RendersWithoutInput()
    {
        // Simplest case: TextBox renders, no input, just Ctrl+C to exit
        // Wait for alternate screen instead of text to isolate rendering from input
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("Initial")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        // Wait for the text to appear (not just alternate screen) so we can verify it
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Initial"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Initial"));
    }

    [Fact]
    public async Task RunFirst_TextBox_SingleCharacterInput()
    {
        // One character typed into TextBox
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("").OnTextChanged(args => capturedText = args.NewText)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.A)  // Single letter 'a'
            .WaitUntil(s => s.ContainsText("a"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.TextBox("").OnTextChanged(args => capturedText = args.NewText)
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("Hello")
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("OK"), TimeSpan.FromSeconds(5))
            .Tab()  // Move focus from Button to TextBox
            .Key(Hex1bKey.X)  // Type 'x' in TextBox
            .WaitUntil(s => s.ContainsText("x"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();
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
            .WaitUntil(s => s.ContainsText("First"), TimeSpan.FromSeconds(5))
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

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

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
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("OK") && s.ContainsText("pre-filled"), TimeSpan.FromSeconds(5), "UI to render")
            .Tab()  // Move focus from Button to TextBox
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)  // Immediately exit
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Use captured snapshot
        Assert.True(snapshot.ContainsText("pre-filled"));
    }

    [Fact]
    public async Task RunFirst_TextBoxInVStack_NoFocusChange()
    {
        // TextBox in VStack, has initial focus, just exit without any interaction
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("test-value")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        // Wait for the text to appear (not just alternate screen) so we can verify it
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("test-value"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("test-value"));
    }

    [Fact]
    public async Task RunFirst_ButtonInVStack_Works()
    {
        // Button in VStack, has initial focus
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.Button("Click Me")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Click Me"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("Click Me"));
    }

    [Fact]
    public async Task RunFirst_TextBoxInVStack_RendersAndExits()
    {
        // TextBox in VStack, verify it renders and Ctrl+C exits properly
        using var workload = new Hex1bAppWorkloadAdapter();

        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 24).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("test")
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        
        // Capture snapshot BEFORE exiting - after await runTask the alternate screen buffer is empty
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("test"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("test"));
    }

    #endregion

    #region Viewport Scrolling Tests

    [Fact]
    public async Task Integration_Viewport_CursorAtEnd_ShowsTailOfText()
    {
        // Terminal width 15 → bracket viewport = 13 chars
        // Text "abcdefghijklmnopqrst" (20 chars) → scrolls to show end
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 3).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [v.TextBox("abcdefghijklmnopqrst")])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end (position 20), viewport shows last 13 chars
        // ScrollOffset = 20 - 13 + 1 = 8, so visible text starts at 'i' (index 8)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ijklmnopqrst"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("ijklmnopqrst"));
    }

    [Fact]
    public async Task Integration_Viewport_HomeKey_ScrollsToStart()
    {
        // Type long text, then press Home — should scroll to show start
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 3).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [v.TextBox("abcdefghijklmnopqrst")])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render, then press Home to move cursor to start
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("pqrst"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Home)
            .WaitUntil(s => s.ContainsText("[abcdefghijklm"), TimeSpan.FromSeconds(5))
            .Capture("after_home")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("[abcdefghijklm"));
    }

    [Fact]
    public async Task Integration_Viewport_ShortText_NoScrolling()
    {
        // Text fits in viewport — no scrolling needed
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 3).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [v.TextBox("hello")])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[hello"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("[hello"));
    }

    [Fact]
    public async Task Integration_Viewport_TypePastEnd_ScrollsRight()
    {
        // Start with empty textbox in narrow terminal, type characters past the viewport width
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [v.TextBox("")])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for empty textbox (renders as "[ ]" with cursor space), then type 12 characters (viewport = 8 in bracket mode)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("[ ]"), TimeSpan.FromSeconds(5))
            .Type("abcdefghijkl")
            .WaitUntil(s => s.ContainsText("fghijkl"), TimeSpan.FromSeconds(5))
            .Capture("after_typing")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // After typing 12 chars in 8-char viewport, should see the end of the text
        // ScrollOffset = 12 - 8 + 1 = 5, visible text = "fghijkl"
        Assert.True(snapshot.ContainsText("fghijkl"));
    }

    #endregion

    #region Multiline State Tests

    [Fact]
    public async Task State_GetLineCount_SingleLine()
    {
        var state = new TextBoxState { Text = "hello world" };
        Assert.Equal(1, state.GetLineCount());
    }

    [Fact]
    public async Task State_GetLineCount_MultipleLines()
    {
        var state = new TextBoxState { Text = "line1\nline2\nline3" };
        Assert.Equal(3, state.GetLineCount());
    }

    [Fact]
    public async Task State_GetLineCount_EmptyString()
    {
        var state = new TextBoxState { Text = "" };
        Assert.Equal(1, state.GetLineCount());
    }

    [Fact]
    public async Task State_GetLineCount_TrailingNewline()
    {
        var state = new TextBoxState { Text = "line1\n" };
        Assert.Equal(2, state.GetLineCount());
    }

    [Fact]
    public async Task State_GetLineStartOffset_FirstLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        Assert.Equal(0, state.GetLineStartOffset(0));
    }

    [Fact]
    public async Task State_GetLineStartOffset_SecondLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        Assert.Equal(4, state.GetLineStartOffset(1));
    }

    [Fact]
    public async Task State_GetLineStartOffset_ThirdLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        Assert.Equal(8, state.GetLineStartOffset(2));
    }

    [Fact]
    public async Task State_GetLineLength_MiddleLine()
    {
        var state = new TextBoxState { Text = "abc\ndefg\nhi" };
        Assert.Equal(4, state.GetLineLength(1)); // "defg"
    }

    [Fact]
    public async Task State_GetLineLength_LastLine()
    {
        var state = new TextBoxState { Text = "abc\ndefg\nhi" };
        Assert.Equal(2, state.GetLineLength(2)); // "hi"
    }

    [Fact]
    public async Task State_OffsetToLineColumn_FirstLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        Assert.Equal((0, 2), state.OffsetToLineColumn(2)); // 'c'
    }

    [Fact]
    public async Task State_OffsetToLineColumn_SecondLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        Assert.Equal((1, 1), state.OffsetToLineColumn(5)); // 'e'
    }

    [Fact]
    public async Task State_OffsetToLineColumn_AtNewline()
    {
        var state = new TextBoxState { Text = "abc\ndef" };
        Assert.Equal((0, 3), state.OffsetToLineColumn(3)); // just before '\n'
    }

    [Fact]
    public async Task State_OffsetToLineColumn_AfterNewline()
    {
        var state = new TextBoxState { Text = "abc\ndef" };
        Assert.Equal((1, 0), state.OffsetToLineColumn(4)); // start of "def"
    }

    [Fact]
    public async Task State_LineColumnToOffset_Roundtrip()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi" };
        var (line, col) = state.OffsetToLineColumn(5);
        Assert.Equal(5, state.LineColumnToOffset(line, col));
    }

    [Fact]
    public async Task State_LineColumnToOffset_ClampsColumn()
    {
        var state = new TextBoxState { Text = "abc\nd" };
        // Column 10 on line 1 ("d") should clamp to offset 5 (end of "d")
        Assert.Equal(5, state.LineColumnToOffset(1, 10));
    }

    [Fact]
    public async Task State_GetLineText()
    {
        var state = new TextBoxState { Text = "abc\ndefg\nhi" };
        Assert.Equal("defg", state.GetLineText(1));
    }

    #endregion

    #region Multiline Vertical Navigation Tests

    [Fact]
    public async Task State_MoveUp_MovesToPreviousLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 5; // 'e' on line 1
        state.MoveUp();
        Assert.Equal((0, 1), state.OffsetToLineColumn(state.CursorPosition)); // 'b' on line 0
    }

    [Fact]
    public async Task State_MoveDown_MovesToNextLine()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 1; // 'b' on line 0
        state.MoveDown();
        Assert.Equal((1, 1), state.OffsetToLineColumn(state.CursorPosition)); // 'e' on line 1
    }

    [Fact]
    public async Task State_MoveUp_OnFirstLine_StaysOnFirstLine()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true };
        state.CursorPosition = 2;
        state.MoveUp();
        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public async Task State_MoveDown_OnLastLine_StaysOnLastLine()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true };
        state.CursorPosition = 5;
        state.MoveDown();
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public async Task State_MoveUp_ClampsToShorterLine()
    {
        var state = new TextBoxState { Text = "ab\nabcdef\ngh", IsMultiline = true };
        state.CursorPosition = 8; // 'e' (col 5) on "abcdef"
        state.MoveUp();
        // Line 0 "ab" has length 2, so column clamps to 2 (end of "ab")
        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public async Task State_MoveDown_PreservesPreferredColumn()
    {
        var state = new TextBoxState { Text = "abcdef\nab\nabcdef", IsMultiline = true };
        state.CursorPosition = 5; // col 5 on "abcdef"
        state.MoveDown(); // moves to "ab", clamped to col 2
        Assert.Equal((1, 2), state.OffsetToLineColumn(state.CursorPosition));
        state.MoveDown(); // moves to "abcdef", restores to col 5
        Assert.Equal((2, 5), state.OffsetToLineColumn(state.CursorPosition));
    }

    [Fact]
    public async Task State_MoveUp_WithShift_CreatesSelection()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true };
        state.CursorPosition = 5; // 'e' on line 1
        state.MoveUp(extend: true);
        Assert.True(state.HasSelection);
        Assert.Equal(5, state.SelectionAnchor);
        Assert.Equal(1, state.CursorPosition); // 'b' on line 0
    }

    #endregion

    #region Multiline Enter Key Tests

    [Fact]
    public async Task State_InsertNewline_InsertsAtCursor()
    {
        var state = new TextBoxState { Text = "abcdef", IsMultiline = true };
        state.CursorPosition = 3;
        state.InsertNewline();
        Assert.Equal("abc\ndef", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public async Task State_InsertNewline_AtStart()
    {
        var state = new TextBoxState { Text = "abc", IsMultiline = true };
        state.CursorPosition = 0;
        state.InsertNewline();
        Assert.Equal("\nabc", state.Text);
        Assert.Equal(1, state.CursorPosition);
    }

    [Fact]
    public async Task State_InsertNewline_AtEnd()
    {
        var state = new TextBoxState { Text = "abc", IsMultiline = true };
        state.CursorPosition = 3;
        state.InsertNewline();
        Assert.Equal("abc\n", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public async Task State_InsertNewline_DeletesSelection()
    {
        var state = new TextBoxState { Text = "abcdef", IsMultiline = true };
        state.SelectionAnchor = 1;
        state.CursorPosition = 4;
        state.InsertNewline();
        Assert.Equal("a\nef", state.Text);
        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_Enter_InMultilineMode_InsertsNewline()
    {
        var state = new TextBoxState { Text = "abc", IsMultiline = true };
        state.CursorPosition = 3;
        var handled = state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        Assert.True(handled);
        Assert.Equal("abc\n", state.Text);
    }

    [Fact]
    public async Task HandleInput_Enter_InSingleLineMode_NotHandled()
    {
        var state = new TextBoxState { Text = "abc", IsMultiline = false };
        state.CursorPosition = 3;
        var handled = state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        Assert.False(handled);
        Assert.Equal("abc", state.Text);
    }

    #endregion

    #region Multiline Home/End Tests

    [Fact]
    public async Task HandleInput_Home_MultilineMode_GoesToLineStart()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 6; // 'f' on line 1
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None));
        Assert.Equal(4, state.CursorPosition); // start of "def"
    }

    [Fact]
    public async Task HandleInput_End_MultilineMode_GoesToLineEnd()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 4; // 'd' on line 1
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None));
        Assert.Equal(7, state.CursorPosition); // end of "def"
    }

    [Fact]
    public async Task HandleInput_CtrlHome_MultilineMode_GoesToDocumentStart()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 9; // 'h' on line 2
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.Control));
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_CtrlEnd_MultilineMode_GoesToDocumentEnd()
    {
        var state = new TextBoxState { Text = "abc\ndef\nghi", IsMultiline = true };
        state.CursorPosition = 0;
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.Control));
        Assert.Equal(11, state.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_Home_SingleLineMode_GoesToDocumentStart()
    {
        var state = new TextBoxState { Text = "hello", IsMultiline = false };
        state.CursorPosition = 3;
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None));
        Assert.Equal(0, state.CursorPosition);
    }

    #endregion

    #region Multiline Up/Down Arrow Tests

    [Fact]
    public async Task HandleInput_UpArrow_MultilineMode_MovesUp()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true };
        state.CursorPosition = 5; // 'e'
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        Assert.Equal(1, state.CursorPosition); // 'b'
    }

    [Fact]
    public async Task HandleInput_DownArrow_MultilineMode_MovesDown()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true };
        state.CursorPosition = 1; // 'b'
        state.HandleInput(new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        Assert.Equal(5, state.CursorPosition); // 'e'
    }

    [Fact]
    public async Task HandleInput_UpArrow_SingleLineMode_NotHandled()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = false };
        state.CursorPosition = 5;
        var handled = state.HandleInput(new Hex1bKeyEvent(Hex1bKey.UpArrow, '\0', Hex1bModifiers.None));
        Assert.False(handled);
        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public async Task HandleInput_DownArrow_SingleLineMode_NotHandled()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = false };
        state.CursorPosition = 1;
        var handled = state.HandleInput(new Hex1bKeyEvent(Hex1bKey.DownArrow, '\0', Hex1bModifiers.None));
        Assert.False(handled);
        Assert.Equal(1, state.CursorPosition);
    }

    #endregion

    #region Multiline Node Integration Tests

    [Fact]
    public async Task Integration_Multiline_EnterInsertsNewline()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(5).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("hello")
            .Key(Hex1bKey.Enter)
            .Type("world")
            .WaitUntil(s => s.ContainsText("world"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("hello\nworld", capturedText);
    }

    [Fact]
    public async Task Integration_Multiline_RendersMultipleLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("line1\nline2\nline3").Multiline().Height(5)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("line1") && s.ContainsText("line2") && s.ContainsText("line3"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("line1"));
        Assert.True(snapshot.ContainsText("line2"));
        Assert.True(snapshot.ContainsText("line3"));
    }

    [Fact]
    public async Task Integration_Multiline_UpDownArrowNavigation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("abc\ndef").Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end of "def" (position 7). Press Up to go to "abc", then type "X"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("abc"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow)
            .Type("X")
            .WaitUntil(s => s.ContainsText("abcX"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("abcX\ndef", capturedText);
    }

    [Fact]
    public async Task Integration_Multiline_WordWrap_WrapsLongLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("hello world this is a long sentence").Multiline().WordWrap().Height(5)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // The text is 35 chars, viewport is 20 chars. Should wrap.
        // "hello world this is " (20) + "a long sentence" (15)
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Home)
            .Ctrl().Key(Hex1bKey.Home)
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Both parts of the wrapped text should be visible
        Assert.True(snapshot.ContainsText("hello"));
        Assert.True(snapshot.ContainsText("sentence"));
    }

    [Fact]
    public async Task Integration_Multiline_DefaultTextBox_StillSingleLine()
    {
        // Verify that a default TextBox (no Multiline()) still works as single-line
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("hello")
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Should still have brackets for single-line
        Assert.True(snapshot.ContainsText("hello"));
    }

    [Fact]
    public async Task Integration_Multiline_VerticalScrolling()
    {
        // Test vertical scrolling when content exceeds visible height
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("line1\nline2\nline3\nline4\nline5\nline6\nline7").Multiline().Height(3)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end (line7), so viewport should scroll to show last 3 lines
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("line7"), TimeSpan.FromSeconds(5))
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.True(snapshot.ContainsText("line7"));
        // line1 should NOT be visible (it's scrolled off)
        Assert.False(snapshot.ContainsText("line1"));
    }

    #endregion

    #region MaxLines Tests

    [Fact]
    public async Task State_InsertNewline_RespectsMaxLines()
    {
        var state = new TextBoxState { Text = "abc\ndef", IsMultiline = true, MaxLines = 2 };
        state.CursorPosition = 3;
        state.InsertNewline();
        // Should not insert — already at 2 lines
        Assert.Equal("abc\ndef", state.Text);
        Assert.Equal(3, state.CursorPosition);
    }

    [Fact]
    public async Task State_InsertNewline_AllowedBelowMaxLines()
    {
        var state = new TextBoxState { Text = "abc", IsMultiline = true, MaxLines = 3 };
        state.CursorPosition = 3;
        state.InsertNewline();
        Assert.Equal("abc\n", state.Text);
        Assert.Equal(4, state.CursorPosition);
        // Now at 2 lines, can still add one more
        state.InsertNewline();
        Assert.Equal("abc\n\n", state.Text);
        // Now at 3 lines, should block further
        state.InsertNewline();
        Assert.Equal("abc\n\n", state.Text);
    }

    [Fact]
    public async Task State_InsertNewline_NoLimit_WhenMaxLinesNull()
    {
        var state = new TextBoxState { Text = "a\nb\nc", IsMultiline = true, MaxLines = null };
        state.CursorPosition = 5;
        state.InsertNewline();
        Assert.Equal("a\nb\nc\n", state.Text);
    }

    [Fact]
    public async Task HandleInput_Enter_RespectsMaxLines()
    {
        var state = new TextBoxState { Text = "line1\nline2\nline3", IsMultiline = true, MaxLines = 3 };
        state.CursorPosition = 5;
        var handled = state.HandleInput(new Hex1bKeyEvent(Hex1bKey.Enter, '\r', Hex1bModifiers.None));
        Assert.True(handled); // Input is consumed even if newline is blocked
        Assert.Equal("line1\nline2\nline3", state.Text); // No change
    }

    #endregion

    #region MoveDown/MoveUp Creates New Line Tests

    [Fact]
    public async Task Integration_MoveDown_OnLastLine_CreatesNewLine()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var capturedText = "hello";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().OnTextChanged(e => capturedText = e.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.End) // move to end of line
            .Key(Hex1bKey.DownArrow) // should create new line
            .WaitUntil(s => capturedText.Contains('\n'), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("hello\n", capturedText);
    }

    [Fact]
    public async Task Integration_MoveDown_OnLastLine_WithMaxLines_Blocked()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var capturedText = "line1\nline2";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline(2).OnTextChanged(e => capturedText = e.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("line1"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.DownArrow) // should be blocked — already at 2 lines
            .Key(Hex1bKey.DownArrow) // extra press to ensure processing
            .WaitUntil(s => s.ContainsText("line2"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("line1\nline2", capturedText);
    }

    [Fact]
    public async Task Integration_MoveDown_NotOnLastLine_NavigatesNormally()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var capturedText = "abc\ndef\nghi";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().OnTextChanged(e => capturedText = e.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        // Cursor starts at end of text (line 2). Press Up twice to reach line 0,
        // then Down once to navigate to line 1 (not the last line).
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("abc"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow) // line 2 → line 1
            .Key(Hex1bKey.UpArrow) // line 1 → line 0
            .Key(Hex1bKey.DownArrow) // line 0 → line 1 (not last, no new line)
            .Key(Hex1bKey.Home) // start of line 1
            .Type("X") // verify we're on line 1
            .WaitUntil(s => capturedText.Contains("Xdef"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Text should have X inserted on line 1, no new lines created
        Assert.Equal("abc\nXdef\nghi", capturedText);
    }

    [Fact]
    public async Task Integration_MoveUp_OnFirstLine_IsNoOp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var capturedText = "hello";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().OnTextChanged(e => capturedText = e.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow) // should be a no-op on first line
            .Type("X") // type to verify no new line was created
            .WaitUntil(s => capturedText.Contains("X"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // No newline should have been inserted — X appended to same line
        Assert.DoesNotContain("\n", capturedText);
    }

    [Fact]
    public async Task Integration_MaxLines_BlocksEnterAtLimit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();
        var capturedText = "";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline(3).OnTextChanged(e => capturedText = e.NewText)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Type("a")
            .Key(Hex1bKey.Enter)
            .Type("b")
            .Key(Hex1bKey.Enter)
            .Type("c")
            .Key(Hex1bKey.Enter) // should be blocked — already at 3 lines
            .Type("d")
            .WaitUntil(s => capturedText.Contains("cd"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("a\nb\ncd", capturedText);
    }

    #endregion

    #region Multiline Per-Line Horizontal Viewport Tests

    // ────────────────────────────────────────────────────────────────────────
    // These tests verify that in multiline mode WITHOUT word wrap, each line
    // has an independent horizontal viewport:
    //
    //   • Lines longer than the viewport width are truncated to viewport width
    //   • The line containing the cursor is scrolled horizontally so the cursor
    //     is always visible (same logic as single-line viewport scrolling)
    //   • Lines that do NOT contain the cursor snap their viewport back to
    //     offset 0 (showing the beginning of the line)
    //   • Word-wrapped mode is unaffected — it still wraps at word boundaries
    //     and does NOT use per-line horizontal scrolling
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Integration_MultilineNoWrap_LongLinesTruncatedToViewportWidth()
    {
        // A multiline text box (no word wrap) in a narrow terminal should
        // truncate each line to the viewport width rather than rendering
        // the full line text beyond the visible area.
        using var workload = new Hex1bAppWorkloadAdapter();
        // Width 15 means the text field gets ~15 columns (fill mode).
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("short\nabcdefghijklmnopqrstuvwxyz").Multiline().Height(3)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("short"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow) // move cursor to line 0 so line 1 snaps to start
            .WaitUntil(s => s.ContainsText("abcdef"), TimeSpan.FromSeconds(5))
            .Capture("truncated")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // "short" fits within viewport — should be fully visible
        Assert.True(snapshot.ContainsText("short"));
        // The long line starts with "abcdefghijklmno" (15 chars) — that prefix should be visible
        Assert.True(snapshot.ContainsText("abcdefghijklmno"));
        // Characters beyond the viewport (e.g. "pqrstuvwxyz") should NOT appear
        Assert.False(snapshot.ContainsText("qrstuvwxyz"));
    }

    [Fact]
    public async Task Integration_MultilineNoWrap_CursorLineScrollsHorizontally()
    {
        // When the cursor is on a long line and moved to the end, the viewport
        // for that line should scroll right to keep the cursor visible, showing
        // the end portion of the line instead of the beginning.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();
        var capturedText = "short\nabcdefghijklmnopqrstuvwxyz";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end of text (end of line 1). Press End to ensure
        // we're at the very end of the long line, then capture.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("short"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.End) // ensure cursor is at end of long line
            .WaitUntil(s => s.ContainsText("xyz"), TimeSpan.FromSeconds(5))
            .Capture("scrolled")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // The cursor line should have scrolled to show the end of the alphabet.
        // The tail "xyz" must be visible because the cursor is at the end.
        Assert.True(snapshot.ContainsText("xyz"));
    }

    [Fact]
    public async Task Integration_MultilineNoWrap_NonCursorLineSnapsToStart()
    {
        // When the cursor moves away from a long line to a different line,
        // the previously-scrolled line should snap its viewport back to
        // offset 0, showing the beginning of that line.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();
        var capturedText = "short\nabcdefghijklmnopqrstuvwxyz";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // 1) Wait for render, cursor at end of line 1 (the long line).
        //    The long line is scrolled to show "xyz".
        // 2) Press Up to move cursor to line 0 ("short").
        //    Now line 1 should snap back to showing "abcdefghijklmno" (start).
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("short"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.End) // cursor at end of long line (scrolled right)
            .WaitUntil(s => s.ContainsText("xyz"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow) // move cursor to line 0
            .WaitUntil(s => s.ContainsText("abcdef"), TimeSpan.FromSeconds(5))
            .Capture("snapped")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Line 0 ("short") should be visible since the cursor is there now
        Assert.True(snapshot.ContainsText("short"));
        // Line 1 should have snapped back to start — "abcdef" prefix visible
        Assert.True(snapshot.ContainsText("abcdef"));
        // The tail of line 1 should NOT be visible anymore since cursor left that line
        Assert.False(snapshot.ContainsText("xyz"));
    }

    [Fact]
    public async Task Integration_MultilineNoWrap_EachLineHasIndependentViewport()
    {
        // With multiple long lines, only the cursor line scrolls. Other lines
        // always show their beginning. This tests that viewports are truly
        // independent per line.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();
        var capturedText = "AAAAAAAAAAAAAAAAAAAAA\nBBBBBBBBBBBBBBBBBBBBB\nCCCCCCCCCCCCCCCCCCCCC";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(4).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end of line 2 (the C line, which is scrolled right).
        // Move up to line 1 (B line) and then to the end of that line.
        // Line 1 should scroll to show end of Bs; lines 0 and 2 show start.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.UpArrow) // cursor to line 1 (B line)
            .Key(Hex1bKey.End)     // cursor to end of B line
            .WaitUntil(s => !s.ContainsText("CCCCCCCCCCCCCCCCCCCCC"), TimeSpan.FromSeconds(5))
            .Capture("independent")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Line 0 (A line): cursor not here → shows from start "AAAAAAAAAAAAAAA" (15 As)
        Assert.True(snapshot.ContainsText("AAAAAAAAAAAAAAA"));
        // Line 2 (C line): cursor not here → shows from start "CCCCCCCCCCCCCCC" (15 Cs)
        Assert.True(snapshot.ContainsText("CCCCCCCCCCCCCCC"));
    }

    [Fact]
    public async Task Integration_MultilineNoWrap_ShortLinesUnaffected()
    {
        // Lines that fit entirely within the viewport width should be rendered
        // in full regardless of cursor position. Only long lines are affected
        // by the per-line viewport scrolling.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("hi\nthere\nworld").Multiline().Height(4)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hi") && s.ContainsText("there") && s.ContainsText("world"), TimeSpan.FromSeconds(5))
            .Capture("short")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // All short lines should be fully visible regardless of cursor position
        Assert.True(snapshot.ContainsText("hi"));
        Assert.True(snapshot.ContainsText("there"));
        Assert.True(snapshot.ContainsText("world"));
    }

    [Fact]
    public async Task Integration_MultilineNoWrap_TypingScrollsCursorLineOnly()
    {
        // When typing at the end of a long line, the cursor line should scroll
        // to keep the cursor visible, while other lines remain at offset 0.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(10, 10).Build();
        var capturedText = "top\nbottom";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end of "bottom". Type enough to exceed viewport.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("top"), TimeSpan.FromSeconds(5))
            .Type("XXXXXXXXXXXX") // extend line 1 well beyond 10 chars
            .WaitUntil(s => capturedText.Contains("XXXXXXXXXXXX"), TimeSpan.FromSeconds(5))
            .Capture("typed")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Line 0 "top" is short and cursor isn't there → fully visible
        Assert.True(snapshot.ContainsText("top"));
        // Line 1 cursor is at end after typing Xs → should show the end portion
        // The beginning "bottom" should have scrolled off if the line is long enough
        Assert.True(snapshot.ContainsText("XXXX")); // end of the typed text visible
    }

    [Fact]
    public async Task Integration_MultilineWithWrap_NotAffectedByPerLineViewport()
    {
        // Word-wrapped multiline mode should NOT use per-line horizontal scrolling.
        // Long lines should wrap at word boundaries as before. This test ensures
        // the per-line viewport feature does not break word-wrap behavior.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(15, 10).Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox("hello world overflow text here").Multiline().WordWrap().Height(5)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("hello"), TimeSpan.FromSeconds(5))
            .Capture("wrapped")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // With word wrap, all text should be visible across multiple visual lines.
        // The words wrap at boundaries so all content appears on screen.
        Assert.True(snapshot.ContainsText("hello"));
        Assert.True(snapshot.ContainsText("overflow"));
        Assert.True(snapshot.ContainsText("here"));
    }

    #endregion

    #region Multiline Preferred Column Navigation Tests

    [Fact]
    public async Task Integration_Multiline_UpDown_PreservesColumnAcrossShortLine()
    {
        // When navigating up/down between long lines via a short intermediate line,
        // the cursor should restore to the original column on the target line
        // (standard text editor "preferred column" behavior).
        // Line 0: "Hello World!" (12 chars)
        // Line 1: "Short"        (5 chars)
        // Line 2: "Another Long!" (13 chars)
        // Starting at col 10 on line 0, Down clamps to col 5 on "Short",
        // then another Down should restore to col 10 on "Another Long!".
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var capturedText = "Hello World!\nShort\nAnother Long!";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(4).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at end of text (line 2, col 13).
        // Navigate to line 0, col 10: Home to start of line 2, Up twice to line 0,
        // Home to start, then Right×10 to col 10.
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Home)      // start of current (last) line
            .Key(Hex1bKey.UpArrow)   // line 1
            .Key(Hex1bKey.UpArrow)   // line 0
            .Key(Hex1bKey.Home);     // col 0 on line 0
        for (int i = 0; i < 10; i++)
            builder = builder.Key(Hex1bKey.RightArrow); // col 10 on line 0

        // Down through "Short" (clamped to col 5), Down to "Another Long!" (restored to col 10)
        builder = builder
            .Key(Hex1bKey.DownArrow)  // line 1, col 5 (clamped)
            .Key(Hex1bKey.DownArrow)  // line 2, col 10 (restored)
            .Key(Hex1bKey.Delete);    // delete char at col 10 to verify position

        await builder.WaitUntil(s => true, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Col 10 in "Another Long!" is 'n' → deleting gives "Another Log!"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => true, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("Hello World!\nShort\nAnother Log!", capturedText);
    }

    [Fact]
    public async Task Integration_Multiline_LeftArrowClearsPreferredColumn()
    {
        // After pressing Left/Right, the preferred column should be reset so that
        // the next Up/Down uses the actual cursor column, not a stale value.
        // Line 0: "ABCDEFGHIJ" (10 chars)
        // Line 1: "abcdefghij" (10 chars)
        // Start at col 8 on line 0, Down to col 8 on line 1.
        // Press Left 5 times to col 3 on line 1.
        // Press Up — should go to col 3 on line 0 (not col 8).
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var capturedText = "ABCDEFGHIJ\nabcdefghij";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Navigate to line 0, col 8: Home → UpArrow → Home → Right×8
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ABCDEFGHIJ"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Home)
            .Key(Hex1bKey.UpArrow)
            .Key(Hex1bKey.Home);
        for (int i = 0; i < 8; i++)
            builder = builder.Key(Hex1bKey.RightArrow); // col 8 on line 0

        // Down to line 1 col 8, Left×5 to col 3, Up to line 0 col 3, Delete to verify
        builder = builder.Key(Hex1bKey.DownArrow); // line 1, col 8
        for (int i = 0; i < 5; i++)
            builder = builder.Key(Hex1bKey.LeftArrow); // line 1, col 3
        builder = builder
            .Key(Hex1bKey.UpArrow)  // line 0, col 3 (not 8, Left cleared preferred column)
            .Key(Hex1bKey.Delete);  // delete char at col 3 → 'D' deleted

        await builder.WaitUntil(s => true, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => true, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("ABCEFGHIJ\nabcdefghij", capturedText);
    }

    [Fact]
    public async Task Integration_Multiline_TypingClearsPreferredColumn()
    {
        // After typing a character, the preferred column should be reset so that
        // the next Up/Down uses the actual cursor position.
        // Line 0: "ABCDEF" (6 chars)
        // Line 1: "abcdef" (6 chars)
        // Navigate to col 5 on line 0, Down to col 5 on line 1,
        // press Home to go to col 0, type "X" (now col 1),
        // press Up — should go to col 1 on line 0 (not col 5).
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();
        var capturedText = "ABCDEF\nabcdef";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(capturedText).Multiline().Height(3).OnTextChanged(e => capturedText = e.NewText)
                ])),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Navigate to line 0, col 5: Home → Up → Home → Right×5
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("ABCDEF"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Home)
            .Key(Hex1bKey.UpArrow)
            .Key(Hex1bKey.Home);
        for (int i = 0; i < 5; i++)
            builder = builder.Key(Hex1bKey.RightArrow); // col 5 on line 0

        // Down to line 1 col 5, Home to col 0, type "X" → col 1
        // Up should go to col 1 on line 0, Delete to verify
        builder = builder
            .Key(Hex1bKey.DownArrow)  // line 1, col 5
            .Key(Hex1bKey.Home)       // line 1, col 0
            .Type("X")               // line 1, col 1 — typed "X" at start
            .Key(Hex1bKey.UpArrow)    // line 0, col 1 (not 5, typing reset preferred col)
            .Key(Hex1bKey.Delete);    // delete char at col 1 → 'B' deleted

        await builder.WaitUntil(s => true, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => true, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        Assert.Equal("ACDEF\nXabcdef", capturedText);
    }

    #endregion
}