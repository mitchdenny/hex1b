// NOTE: These tests verify mouse interactions with the editor widget.
// As mouse behavior evolves (e.g., margin click, minimap click), these tests may need updating.
// Tests use CellPatternSearcher for cell-level assertions and resolve colors from the theme.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class EditorMouseTests
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
                TimeSpan.FromSeconds(5), "editor visible in alt screen")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    // ── Click positioning ────────────────────────────────────────

    [TestMethod]
    public async Task Click_PositionsCursorAtClickLocation()
    {
        // NOTE: Click behavior may change with margin/gutter support.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello world\nLine two");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Click at column 5 ("o" in "Hello"), row 0
        var cursorAtClick = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(5, 0)
            .WaitUntil(s => s.SearchPattern(cursorAtClick).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at click position (5,0)")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Cursor should be at offset 5 (0-based column 5 = 1-based column 6)
        Assert.AreEqual(5, state.Cursor.Position.Value);
    }

    [TestMethod]
    public async Task Click_OnSecondLine_PositionsCursorCorrectly()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello\nWorld\nThird");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Click at column 2 on line 2 (row 1 on screen), "r" in "World"
        var cursorAtClick = new CellPatternSearcher()
            .Find(ctx => ctx.X == 2 && ctx.Y == 1
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 1)
            .WaitUntil(s => s.SearchPattern(cursorAtClick).HasMatches,
                TimeSpan.FromSeconds(2), "cursor at (2,1)")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // "Hello\n" = 6 chars, then column 2 (0-based) = offset 8
        Assert.AreEqual(8, state.Cursor.Position.Value);
    }

    [TestMethod]
    public async Task Click_BeyondLineEnd_ClampsToLineEnd()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hi\nWorld");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Click at column 20 on line 1 (row 0) — well past "Hi"
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(20, 0)
            .WaitUntil(_ => state.Cursor.Position.Value == 2,
                TimeSpan.FromSeconds(2), "cursor clamped to end of 'Hi'")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // "Hi" is 2 chars, so cursor should be at offset 2 (after "Hi")
        Assert.AreEqual(2, state.Cursor.Position.Value);
    }

    [TestMethod]
    public async Task Click_InTildeArea_ClampsToDocumentEnd()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Click on row 5 (well past the single line of text, in ~ area)
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(0, 5)
            .WaitUntil(_ => state.Cursor.Position.Value == 5,
                TimeSpan.FromSeconds(2), "cursor at document end from tilde click")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.AreEqual(5, state.Cursor.Position.Value);
    }

    [TestMethod]
    public async Task Click_ClearsExistingSelection()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello world");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Select all first, then click to clear
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.A)
            .WaitUntil(_ => state.Cursor.HasSelection,
                TimeSpan.FromSeconds(2), "selection exists after Ctrl+A")
            .ClickAt(3, 0)
            .WaitUntil(_ => !state.Cursor.HasSelection && state.Cursor.Position.Value == 3,
                TimeSpan.FromSeconds(2), "selection cleared after click")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsFalse(state.Cursor.HasSelection);
        Assert.AreEqual(3, state.Cursor.Position.Value);
    }

    // ── Double-click word selection ──────────────────────────────

    [TestMethod]
    public async Task DoubleClick_SelectsWord()
    {
        // NOTE: Word boundary logic may evolve for unicode/programming language tokens.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello world");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var selBg = ToCellColor(theme.Get(EditorTheme.SelectionBackgroundColor));

        await WaitForEditor(terminal);

        // Double-click on "world" — send two rapid clicks
        var selectionOnWorld = new CellPatternSearcher()
            .Find(ctx => ctx.X == 6 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, selBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(8, 0) // first click
            .ClickAt(8, 0) // second click (app computes double-click from timing)
            .WaitUntil(s => s.SearchPattern(selectionOnWorld).HasMatches,
                TimeSpan.FromSeconds(2), "word 'world' selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(state.Cursor.HasSelection);
        // "world" is at offsets 6-11
        Assert.AreEqual(6, state.Cursor.SelectionStart!.Value);
        Assert.AreEqual(11, state.Cursor.SelectionEnd!.Value);
    }

    // ── Triple-click line selection ──────────────────────────────

    [TestMethod]
    public async Task TripleClick_SelectsEntireLine()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello\nWorld\nThird");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Triple-click on line 2 ("World") — send three rapid clicks
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(2, 1)
            .ClickAt(2, 1)
            .ClickAt(2, 1)
            .WaitUntil(_ => state.Cursor.HasSelection
                         && state.Cursor.SelectionStart!.Value == 6 // start of "World\n"
                         && state.Cursor.SelectionEnd!.Value == 12,  // start of "Third"
                TimeSpan.FromSeconds(2), "line 'World' selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(state.Cursor.HasSelection);
    }

    // ── Drag selection ───────────────────────────────────────────

    [TestMethod]
    public async Task Drag_CreatesSelection()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello world");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var selBg = ToCellColor(theme.Get(EditorTheme.SelectionBackgroundColor));

        await WaitForEditor(terminal);

        // Drag from column 0 to column 5 on row 0 — should select "Hello"
        var selectionOnHello = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Background, selBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(0, 0, 5, 0)
            .WaitUntil(s => s.SearchPattern(selectionOnHello).HasMatches,
                TimeSpan.FromSeconds(2), "drag selection on 'Hello'")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(0, state.Cursor.SelectionStart!.Value);
        Assert.AreEqual(5, state.Cursor.SelectionEnd!.Value);
    }

    [TestMethod]
    public async Task Drag_AcrossLines_CreatesMultiLineSelection()
    {
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello\nWorld");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Drag from (2,0) in "Hello" to (3,1) in "World"
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(2, 0, 3, 1)
            .WaitUntil(_ => state.Cursor.HasSelection
                         && state.Cursor.SelectionStart!.Value == 2
                         && state.Cursor.SelectionEnd!.Value == 9, // "Hello\n" = 6, then col 3
                TimeSpan.FromSeconds(2), "multi-line drag selection")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(state.Cursor.HasSelection);
    }

    // ── Scroll wheel ─────────────────────────────────────────────

    [TestMethod]
    public async Task ScrollDown_ScrollsViewport()
    {
        // NOTE: This test verifies mouse scroll wheel changes the viewport.
        // As editor scroll behavior evolves, expected output may change.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line {i}"));
        var (workload, terminal, app, state, theme, runTask) = SetupEditor(lines, width: 40, height: 5);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Mouse scroll down: each tick scrolls 3 lines, 2 ticks = 6 lines
        // ScrollOffset goes from 1 to 7, showing "Line 7" at row 0
        var line7AtTop = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ctx.Cell.Character == "7");

        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(2)
            .WaitUntil(s => s.SearchPattern(line7AtTop).HasMatches,
                TimeSpan.FromSeconds(2), "scrolled to Line 7 at top")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public async Task ScrollUp_ScrollsViewportBack()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line {i}"));
        var (workload, terminal, app, state, theme, runTask) = SetupEditor(lines, width: 40, height: 5);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Scroll down, then back up
        var line1Visible = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 0
                      && ctx.Cell.Character == "1"); // "Line 1" has "1" at col 5

        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(3) // scroll down 9 lines
            .ScrollUp(3)   // scroll back up 9 lines
            .WaitUntil(s => s.SearchPattern(line1Visible).HasMatches,
                TimeSpan.FromSeconds(2), "Line 1 visible again after scroll up")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    // ── Ctrl+Click multi-cursor ───────────────────────────────────

    [TestMethod]
    public async Task CtrlClick_AddsSecondCursor()
    {
        // NOTE: Ctrl+Click adds a cursor at the clicked position.
        // This may evolve if we add cursor merging on overlap.
        var (workload, terminal, app, state, theme, runTask) = SetupEditor("Hello world\nLine two");
        using var _ = workload; using var __ = terminal; using var ___ = app;

        var cursorBg = ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor));

        await WaitForEditor(terminal);

        // Ctrl+Click at column 5, row 1 to add a second cursor
        var secondCursor = new CellPatternSearcher()
            .Find(ctx => ctx.X == 5 && ctx.Y == 1
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().ClickAt(5, 1)
            .WaitUntil(s => s.SearchPattern(secondCursor).HasMatches,
                TimeSpan.FromSeconds(2), "second cursor at (5,1)")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Original cursor should still be at (0,0)
        var snapshot = terminal.CreateSnapshot();
        var firstCursorCell = snapshot.GetCell(0, 0);
        Assert.IsTrue(ColorEquals(firstCursorCell.Background, cursorBg), "First cursor should still be visible at (0,0)");

        // State should have 2 cursors
        Assert.AreEqual(2, state.Cursors.Count);
    }

    [TestMethod]
    public void AddCursorAtPosition_AddsAndTogglesCursor()
    {
        // Unit test for the toggle behavior
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);

        // Initial: one cursor at offset 0
        TestSeq.Single(state.Cursors);

        // Add cursor at offset 5
        state.AddCursorAtPosition(new DocumentOffset(5));
        Assert.AreEqual(2, state.Cursors.Count);

        // Add cursor at offset 5 again — should remove it (toggle)
        state.AddCursorAtPosition(new DocumentOffset(5));
        TestSeq.Single(state.Cursors);
    }

    // ── Unit tests for EditorState methods ───────────────────────

    [TestMethod]
    public void SetCursorPosition_SetsCursorAndClearsSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);
        state.SelectAll();
        Assert.IsTrue(state.Cursor.HasSelection);

        state.SetCursorPosition(new DocumentOffset(5));

        Assert.AreEqual(5, state.Cursor.Position.Value);
        Assert.IsFalse(state.Cursor.HasSelection);
    }

    [TestMethod]
    public void SetCursorPosition_WithExtend_CreatesSelection()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);

        state.SetCursorPosition(new DocumentOffset(5), extend: true);

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(0, state.Cursor.SelectionStart!.Value);
        Assert.AreEqual(5, state.Cursor.SelectionEnd!.Value);
    }

    [TestMethod]
    public void SetCursorPosition_ClampsToDocumentBounds()
    {
        var doc = new Hex1bDocument("Hi");
        var state = new EditorState(doc);

        state.SetCursorPosition(new DocumentOffset(100));

        Assert.AreEqual(2, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void SetCursorPosition_CollapsesMultiCursors()
    {
        var doc = new Hex1bDocument("Hello Hello");
        var state = new EditorState(doc);
        // Create multi-cursor via AddCursorAtNextMatch
        state.SelectWordAt(new DocumentOffset(0)); // select first "Hello"
        state.AddCursorAtNextMatch(); // adds cursor at second "Hello"
        Assert.AreEqual(2, state.Cursors.Count);

        state.SetCursorPosition(new DocumentOffset(3));

        TestSeq.Single(state.Cursors);
        Assert.AreEqual(3, state.Cursor.Position.Value);
    }

    [TestMethod]
    public void SelectWordAt_SelectsWordUnderOffset()
    {
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);

        state.SelectWordAt(new DocumentOffset(8)); // in "world"

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(6, state.Cursor.SelectionStart!.Value);
        Assert.AreEqual(11, state.Cursor.SelectionEnd!.Value);
    }

    [TestMethod]
    public void SelectLineAt_SelectsEntireLine()
    {
        var doc = new Hex1bDocument("Hello\nWorld\nThird");
        var state = new EditorState(doc);

        state.SelectLineAt(new DocumentOffset(8)); // in "World"

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(6, state.Cursor.SelectionStart!.Value);  // start of "World\n"
        Assert.AreEqual(12, state.Cursor.SelectionEnd!.Value);   // start of "Third"
    }

    [TestMethod]
    public void SelectLineAt_LastLine_SelectsToDocumentEnd()
    {
        var doc = new Hex1bDocument("Hello\nWorld");
        var state = new EditorState(doc);

        state.SelectLineAt(new DocumentOffset(8)); // in "World" (last line)

        Assert.IsTrue(state.Cursor.HasSelection);
        Assert.AreEqual(6, state.Cursor.SelectionStart!.Value);
        Assert.AreEqual(11, state.Cursor.SelectionEnd!.Value); // doc length
    }

    // ── Editor inside container widgets ──────────────────────────

    [TestMethod]
    public async Task Drag_InsideSplitter_CreatesSelection()
    {
        // Regression: editor drag selection stopped working when editor is inside a splitter.
        var doc = new Hex1bDocument("Hello world");
        var state = new EditorState(doc);

        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(60, 10).Build();

        var theme = Hex1bThemes.Default;

        // Editor inside an HSplitter (left=text widget, right=editor) — like SharedEditor demo
        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.HSplitter(
                    left => [left.Text("Explorer").FillWidth().FillHeight()],
                    right => [right.Editor(state).FillWidth().FillHeight()],
                    leftWidth: 15).FillWidth().FillHeight()),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme, EnableMouse = true });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        using var _ = workload; using var __ = terminal; using var ___ = app;

        await WaitForEditor(terminal);

        // Editor starts at column 18 (15 left + 3 divider), drag within editor area
        // Drag from col 18 to col 23 (5 chars into editor = "Hello")
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(18, 0, 23, 0)
            .WaitUntil(_ => state.Cursor.HasSelection,
                TimeSpan.FromSeconds(2), "drag selection inside splitter")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.IsTrue(state.Cursor.HasSelection, "Editor inside splitter should support drag selection");
    }
}
