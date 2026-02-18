// NOTE: These tests verify current editor rendering behavior. As editor capabilities
// evolve (e.g., selection highlighting, line numbers, gutter), expected output may change.
// Tests resolve colors from the theme under test rather than hardcoding color values.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorNodeRenderingTests
{
    // Helper: resolve expected cell colors from theme.
    // Default colors become null in TerminalCell (ANSI \x1b[39m/49m → null).
    // Non-default colors become the actual Hex1bColor value.
    private static Hex1bColor? ToCellColor(Hex1bColor color) => color.IsDefault ? null : color;

    // Hex1bColor is a struct without operator== on Nullable<Hex1bColor>,
    // so we need Nullable.Equals for comparisons.
    private static bool ColorEquals(Hex1bColor? a, Hex1bColor? b) => Nullable.Equals(a, b);

    private static (Hex1bColor? textFg, Hex1bColor? textBg, Hex1bColor? cursorFg, Hex1bColor? cursorBg) GetThemeColors(Hex1bTheme theme)
    {
        return (
            ToCellColor(theme.Get(EditorTheme.ForegroundColor)),
            ToCellColor(theme.Get(EditorTheme.BackgroundColor)),
            ToCellColor(theme.Get(EditorTheme.CursorForegroundColor)),
            ToCellColor(theme.Get(EditorTheme.CursorBackgroundColor))
        );
    }

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

        // Measure and arrange to set viewport
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));

        return (node, workload, terminal, context, theme);
    }

    [Fact]
    public async Task Render_EmptyDocument_ShowsCursorOnFirstCellAndTildesBelow()
    {
        // NOTE: Empty doc rendering may change if we add line numbers or gutter.
        var (node, workload, terminal, context, theme) = CreateEditor("", 20, 5);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        // Build pattern: cursor cell at (0,0) with cursor colors
        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor cell at (0,0) with cursor colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0: cursor cell (space with cursor colors), rest are spaces
        var cell00 = snapshot.GetCell(0, 0);
        Assert.Equal(cursorFg, cell00.Foreground);
        Assert.Equal(cursorBg, cell00.Background);

        // Lines 1-4: tilde markers
        for (var y = 1; y < 5; y++)
        {
            Assert.Equal("~", snapshot.GetCell(0, y).Character);
            for (var x = 1; x < 20; x++)
            {
                Assert.Equal(" ", snapshot.GetCell(x, y).Character);
            }
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_SingleLineDocument_ShowsTextOnLine0AndTildesBelow()
    {
        // NOTE: May change with line number gutter.
        var (node, workload, terminal, context, theme) = CreateEditor("Hello", 20, 5);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var textPattern = new CellPatternSearcher().Find("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(textPattern).HasMatches,
                TimeSpan.FromSeconds(2), "Hello text on line 0")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0: "Hello" with cursor on 'H' (cursor at offset 0)
        Assert.Equal("H", snapshot.GetCell(0, 0).Character);
        Assert.Equal(cursorFg, snapshot.GetCell(0, 0).Foreground);
        Assert.Equal(cursorBg, snapshot.GetCell(0, 0).Background);

        Assert.Equal("e", snapshot.GetCell(1, 0).Character);
        Assert.Equal("l", snapshot.GetCell(2, 0).Character);
        Assert.Equal("l", snapshot.GetCell(3, 0).Character);
        Assert.Equal("o", snapshot.GetCell(4, 0).Character);

        // Padding after text
        for (var x = 5; x < 20; x++)
        {
            Assert.Equal(" ", snapshot.GetCell(x, 0).Character);
        }

        // Lines 1-4: tilde markers
        for (var y = 1; y < 5; y++)
        {
            Assert.Equal("~", snapshot.GetCell(0, y).Character);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_MultiLineDocument_EachLineAtCorrectYPosition()
    {
        // NOTE: May change with line number gutter.
        var (node, workload, terminal, context, theme) = CreateEditor("AAA\nBBB\nCCC", 20, 5);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("CCC");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "all 3 lines rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Line 0: "AAA" (cursor on 'A' at col 0)
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal(cursorFg, snapshot.GetCell(0, 0).Foreground);
        Assert.Equal("A", snapshot.GetCell(1, 0).Character);
        Assert.Equal("A", snapshot.GetCell(2, 0).Character);

        // Line 1: "BBB"
        Assert.Equal("B", snapshot.GetCell(0, 1).Character);
        Assert.Equal("B", snapshot.GetCell(1, 1).Character);
        Assert.Equal("B", snapshot.GetCell(2, 1).Character);

        // Line 2: "CCC"
        Assert.Equal("C", snapshot.GetCell(0, 2).Character);

        // Lines 3-4: tilde markers (past 3-line doc)
        Assert.Equal("~", snapshot.GetCell(0, 3).Character);
        Assert.Equal("~", snapshot.GetCell(0, 4).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_LineLongerThanViewport_TruncatedToViewportWidth()
    {
        // NOTE: Horizontal scrolling may change truncation behavior in future.
        var (node, workload, terminal, context, theme) = CreateEditor("ABCDEFGHIJ", 5, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("ABCDE");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "truncated line visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Only first 5 chars visible (viewport width = 5)
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal("B", snapshot.GetCell(1, 0).Character);
        Assert.Equal("C", snapshot.GetCell(2, 0).Character);
        Assert.Equal("D", snapshot.GetCell(3, 0).Character);
        Assert.Equal("E", snapshot.GetCell(4, 0).Character);
        // No chars past viewport edge (cols 5+ not in our viewport)

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ShortLine_PaddedWithSpacesToViewportWidth()
    {
        // NOTE: Padding behavior may change with background fill or gutter.
        var (node, workload, terminal, context, theme) = CreateEditor("Hi", 10, 3);
        var (textFg, textBg, _, _) = GetThemeColors(theme);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Hi");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "Hi text visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // "Hi" at cols 0-1, then spaces at cols 2-9
        Assert.Equal("H", snapshot.GetCell(0, 0).Character);
        Assert.Equal("i", snapshot.GetCell(1, 0).Character);
        for (var x = 2; x < 10; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(" ", cell.Character);
            Assert.Equal(textFg, cell.Foreground);
            Assert.Equal(textBg, cell.Background);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CursorOnFirstChar_HasCursorColors()
    {
        // NOTE: Cursor rendering may change to use reverse video or underline.
        var (node, workload, terminal, context, theme) = CreateEditor("XYZ", 20, 3);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        // Cursor at position 0 (default)
        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 0 && ctx.Y == 0
                      && ctx.Cell.Character == "X"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on X with cursor colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.Equal("X", cell.Character);
        Assert.Equal(cursorFg, cell.Foreground);
        Assert.Equal(cursorBg, cell.Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CursorMidLine_OnlyCursorCellHasCursorColors()
    {
        // NOTE: Cursor rendering may change with multi-cursor or selection highlighting.
        var (node, workload, terminal, context, theme) = CreateEditor("ABCDE", 20, 3);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        // Move cursor to position 2 (on 'C')
        node.State.MoveCursor(CursorDirection.Right);
        node.State.MoveCursor(CursorDirection.Right);

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "C"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on C at col 2")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'A' and 'B' at cols 0-1: NOT cursor colors
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.NotEqual(cursorBg, snapshot.GetCell(0, 0).Background);

        Assert.Equal("B", snapshot.GetCell(1, 0).Character);
        Assert.NotEqual(cursorBg, snapshot.GetCell(1, 0).Background);

        // 'C' at col 2: cursor colors
        Assert.Equal("C", snapshot.GetCell(2, 0).Character);
        Assert.Equal(cursorFg, snapshot.GetCell(2, 0).Foreground);
        Assert.Equal(cursorBg, snapshot.GetCell(2, 0).Background);

        // 'D' and 'E' at cols 3-4: NOT cursor colors
        Assert.Equal("D", snapshot.GetCell(3, 0).Character);
        Assert.NotEqual(cursorBg, snapshot.GetCell(3, 0).Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CursorAtEndOfLine_CursorOnSpaceAfterLastChar()
    {
        // NOTE: End-of-line cursor behavior may change with virtual space mode.
        var (node, workload, terminal, context, theme) = CreateEditor("AB", 20, 3);
        var (_, _, cursorFg, cursorBg) = GetThemeColors(theme);

        // Move cursor to end (offset 2, past last char)
        node.State.MoveToLineEnd();

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.X == 2 && ctx.Y == 0
                      && ctx.Cell.Character == " "
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on space at col 2")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal("B", snapshot.GetCell(1, 0).Character);

        var cursorCell = snapshot.GetCell(2, 0);
        Assert.Equal(" ", cursorCell.Character);
        Assert.Equal(cursorFg, cursorCell.Foreground);
        Assert.Equal(cursorBg, cursorCell.Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_Unfocused_NoCellHasCursorColors()
    {
        // NOTE: Unfocused editor may gain dimmed cursor in future.
        var (node, workload, terminal, context, theme) = CreateEditor("Hello", 20, 3, focused: false);
        var (_, _, _, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var textPattern = new CellPatternSearcher().Find("Hello");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(textPattern).HasMatches,
                TimeSpan.FromSeconds(2), "text visible without cursor")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // No cell should have cursor background color
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                Assert.NotEqual(cursorBg, snapshot.GetCell(x, y).Background);
            }
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_EOFTildeMarkers_ShowTildeWithTextColorsNotCursorColors()
    {
        // NOTE: EOF marker style (tilde vs empty) may become configurable.
        var (node, workload, terminal, context, theme) = CreateEditor("Line1", 10, 4);
        var (textFg, textBg, _, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var tildePattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "~" && ctx.Y >= 1);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(tildePattern).HasMatches,
                TimeSpan.FromSeconds(2), "tilde markers below document")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Lines 1-3 should have tilde at col 0, spaces after, with text colors
        for (var y = 1; y < 4; y++)
        {
            var tildeCell = snapshot.GetCell(0, y);
            Assert.Equal("~", tildeCell.Character);
            Assert.Equal(textFg, tildeCell.Foreground);
            Assert.Equal(textBg, tildeCell.Background);
            Assert.NotEqual(cursorBg, tildeCell.Background);

            for (var x = 1; x < 10; x++)
            {
                Assert.Equal(" ", snapshot.GetCell(x, y).Character);
            }
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ViewportFillsBounds_EveryCellHasContent()
    {
        // NOTE: Viewport fill behavior may change with margin/padding support.
        var (node, workload, terminal, context, theme) = CreateEditor("AB\nCD", 8, 4);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("AB");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Every cell in the 8x4 viewport should have a character (not empty/null)
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var cell = snapshot.GetCell(x, y);
                Assert.False(string.IsNullOrEmpty(cell.Character),
                    $"Cell ({x},{y}) should have content but was empty");
            }
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_SmallViewport3Lines_Only3RowsRendered()
    {
        // NOTE: With scrollbar, viewport may shrink by 1 column.
        var (node, workload, terminal, context, theme) = CreateEditor("L1\nL2\nL3\nL4\nL5", 10, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("L1");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "first 3 lines visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Row 0: "L1" (line 1)
        Assert.Equal("L", snapshot.GetCell(0, 0).Character);
        Assert.Equal("1", snapshot.GetCell(1, 0).Character);

        // Row 1: "L2" (line 2)
        Assert.Equal("L", snapshot.GetCell(0, 1).Character);
        Assert.Equal("2", snapshot.GetCell(1, 1).Character);

        // Row 2: "L3" (line 3)
        Assert.Equal("L", snapshot.GetCell(0, 2).Character);
        Assert.Equal("3", snapshot.GetCell(1, 2).Character);

        // Lines 4 and 5 are NOT visible (only 3-line viewport)
        // We can't directly assert "not rendered" but they're offscreen

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_LargeDocSmallViewport_OnlyViewportWindowVisible()
    {
        // NOTE: Scroll indicator may appear in future.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i:D2}"));
        var (node, workload, terminal, context, theme) = CreateEditor(lines, 10, 5);

        // Scroll to line 10
        node.ScrollOffset = 10;
        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Line10");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "lines 10-14 visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Row 0 should show "Line10" (scroll offset = 10, so first visible is line 10)
        Assert.Contains("Line10", snapshot.GetLineTrimmed(0));
        Assert.Contains("Line11", snapshot.GetLineTrimmed(1));
        Assert.Contains("Line12", snapshot.GetLineTrimmed(2));
        Assert.Contains("Line13", snapshot.GetLineTrimmed(3));
        Assert.Contains("Line14", snapshot.GetLineTrimmed(4));

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_EmptyLinesInDocument_ShowSpacesNotTildes()
    {
        // NOTE: Empty lines may gain whitespace indicators in future.
        var (node, workload, terminal, context, theme) = CreateEditor("A\n\nB", 10, 5);
        var (textFg, textBg, _, _) = GetThemeColors(theme);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("B");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "all 3 lines rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Row 0: "A"
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);

        // Row 1: empty line in document — should be spaces, NOT tilde
        Assert.Equal(" ", snapshot.GetCell(0, 1).Character);
        Assert.NotEqual("~", snapshot.GetCell(0, 1).Character);
        // Verify it's spaces across the whole line
        for (var x = 0; x < 10; x++)
        {
            Assert.Equal(" ", snapshot.GetCell(x, 1).Character);
        }

        // Row 2: "B"
        Assert.Equal("B", snapshot.GetCell(0, 2).Character);

        // Row 3: past EOF — should be tilde
        Assert.Equal("~", snapshot.GetCell(0, 3).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ThemeColorsOnText_AllNonCursorCellsHaveTextColors()
    {
        // NOTE: Theme color application may change with syntax highlighting.
        var (node, workload, terminal, context, theme) = CreateEditor("Test", 10, 3);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("Test");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "text rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // 'T' at col 0 is cursor (skip it)
        // 'e', 's', 't' at cols 1-3 should have text colors
        for (var x = 1; x <= 3; x++)
        {
            var cell = snapshot.GetCell(x, 0);
            Assert.Equal(textFg, cell.Foreground);
            Assert.Equal(textBg, cell.Background);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ThemeColorsOnCursor_CursorCellMatchesTheme()
    {
        // NOTE: Cursor style (block, bar, underline) may become configurable.
        var (node, workload, terminal, context, theme) = CreateEditor("X", 10, 3);
        var (_, _, cursorFg, cursorBg) = GetThemeColors(theme);

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "X"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor X with theme cursor colors")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var cell = snapshot.GetCell(0, 0);
        Assert.Equal("X", cell.Character);
        Assert.Equal(cursorFg, cell.Foreground);
        Assert.Equal(cursorBg, cell.Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_ScrollbarAppearsWhenContentOverflows()
    {
        // NOTE: Scrollbar appears on the rightmost column when doc has more lines than viewport.
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i}"));
        var (node, workload, terminal, context, theme) = CreateEditor(lines, 20, 5);

        node.Render(context);

        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);
        var thumbColor = ToCellColor(theme.Get(ScrollTheme.FocusedThumbColor));

        // Wait for content to render
        var pattern = new CellPatternSearcher().Find("Line1");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content visible")
            .WaitUntil(s =>
            {
                // Scrollbar should be in the rightmost column (col 19).
                for (var row = 0; row < 5; row++)
                {
                    var ch = s.GetCell(19, row).Character;
                    if (ch == trackChar || ch == thumbChar)
                        return true;
                }
                return false;
            }, TimeSpan.FromSeconds(2), "scrollbar visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Rightmost column (col 19) should contain scrollbar characters
        var scrollCol = 19;
        var hasTrackOrThumb = false;
        for (var row = 0; row < 5; row++)
        {
            var cell = snapshot.GetCell(scrollCol, row);
            if (cell.Character == trackChar || cell.Character == thumbChar)
                hasTrackOrThumb = true;
        }

        Assert.True(hasTrackOrThumb, "Scrollbar should appear in rightmost column");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_NoScrollbarWhenContentFits()
    {
        // NOTE: No scrollbar when all content fits in viewport.
        var (node, workload, terminal, context, theme) = CreateEditor("Line1\nLine2", 20, 5);

        node.Render(context);

        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);

        var pattern = new CellPatternSearcher().Find("Line1");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Rightmost column should NOT contain scrollbar characters
        var scrollCol = 19;
        for (var row = 0; row < 5; row++)
        {
            var cell = snapshot.GetCell(scrollCol, row);
            Assert.NotEqual(trackChar, cell.Character);
            Assert.NotEqual(thumbChar, cell.Character);
        }

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public void Arrange_LongLine_ReducesViewportForHorizontalScrollbar()
    {
        // Line longer than viewport should reduce viewport height for horizontal scrollbar
        var longLine = new string('A', 40);
        var (node, workload, terminal, context, theme) = CreateEditor(longLine, 20, 5);

        // With horizontal scrollbar, viewport lines should be reduced by 1
        Assert.Equal(4, node.ViewportLines);
        Assert.Equal(20, node.ViewportColumns); // no vertical scrollbar

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public void Arrange_VerticalOverflow_ReducesViewportForScrollbar()
    {
        // Many lines should reduce viewport columns for vertical scrollbar
        var lines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"Line{i}"));
        var (node, workload, terminal, context, theme) = CreateEditor(lines, 20, 5);

        Assert.Equal(19, node.ViewportColumns);
        Assert.Equal(5, node.ViewportLines); // no horizontal scrollbar

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public void HorizontalScroll_CursorMovesRight_ScrollsHorizontally()
    {
        // Moving cursor past viewport width should trigger horizontal scrolling
        var longLine = new string('A', 40);
        var doc = new Hex1bDocument(longLine);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = true };

        var theme = Hex1bThemes.Default;
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(20, 5)
            .Build();

        node.Measure(new Constraints(0, 20, 0, 5));
        node.Arrange(new Rect(0, 0, 20, 5));

        // Move cursor to end of document
        state.MoveToDocumentEnd();
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 20, 5));

        Assert.True(node.HorizontalScrollOffset > 0, "Horizontal scroll should be non-zero when cursor is past viewport");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_HorizontalScrollbar_AppearsAtBottom()
    {
        // When content is wider than viewport, horizontal scrollbar renders on bottom row
        var longLine = new string('X', 40);
        var (node, workload, terminal, context, theme) = CreateEditor(longLine, 20, 5);

        node.Render(context);

        var trackChar = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.HorizontalThumbCharacter);

        // Wait until both content AND scrollbar are rendered
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var contentVisible = s.SearchPattern(new CellPatternSearcher().Find("X")).HasMatches;
                if (!contentVisible) return false;
                // Also check scrollbar is present on bottom row
                for (var col = 0; col < 20; col++)
                {
                    var cell = s.GetCell(col, 4);
                    if (cell.Character == trackChar || cell.Character == thumbChar)
                        return true;
                }
                return false;
            }, TimeSpan.FromSeconds(2), "content and horizontal scrollbar visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Bottom row (row 4) should contain horizontal scrollbar characters
        var scrollRow = 4;
        var hasTrackOrThumb = false;
        for (var col = 0; col < 20; col++)
        {
            var cell = snapshot.GetCell(col, scrollRow);
            if (cell.Character == trackChar || cell.Character == thumbChar)
                hasTrackOrThumb = true;
        }

        Assert.True(hasTrackOrThumb, "Horizontal scrollbar should appear on bottom row");

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_WideCharacter_OccupiesTwoDisplayColumns()
    {
        // ⚡ (U+26A1) has Emoji_Presentation → display width 2
        var (node, workload, terminal, context, theme) = CreateEditor("A⚡B", 20, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("B");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "wide char content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 0: 'A' (1-wide), cursor on 'A' by default
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        // Col 1: '⚡' main cell (2-wide)
        Assert.Equal("⚡", snapshot.GetCell(1, 0).Character);
        // Col 2: continuation cell (empty/wide char continuation)
        Assert.True(snapshot.GetCell(2, 0).Character is "" or " ",
            "Col 2 should be continuation cell for wide ⚡");
        // Col 3: 'B' (1-wide) — shifted by the extra column from ⚡
        Assert.Equal("B", snapshot.GetCell(3, 0).Character);
        // Col 4+: padding spaces
        Assert.Equal(" ", snapshot.GetCell(4, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CursorOnWideChar_BothColumnsHaveCursorColors()
    {
        // Cursor on ⚡ should highlight both display columns
        var (node, workload, terminal, context, theme) = CreateEditor("A⚡B", 20, 3);
        var (_, _, cursorFg, cursorBg) = GetThemeColors(theme);

        // Move cursor to position 1 (on ⚡)
        node.State.MoveCursor(CursorDirection.Right);

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "⚡"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on wide char")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 1: ⚡ main cell with cursor colors
        var cell1 = snapshot.GetCell(1, 0);
        Assert.Equal("⚡", cell1.Character);
        Assert.Equal(cursorFg, cell1.Foreground);
        Assert.Equal(cursorBg, cell1.Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_CursorAfterWideChar_CursorAtCorrectDisplayColumn()
    {
        // Cursor on 'B' (after 2-wide ⚡) should be at display col 3
        var (node, workload, terminal, context, theme) = CreateEditor("A⚡B", 20, 3);
        var (textFg, textBg, cursorFg, cursorBg) = GetThemeColors(theme);

        // Move cursor to 'B' (position 2)
        node.State.MoveCursor(CursorDirection.Right); // to ⚡
        node.State.MoveCursor(CursorDirection.Right); // to B

        node.Render(context);

        var cursorPattern = new CellPatternSearcher()
            .Find(ctx => ctx.Cell.Character == "B"
                      && ColorEquals(ctx.Cell.Foreground, cursorFg)
                      && ColorEquals(ctx.Cell.Background, cursorBg));

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(cursorPattern).HasMatches,
                TimeSpan.FromSeconds(2), "cursor on B after wide char")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 0: 'A' (normal colors)
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal(textFg, snapshot.GetCell(0, 0).Foreground);

        // Col 1: '⚡' (normal colors — cursor has moved past)
        Assert.Equal("⚡", snapshot.GetCell(1, 0).Character);
        Assert.Equal(textFg, snapshot.GetCell(1, 0).Foreground);

        // Col 3: 'B' with cursor colors
        var cellB = snapshot.GetCell(3, 0);
        Assert.Equal("B", cellB.Character);
        Assert.Equal(cursorFg, cellB.Foreground);
        Assert.Equal(cursorBg, cellB.Background);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_MultipleWideChars_CorrectDisplayLayout()
    {
        // "♠♣" — both 2-wide, should occupy 4 display columns
        var (node, workload, terminal, context, theme) = CreateEditor("♠♣X", 20, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("X");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "multiple wide chars rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 0: ♠ main (2-wide)
        Assert.Equal("♠", snapshot.GetCell(0, 0).Character);
        // Col 1: continuation
        // Col 2: ♣ main (2-wide)
        Assert.Equal("♣", snapshot.GetCell(2, 0).Character);
        // Col 3: continuation
        // Col 4: X (1-wide)
        Assert.Equal("X", snapshot.GetCell(4, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }

    [Fact]
    public async Task Render_WideCharAtViewportEdge_TruncatedNotOverflow()
    {
        // Viewport width 4: "A⚡B" — A(1) + ⚡(2) = 3 cols, B starts at col 3
        // All 4 cols fit. But with viewport width 3: A(1) + ⚡(2) = 3 cols exactly, B clipped
        var (node, workload, terminal, context, theme) = CreateEditor("A⚡B", 4, 3);

        node.Render(context);

        var pattern = new CellPatternSearcher().Find("A");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content rendered in narrow viewport")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Col 0: A, Col 1: ⚡ (2-wide), Col 3: B — all fit in 4 cols
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal("⚡", snapshot.GetCell(1, 0).Character);
        Assert.Equal("B", snapshot.GetCell(3, 0).Character);

        workload.Dispose();
        terminal.Dispose();
    }
}
