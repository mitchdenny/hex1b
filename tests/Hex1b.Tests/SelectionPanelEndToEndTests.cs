using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// End-to-end behaviour tests for <see cref="SelectionPanelWidget"/> driving
/// real keyboard and mouse input through a live <see cref="Hex1bApp"/>.
/// Asserts on the actual COPIED TEXT payload (not just internal node state)
/// so any regression in the input pipeline, capture-override routing,
/// reconciliation, layout, or text-extraction will surface as a failed
/// payload assertion.
/// </summary>
/// <remarks>
/// Categorised as:
/// <list type="bullet">
/// <item>A. Keyboard, single-screen — content fits the viewport.</item>
/// <item>B. Mouse, single-screen.</item>
/// <item>C. Keyboard, scroll-spanning — selection spans content beyond the
///       initially visible viewport via cursor navigation in panel-local
///       coordinates.</item>
/// <item>D. Mouse, wheel-during-drag scroll-spanning — selection extends
///       to newly-visible content while the wheel scrolls the surrounding
///       ScrollPanel mid-drag.</item>
/// </list>
/// </remarks>
public class SelectionPanelEndToEndTests
{
    // ------------------------------------------------------------------
    // Test fixtures and helpers.
    // ------------------------------------------------------------------

    /// <summary>
    /// Test harness wrapping a live Hex1bApp + Hex1bTerminal. Disposes
    /// everything at scope exit. <c>CopiedTexts</c> captures every payload
    /// delivered to <c>OnCopy</c> so tests can assert <c>Single()</c>,
    /// <c>Empty</c>, ordering, etc.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        public required Hex1bAppWorkloadAdapter Workload { get; init; }
        public required Hex1bTerminal Terminal { get; init; }
        public required Hex1bApp App { get; init; }
        public required Task RunTask { get; init; }
        public required List<SelectionPanelCopyEventArgs> CopyEvents { get; init; }

        public List<string> CopiedTexts => CopyEvents.ConvertAll(e => Norm(e.Text));

        /// <summary>
        /// Normalises CR/LF line endings so payload assertions are
        /// stable across platforms (the Surface text-readback uses
        /// <see cref="System.Text.StringBuilder.AppendLine"/>, which
        /// emits <c>\r\n</c> on Windows and <c>\n</c> on Unix).
        /// </summary>
        public static string Norm(string s) => s.Replace("\r\n", "\n").Replace("\r", "\n");

        public SelectionPanelNode? FindPanel() => Walk<SelectionPanelNode>(App.RootNode);
        public ScrollPanelNode? FindScrollPanel() => Walk<ScrollPanelNode>(App.RootNode);

        private static T? Walk<T>(Hex1bNode? node) where T : Hex1bNode
        {
            if (node is null) return null;
            if (node is T match) return match;
            foreach (var child in node.GetChildren())
            {
                var found = Walk<T>(child);
                if (found is not null) return found;
            }
            return null;
        }

