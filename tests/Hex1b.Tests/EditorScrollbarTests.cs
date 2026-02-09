// NOTE: These tests verify editor scrollbar behavior including rendering, mouse interaction,
// thumb dragging stability, and the cursor-visibility-vs-scroll-independence invariant.
// Tests use both unit (node-level) and integration (app-level) patterns.

using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

public class EditorScrollbarTests
{
    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>Creates a document with numbered lines for predictable assertions.</summary>
    private static string MakeLines(int count, int width = 6)
    {
        // "L001\n", "L002\n", ... each exactly `width` chars (including newline's visible part)
        return string.Join("\n", Enumerable.Range(1, count).Select(i => $"L{i:D3}" + new string('.', Math.Max(0, width - 4))));
    }

    /// <summary>Creates a document with lines of a specified width for horizontal scroll testing.</summary>
    private static string MakeWideLines(int lineCount, int lineWidth)
    {
        return string.Join("\n", Enumerable.Range(1, lineCount)
            .Select(i =>
            {
                var prefix = $"L{i:D3}:";
                return prefix + new string((char)('A' + (i - 1) % 26), Math.Max(0, lineWidth - prefix.Length));
            }));
    }

    private static EditorNode CreateNode(string text, int width, int height)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc);
        var node = new EditorNode { State = state, IsFocused = true };
        node.Measure(new Constraints(0, width, 0, height));
        node.Arrange(new Rect(0, 0, width, height));
        return node;
    }

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
            .WaitUntil(s => s.Terminal.InAlternateScreen,
                TimeSpan.FromSeconds(2), "editor visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 1: Vertical scrollbar visibility & rendering
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void VScrollbar_AppearsWhenContentExceedsViewport()
    {
        // 50 lines in 10-line viewport → scrollbar
        var node = CreateNode(MakeLines(50), 30, 10);
        Assert.Equal(29, node.ViewportColumns); // 30 - 1 for scrollbar
        Assert.Equal(10, node.ViewportLines);
    }

    [Fact]
    public void VScrollbar_AbsentWhenContentFits()
    {
        // 5 lines in 10-line viewport → no scrollbar
        var node = CreateNode(MakeLines(5), 30, 10);
        Assert.Equal(30, node.ViewportColumns);
        Assert.Equal(10, node.ViewportLines);
    }

    [Fact]
    public async Task VScrollbar_RendersTrackAndThumb()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var trackChar = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.VerticalThumbCharacter);

        // Wait for content and check scrollbar column (col 29)
        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var hasTrack = false;
        var hasThumb = false;
        for (var row = 0; row < 10; row++)
        {
            var ch = snapshot.GetCell(29, row).Character;
            if (ch == trackChar) hasTrack = true;
            if (ch == thumbChar) hasThumb = true;
        }

        Assert.True(hasTrack, "Should have track characters on rightmost column");
        Assert.True(hasThumb, "Should have thumb characters on rightmost column");
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 2: Horizontal scrollbar visibility & rendering
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void HScrollbar_AppearsWhenLinesExceedViewportWidth()
    {
        // 5 lines, each 80 chars, in 30-wide viewport → h-scrollbar
        var node = CreateNode(MakeWideLines(5, 80), 30, 10);
        Assert.Equal(9, node.ViewportLines); // 10 - 1 for h-scrollbar
    }

    [Fact]
    public void HScrollbar_AbsentWhenContentFitsWidth()
    {
        // 5 lines, each 10 chars, in 30-wide viewport → no h-scrollbar
        var node = CreateNode(MakeWideLines(5, 10), 30, 10);
        Assert.Equal(10, node.ViewportLines);
    }

    [Fact]
    public async Task HScrollbar_RendersOnBottomRow()
    {
        var text = MakeWideLines(3, 80); // 3 lines, 80 chars each
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var trackChar = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var thumbChar = theme.Get(ScrollTheme.HorizontalThumbCharacter);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        var hasTrackOrThumb = false;
        var bottomRow = 9; // 10-line viewport, bottom row
        for (var col = 0; col < 30; col++)
        {
            var ch = snapshot.GetCell(col, bottomRow).Character;
            if (ch == trackChar || ch == thumbChar) hasTrackOrThumb = true;
        }

        Assert.True(hasTrackOrThumb, "Horizontal scrollbar should appear on bottom row");
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 3: Both scrollbars (wide + tall content)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void BothScrollbars_WideAndTallContent()
    {
        // 50 lines, each 80 chars → both scrollbars
        var node = CreateNode(MakeWideLines(50, 80), 30, 10);
        Assert.Equal(29, node.ViewportColumns); // -1 for v-scrollbar
        Assert.Equal(9, node.ViewportLines);    // -1 for h-scrollbar
    }

    [Fact]
    public async Task BothScrollbars_RenderCorrectly()
    {
        var text = MakeWideLines(50, 80);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var vTrack = theme.Get(ScrollTheme.VerticalTrackCharacter);
        var vThumb = theme.Get(ScrollTheme.VerticalThumbCharacter);
        var hTrack = theme.Get(ScrollTheme.HorizontalTrackCharacter);
        var hThumb = theme.Get(ScrollTheme.HorizontalThumbCharacter);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // Vertical scrollbar on col 29, rows 0-8 (not row 9 — that's the h-scrollbar)
        var hasVScrollbar = false;
        for (var row = 0; row < 9; row++)
        {
            var ch = snapshot.GetCell(29, row).Character;
            if (ch == vTrack || ch == vThumb) hasVScrollbar = true;
        }
        Assert.True(hasVScrollbar, "Vertical scrollbar should render on col 29");

        // Horizontal scrollbar on row 9, cols 0-28 (not col 29 — that's the corner)
        var hasHScrollbar = false;
        for (var col = 0; col < 29; col++)
        {
            var ch = snapshot.GetCell(col, 9).Character;
            if (ch == hTrack || ch == hThumb) hasHScrollbar = true;
        }
        Assert.True(hasHScrollbar, "Horizontal scrollbar should render on row 9");
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 4: Cursor position & scroll at document edges
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CursorAtDocStart_ContentShowsFirstLines()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "first line visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("L001", snapshot.GetLineTrimmed(0));
        Assert.Contains("L002", snapshot.GetLineTrimmed(1));
    }

    [Fact]
    public async Task CursorAtDocEnd_ContentShowsLastLines()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Press Ctrl+End to jump to document end
        var pattern = new CellPatternSearcher().Find("L050");
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.End)
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "last line visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        // Last line should be visible somewhere
        var foundLast = false;
        for (var row = 0; row < 10; row++)
        {
            if (snapshot.GetLineTrimmed(row).Contains("L050"))
            {
                foundLast = true;
                break;
            }
        }
        Assert.True(foundLast, "Last line L050 should be visible after Ctrl+End");
    }

    [Fact]
    public async Task CursorAtEndOfLongLine_HScrollShowsEnd()
    {
        var text = MakeWideLines(5, 80);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Press End to move to end of first line
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.End)
            .WaitUntil(s =>
            {
                var snap = s;
                // Line 1 starts with "L001:AAA..." — after scrolling right, "L001" should NOT be visible
                return !snap.GetLineTrimmed(0).StartsWith("L001");
            }, TimeSpan.FromSeconds(2), "horizontal scroll activated")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Verify we can see the end of the line (the last chars should be 'A's)
        var snapshot = terminal.CreateSnapshot();
        var line0 = snapshot.GetLineTrimmed(0);
        Assert.Contains("A", line0);
        // "L001" prefix should have scrolled off
        Assert.DoesNotContain("L001", line0);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 5: Scroll-does-NOT-flick-back (stability)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void ScrollManually_DoesNotFlickBackOnReArrange()
    {
        // Core invariant: Setting scroll offset without moving cursor must be stable
        // across multiple Arrange passes.
        var node = CreateNode(MakeLines(50), 30, 10);
        Assert.Equal(1, node.ScrollOffset); // starts at top

        // Simulate scrollbar/wheel: change scroll without cursor move
        node.ScrollOffset = 25;
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(25, node.ScrollOffset);

        // Arrange again — should STILL be at 25
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(25, node.ScrollOffset);

        // And a third time
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(25, node.ScrollOffset);
    }

    [Fact]
    public async Task ScrollWheel_ThenWait_DoesNotFlickBack()
    {
        // Integration test: scroll down with wheel, then wait — should stay scrolled.
        var text = MakeLines(100);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Cursor is at line 1. Scroll down 10 ticks (3 lines per tick = 30 lines)
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(10)
            .WaitUntil(s =>
            {
                var snap = s;
                // L001 should NOT be visible
                return !snap.GetLineTrimmed(0).Contains("L001");
            }, TimeSpan.FromSeconds(2), "scrolled away from top")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshotAfterScroll = terminal.CreateSnapshot();
        var line0After = snapshotAfterScroll.GetLineTrimmed(0);

        // Wait 500ms — scroll should be stable, no flick-back
        await Task.Delay(500);

        var snapshotAfterWait = terminal.CreateSnapshot();
        var line0AfterWait = snapshotAfterWait.GetLineTrimmed(0);

        Assert.Equal(line0After, line0AfterWait);
        Assert.DoesNotContain("L001", line0AfterWait);
    }

    [Fact]
    public async Task ScrollWheel_ThenTypeChar_SnapsBackToCursor()
    {
        // After scrolling away, typing should snap back to cursor position.
        var text = MakeLines(100);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Scroll down 10 ticks
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(10)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "scrolled away")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Type a character — cursor is still at line 1, should snap back
        var pattern = new CellPatternSearcher().Find("X");
        await new Hex1bTerminalInputSequenceBuilder()
            .FastType("X")
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "typed X visible near cursor")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Line 1 should be visible again (cursor snapped back)
        var snapshot = terminal.CreateSnapshot();
        var foundX = false;
        for (var row = 0; row < 10; row++)
        {
            if (snapshot.GetLineTrimmed(row).Contains("X"))
            {
                foundX = true;
                break;
            }
        }
        Assert.True(foundX, "After typing, viewport should snap back to show cursor + typed text");
    }

    [Fact]
    public void EnterManyLines_ThenScrollUp_StaysScrolled()
    {
        // Exact repro of the user's bug: press Enter N times, scroll up, it flicks back.
        var node = CreateNode("Start", 30, 10);

        // Simulate pressing Enter 30 times (cursor moves to line 31)
        for (var i = 0; i < 30; i++)
        {
            node.State.InsertText("\n");
            node.NotifyCursorChanged();
            node.Arrange(new Rect(0, 0, 30, 10));
        }

        // Cursor is at line 31, scroll shows lines around 22-31
        var scrollAfterEnters = node.ScrollOffset;
        Assert.True(scrollAfterEnters > 20, $"Should have scrolled down, at {scrollAfterEnters}");

        // Now scroll up via ScrollOffset (simulates wheel/scrollbar)
        node.ScrollOffset = 1;
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(1, node.ScrollOffset);

        // Re-arrange again — must NOT flick back
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(1, node.ScrollOffset);

        // Third arrange — still stable
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(1, node.ScrollOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 6: Mouse wheel scrolling
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task WheelDown_ScrollsContentDown()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Initial: line 1 visible
        var initialPattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(initialPattern).HasMatches,
                TimeSpan.FromSeconds(2), "initial content")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Scroll down 5 ticks (15 lines)
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(5)
            .WaitUntil(s =>
            {
                var snap = s;
                return !snap.GetLineTrimmed(0).Contains("L001");
            }, TimeSpan.FromSeconds(2), "scrolled down")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        // L001 should NOT be on row 0 anymore
        Assert.DoesNotContain("L001", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task WheelUp_ScrollsContentUp()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // First scroll down, then scroll back up
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollDown(5)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "scrolled down")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Now scroll up
        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .ScrollUp(5)
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "scrolled back up to top")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("L001", snapshot.GetLineTrimmed(0));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 7: Scrollbar click (page jump)
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task VScrollbarClick_BelowThumb_PagesDown()
    {
        var text = MakeLines(100);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Click on vertical scrollbar track below the thumb (col 29, row 8)
        // For 100 lines in 10 rows, thumb is near top. Row 8 is below it.
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(29, 8)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "paged down")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.DoesNotContain("L001", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task HScrollbarClick_RightOfThumb_PagesRight()
    {
        var text = MakeWideLines(3, 120); // very wide lines
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Click on horizontal scrollbar track right of thumb (col 20, bottom row)
        var bottomRow = 9;
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(20, bottomRow)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "paged right")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        // After paging right, "L001:" prefix should have scrolled off
        Assert.DoesNotContain("L001", snapshot.GetLineTrimmed(0));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 8: Scrollbar thumb drag
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task VScrollbarDrag_ThumbDown_ScrollsContent()
    {
        var text = MakeLines(100);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "content visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Drag the scrollbar thumb from row 0 to row 5 on col 29
        // For 100 lines in 10 rows, thumb starts at top
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(29, 0, 29, 5)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "dragged scrollbar down")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.DoesNotContain("L001", snapshot.GetLineTrimmed(0));
    }

    [Fact]
    public async Task VScrollbarDrag_IsStable_DoesNotFlickBack()
    {
        // After dragging the scrollbar thumb, the scroll position must remain stable.
        var text = MakeLines(100);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "initial content")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Drag scrollbar thumb down
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(29, 0, 29, 5)
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "scrollbar dragged")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshotAfterDrag = terminal.CreateSnapshot();
        var line0After = snapshotAfterDrag.GetLineTrimmed(0);

        // Wait 500ms — must be stable
        await Task.Delay(500);

        var snapshotAfterWait = terminal.CreateSnapshot();
        var line0AfterWait = snapshotAfterWait.GetLineTrimmed(0);

        Assert.Equal(line0After, line0AfterWait);
        Assert.DoesNotContain("L001", line0AfterWait);
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 9: Predicted content verification
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task ScrollTo_SpecificLine_ShowsExpectedContent()
    {
        // 50 lines: L001..L050, viewport 30x10. After Ctrl+End, viewport should
        // show lines L041-L050 (or close, depending on cursor position).
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        // Go to end of document
        var pattern = new CellPatternSearcher().Find("L050");
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.End)
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "end of document visible")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();

        // The last line L050 should be visible. With cursor at end, lines
        // around L041-L050 should be in the viewport.
        var seenLines = new List<string>();
        for (var row = 0; row < 10; row++)
        {
            seenLines.Add(snapshot.GetLineTrimmed(row));
        }

        Assert.Contains(seenLines, l => l.Contains("L050"));
        // L001 should NOT be visible
        Assert.DoesNotContain(seenLines, l => l.Contains("L001"));
    }

    [Fact]
    public async Task PageDown_ThenPageUp_ReturnsToOriginalContent()
    {
        var text = MakeLines(50);
        var (workload, terminal, app, state, theme, _) = SetupEditor(text, 30, 10);
        using var _ = workload; using var __ = terminal; using var ___ = app;
        await WaitForEditor(terminal);

        var pattern = new CellPatternSearcher().Find("L001");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "initial state")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Page down twice to scroll past L001, then page up twice to return
        await new Hex1bTerminalInputSequenceBuilder()
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.GetLineTrimmed(0).Contains("L001"),
                TimeSpan.FromSeconds(2), "paged down past L001")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .PageUp()
            .PageUp()
            .WaitUntil(s => s.SearchPattern(pattern).HasMatches,
                TimeSpan.FromSeconds(2), "paged back up to L001")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = terminal.CreateSnapshot();
        Assert.Contains("L001", snapshot.GetLineTrimmed(0));
    }

    // ═══════════════════════════════════════════════════════════
    // SECTION 10: Horizontal scroll stability
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void HScrollManually_DoesNotFlickBackOnReArrange()
    {
        var node = CreateNode(MakeWideLines(5, 80), 30, 10);

        // Set horizontal scroll
        node.HorizontalScrollOffset = 20;
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(20, node.HorizontalScrollOffset);

        // Re-arrange — must stay
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(20, node.HorizontalScrollOffset);
    }

    [Fact]
    public void CursorMove_SnapsHorizontalScroll()
    {
        // After horizontal scroll, moving cursor to beginning should snap back.
        var node = CreateNode(MakeWideLines(5, 80), 30, 10);

        // Move cursor to end of line (triggers horizontal scroll)
        node.State.MoveToLineEnd();
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.True(node.HorizontalScrollOffset > 0, "Should have scrolled right");

        // Now Home (triggers cursor move back to column 0)
        node.State.MoveToLineStart();
        node.NotifyCursorChanged();
        node.Arrange(new Rect(0, 0, 30, 10));
        Assert.Equal(0, node.HorizontalScrollOffset);
    }
}
