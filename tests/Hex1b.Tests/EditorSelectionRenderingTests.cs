// NOTE: These tests verify selection rendering behavior. As selection rendering evolves
// (e.g., multi-line selection, block selection), expected output may change.
// Tests resolve colors from the theme under test rather than hardcoding color values.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorSelectionRenderingTests
{
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private record ThemeColors(
        Hex1bColor? TextFg, Hex1bColor? TextBg,
        Hex1bColor? CursorFg, Hex1bColor? CursorBg,
        Hex1bColor? SelectionFg, Hex1bColor? SelectionBg);

    private static ThemeColors GetThemeColors(Hex1bTheme theme) => new(
        ToCellColor(theme.Get(EditorTheme.ForegroundColor)),
        ToCellColor(theme.Get(EditorTheme.BackgroundColor)),
        ToCellColor(theme.Get(EditorTheme.CursorForegroundColor)),
        ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor)),
        ToCellColor(theme.Get(EditorTheme.SelectionForegroundColor)),
        ToCellColor(theme.Get(EditorTheme.SelectionBackgroundColor)));

    private static (EditorNode node, Hex1bAppWorkloadAdapter workload, Hex1bTerminal terminal, Hex1bRenderContext context, Hex1bTheme theme) CreateEditor(
        string text, int width, int height, bool focused = true)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = focused };

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .Build();
        var context = new Hex1bRenderContext(workload, theme);

        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));

        return (node, workload, terminal, context, theme);
    }

    // ── Single-line selection ───────────────────────────────────

    [Fact]
    public async Task Render_SelectionOnSingleLine_HighlightsSelectedCells()
    {
        // NOTE: Selection color may change with theme updates.
        var (node, workload, terminal, context, theme) = CreateEditor("Hello World", 20, 3);
        var colors = GetThemeColors(theme);

        // Select "ello" (offset 1..5): anchor at 1, cursor at 5
        node.State.Cursor.SelectionAnchor = new DocumentOffset(1);
        node.State.Cursor.Position = new DocumentOffset(5);

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "e"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg)
                      && ColorEquals(ctx.Cell.Background, colors.SelectionBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "e selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'H' at col 0 should be normal text color
        var hCell = snapshot.GetCell(0, 0);
        Assert.Equal("H", hCell.Character);
        Assert.True(ColorEquals(colors.TextFg, hCell.Foreground),
            $"H fg: expected {colors.TextFg}, got {hCell.Foreground}");

        // 'e' at col 1 should be selection color (start of selection)
        var eCell = snapshot.GetCell(1, 0);
        Assert.Equal("e", eCell.Character);
        Assert.True(ColorEquals(colors.SelectionFg, eCell.Foreground),
            $"e fg: expected {colors.SelectionFg}, got {eCell.Foreground}");
        Assert.True(ColorEquals(colors.SelectionBg, eCell.Background),
            $"e bg: expected {colors.SelectionBg}, got {eCell.Background}");

        // 'l' at col 2 should be selection color
        var lCell = snapshot.GetCell(2, 0);
        Assert.Equal("l", lCell.Character);
        Assert.True(ColorEquals(colors.SelectionFg, lCell.Foreground));
        Assert.True(ColorEquals(colors.SelectionBg, lCell.Background));

        // 'o' at col 4 should be selection color (last selected char)
        var oCell = snapshot.GetCell(4, 0);
        Assert.Equal("o", oCell.Character);
        Assert.True(ColorEquals(colors.SelectionFg, oCell.Foreground));
        Assert.True(ColorEquals(colors.SelectionBg, oCell.Background));

        // ' ' at col 5 should be cursor (cursor position is at end of selection)
        var spaceCell = snapshot.GetCell(5, 0);
        Assert.True(ColorEquals(colors.CursorFg, spaceCell.Foreground),
            $"cursor fg: expected {colors.CursorFg}, got {spaceCell.Foreground}");
        Assert.True(ColorEquals(colors.CursorBg, spaceCell.Background),
            $"cursor bg: expected {colors.CursorBg}, got {spaceCell.Background}");

        // 'W' at col 6 should be normal
        var wCell = snapshot.GetCell(6, 0);
        Assert.Equal("W", wCell.Character);
        Assert.True(ColorEquals(colors.TextFg, wCell.Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_SelectionReversed_SameHighlighting()
    {
        // NOTE: Reverse selection (anchor after cursor) should highlight the same range.
        var (node, workload, terminal, context, theme) = CreateEditor("abcdef", 20, 3);
        var colors = GetThemeColors(theme);

        // Select "bcd" reversed: cursor at 1, anchor at 4
        node.State.Cursor.Position = new DocumentOffset(1);
        node.State.Cursor.SelectionAnchor = new DocumentOffset(4);

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "c"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "c selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'a' at col 0 should be normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(0, 0).Foreground));

        // Cursor at col 1 — cursor takes priority over selection
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(1, 0).Foreground));

        // 'c' at col 2 should be selection
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(2, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionBg, snapshot.GetCell(2, 0).Background));

        // 'd' at col 3 should be selection
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(3, 0).Foreground));

        // 'e' at col 4 should be normal (selection end is exclusive)
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(4, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Multi-line selection ────────────────────────────────────

    [Fact]
    public async Task Render_MultiLineSelection_HighlightsAcrossLines()
    {
        // NOTE: Multi-line selection colors the end of line 1 and start of line 2.
        var (node, workload, terminal, context, theme) = CreateEditor("abc\ndef\nghi", 20, 5);
        var colors = GetThemeColors(theme);

        // Select from offset 1 ("bc\nd") to offset 5: anchor=1, cursor=5
        node.State.Cursor.SelectionAnchor = new DocumentOffset(1);
        node.State.Cursor.Position = new DocumentOffset(5);

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "b"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "b selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 1 (row 0): "abc" — 'a' normal, 'b','c' selected
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(0, 0).Foreground)); // 'a' normal
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(1, 0).Foreground)); // 'b' selected
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(2, 0).Foreground)); // 'c' selected

        // Line 2 (row 1): "def" — 'd' selected, cursor at 'e' (offset 5 = line 2 col 2)
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(0, 1).Foreground)); // 'd' selected
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(1, 1).Foreground)); // 'e' = cursor

        // 'f' at col 2 should be normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(2, 1).Foreground)); // 'f' normal

        // Line 3 (row 2): "ghi" — all normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(0, 2).Foreground)); // 'g' normal

        workload.Dispose();
        terminal.Dispose();
    }

    // ── No selection when unfocused ─────────────────────────────

    [Fact]
    public async Task Render_Unfocused_NoSelectionHighlighting()
    {
        // NOTE: Unfocused editors should not show selection or cursor highlighting.
        var (node, workload, terminal, context, theme) = CreateEditor("Hello", 20, 3, focused: false);
        var colors = GetThemeColors(theme);

        node.State.Cursor.SelectionAnchor = new DocumentOffset(0);
        node.State.Cursor.Position = new DocumentOffset(5);

        node.Render(context);

        // Just wait for render to flush
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello"),
                TimeSpan.FromSeconds(2), "content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // All cells should be normal text color — no selection, no cursor
        for (var col = 0; col < 5; col++)
        {
            Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(col, 0).Foreground),
                $"Col {col}: expected normal fg, got {snapshot.GetCell(col, 0).Foreground}");
        }

        workload.Dispose();
        terminal.Dispose();
    }

    // ── SelectAll rendering ─────────────────────────────────────

    [Fact]
    public async Task Render_SelectAll_HighlightsEntireDocument()
    {
        // NOTE: SelectAll should highlight all text, cursor at end.
        var (node, workload, terminal, context, theme) = CreateEditor("abc", 20, 3);
        var colors = GetThemeColors(theme);

        node.State.SelectAll();

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "a"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "a selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // All 3 chars selected
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(0, 0).Foreground)); // 'a'
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(1, 0).Foreground)); // 'b'
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(2, 0).Foreground)); // 'c'

        // Cursor at col 3 (after text)
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(3, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Multi-cursor selection ──────────────────────────────────

    [Fact]
    public async Task Render_MultiCursorWithSelection_HighlightsBothSelections()
    {
        // NOTE: Multiple cursors each with their own selection should all render.
        var (node, workload, terminal, context, theme) = CreateEditor("aaa bbb ccc", 20, 3);
        var colors = GetThemeColors(theme);

        // Cursor 0 selects "aaa" (0..3)
        node.State.Cursor.SelectionAnchor = new DocumentOffset(0);
        node.State.Cursor.Position = new DocumentOffset(3);

        // Cursor 1 selects "ccc" (8..11)
        node.State.Cursors.Add(new DocumentOffset(11), new DocumentOffset(8));

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 8
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "second selection")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // "aaa" selected (cols 0,1,2)
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(0, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(1, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(2, 0).Foreground));

        // Cursor at col 3 (end of "aaa")
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(3, 0).Foreground));

        // " bbb " normal (cols 4..7)
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(4, 0).Foreground));
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(5, 0).Foreground));

        // "ccc" selected (cols 8,9,10)
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(8, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(9, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(10, 0).Foreground));

        // Cursor at col 11 (end of "ccc")
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(11, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Cursor overrides selection ──────────────────────────────

    [Fact]
    public async Task Render_CursorInsideSelection_CursorTakesPriority()
    {
        // NOTE: When cursor is within a selected range, cursor color should win.
        var (node, workload, terminal, context, theme) = CreateEditor("abcde", 20, 3);
        var colors = GetThemeColors(theme);

        // Select range 2..5 with cursor at 2, anchor at 5
        node.State.Cursor.Position = new DocumentOffset(2);
        node.State.Cursor.SelectionAnchor = new DocumentOffset(5);

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "d"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "d selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'a' at col 0 — normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(0, 0).Foreground));

        // 'b' at col 1 — normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(1, 0).Foreground));

        // 'c' at col 2 — cursor (overrides selection)
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(2, 0).Foreground));
        Assert.True(ColorEquals(colors.CursorBg, snapshot.GetCell(2, 0).Background));

        // 'd' at col 3 — selection
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(3, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionBg, snapshot.GetCell(3, 0).Background));

        // 'e' at col 4 — selection
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(4, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Empty selection (no highlight) ──────────────────────────

    [Fact]
    public async Task Render_NoSelection_NoHighlighting()
    {
        // NOTE: Without any selection, only cursor should be highlighted.
        var (node, workload, terminal, context, theme) = CreateEditor("Hello", 20, 3);
        var colors = GetThemeColors(theme);

        // Cursor at offset 2, no selection
        node.State.Cursor.Position = new DocumentOffset(2);

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "l"
                      && ColorEquals(ctx.Cell.Foreground, colors.CursorFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on l")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'H' and 'e' normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(0, 0).Foreground));
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(1, 0).Foreground));

        // 'l' at col 2 — cursor
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(2, 0).Foreground));

        // 'l' at col 3, 'o' at col 4 — normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(3, 0).Foreground));
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(4, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Selection on padded area ────────────────────────────────

    [Fact]
    public async Task Render_SelectionDoesNotExtendBeyondText()
    {
        // NOTE: Selection should not highlight the padding area beyond actual text.
        var (node, workload, terminal, context, theme) = CreateEditor("ab", 10, 3);
        var colors = GetThemeColors(theme);

        // Select all: anchor=0, cursor=2
        node.State.SelectAll();

        node.Render(context);

        var selPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "a"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selPattern).HasMatches,
                TimeSpan.FromSeconds(2), "a selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'a' and 'b' selected (cols 0,1)
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(0, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(1, 0).Foreground));

        // Cursor at col 2
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(2, 0).Foreground));

        // Padding beyond text (col 3+) should be normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(3, 0).Foreground));
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(4, 0).Foreground));

        workload.Dispose();
        terminal.Dispose();
    }

    // ── Shift+Right integration test ────────────────────────────

    [Fact]
    public async Task Integration_ShiftRight_CreatesVisibleSelection()
    {
        // NOTE: This integration test verifies that Shift+Right through the app
        // creates a visible selection with correct colors.
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();

        var theme = Hex1bThemes.Default;
        var colors = GetThemeColors(theme);

        var doc = new Hex1bDocument("Hello");
        var state = new EditorState(doc);

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(ctx.Editor(state)),
            new Hex1bAppOptions { WorkloadAdapter = workload, Theme = theme });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Wait for initial render
        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Foreground, colors.CursorFg)
                      && ColorEquals(ctx.Cell.Background, colors.CursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on H")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Press Shift+Right 3 times to select "Hel"
        var selectionPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "H"
                      && ColorEquals(ctx.Cell.Foreground, colors.SelectionFg)
                      && ColorEquals(ctx.Cell.Background, colors.SelectionBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.SearchPattern(selectionPattern).HasMatches,
                TimeSpan.FromSeconds(2), "H selected")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'H','e','l' should be selected (cols 0,1,2)
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(0, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(1, 0).Foreground));
        Assert.True(ColorEquals(colors.SelectionFg, snapshot.GetCell(2, 0).Foreground));

        // Cursor at col 3 ('l')
        Assert.True(ColorEquals(colors.CursorFg, snapshot.GetCell(3, 0).Foreground));

        // 'o' at col 4 should be normal
        Assert.True(ColorEquals(colors.TextFg, snapshot.GetCell(4, 0).Foreground));

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;
    }

    [Fact]
    public async Task SelectAll_WithTextBeyondViewport_DoesNotCrash()
    {
        // Regression: selecting text longer than the viewport caused IndexOutOfRangeException
        // in BuildCellTypes because selection range extended beyond the display width array.
        var longText = "This is a long line that exceeds the viewport width easily";
        var (node, workload, terminal, context, theme) = CreateEditor(longText, 20, 5);
        var colors = GetThemeColors(theme);

        // Select all (selection spans full document, but display is only 20 chars wide)
        node.State.SelectAll();

        // This must not throw IndexOutOfRangeException
        node.Render(context);

        // Wait for selection to appear on visible cells
        var selVisible = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Background, colors.SelectionBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(selVisible).HasMatches,
                TimeSpan.FromSeconds(2), "selection visible on overflow line")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // All visible cells on the text line should have selection or cursor colors
        var snapshot = terminal.CreateSnapshot();
        for (int col = 0; col < 20; col++)
        {
            var cell = snapshot.GetCell(col, 0);
            Assert.True(
                ColorEquals(colors.SelectionBg, cell.Background) || ColorEquals(colors.CursorBg, cell.Background),
                $"Cell ({col},0) should be selected or cursor, got bg={cell.Background}");
        }
    }
}