        public async ValueTask DisposeAsync()
        {
            App.Dispose();
            Terminal.Dispose();
            Workload.Dispose();
            try { await RunTask; } catch { /* run loop cancelled */ }
        }
    }

    /// <summary>
    /// Builds a <see cref="Harness"/> with a SelectionPanel wrapping a
    /// VScrollPanel-hosted text payload. The SelectionPanel is the only
    /// focusable-relevant node so F12 reaches it without competition.
    /// </summary>
    private static async Task<Harness> BuildScrollHarnessAsync(
        string content,
        int width,
        int height,
        bool includeFocusedSibling = false)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        var copyEvents = new List<SelectionPanelCopyEventArgs>();

        var app = new Hex1bApp(
            ctx =>
            {
                Hex1bWidget body = ctx.VScrollPanel(sv =>
                [
                    sv.SelectionPanel(sv.Text(content))
                        .OnCopy((SelectionPanelCopyEventArgs args) => copyEvents.Add(args))
                ], showScrollbar: false);

                if (includeFocusedSibling)
                {
                    // Pin a TextBox below the panel so F12-global has to
                    // bypass an active focus owner. Smaller height to keep
                    // viewport math the same for the SelectionPanel.
                    body = ctx.VStack(v =>
                    [
                        v.VScrollPanel(sv =>
                        [
                            sv.SelectionPanel(sv.Text(content))
                                .OnCopy((SelectionPanelCopyEventArgs args) => copyEvents.Add(args))
                        ], showScrollbar: false).Fill(),
                        v.TextBox(),
                    ]);
                }

                return Task.FromResult<Hex1bWidget>(body);
            },
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "selection panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        return new Harness
        {
            Workload = workload,
            Terminal = terminal,
            App = app,
            RunTask = runTask,
            CopyEvents = copyEvents,
        };
    }

    /// <summary>
    /// Builds a SelectionPanel wrapping the content directly (no
    /// ScrollPanel). Used by single-screen tests where panel.Bounds.Height
    /// equals the line count and no off-screen content exists.
    /// </summary>
    private static async Task<Harness> BuildPlainHarnessAsync(string content, int width, int height)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(width, height).Build();

        var copyEvents = new List<SelectionPanelCopyEventArgs>();

        var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.SelectionPanel(ctx.Text(content))
                    .OnCopy((SelectionPanelCopyEventArgs args) => copyEvents.Add(args))),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5),
                "plain selection panel ready")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        return new Harness
        {
            Workload = workload,
            Terminal = terminal,
            App = app,
            RunTask = runTask,
            CopyEvents = copyEvents,
        };
    }

    // Block-friendly fixed-width content: 5 rows × 24 cols. Each row has
    // 4 distinct char "lanes" so block selections produce unambiguous text.
    //   Cols:  0123456789012345678901234
    //   Row 0: AAAAA-BBBBB-CCCCC-DDDDD
    //   Row 1: EEEEE-FFFFF-GGGGG-HHHHH
    //   Row 2: IIIII-JJJJJ-KKKKK-LLLLL
    //   Row 3: MMMMM-NNNNN-OOOOO-PPPPP
    //   Row 4: QQQQQ-RRRRR-SSSSS-TTTTT
    private const string FixedContent5Rows =
        "AAAAA-BBBBB-CCCCC-DDDDD\n" +
        "EEEEE-FFFFF-GGGGG-HHHHH\n" +
        "IIIII-JJJJJ-KKKKK-LLLLL\n" +
        "MMMMM-NNNNN-OOOOO-PPPPP\n" +
        "QQQQQ-RRRRR-SSSSS-TTTTT";

    // 30 rows of distinct content for scroll-spanning tests. Each row is
    // 24 chars and starts with its row number so substring assertions are
    // unambiguous.
    private static string Build30RowContent()
    {
        var rows = new string[30];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = $"Row{i:00}-payload-rest-here"; // 24 chars
        return string.Join('\n', rows);
    }

    // ------------------------------------------------------------------
    // Category A — Keyboard, single-screen
    // ------------------------------------------------------------------

    [Fact]
    public async Task Keyboard_LineSelect_BufferTopToBufferBottom_CopiesAllVisibleLines()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode, TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)                                       // jump to top (row 0)
            .Shift().Key(Hex1bKey.V)                              // line mode
            .Shift().Key(Hex1bKey.G)                              // jump to bottom
            .Key(Hex1bKey.Y)                                       // commit
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var text = Assert.Single(h.CopiedTexts);
        Assert.Equal(
            "AAAAA-BBBBB-CCCCC-DDDDD\n" +
            "EEEEE-FFFFF-GGGGG-HHHHH\n" +
            "IIIII-JJJJJ-KKKKK-LLLLL\n" +
            "MMMMM-NNNNN-OOOOO-PPPPP\n" +
            "QQQQQ-RRRRR-SSSSS-TTTTT",
            text);
    }

    [Fact]
    public async Task Keyboard_LineSelect_SingleLineAtCursor_CopiesThatLine()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // F12 enters copy mode at panel-local bottom (row Bounds.Height-1
        // = 7 here, an empty row). Navigate to a known content row first
        // (row 4 = bottom content row "QQQQQ-...") then commit a single
        // line selection.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)                              // top: row 0
            .Down().Down().Down().Down()                  // → row 4
            .WaitUntil(_ => panel!.CursorRow == 4,
                TimeSpan.FromSeconds(2), "cursor on row 4")
            .Shift().Key(Hex1bKey.V)                      // line mode
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.Equal("QQQQQ-RRRRR-SSSSS-TTTTT", Assert.Single(h.CopiedTexts));
    }

    [Fact]
    public async Task Keyboard_CharSelect_PartialFirstAndLastRow_CopiesStreamSubstring()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // F12 → G (row 0, col 0) → Right×6 (row 0, col 6) → V (start char
        // selection here) → Down (row 1) → Right×4 (row 1, col 10) → Y.
        // Stream from (0,6) to (1,10):
        //   row 0 [6..end]  = "BBBBB-CCCCC-DDDDD" (after trim)
        //   row 1 [0..10]   = "EEEEE-FFFFF" (cells 0..10 inclusive)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode, TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)
            .Right().Right().Right().Right().Right().Right()
            .Key(Hex1bKey.V)
            .Down()
            .Right().Right().Right().Right()
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.Equal(
            "BBBBB-CCCCC-DDDDD\n" +
            "EEEEE-FFFFF",
            Assert.Single(h.CopiedTexts));
    }

    [Fact]
    public async Task Keyboard_BlockSelect_RectangleAcrossRows_CopiesRectangleText()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // F12 → G (row 0,col 0) → Right×6 → Alt+V (block mode start at
        // (0,6)) → Down×2 → Right×4 (cursor at (2,10)) → Y.
        // Block rows 0..2, cols 6..10:
        //   row 0 cells 6..10 = "BBBBB"
        //   row 1 cells 6..10 = "FFFFF"
        //   row 2 cells 6..10 = "JJJJJ"
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode, TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)
            .Right().Right().Right().Right().Right().Right()
            .Alt().Key(Hex1bKey.V)
            .Down().Down()
            .Right().Right().Right().Right()
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var args = Assert.Single(h.CopyEvents);
        Assert.Equal(SelectionMode.Block, args.Mode);
        Assert.Equal("BBBBB\nFFFFF\nJJJJJ", Harness.Norm(args.Text));
    }

    [Fact]
    public async Task Keyboard_Cancel_NoCopyDelivered_AndExitsCopyMode()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode, TimeSpan.FromSeconds(2), "copy mode entered")
            .Shift().Key(Hex1bKey.V)
            .Up().Up()
            .Escape()
            .WaitUntil(_ => !panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "copy mode cancelled")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.Empty(h.CopiedTexts);
        Assert.False(panel!.HasSelection);
    }

    [Fact]
    public async Task Keyboard_F12_GlobalRouting_FiresEvenWhenSiblingHasFocus()
    {
        // Sibling TextBox owns focus by default (last-rendered focusable in
        // a VStack). F12 must still reach the SelectionPanel because its
        // entry binding is registered .Global().
        await using var h = await BuildScrollHarnessAsync(
            FixedContent5Rows, 30, 12, includeFocusedSibling: true);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "copy mode entered via global F12")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.True(panel!.IsInCopyMode);
    }

    // ------------------------------------------------------------------
    // Category B — Mouse, single-screen
    // ------------------------------------------------------------------

    [Fact]
    public async Task Mouse_Drag_CharSelect_AcrossOneRow_CopiesText()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // Drag from (0, 0) to (10, 0) — char mode, single row, cells 0..10.
        // Row 0 content cells 0..10 = "AAAAA-BBBBB"
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 0, fromY: 0, toX: 10, toY: 0)
            .WaitUntil(_ => panel!.IsInCopyMode && panel.HasSelection,
                TimeSpan.FromSeconds(2), "selection ready")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.Equal("AAAAA-BBBBB", Assert.Single(h.CopiedTexts));
    }

    [Fact]
    public async Task Mouse_CtrlDrag_LineSelect_AcrossThreeRows_CopiesAllRows()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // Ctrl+drag from (5, 1) to (15, 3) — line mode, rows 1..3 full.
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Drag(fromX: 5, fromY: 1, toX: 15, toY: 3)
            .WaitUntil(_ => panel!.IsInCopyMode
                && panel.CursorSelectionMode == SelectionMode.Line
                && panel.CursorRow == 3,
                TimeSpan.FromSeconds(2), "line drag complete")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var args = Assert.Single(h.CopyEvents);
        Assert.Equal(SelectionMode.Line, args.Mode);
        Assert.Equal(
            "EEEEE-FFFFF-GGGGG-HHHHH\n" +
            "IIIII-JJJJJ-KKKKK-LLLLL\n" +
            "MMMMM-NNNNN-OOOOO-PPPPP",
            Harness.Norm(args.Text));
    }

    [Fact]
    public async Task Mouse_AltDrag_BlockSelect_AcrossRowsAndCols_CopiesRectangle()
    {
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // Alt+drag from (6, 0) to (10, 2) — block, cols 6..10, rows 0..2.
        //   row 0 cells 6..10 = "BBBBB"
        //   row 1 cells 6..10 = "FFFFF"
        //   row 2 cells 6..10 = "JJJJJ"
        await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Drag(fromX: 6, fromY: 0, toX: 10, toY: 2)
            .WaitUntil(_ => panel!.IsInCopyMode
                && panel.CursorSelectionMode == SelectionMode.Block
                && panel.CursorRow == 2,
                TimeSpan.FromSeconds(2), "block drag complete")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        Assert.Equal("BBBBB\nFFFFF\nJJJJJ", Assert.Single(h.CopiedTexts));
    }

    [Fact]
    public async Task Mouse_Drag_ThenKeyboardRefine_ThenCommit_CopiesExtendedSelection()
    {
        // Proves the mouse→keyboard handoff: drag installs keyboard
        // capture on release, so arrow keys / End / Y continue to work as
        // selection commands.
        await using var h = await BuildPlainHarnessAsync(FixedContent5Rows, 30, 8);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        // Initial mouse selection: char-mode drag from (0,0) to (4,0)
        // (cells 0..4 of row 0).
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(fromX: 0, fromY: 0, toX: 4, toY: 0)
            .WaitUntil(_ => panel!.IsInCopyMode
                && panel.AnchorRow == 0 && panel.AnchorCol == 0
                && panel.CursorRow == 0 && panel.CursorCol == 4,
                TimeSpan.FromSeconds(2), "initial mouse selection")
            // Keyboard refinement: extend to end of row 1 then commit.
            .Down()
            .Key(Hex1bKey.End)
            .WaitUntil(_ => panel!.CursorRow == 1 && panel.CursorCol > 4,
                TimeSpan.FromSeconds(2), "keyboard refinement extended")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        // Char-mode stream from (0,0) to (1,end-of-row):
        //   row 0 cells 0..end = "AAAAA-BBBBB-CCCCC-DDDDD"
        //   row 1 cells 0..end = "EEEEE-FFFFF-GGGGG-HHHHH"
        Assert.Equal(
            "AAAAA-BBBBB-CCCCC-DDDDD\n" +
            "EEEEE-FFFFF-GGGGG-HHHHH",
            Assert.Single(h.CopiedTexts));
    }

    // ------------------------------------------------------------------
    // Category C — Keyboard, scroll-spanning (content > viewport)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Keyboard_LineSelect_BufferTopToBottom_CopiesAll30Rows_WhenContentExceedsViewport()
    {
        // 30 rows of content inside a 10-row viewport. The selection must
        // include rows that were never visible in the initial viewport,
        // proving SelectionPanel reads from FULL content not the viewport.
        var content = Build30RowContent();
        await using var h = await BuildScrollHarnessAsync(content, 40, 10);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)                                       // top of full content
            .Shift().Key(Hex1bKey.V)                              // line mode
            .Shift().Key(Hex1bKey.G)                              // bottom of full content
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var text = Assert.Single(h.CopiedTexts);
        var lines = text.Split('\n');
        Assert.Equal(30, lines.Length);
        for (int i = 0; i < 30; i++)
        {
            Assert.Equal($"Row{i:00}-payload-rest-here", lines[i]);
        }
    }

    [Fact]
    public async Task Keyboard_LineSelect_PageDownFromTop_CopiesPageWorthOfRows()
    {
        // Line mode + PageDown: selection extends by PageRows (=20) lines,
        // many of which were never in the visible viewport. Use line mode
        // so we don't have to deal with char-mode start-column semantics.
        var content = Build30RowContent();
        await using var h = await BuildScrollHarnessAsync(content, 40, 10);
        var panel = h.FindPanel();
        Assert.NotNull(panel);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode,
                TimeSpan.FromSeconds(2), "copy mode entered")
            .Key(Hex1bKey.G)
            .Shift().Key(Hex1bKey.V)
            .Key(Hex1bKey.PageDown)
            .WaitUntil(_ => panel!.CursorRow >= 20,
                TimeSpan.FromSeconds(2), "page-down advanced cursor by PageRows")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var text = Assert.Single(h.CopiedTexts);
        var lines = text.Split('\n');
        // PageRows=20 takes cursor from 0 to 20, inclusive both ends → 21 rows.
        Assert.Equal(21, lines.Length);
        Assert.Equal("Row00-payload-rest-here", lines[0]);
        Assert.Equal("Row20-payload-rest-here", lines[^1]);
        // Row 9 was the last row of the initial viewport — proves we crossed it.
        Assert.Contains("Row10-payload-rest-here", lines);
        Assert.Contains("Row15-payload-rest-here", lines);
        // Row 21+ must NOT be present.
        Assert.DoesNotContain("Row21-payload-rest-here", lines);
        Assert.DoesNotContain("Row29-payload-rest-here", lines);
    }

    [Fact]
    public async Task Keyboard_PreScrolledViewport_LineSelect_BufferBottomToTop_CopiesAllUpward()
    {
        // Pre-scroll the ScrollPanel to the bottom so the visible viewport
        // shows rows ~20..29. Enter copy mode (cursor lands at row 29 —
        // visually at the bottom of what's visible NOW). Toggle line mode,
        // jump to top via G, commit. Selection must include ALL 30 rows
        // including the rows that were ABOVE the pre-scrolled viewport.
        var content = Build30RowContent();
        await using var h = await BuildScrollHarnessAsync(content, 40, 10);
        var scroll = h.FindScrollPanel();
        var panel = h.FindPanel();
        Assert.NotNull(scroll);
        Assert.NotNull(panel);

        scroll!.ScrollToBottom();
        h.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => scroll.Offset > 0 && panel!.Bounds.Y < 0,
                TimeSpan.FromSeconds(2), "scroll applied + layout reflects offset")
            .Key(Hex1bKey.F12)
            .WaitUntil(_ => panel!.IsInCopyMode && panel.CursorRow == 29,
                TimeSpan.FromSeconds(2), "copy mode entered at bottom of full content")
            .Shift().Key(Hex1bKey.V)
            .Key(Hex1bKey.G)
            .WaitUntil(_ => panel!.CursorRow == 0,
                TimeSpan.FromSeconds(2), "cursor jumped to top via G")
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var text = Assert.Single(h.CopiedTexts);
        var lines = text.Split('\n');
        Assert.Equal(30, lines.Length);
        Assert.Equal("Row00-payload-rest-here", lines[0]);
        Assert.Equal("Row29-payload-rest-here", lines[^1]);
    }

    // ------------------------------------------------------------------
    // Category D — Mouse, wheel-during-drag scroll-spanning
    // ------------------------------------------------------------------

    [Fact]
    public async Task Mouse_CtrlDrag_ThenScrollDownDuringDrag_CopiesContentBeyondInitialViewport()
    {
        // Ctrl+drag = line mode so each "row passed under the mouse" lands
        // in the payload as a full row. Drag down a couple rows to enter
        // copy mode then scroll down: rows previously off-screen below the
        // viewport slide into view, and the synthetic drag-move follows
        // the (stationary) mouse cursor onto them — extending the selection
        // into rows the user could not have selected without scrolling.
        var content = Build30RowContent();
        await using var h = await BuildScrollHarnessAsync(content, 40, 10);
        var scroll = h.FindScrollPanel();
        var panel = h.FindPanel();
        Assert.NotNull(scroll);
        Assert.NotNull(panel);
        Assert.Equal(0, scroll!.Offset);

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().MouseMoveTo(5, 1)
            .Ctrl().MouseDown()
            .Wait(TimeSpan.FromMilliseconds(20))
            .Ctrl().MouseMoveTo(5, 3)
            .WaitUntil(_ => panel!.IsInCopyMode
                && panel.CursorSelectionMode == SelectionMode.Line
                && panel.AnchorRow == 1 && panel.CursorRow == 3,
                TimeSpan.FromSeconds(2), "line drag started")
            .ScrollDown(5)
            .WaitUntil(_ => scroll.Offset > 0 && panel!.CursorRow > 3,
                TimeSpan.FromSeconds(2), "scroll-during-drag extended cursor")
            .MouseUp()
            .Wait(TimeSpan.FromMilliseconds(20))
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var args = Assert.Single(h.CopyEvents);
        Assert.Equal(SelectionMode.Line, args.Mode);

        var lines = Harness.Norm(args.Text).Split('\n');
        // Anchor row was 1. Cursor row landed at panel.CursorRow at
        // commit time (>= 4 because scroll added rows). Line mode payload
        // is rows [1..cursorRow] inclusive in panel-local coords.
        Assert.True(lines.Length >= 4,
            $"Expected at least 4 lines (anchor row 1 + scroll-extended), got {lines.Length}: {args.Text}");
        Assert.Equal("Row01-payload-rest-here", lines[0]);
        // Must include rows that were OFF-SCREEN at drag start (viewport
        // showed rows 0..9, so row 10+ counts as scrolled-into-view).
        Assert.Contains(lines, l => l.StartsWith("Row10-", StringComparison.Ordinal));
        // And must NOT include rows above the anchor.
        Assert.DoesNotContain(lines, l => l.StartsWith("Row00-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Mouse_CtrlDrag_ThenScrollUpDuringDrag_CopiesContentAboveInitialViewport()
    {
        // Pre-scroll to the bottom so the viewport shows rows ~20..29.
        // Ctrl+drag starts a line selection inside that viewport. Wheel UP
        // brings rows from above the viewport into view — the synthetic
        // drag-move follows the (stationary) mouse cursor up the panel-local
        // coordinate space, extending the selection upward.
        var content = Build30RowContent();
        await using var h = await BuildScrollHarnessAsync(content, 40, 10);
        var scroll = h.FindScrollPanel();
        var panel = h.FindPanel();
        Assert.NotNull(scroll);
        Assert.NotNull(panel);

        scroll!.ScrollToBottom();
        h.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => scroll.Offset > 0 && panel!.Bounds.Y < 0,
                TimeSpan.FromSeconds(2), "scroll applied + layout reflects offset")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        // Capture the panel-local row that the mouse position corresponds
        // to, given current offset. With ScrollToBottom, panel.Bounds.Y is
        // negative; terminal row 8 → panel-local row 8 - panel.Bounds.Y.
        int initialAnchorRow = 8 - panel!.Bounds.Y;

        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().MouseMoveTo(5, 8)
            .Ctrl().MouseDown()
            .Wait(TimeSpan.FromMilliseconds(20))
            .Ctrl().MouseMoveTo(5, 5)
            .WaitUntil(_ => panel!.IsInCopyMode
                && panel.CursorSelectionMode == SelectionMode.Line,
                TimeSpan.FromSeconds(2), "line drag started")
            .ScrollUp(5)
            .WaitUntil(_ => scroll.Offset < scroll.MaxOffset,
                TimeSpan.FromSeconds(2), "scroll up applied")
            .MouseUp()
            .Wait(TimeSpan.FromMilliseconds(20))
            .Key(Hex1bKey.Y)
            .WaitUntil(_ => h.CopiedTexts.Count == 1,
                TimeSpan.FromSeconds(2), "copy delivered")
            .Build()
            .ApplyAsync(h.Terminal, TestContext.Current.CancellationToken);

        var args = Assert.Single(h.CopyEvents);
        Assert.Equal(SelectionMode.Line, args.Mode);

        var lines = Harness.Norm(args.Text).Split('\n');
        Assert.True(lines.Length >= 2,
            $"Expected at least 2 lines after scroll-up extension, got {lines.Length}: {args.Text}");
        // Bottom of the selection is the original mouse-down panel-local
        // row (anchor). Selection extended UPWARD from there into rows
        // above the initial viewport.
        Assert.Equal($"Row{initialAnchorRow:00}-payload-rest-here", lines[^1]);
        // Top row index must be SMALLER than the row currently under the
        // mouse (because we scrolled up after grab, bringing higher rows
        // under the cursor than were originally there).
        var firstRowLabel = lines[0];
        // first line should be "RowNN-..." with NN < initialAnchorRow.
        Assert.StartsWith("Row", firstRowLabel, StringComparison.Ordinal);
        var firstRowNumber = int.Parse(firstRowLabel.Substring(3, 2));
        Assert.True(firstRowNumber < initialAnchorRow,
            $"Expected scroll-up to extend selection to a row above {initialAnchorRow}, got {firstRowLabel}");
    }
}
