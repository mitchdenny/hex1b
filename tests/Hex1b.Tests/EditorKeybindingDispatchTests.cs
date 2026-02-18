// NOTE: These tests verify that keybindings in the full Hex1bApp dispatch to correct
// EditorState operations. As editor capabilities evolve (e.g., custom keymaps, vim mode),
// default bindings may change. Tests resolve colors from the theme under test.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorKeybindingDispatchTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bApp app,
        EditorState state, Hex1bTheme theme, Task runTask) SetupEditor(
        string text, int width = 40, int height = 10)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        return (workload, terminal, app, state, theme, runTask);
    }

    private static async Task WaitForEditor(Hex1bTerminal terminal)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen,
                TimeSpan.FromSeconds(2), "editor visible in alt screen")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    private static async Task ExitAndWait(Hex1bTerminal terminal, Task runTask)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task LeftArrow_MovesCursorLeft()
    {
        // NOTE: Left arrow behavior may change with soft-wrap navigation.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("ABCDE");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Move cursor right twice via keyboard, then left once
        var cursorOnC = new CellPatternSearcher()
            .Find(ctx => ctx.X == 2 && ctx.Y == 0
                      && ctx.Cell.Character == "C"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        var cursorOnB = new CellPatternSearcher()
            .Find(ctx => ctx.X == 1 && ctx.Y == 0
                      && ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Right()
            .Right()
            .WaitUntil(s => s.SearchPattern(cursorOnC).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on C after two Rights")
            .Left()
            .WaitUntil(s => s.SearchPattern(cursorOnB).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on B after Left")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(1), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task RightArrow_MovesCursorRight()
    {
        // NOTE: Right arrow at EOL may gain soft-wrap-to-next-line behavior.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("ABCDE");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        var cursorOnB = new CellPatternSearcher()
            .Find(ctx => ctx.X == 1 && ctx.Y == 0
                      && ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Right()
            .WaitUntil(s => s.SearchPattern(cursorOnB).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on B after Right")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(1), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task UpArrow_MovesCursorToPreviousLine()
    {
        // NOTE: Up arrow column tracking may change with virtual column memory.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("AAA\nBBB\nCCC");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Press Down to go to line 2, then Up to return to line 1
        var cursorOnB = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 1
                      && ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        var cursorOnA = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "A"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Down()
            .WaitUntil(s => s.SearchPattern(cursorOnB).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on B after Down")
            .Up()
            .WaitUntil(s => s.SearchPattern(cursorOnA).HasMatches,
                TimeSpan.FromSeconds(2), "cursor back to A after Up")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var pos = state.Document.OffsetToPosition(state.Cursor.Position);
        Assert.Equal(1, pos.Line);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task DownArrow_MovesCursorToNextLine()
    {
        // NOTE: Down arrow at last line may gain "create new line" option.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("AAA\nBBB\nCCC");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        var cursorOnLine1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 1
                      && ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Down()
            .WaitUntil(s => s.SearchPattern(cursorOnLine1).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on line 1 after Down")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var pos = state.Document.OffsetToPosition(state.Cursor.Position);
        Assert.Equal(2, pos.Line);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task HomeKey_MovesCursorToLineStart()
    {
        // NOTE: Smart-home (first non-whitespace) may be added.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Move right via End key, then Home back
        var cursorAtEnd = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        var cursorAtCol0 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .End()
            .WaitUntil(s => s.SearchPattern(cursorAtEnd).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at end of line")
            .Home()
            .WaitUntil(s => s.SearchPattern(cursorAtCol0).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at col 0 after Home")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(0), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task EndKey_MovesCursorToLineEnd()
    {
        // NOTE: End key behavior may change with trailing whitespace handling.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        var cursorAtCol5 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ctx.Cell.Character == " "
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .End()
            .WaitUntil(s => s.SearchPattern(cursorAtCol5).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at end of line after End")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(5), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task CtrlHome_MovesCursorToDocumentStart()
    {
        // NOTE: Ctrl+Home may gain scroll animation.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("AAA\nBBB\nCCC");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // First move to end via Ctrl+End, then test Ctrl+Home
        // After Ctrl+End, cursor is past last char at col 3 on line 3
        var cursorAtDocEnd = new CellPatternSearcher()
            .Find(ctx => ctx.X == 3 && ctx.Y == 2
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        var cursorAtOrigin = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "A"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().End()
            .WaitUntil(s => s.SearchPattern(cursorAtDocEnd).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at document end")
            .Ctrl().Home()
            .WaitUntil(s => s.SearchPattern(cursorAtOrigin).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at (0,0) after Ctrl+Home")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(0), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task CtrlEnd_MovesCursorToDocumentEnd()
    {
        // NOTE: Ctrl+End may gain scroll animation.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("AAA\nBBB\nCCC");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().End()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(new DocumentOffset(11), state.Cursor.Position); // "AAA\nBBB\nCCC" = 11 chars
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task PageDown_MovesCursorDownByViewportLines()
    {
        // NOTE: PageDown may gain half-page option.
        var lines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line{i:D2}"));
        var (workload, terminal, app, state, theme, runTask) = SetupEditor(lines, 40, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        var initialLine = state.Document.OffsetToPosition(state.Cursor.Position).Line;

        await new Hex1bTerminalInputSequenceBuilder()
            .PageDown()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var newLine = state.Document.OffsetToPosition(state.Cursor.Position).Line;
        Assert.True(newLine > initialLine, $"Cursor should move down from line {initialLine}, now at {newLine}");
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task PageUp_MovesCursorUpByViewportLines()
    {
        // NOTE: PageUp may gain half-page option.
        var lines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"Line{i:D2}"));
        var (workload, terminal, app, state, theme, runTask) = SetupEditor(lines, 40, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Use PageDown first to get to a lower position, then test PageUp
        await new Hex1bTerminalInputSequenceBuilder()
            .PageDown()
            .PageDown()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var lineBeforePageUp = state.Document.OffsetToPosition(state.Cursor.Position).Line;

        await new Hex1bTerminalInputSequenceBuilder()
            .PageUp()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var newLine = state.Document.OffsetToPosition(state.Cursor.Position).Line;
        Assert.True(newLine < lineBeforePageUp, $"Cursor should move up from line {lineBeforePageUp}, now at {newLine}");
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task ShiftRight_ExtendsSelection()
    {
        // NOTE: Selection rendering will be tested when visual selection is implemented.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("ABCDE");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Right()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Selection anchor at 0, cursor moved to 1
        Assert.True(state.Cursor.HasSelection);
        Assert.Equal(new DocumentOffset(0), state.Cursor.SelectionStart);
        Assert.Equal(new DocumentOffset(1), state.Cursor.SelectionEnd);
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task CtrlBackspace_DeletesPreviousWord()
    {
        // NOTE: Word deletion boundaries may change with language-aware word detection.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("hello world");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        // Move cursor to end via Ctrl+End, then Ctrl+Backspace
        await WaitForEditor(terminal);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().End()
            .WaitUntil(_ => state.Cursor.Position.Value == state.Document.Length,
                TimeSpan.FromSeconds(2), "cursor at document end")
            .Ctrl().Backspace()
            .WaitUntil(_ => !state.Document.GetText().Contains("world", StringComparison.Ordinal),
                TimeSpan.FromSeconds(2), "previous word deleted")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // "world" deleted (word boundary deletes word + leading space)
        Assert.DoesNotContain("world", state.Document.GetText());
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task CtrlDelete_DeletesNextWord()
    {
        // NOTE: Word deletion boundaries may change with language-aware word detection.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("hello world");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Delete()
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // "hello " deleted (word + trailing space)
        Assert.DoesNotContain("hello", state.Document.GetText());
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task CtrlShiftK_DeletesCurrentLine()
    {
        // NOTE: Line deletion may gain undo grouping in future.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("AAA\nBBB\nCCC");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Ctrl+Shift+K deletes current line (line 1 = "AAA\n")
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Shift().Key(Hex1bKey.K)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // "AAA\n" should be deleted, leaving "BBB\nCCC"
        Assert.DoesNotContain("AAA", state.Document.GetText());
        Assert.Contains("BBB", state.Document.GetText());
        Assert.Contains("CCC", state.Document.GetText());
        await ExitAndWait(terminal, runTask);
    }

    [Fact]
    public async Task AnyCharacter_InsertsText()
    {
        // NOTE: Character insertion may change with IME composition.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        var textFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));

        await WaitForEditor(terminal);

        // Type 'x' — should appear at col 0 with text colors
        var xAtCol0 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "x"
                      && ColorEquals(ctx.Cell.Foreground, textFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("x")
            .WaitUntil(s => s.SearchPattern(xAtCol0).HasMatches,
                TimeSpan.FromSeconds(2), "x typed with text colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.Equal("x", state.Document.GetText());
        Assert.Equal(new DocumentOffset(1), state.Cursor.Position);
        await ExitAndWait(terminal, runTask);
    }
}
