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
    public void Measure_ReturnsCorrectSize()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "hello" } };

        var size = node.Measure(Constraints.Unbounded);

        // "[hello]" = 2 brackets + 5 chars = 7
        Assert.Equal(7, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_EmptyText_HasMinWidth()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "" } };

        var size = node.Measure(Constraints.Unbounded);

        // "[ ]" = 2 brackets + 1 min char = 3
        Assert.Equal(3, size.Width);
    }

    [Fact]
    public void Measure_LongText_MeasuresFullWidth()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "This is a very long text input" } };

        var size = node.Measure(Constraints.Unbounded);

        // 30 chars + 2 brackets = 32
        Assert.Equal(32, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact]
    public void Measure_RespectsMaxWidthConstraint()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "Long text here" } };

        var size = node.Measure(new Constraints(0, 10, 0, 5));

        Assert.Equal(10, size.Width);
    }

    #endregion

    #region Rendering Tests - Unfocused State

    [Fact]
    public void Render_Unfocused_ShowsBrackets()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "test" },
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[test]", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Unfocused_EmptyText_ShowsEmptyBrackets()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "" },
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[]", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Unfocused_LongText_RendersCompletely()
    {
        using var terminal = new Hex1bTerminal(80, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "This is a longer piece of text" },
            IsFocused = false
        };

        node.Render(context);

        Assert.Contains("[This is a longer piece of text]", terminal.GetLineTrimmed(0));
    }

    #endregion

    #region Rendering Tests - Focused State with Cursor

    [Fact]
    public void Render_Focused_ShowsCursor()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "abc", CursorPosition = 1 },
            IsFocused = true
        };

        node.Render(context);

        // When focused, the cursor character should be highlighted with ANSI codes
        Assert.Contains("\x1b[", terminal.RawOutput);
        // The text content should still be visible
        Assert.Contains("a", terminal.GetLineTrimmed(0));
        Assert.Contains("b", terminal.GetLineTrimmed(0));
        Assert.Contains("c", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_CursorAtStart_HighlightsFirstChar()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "hello", CursorPosition = 0 },
            IsFocused = true
        };

        node.Render(context);

        // Should have ANSI codes for cursor highlighting
        Assert.Contains("\x1b[", terminal.RawOutput);
        Assert.Contains("hello", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_Focused_CursorAtEnd_ShowsCursorSpace()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "test", CursorPosition = 4 },
            IsFocused = true
        };

        node.Render(context);

        // When cursor is at end, a space is shown as cursor placeholder
        Assert.Contains("\x1b[", terminal.RawOutput);
    }

    [Fact]
    public void Render_Focused_EmptyText_ShowsCursorSpace()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var node = new TextBoxNode
        {
            State = new TextBoxState { Text = "", CursorPosition = 0 },
            IsFocused = true
        };

        node.Render(context);

        // Should still have the brackets and ANSI codes for cursor
        Assert.Contains("[", terminal.GetLineTrimmed(0));
        Assert.Contains("]", terminal.GetLineTrimmed(0));
        Assert.Contains("\x1b[", terminal.RawOutput);
    }

    #endregion

    #region Rendering Tests - Selection

    [Fact]
    public void Render_WithSelection_HighlightsSelectedText()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var state = new TextBoxState { Text = "hello world" };
        state.SelectionAnchor = 0;
        state.CursorPosition = 5;

        var node = new TextBoxNode { State = state, IsFocused = true };

        node.Render(context);

        // Should have ANSI codes for selection highlighting
        Assert.Contains("\x1b[", terminal.RawOutput);
        // The text should still be present
        Assert.Contains("hello", terminal.GetLineTrimmed(0));
        Assert.Contains("world", terminal.GetLineTrimmed(0));
    }

    [Fact]
    public void Render_WithSelection_InMiddle_HighlightsCorrectPortion()
    {
        using var terminal = new Hex1bTerminal(40, 5);
        var context = new Hex1bRenderContext(terminal);
        var state = new TextBoxState { Text = "abcdefgh" };
        state.SelectionAnchor = 2;
        state.CursorPosition = 5;

        var node = new TextBoxNode { State = state, IsFocused = true };

        node.Render(context);

        // Should contain the full text
        Assert.Contains("abcdefgh", terminal.GetLineTrimmed(0));
        // Should have selection ANSI codes
        Assert.Contains("\x1b[", terminal.RawOutput);
    }

    #endregion

    #region Input Handling Tests

    [Fact]
    public void HandleInput_WhenFocused_UpdatesState()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None));

        Assert.Equal(InputResult.Handled, result);
        Assert.Equal("helloX", state.Text);
    }

    [Fact]
    public void HandleInput_WhenNotFocused_DoesNotHandle()
    {
        var state = new TextBoxState { Text = "hello" };
        var node = new TextBoxNode { State = state, IsFocused = false };

        var result = node.HandleInput(new Hex1bKeyEvent(Hex1bKey.X, 'X', Hex1bModifiers.None));

        Assert.Equal(InputResult.NotHandled, result);
        Assert.Equal("hello", state.Text);
    }

    [Fact]
    public void HandleInput_Backspace_DeletesCharacter()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 5 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Backspace, '\b', Hex1bModifiers.None));

        Assert.Equal("hell", state.Text);
        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Delete_DeletesCharacterAhead()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 0 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Delete, '\0', Hex1bModifiers.None));

        Assert.Equal("ello", state.Text);
        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_LeftArrow_MovesCursorLeft()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(2, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_RightArrow_MovesCursorRight()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.RightArrow, '\0', Hex1bModifiers.None));

        Assert.Equal(4, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_Home_MovesCursorToStart()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.Home, '\0', Hex1bModifiers.None));

        Assert.Equal(0, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_End_MovesCursorToEnd()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.End, '\0', Hex1bModifiers.None));

        Assert.Equal(5, state.CursorPosition);
    }

    [Fact]
    public void HandleInput_ShiftLeftArrow_CreatesSelection()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 3 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.LeftArrow, '\0', Hex1bModifiers.Shift));

        Assert.Equal(2, state.CursorPosition);
        Assert.True(state.HasSelection);
        Assert.Equal(3, state.SelectionAnchor);
    }

    [Fact]
    public void HandleInput_CtrlA_SelectsAll()
    {
        var state = new TextBoxState { Text = "hello", CursorPosition = 2 };
        var node = new TextBoxNode { State = state, IsFocused = true };

        node.HandleInput(new Hex1bKeyEvent(Hex1bKey.A, 'a', Hex1bModifiers.Control));

        Assert.True(state.HasSelection);
        Assert.Equal(0, state.SelectionAnchor);
        Assert.Equal(5, state.CursorPosition);
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

    #region Layout Tests

    [Fact]
    public void Arrange_SetsBounds()
    {
        var node = new TextBoxNode { State = new TextBoxState { Text = "test" } };
        var bounds = new Rect(5, 10, 20, 1);

        node.Arrange(bounds);

        Assert.Equal(bounds, node.Bounds);
    }

    #endregion

    #region Integration Tests with Hex1bApp

    [Fact]
    public async Task Integration_TextBox_RendersViaHex1bApp()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "Initial Text" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        Assert.True(terminal.ContainsText("Initial Text"));
    }

    [Fact]
    public async Task Integration_TextBox_ReceivesInput()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.H, 'H', shift: true);
        terminal.SendKey(ConsoleKey.E, 'e');
        terminal.SendKey(ConsoleKey.L, 'l');
        terminal.SendKey(ConsoleKey.L, 'l');
        terminal.SendKey(ConsoleKey.O, 'o');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("Hello", textState.Text);
    }

    [Fact]
    public async Task Integration_TextBox_InNarrowTerminal_StillWorks()
    {
        using var terminal = new Hex1bTerminal(15, 5);
        var textState = new TextBoxState { Text = "Short" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.End, '\0');
        terminal.SendKey(ConsoleKey.X, 'X');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("ShortX", textState.Text);
    }

    [Fact]
    public async Task Integration_TextBox_TabBetweenMultiple()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var state1 = new TextBoxState { Text = "" };
        var state2 = new TextBoxState { Text = "" };

        using var app = new Hex1bApp<object>(
            new object(),
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(state1),
                    v.TextBox(state2)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Type in first box
        terminal.SendKey(ConsoleKey.A, 'A', shift: true);
        terminal.SendKey(ConsoleKey.B, 'B', shift: true);
        // Tab to second box
        terminal.SendKey(ConsoleKey.Tab, '\t');
        // Type in second box
        terminal.SendKey(ConsoleKey.X, 'X', shift: true);
        terminal.SendKey(ConsoleKey.Y, 'Y', shift: true);
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("AB", state1.Text);
        Assert.Equal("XY", state2.Text);
    }

    [Fact]
    public async Task Integration_TextBox_BackspaceWorks()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "test" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.End, '\0');
        terminal.SendKey(ConsoleKey.Backspace, '\b');
        terminal.SendKey(ConsoleKey.Backspace, '\b');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("te", textState.Text);
    }

    [Fact]
    public async Task Integration_TextBox_CursorNavigationWorks()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "abc" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        // Go to start, then right, then insert
        terminal.SendKey(ConsoleKey.Home, '\0');
        terminal.SendKey(ConsoleKey.RightArrow, '\0');
        terminal.SendKey(ConsoleKey.X, 'X');
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("aXbc", textState.Text);
    }

    [Fact]
    public async Task Integration_TextBox_SpecialCharactersWork()
    {
        using var terminal = new Hex1bTerminal(80, 24);
        var textState = new TextBoxState { Text = "" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.SendKey(ConsoleKey.Oem4, '@');
        terminal.SendKey(ConsoleKey.D1, '!', shift: true);
        terminal.SendKey(ConsoleKey.D3, '#', shift: true);
        terminal.CompleteInput();

        await app.RunAsync();

        Assert.Equal("@!#", textState.Text);
    }

    [Fact]
    public async Task Integration_TextBox_LongTextInNarrowTerminal_Wraps()
    {
        using var terminal = new Hex1bTerminal(10, 5);
        var textState = new TextBoxState { Text = "LongTextHere" };

        using var app = new Hex1bApp<TextBoxState>(
            textState,
            (ctx, ct) => Task.FromResult<Hex1bWidget>(
                ctx.VStack(v => [
                    v.TextBox(s => s)
                ])
            ),
            new Hex1bAppOptions { Terminal = terminal }
        );

        terminal.CompleteInput();
        await app.RunAsync();

        // The text box renders as "[LongTextHere]" which is 14 chars
        // In a 10-char wide terminal, it will wrap
        // Check that the text content is present (split across lines)
        Assert.True(terminal.ContainsText("[LongText"));
    }

    #endregion
}
