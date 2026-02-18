// NOTE: These tests verify the full Editor widget lifecycle through Hex1bApp:
// widget → reconcile → measure → arrange → render → input → re-render.
// As editor capabilities evolve (e.g., selection highlighting, line numbers,
// status bar), expected screen output may change.
// Tests resolve colors from the theme under test rather than hardcoding values.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorIntegrationTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    [Fact]
    public async Task Editor_RendersInitialContent()
    {
        // NOTE: Initial rendering may change with gutter/line numbers.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorFg = ToCellColor(theme.Get(EditorTheme.CursorForegroundColor));
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for rendered content — verify text AND cursor cell colors
        var pattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(5), "cursor on H with theme colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify content while still in alternate screen
        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("Hello World", snapshot.GetLineTrimmed(0));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Editor_TypeCharacter_AppearsOnScreen()
    {
        // NOTE: Character rendering may change with IME support.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorFg = ToCellColor(theme.Get(EditorTheme.CursorForegroundColor));
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for editor to render, then type 'a'
        // After typing 'a', cell(0,0) should be 'a' with text colors,
        // cursor should move to cell(1,0) with cursor colors.
        var cursorAtCol1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 1 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("a")
            .WaitUntil(s => s.SearchPattern(cursorAtCol1).HasMatches,
                TimeSpan.FromSeconds(5), "cursor moved to col 1 after typing 'a'")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("a", snapshot.GetCell(0, 0).Character);
    }

    [Fact]
    public async Task Editor_TypeMultipleCharacters_AllVisibleWithCursorAtEnd()
    {
        // NOTE: Multi-char rendering may change with ligature support.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Type "hello" — cursor should be at col 5
        var cursorAtCol5 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("hello")
            .WaitUntil(s => s.SearchPattern(cursorAtCol5).HasMatches,
                TimeSpan.FromSeconds(5), "cursor at col 5 after typing hello")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("h", snapshot.GetCell(0, 0).Character);
        Assert.Equal("e", snapshot.GetCell(1, 0).Character);
        Assert.Equal("l", snapshot.GetCell(2, 0).Character);
        Assert.Equal("l", snapshot.GetCell(3, 0).Character);
        Assert.Equal("o", snapshot.GetCell(4, 0).Character);
    }

    [Fact]
    public async Task Editor_Backspace_DeletesPreviousChar()
    {
        // NOTE: Backspace behavior may change with auto-indent unindent.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Type "ab", then Backspace → "a" with cursor at col 1
        var cursorOnA = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0 && ctx.Cell.Character == "a");
        var cursorAtCol1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 1 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("ab")
            .WaitUntil(s => s.SearchPattern(cursorOnA).HasMatches,
                TimeSpan.FromSeconds(5), "a and b typed")
            .Backspace()
            .WaitUntil(s => s.SearchPattern(cursorAtCol1).HasMatches
                         && s.GetCell(0, 0).Character == "a"
                         && s.GetCell(1, 0).Character != "b",
                TimeSpan.FromSeconds(5), "b deleted, cursor back to col 1")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("a", doc.GetText());
    }

    [Fact]
    public async Task Editor_Enter_InsertsNewline()
    {
        // NOTE: Enter behavior may change with auto-indent.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("AB");
        var state = new EditorState(doc);

        // Move cursor between A and B
        state.MoveCursor(CursorDirection.Right);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // After Enter: line 0 = "A", line 1 = "B"
        var bOnLine1 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 1 && ctx.Cell.Character == "B");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Enter()
            .WaitUntil(s => s.SearchPattern(bOnLine1).HasMatches,
                TimeSpan.FromSeconds(5), "B moved to line 1 after Enter")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("A\nB", doc.GetText());
    }

    [Fact]
    public async Task Editor_ArrowKeys_MoveCursorVisually()
    {
        // NOTE: Cursor position may include virtual space in future.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("ABCDE");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor starts at col 0. Right arrow → col 1 (cursor on 'B').
        var cursorOnB = new CellPatternSearcher()
            .Find(ctx => ctx.X == 1 && ctx.Y == 0
                      && ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Right()
            .WaitUntil(s => s.SearchPattern(cursorOnB).HasMatches,
                TimeSpan.FromSeconds(5), "cursor moved to B at col 1")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Editor_HomeEnd_MoveCursorToLineStartEnd()
    {
        // NOTE: Home may gain smart-home (first non-whitespace) behavior.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);
        // Start cursor at middle
        state.MoveCursor(CursorDirection.Right);
        state.MoveCursor(CursorDirection.Right);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Press End → cursor at col 5 (past last char)
        var cursorAtEnd = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        // Press Home → cursor at col 0
        var cursorAtHome = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .End()
            .WaitUntil(s => s.SearchPattern(cursorAtEnd).HasMatches,
                TimeSpan.FromSeconds(5), "cursor at end of line")
            .Home()
            .WaitUntil(s => s.SearchPattern(cursorAtHome).HasMatches,
                TimeSpan.FromSeconds(5), "cursor at start of line")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Editor_Tab_Inserts4Spaces()
    {
        // NOTE: Tab behavior may change with tab-to-spaces toggle.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Tab → 4 spaces, cursor at col 4
        var cursorAtCol4 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 4 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Tab()
            .WaitUntil(s => s.SearchPattern(cursorAtCol4).HasMatches,
                TimeSpan.FromSeconds(5), "cursor at col 4 after tab")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("    ", doc.GetText()); // 4 spaces
    }

    [Fact]
    public async Task Editor_CursorFollowsTypedText()
    {
        // NOTE: Cursor tracking may change with multi-cursor.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // After "hello", cursor at col 5
        var cursorAtCol5 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("hello")
            .WaitUntil(s => s.SearchPattern(cursorAtCol5).HasMatches,
                TimeSpan.FromSeconds(5), "cursor follows typed text to col 5")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(new DocumentOffset(5), state.Cursor.Position);
    }

    [Fact]
    public async Task Editor_OnTextChanged_CallbackFires()
    {
        // NOTE: Callback may gain debouncing in future.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);
        var callbackFired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.Editor(state).OnTextChanged(_ => callbackFired.TrySetResult())),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("x")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Wait for callback
        await callbackFired.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Editor_EmptyDocumentEditing_TypeAndVerify()
    {
        // NOTE: Empty document may show placeholder text in future.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));
        var textFg = ToCellColor(theme.Get(EditorTheme.ForegroundColor));

        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Type "X" — cell(0,0) = 'X' with text colors, cursor at col 1 with cursor colors
        var xTyped = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0 && ctx.Cell.Character == "X"
                      && ColorEquals(ctx.Cell.Foreground, textFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Type("X")
            .WaitUntil(s => s.SearchPattern(xTyped).HasMatches,
                TimeSpan.FromSeconds(5), "X typed in empty document")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("X", doc.GetText());
    }

    [Fact]
    public async Task Editor_LongDocumentScrolling_PageDownShowsLaterLines()
    {
        // NOTE: Scroll behavior may gain smooth scrolling.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var lines = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line{i:D3}"));
        var doc = new Hex1bDocument(lines);
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initial state: Line001 visible. After PageDown: later lines visible.
        var initialPattern = new CellPatternSearcher().Find("Line001");
        var scrolledPattern = new CellPatternSearcher().Find("Line01");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(initialPattern).HasMatches,
                TimeSpan.FromSeconds(5), "Line001 visible initially")
            .PageDown()
            .WaitUntil(s => s.SearchPattern(scrolledPattern).HasMatches,
                TimeSpan.FromSeconds(5), "later lines visible after PageDown")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Editor_DeleteForward_RemovesNextChar()
    {
        // NOTE: Delete forward may change with paired bracket deletion.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("ABC");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Cursor at col 0, Delete → removes 'A', now "BC"
        var bAtCol0 = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0 && ctx.Cell.Character == "B");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Delete()
            .WaitUntil(s => s.SearchPattern(bAtCol0).HasMatches,
                TimeSpan.FromSeconds(5), "A deleted, B now at col 0")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("BC", doc.GetText());
    }

    [Fact]
    public async Task Editor_CtrlA_SelectsAll()
    {
        // NOTE: Selection rendering will change when highlighting is implemented.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Ctrl().Key(Hex1bKey.A)
            .WaitUntil(_ => state.Cursor.HasSelection && state.Cursor.SelectionEnd.Value == 11,
                TimeSpan.FromSeconds(5), "select all completed")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify state: selection covers entire document
        Assert.True(state.Cursor.HasSelection);
        Assert.Equal(new DocumentOffset(0), state.Cursor.SelectionStart);
        Assert.Equal(new DocumentOffset(11), state.Cursor.SelectionEnd);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Editor_MultiLinePaste_RendersAcrossLines()
    {
        // NOTE: Paste may gain formatting/indent adjustment in future.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Simulate paste by directly inserting multi-line text
        state.InsertText("Alpha\nBeta\nGamma");
        app.Invalidate();

        var gammaPattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 2 && ctx.Cell.Character == "G");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(gammaPattern).HasMatches,
                TimeSpan.FromSeconds(5), "3 lines rendered after multi-line insert")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Capture snapshot while still in alternate screen
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("A", snapshot.GetCell(0, 0).Character); // Alpha
        Assert.Equal("B", snapshot.GetCell(0, 1).Character); // Beta
        Assert.Equal("G", snapshot.GetCell(0, 2).Character); // Gamma

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task Editor_ReadOnly_TypingDoesNotModifyDocument()
    {
        // NOTE: Read-only may gain visual indicator (dimmed text, lock icon).
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("Original");
        var state = new EditorState(doc) { IsReadOnly = true };

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var originalPattern = new CellPatternSearcher().Find("Original");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(originalPattern).HasMatches,
                TimeSpan.FromSeconds(5), "Original text visible")
            .Type("X")
            .WaitUntil(s => s.SearchPattern(originalPattern).HasMatches,
                TimeSpan.FromSeconds(5), "Original text still visible after typing")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Document should still be "Original"
        Assert.Equal("Original", doc.GetText());

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
    }

    [Fact]
    public async Task Editor_RapidTyping_AllCharsAppear()
    {
        // NOTE: Input buffering may be added for very rapid typing.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(80, 10).Build();

        var theme = Hex1bThemes.Default;
        var doc = new Hex1bDocument("");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var expected = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        var fullTextPattern = new CellPatternSearcher().Find("ABCDEFGHIJ");

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .FastType(expected)
            .WaitUntil(s => s.SearchPattern(fullTextPattern).HasMatches,
                TimeSpan.FromSeconds(3), "rapid-typed text visible")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(expected, doc.GetText());
    }

    [Fact]
    public async Task Editor_DeleteAllContent_EmptyEditorWithCursor()
    {
        // NOTE: Undo may restore deleted content in future.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 10).Build();

        var theme = Hex1bThemes.Default;
        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        var doc = new Hex1bDocument("Hello World");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Select all, then Delete → empty doc with cursor at (0,0)
        var cursorAtOrigin = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5), "editor visible")
            .Ctrl().Key(Hex1bKey.A)
            .Delete()
            .WaitUntil(s => s.SearchPattern(cursorAtOrigin).HasMatches
                         && !s.ContainsText("Hello"),
                TimeSpan.FromSeconds(5), "content deleted, cursor at origin")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("", doc.GetText());
    }
}
