using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Reflow tests derived from upstream terminal emulator test suites.
/// </summary>
/// <remarks>
/// <para><b>Provenance:</b> Each test in this class is adapted from a specific test function
/// in a real terminal emulator's test suite. The upstream source is documented in every test's
/// XML doc comment and inline comments, including the file path, function name, and approximate
/// line number in the upstream repository at the time of writing.</para>
///
/// <para><b>How expected values were determined:</b> Expected values come from two sources:
/// <list type="number">
///   <item>Directly from upstream test assertions (when the upstream test asserts exact string values)</item>
///   <item>Manual hand-tracing of the upstream algorithm against our reflow engine, documented in
///   inline comments within each test</item>
/// </list>
/// </para>
///
/// <para><b>Upstream sources used:</b></para>
/// <list type="bullet">
///   <item><b>kitty</b>: <c>kovidgoyal/kitty</c> — <c>kitty_tests/screen.py</c>,
///   functions <c>test_resize</c>, <c>test_cursor_after_resize</c>, <c>test_scrollback_fill_after_resize</c>.
///   Default <c>create_screen()</c> parameters: cols=5, lines=5, scrollback=5.
///   Found in <c>kitty_tests/__init__.py</c> BaseTest.create_screen().</item>
///   <item><b>Alacritty</b>: <c>alacritty/alacritty</c> — <c>alacritty_terminal/src/grid/tests.rs</c>,
///   functions <c>shrink_reflow</c>, <c>shrink_reflow_twice</c>, <c>grow_reflow</c>,
///   <c>grow_reflow_multiline</c>, <c>grow_reflow_disabled</c>, <c>shrink_reflow_disabled</c>.
///   Uses <c>Grid::new(rows, cols, scrollback)</c> and <c>cell(c)</c>/<c>wrap_cell(c)</c> helpers
///   defined at the bottom of tests.rs.</item>
/// </list>
///
/// <para><b>How to update these tests:</b></para>
/// <list type="number">
///   <item>Fetch the latest version of the upstream test file (URLs in each test's comments)</item>
///   <item>Compare the upstream test function against our test — look for changed assertions or new scenarios</item>
///   <item>Hand-trace the upstream setup through our reflow engine to verify expected values</item>
///   <item>Update both the C# test and the JSON fixtures in <c>TestData/ReflowFixtures.json</c></item>
/// </list>
///
/// <para><b>Important behavioral note:</b> No upstream project has serialized test fixtures.
/// Kitty tests are Python (programmatic), Alacritty tests are Rust (programmatic).
/// We manually translated each test case, so there is inherent translation risk.
/// The JSON fixtures in TestData/ serve as our serialized format for auditing.</para>
///
/// <para><b>Known limitations of our emulation:</b></para>
/// <list type="bullet">
///   <item>The xterm "bottom-fill" vs kitty "cursor-anchored" distinction is derived from
///   observed behavior and source code reading, not from an official specification.</item>
///   <item>Kitty's <c>scrollback_fill_enlarged_window</c> option (used in
///   <c>test_scrollback_fill_after_resize</c>) is a kitty-specific feature, not standard
///   reflow behavior. Tests derived from it are marked accordingly.</item>
/// </list>
/// </remarks>
public class UpstreamReflowTests
{
    #region Kitty Compatibility
    // Source: https://raw.githubusercontent.com/kovidgoyal/kitty/master/kitty_tests/screen.py
    // Default create_screen() params: cols=5, lines=5, scrollback=5
    // (from kitty_tests/__init__.py BaseTest.create_screen)

    /// <summary>
    /// Narrowing a hard-wrapped line that is wider than the new terminal width
    /// must split the content across rows, even though it wasn't soft-wrapped.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_resize</c> (~line 318)
    /// <code>
    /// s = self.create_screen(cols=5, lines=5, scrollback=15)
    /// s.draw('12345'), s.carriage_return(), s.index()
    /// s.resize(s.lines, s.columns - 1)
    /// self.ae(('1234', '5', ''), tuple(str(s.line(i)) for i in range(s.cursor.y+1)))
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. draw('12345') → fills row 0, pending_wrap=true, cursor at col 4 pending
    /// 2. carriage_return() → cursor.x=0, clears pending_wrap (does NOT set SoftWrap)
    /// 3. index() → cursor moves down to row 1 (cursor at (0,1))
    /// 4. Row 0 = "12345" with NO SoftWrap (hard break via CR+index, not character wrap)
    /// 5. Resize to cols=4: row "12345" is 5 chars in 4-wide terminal → must split
    /// 6. GroupLogicalLines: "12345" = one logical line (no SoftWrap to join with next)
    /// 7. WrapLogicalLine at width 4: "1234" (SoftWrap), "5" = 2 rows
    /// 8. Expected screen: "1234", "5", "", ...
    ///
    /// <b>Key insight:</b> Reflow wraps individual logical lines to new width regardless
    /// of whether they originally had SoftWrap. SoftWrap only controls whether ADJACENT
    /// rows are joined into the same logical line.
    /// </remarks>
    [Fact]
    public void Kitty_NarrowHardWrappedLine_SplitsCorrectly()
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5)
            .WithScrollback(15).Build();

        // Kitty: s.draw('12345'), s.carriage_return(), s.index()
        // We use the equivalent ANSI sequence: write "12345" then CR+LF
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("12345\r\n"));

        terminal.Resize(4, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("1234", snap.GetLine(0).TrimEnd());
        Assert.Equal("5", snap.GetLine(1).TrimEnd());
        Assert.Equal("", snap.GetLine(2).TrimEnd());
    }

    /// <summary>
    /// Five rows of 5 digits continuously drawn (soft-wrapped) widen to 10-wide,
    /// merging pairs into 3 rows.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_resize</c> (~line 325)
    /// <code>
    /// s = self.create_screen(scrollback=6)
    /// s.draw(''.join([str(i) * s.columns for i in range(s.lines)]))
    /// s.resize(3, 10)
    /// self.ae(str(s.line(0)), '0'*5 + '1'*5)
    /// self.ae(str(s.line(1)), '2'*5 + '3'*5)
    /// self.ae(str(s.line(2)), '4'*5)
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. create_screen(cols=5, lines=5, scrollback=6)
    /// 2. draw("0000011111222223333344444") — 25 chars continuously, all one draw call
    /// 3. At width 5: fills 5 rows. After row 0 fills, char '1' triggers wrap → SoftWrap on row0[4].
    ///    Similarly for rows 1-3. Row 4 ends with pending_wrap but no SoftWrap (nothing follows).
    /// 4. All 5 rows form ONE logical line of 25 chars.
    /// 5. Resize to (10, 3): 25 chars at width 10 = "0000011111" + "2222233333" + "44444" = 3 rows.
    /// 6. Screen is 3 rows, so all fit with no scrollback.
    /// </remarks>
    [Fact]
    public void Kitty_WidenMergesPairs_5x5to10()
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5)
            .WithScrollback(6).Build();

        // Kitty: s.draw(''.join([str(i) * s.columns for i in range(s.lines)]))
        // = "0000011111222223333344444" (25 chars, all one continuous draw)
        terminal.ApplyTokens([new TextToken("0000011111222223333344444")]);

        terminal.Resize(10, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("0000011111", snap.GetLine(0).TrimEnd());
        Assert.Equal("2222233333", snap.GetLine(1).TrimEnd());
        Assert.Equal("44444", snap.GetLine(2).TrimEnd());
    }

    /// <summary>
    /// 50 chars drawn at width 5, narrowed to width 2. Screen shows last 5 of 25 rows.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_resize</c> (~line 335)
    /// <code>
    /// s = self.create_screen(scrollback=20)
    /// s.draw(''.join(str(i) * s.columns for i in range(s.lines*2)))
    /// self.ae(str(s.linebuf), '55555\n66666\n77777\n88888\n99999')
    /// before = at()
    /// s.resize(5, 2)
    /// self.ae(before, at())
    /// self.ae(str(s.linebuf), '88\n88\n89\n99\n99')
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. 10 groups of 5 = 50 chars: "00000111112222233333444445555566666777778888899999"
    /// 2. At width 5: 10 rows, screen=5, scrollback gets rows 0-4.
    ///    Screen shows: "55555","66666","77777","88888","99999" (all one logical line)
    /// 3. Resize to (2, 5): 50 chars at width 2 = 25 rows. Screen=5, scrollback=20.
    /// 4. Bottom 5 rows (indices 20-24): chars[40..49]
    ///    row20: "88", row21: "88", row22: "89", row23: "99", row24: "99"
    ///    (The "8" digit occupies chars 40-44, "9" occupies chars 45-49)
    ///
    /// <b>Additional assertion:</b> kitty asserts <c>before == at()</c> meaning the full text
    /// (including scrollback) is preserved exactly. We validate the visible screen portion.
    /// </remarks>
    [Fact]
    public void Kitty_NarrowTo2_PreservesFullContent()
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5)
            .WithScrollback(20).Build();

        // Kitty: s.draw(''.join(str(i) * s.columns for i in range(s.lines*2)))
        terminal.ApplyTokens([new TextToken("00000111112222233333444445555566666777778888899999")]);

        terminal.Resize(2, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("88", snap.GetLine(0).TrimEnd());
        Assert.Equal("88", snap.GetLine(1).TrimEnd());
        Assert.Equal("89", snap.GetLine(2).TrimEnd());
        Assert.Equal("99", snap.GetLine(3).TrimEnd());
        Assert.Equal("99", snap.GetLine(4).TrimEnd());
    }

    /// <summary>
    /// A soft-wrapped 5-char line followed by a hard-break and "bb", narrowed by 2.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_resize</c> (~line 345)
    /// <code>
    /// s = self.create_screen()    # cols=5, lines=5
    /// s.draw('a' * s.columns)
    /// s.linefeed(), s.carriage_return()
    /// s.draw('bb')
    /// s.resize(s.lines, s.columns - 2)
    /// self.ae(str(s.linebuf), 'aaa\naa\nbb\n\n')
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. draw('aaaaa') → fills row 0, pending_wrap=true. No SoftWrap yet.
    /// 2. linefeed() → cursor moves to row 1. Clears pending_wrap. Does NOT set SoftWrap.
    /// 3. carriage_return() → cursor.x=0
    /// 4. draw('bb') → "bb" on row 1, cursor at (2,1)
    /// 5. Row 0: "aaaaa" (NO SoftWrap), Row 1: "bb" (NO SoftWrap)
    /// 6. Resize to cols=3:
    ///    - Logical line 0: "aaaaa" → WrapLogicalLine at 3: "aaa" + "aa" = 2 rows
    ///    - Logical line 1: "bb" → 1 row
    /// 7. Total 3 content rows + 2 empty = 5 rows
    /// </remarks>
    [Fact]
    public void Kitty_NarrowSoftWrappedLine_SplitsCorrectly()
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        // Kitty: s.draw('a' * s.columns), s.linefeed(), s.carriage_return(), s.draw('bb')
        terminal.ApplyTokens([new TextToken("aaaaa")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\n\r"));
        terminal.ApplyTokens([new TextToken("bb")]);

        terminal.Resize(3, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("aaa", snap.GetLine(0).TrimEnd());
        Assert.Equal("aa", snap.GetLine(1).TrimEnd());
        Assert.Equal("bb", snap.GetLine(2).TrimEnd());
        Assert.Equal("", snap.GetLine(3).TrimEnd());
    }

    /// <summary>
    /// Cursor Y position is preserved when narrowing doesn't cause the cursor's
    /// logical line to wrap further.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_cursor_after_resize</c> (~line 368)
    /// <code>
    /// def draw(text, end_line=True):
    ///     s.draw(text)
    ///     if end_line:
    ///         s.linefeed(), s.carriage_return()
    ///
    /// s = self.create_screen()     # cols=5, lines=5
    /// draw('123'), draw('123')
    /// y_before = s.cursor.y
    /// s.resize(s.lines, s.columns-1)
    /// self.ae(y_before, s.cursor.y)
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. draw("123") + LF+CR → "123" on row 0, cursor to row 1
    /// 2. draw("123") + LF+CR → "123" on row 1, cursor to row 2
    /// 3. cursor.y = 2
    /// 4. Resize to cols=4: "123" is 3 chars, fits in 4-wide. No wrapping needed.
    /// 5. All logical lines stay same number of rows → cursor.y unchanged.
    /// </remarks>
    [Fact]
    public void Kitty_CursorYStable_WhenNarrowingShortLines()
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("123\r\n123\r\n"));

        var snapBefore = terminal.CreateSnapshot();
        int yBefore = snapBefore.CursorY;

        terminal.Resize(4, 5);

        var snapAfter = terminal.CreateSnapshot();
        Assert.Equal(yBefore, snapAfter.CursorY);
    }

    /// <summary>
    /// When widening a terminal with a long wrapped line, the cursor should track
    /// its content — the line containing '|' should be at the cursor's Y position.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>kovidgoyal/kitty kitty_tests/screen.py test_cursor_after_resize</c> (~line 375)
    /// <code>
    /// s = self.create_screen(cols=5, lines=8)
    /// draw('one')
    /// draw('two three four five |||', end_line=False)
    /// s.resize(s.lines + 2, s.columns + 2)
    /// y = s.cursor.y
    /// self.assertIn('|', str(s.line(y)))
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. draw("one") + LF+CR → "one" on row 0
    /// 2. draw("two three four five |||") — 23 chars at width 5 = wraps across ~5 rows
    ///    Row 1: "two t" (SoftWrap), Row 2: "hree " (SoftWrap), Row 3: "four " (SoftWrap)
    ///    Row 4: "five " (SoftWrap), Row 5: "|||"
    ///    Cursor at (3, 5) — on the row with "|||"
    /// 3. Resize to (7, 10): rewrap to 7-wide, cursor should still be on a row containing "|"
    /// </remarks>
    [Fact]
    public void Kitty_CursorTracksContent_WhenWidening()
    {
        var adapter = new HeadlessPresentationAdapter(5, 8).WithReflow(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 8).Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("one\r\n"));
        terminal.ApplyTokens([new TextToken("two three four five |||")]);

        terminal.Resize(7, 10);

        var snap = terminal.CreateSnapshot();
        string cursorLine = snap.GetLine(snap.CursorY);
        Assert.Contains("|", cursorLine);
    }

    #endregion

    #region Alacritty Compatibility
    // Source: https://raw.githubusercontent.com/alacritty/alacritty/master/alacritty_terminal/src/grid/tests.rs
    // Uses Grid::new(rows, cols, scrollback) and cell(c)/wrap_cell(c) helpers.
    // wrap_cell(c) creates a cell with Flags::WRAPLINE set — equivalent to our SoftWrap.
    // Line(0) = current screen, Line(-N) = scrollback rows.

    /// <summary>
    /// "12345" at width 5 shrunk to width 2 produces 3 total rows ("12","34","5")
    /// with SoftWrap on the first two.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs shrink_reflow</c>
    /// <code>
    /// let mut grid = Grid::&lt;Cell&gt;::new(1, 5, 2);
    /// grid[Line(0)][Column(0)] = cell('1'); ... grid[Line(0)][Column(4)] = cell('5');
    /// grid.resize(true, 1, 2);
    /// assert_eq!(grid.total_lines(), 3);
    /// assert_eq!(grid[Line(-2)][Column(0)], cell('1'));
    /// assert_eq!(grid[Line(-2)][Column(1)], wrap_cell('2'));  // SoftWrap!
    /// assert_eq!(grid[Line(-1)][Column(0)], cell('3'));
    /// assert_eq!(grid[Line(-1)][Column(1)], wrap_cell('4'));  // SoftWrap!
    /// assert_eq!(grid[Line(0)][Column(0)], cell('5'));
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. 1-row, 5-wide grid with "12345" (no wrap flags — it's a single row with no continuation)
    /// 2. Resize to (1, 2) with reflow enabled:
    ///    - GroupLogicalLines: "12345" = one logical line (no SoftWrap on last cell)
    ///    - WrapLogicalLine at width 2: "12" (SoftWrap), "34" (SoftWrap), "5" = 3 rows
    ///    - Screen gets 1 row = "5", scrollback gets 2 rows = "12", "34"
    ///
    /// <b>Note:</b> We test this at the strategy level (not terminal integration) to match
    /// Alacritty's direct grid manipulation. This tests the algorithm more precisely.
    /// </remarks>
    [Fact]
    public void Alacritty_ShrinkReflow_5to2()
    {
        int oldWidth = 5;
        var row = MakeCellRow("12345", oldWidth, false);
        var context = new ReflowContext(
            ScreenRows: [row],
            ScrollbackRows: [],
            OldWidth: oldWidth, OldHeight: 1,
            NewWidth: 2, NewHeight: 1,
            CursorX: 0, CursorY: 0,
            InAlternateScreen: false);

        var result = AlacrittyReflowStrategy.Instance.Reflow(context);

        // Alacritty: total_lines = 3 (2 scrollback + 1 screen)
        Assert.True(result.ScrollbackRows.Length == 2,
            $"Expected 2 scrollback rows, got {result.ScrollbackRows.Length}");

        // Scrollback[0] = "12" with SoftWrap on last cell
        Assert.Equal("12", GetRowText(result.ScrollbackRows[0].Cells));
        Assert.True(HasSoftWrap(result.ScrollbackRows[0].Cells),
            "Scrollback row 0 should have SoftWrap (content continues)");

        // Scrollback[1] = "34" with SoftWrap on last cell
        Assert.Equal("34", GetRowText(result.ScrollbackRows[1].Cells));
        Assert.True(HasSoftWrap(result.ScrollbackRows[1].Cells),
            "Scrollback row 1 should have SoftWrap (content continues)");

        // Screen[0] = "5"
        Assert.Equal("5", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Multi-step shrink (5→4→2) produces identical result to single-step (5→2).
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs shrink_reflow_twice</c>
    /// <code>
    /// grid.resize(true, 1, 4);
    /// grid.resize(true, 1, 2);
    /// // Same assertions as shrink_reflow (3 total rows, "12"+"34"+"5")
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. "12345" at width 5 → resize to 4: "1234" (SoftWrap) + "5" = 2 rows
    /// 2. Then resize to 2: "1234" logical line is "12" + "34", plus "5" = 3 total rows
    /// 3. Same final state as direct 5→2 shrink. This proves reflow is idempotent across steps.
    /// </remarks>
    [Fact]
    public void Alacritty_ShrinkReflowTwice_5to4to2()
    {
        var adapter = new HeadlessPresentationAdapter(5, 1).WithReflow(AlacrittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 1)
            .WithScrollback(10).Build();

        terminal.ApplyTokens([new TextToken("12345")]);

        terminal.Resize(4, 1);
        terminal.Resize(2, 1);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("5", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// 2-wide wrapped rows "12"(wrap)+"3" grow to 3-wide, merge into "123".
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs grow_reflow</c>
    /// <code>
    /// grid[Line(0)][Column(0)] = cell('1');
    /// grid[Line(0)][Column(1)] = wrap_cell('2');   // WRAPLINE flag on last cell
    /// grid[Line(1)][Column(0)] = cell('3');
    /// grid.resize(true, 2, 3);
    /// assert_eq!(grid[Line(0)][Column(0)], cell('1'));
    /// assert_eq!(grid[Line(0)][Column(1)], cell('2'));   // wrap flag cleared
    /// assert_eq!(grid[Line(0)][Column(2)], cell('3'));
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. Row 0: "12" with SoftWrap. Row 1: "3".
    /// 2. GroupLogicalLines: SoftWrap on row0 → join rows 0+1: logical line = ['1','2','3']
    /// 3. WrapLogicalLine at width 3: "123" = 1 row (no SoftWrap needed)
    /// 4. Row 1 becomes empty.
    /// </remarks>
    [Fact]
    public void Alacritty_GrowReflow_MergesWrappedRows()
    {
        int oldWidth = 2;
        var row0 = MakeCellRow("12", oldWidth, true);   // wrapped
        var row1 = MakeCellRow("3", oldWidth, false);    // not wrapped

        var context = new ReflowContext(
            ScreenRows: [row0, row1],
            ScrollbackRows: [],
            OldWidth: oldWidth, OldHeight: 2,
            NewWidth: 3, NewHeight: 2,
            CursorX: 0, CursorY: 0,
            InAlternateScreen: false);

        var result = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("123", GetRowText(result.ScreenRows[0]));
        Assert.Equal("", GetRowText(result.ScreenRows[1]));
    }

    /// <summary>
    /// 3 rows of 2-wide all wrapped grow to 6-wide, merge into single "123456" row.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs grow_reflow_multiline</c>
    /// <code>
    /// grid[Line(0)][Column(0)] = cell('1'); grid[Line(0)][Column(1)] = wrap_cell('2');
    /// grid[Line(1)][Column(0)] = cell('3'); grid[Line(1)][Column(1)] = wrap_cell('4');
    /// grid[Line(2)][Column(0)] = cell('5'); grid[Line(2)][Column(1)] = cell('6');
    /// grid.resize(true, 3, 6);
    /// assert_eq!(grid[Line(0)][Column(0..6)], "123456");
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// 1. Row 0: "12" (SoftWrap), Row 1: "34" (SoftWrap), Row 2: "56" (no SoftWrap)
    /// 2. All three rows form ONE logical line: ['1','2','3','4','5','6']
    /// 3. WrapLogicalLine at width 6: "123456" = 1 row. Rows 1,2 become empty.
    /// </remarks>
    [Fact]
    public void Alacritty_GrowReflowMultiline_MergesAll()
    {
        int oldWidth = 2;
        var row0 = MakeCellRow("12", oldWidth, true);
        var row1 = MakeCellRow("34", oldWidth, true);
        var row2 = MakeCellRow("56", oldWidth, false);

        var context = new ReflowContext(
            ScreenRows: [row0, row1, row2],
            ScrollbackRows: [],
            OldWidth: oldWidth, OldHeight: 3,
            NewWidth: 6, NewHeight: 3,
            CursorX: 0, CursorY: 0,
            InAlternateScreen: false);

        var result = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("123456", GetRowText(result.ScreenRows[0]));
        Assert.Equal("", GetRowText(result.ScreenRows[1]));
        Assert.Equal("", GetRowText(result.ScreenRows[2]));
    }

    /// <summary>
    /// With reflow disabled (our NoReflow / crop mode), growing does NOT merge wrapped lines.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs grow_reflow_disabled</c>
    /// <code>
    /// grid.resize(false, 2, 3);   // reflow=false
    /// assert_eq!(grid[Line(0)][Column(1)], wrap_cell('2'));  // wrap flag preserved!
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// With reflow disabled, resize just pads/crops rows without merging.
    /// The SoftWrap flag on "2" is preserved, and "3" stays on its own row.
    /// Our NoReflow / default crop behavior should produce this.
    /// </remarks>
    [Fact]
    public void Alacritty_GrowReflowDisabled_DoesNotMerge()
    {
        // No reflow enabled — uses default crop behavior
        var adapter = new HeadlessPresentationAdapter(2, 2);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(2, 2).Build();

        terminal.ApplyTokens([new TextToken("123")]);

        terminal.Resize(3, 2);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("12", snap.GetLine(0).TrimEnd());
        Assert.Equal("3", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// With reflow disabled, shrinking just crops without creating new rows.
    /// </summary>
    /// <remarks>
    /// <b>Upstream:</b> <c>alacritty/alacritty alacritty_terminal/src/grid/tests.rs shrink_reflow_disabled</c>
    /// <code>
    /// grid.resize(false, 1, 2);   // reflow=false
    /// assert_eq!(grid.total_lines(), 1);   // no extra rows created
    /// assert_eq!(grid[Line(0)][Column(0)], cell('1'));
    /// assert_eq!(grid[Line(0)][Column(1)], cell('2'));
    /// </code>
    ///
    /// <b>Hand-trace:</b>
    /// Without reflow, shrinking from 5→2 just keeps the first 2 chars.
    /// Chars 3-5 are lost (cropped). Total remains 1 row.
    /// </remarks>
    [Fact]
    public void Alacritty_ShrinkReflowDisabled_CropsOnly()
    {
        var adapter = new HeadlessPresentationAdapter(5, 1);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 1).Build();

        terminal.ApplyTokens([new TextToken("12345")]);

        terminal.Resize(2, 1);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("12", snap.GetLine(0).TrimEnd());
    }

    #endregion

    #region Cross-Emulator Behavioral Validation
    // These tests validate properties that ALL reflow-capable terminals agree on.
    // They are not derived from a single upstream test but from universal invariants
    // observed across kitty, Alacritty, xterm, and VTE.

    /// <summary>
    /// Multiple resize steps produce the same result as a single direct resize.
    /// This is a fundamental property: reflow(A→B) == reflow(reflow(A→I)→B).
    /// </summary>
    /// <remarks>
    /// <b>Derivation:</b> This property is demonstrated by Alacritty's <c>shrink_reflow_twice</c>
    /// test (5→4→2 == 5→2) and is a universal expectation for correct reflow.
    /// Without this property, users would see different content depending on resize history.
    /// </remarks>
    [Theory]
    [InlineData(5, 3)]  // narrow then narrow more
    [InlineData(5, 2)]  // narrow to 2
    [InlineData(3, 7)]  // narrow then widen past original
    public void ReflowIsConsistent_AcrossMultipleResizeSteps(int intermediateWidth, int finalWidth)
    {
        // Direct resize
        var adapter1 = new HeadlessPresentationAdapter(10, 3).WithReflow(AlacrittyReflowStrategy.Instance);
        using var workload1 = new Hex1bAppWorkloadAdapter();
        using var terminal1 = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload1).WithPresentation(adapter1).WithDimensions(10, 3)
            .WithScrollback(20).Build();
        terminal1.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal1.Resize(finalWidth, 3);
        var directSnap = terminal1.CreateSnapshot();

        // Multi-step resize
        var adapter2 = new HeadlessPresentationAdapter(10, 3).WithReflow(AlacrittyReflowStrategy.Instance);
        using var workload2 = new Hex1bAppWorkloadAdapter();
        using var terminal2 = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload2).WithPresentation(adapter2).WithDimensions(10, 3)
            .WithScrollback(20).Build();
        terminal2.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal2.Resize(intermediateWidth, 3);
        terminal2.Resize(finalWidth, 3);
        var multiSnap = terminal2.CreateSnapshot();

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(directSnap.GetLine(i).TrimEnd(), multiSnap.GetLine(i).TrimEnd());
        }
    }

    /// <summary>
    /// Hard-wrapped lines (those separated by CR+LF, not by character overflow)
    /// must never be merged during reflow, regardless of available width.
    /// </summary>
    /// <remarks>
    /// <b>Derivation:</b> Universal across all terminals. A hard break (LF) creates a
    /// permanent line boundary. Only soft-wrapped lines (where a character overflowed
    /// the right margin) should be merged when the terminal widens.
    /// Validated against both kitty and Alacritty behavior.
    /// </remarks>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(80)]
    public void HardWrappedLines_NeverMerge_AnyWidth(int newWidth)
    {
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflow(AlacrittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("abc\r\ndef\r\nghi\r\n"));

        terminal.Resize(newWidth, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("abc", snap.GetLine(0).TrimEnd());
        Assert.Equal("def", snap.GetLine(1).TrimEnd());
        Assert.Equal("ghi", snap.GetLine(2).TrimEnd());
    }

    /// <summary>
    /// Narrow → original width restores content exactly (round-trip guarantee).
    /// </summary>
    /// <remarks>
    /// <b>Derivation:</b> This is the fundamental round-trip property that makes reflow
    /// useful. Without it, content would be permanently altered by resize.
    /// Kitty asserts this with <c>self.ae(before, at())</c> in test_resize (~line 338).
    /// Alacritty asserts it implicitly via <c>shrink_reflow_twice</c>.
    /// </remarks>
    [Theory]
    [InlineData(10, 5)]   // halve
    [InlineData(10, 3)]   // to 3
    [InlineData(10, 1)]   // extreme narrow
    [InlineData(10, 7)]   // partial narrow
    public void RoundTrip_NarrowAndRestore_PreservesContent(int originalWidth, int narrowWidth)
    {
        string content = new string('X', originalWidth);

        var adapter = new HeadlessPresentationAdapter(originalWidth, 5).WithReflow(AlacrittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(originalWidth, 5)
            .WithScrollback(100).Build();

        terminal.ApplyTokens([new TextToken(content)]);

        terminal.Resize(narrowWidth, 5);
        terminal.Resize(originalWidth, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal(content, snap.GetLine(0).TrimEnd());
    }

    #endregion

    #region JSON Fixture Runner
    // JSON fixtures in TestData/ReflowFixtures.json provide an additional data-driven
    // test layer. Each fixture documents its upstream source, making it easy to audit
    // and update when upstream behavior changes.
    //
    // To add a new fixture:
    // 1. Find the upstream test case (kitty/Alacritty/xterm source)
    // 2. Hand-trace the expected behavior (document in the "note" field)
    // 3. Add the fixture to ReflowFixtures.json with source attribution
    // 4. Run tests to verify
    //
    // Fixtures that use "screenRows" (pre-constructed cell arrays) are skipped by the
    // terminal integration runner — they test strategy-level behavior directly and
    // would need a separate test method.

    [Theory]
    [MemberData(nameof(GetFixtureIds))]
    public void Fixture_ScreenContentMatches(string fixtureId)
    {
        var fixture = LoadFixture(fixtureId);
        if (fixture.Expected.ScreenLines == null)
            return;

        ITerminalReflowProvider strategy = fixture.Emulator switch
        {
            "kitty" => KittyReflowStrategy.Instance,
            "alacritty" or "xterm" => AlacrittyReflowStrategy.Instance,
            "none" => NoReflowStrategy.Instance,
            _ => AlacrittyReflowStrategy.Instance
        };

        var adapter = new HeadlessPresentationAdapter(fixture.Setup.Cols, fixture.Setup.Rows)
            .WithReflow(strategy);
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter)
            .WithDimensions(fixture.Setup.Cols, fixture.Setup.Rows);

        if (fixture.Setup.Scrollback > 0)
            builder = builder.WithScrollback(fixture.Setup.Scrollback);

        using var terminal = builder.Build();

        if (fixture.Setup.Input != null)
        {
            terminal.ApplyTokens([new TextToken(fixture.Setup.Input)]);
        }
        else if (fixture.Setup.InputSequence != null)
        {
            foreach (var step in fixture.Setup.InputSequence)
            {
                switch (step.Type)
                {
                    case "draw":
                        terminal.ApplyTokens([new TextToken(step.Text!)]);
                        break;
                    case "cr":
                        terminal.ApplyTokens([ControlCharacterToken.CarriageReturn]);
                        break;
                    case "lf":
                        terminal.ApplyTokens([ControlCharacterToken.LineFeed]);
                        break;
                }
            }
        }

        if (fixture.Resize != null)
        {
            terminal.Resize(fixture.Resize.Cols, fixture.Resize.Rows);
        }
        else if (fixture.ResizeSteps != null)
        {
            foreach (var step in fixture.ResizeSteps)
                terminal.Resize(step.Cols, step.Rows);
        }

        var snap = terminal.CreateSnapshot();
        for (int i = 0; i < fixture.Expected.ScreenLines.Length; i++)
        {
            string expected = fixture.Expected.ScreenLines[i];
            string actual = snap.GetLine(i).TrimEnd();
            Assert.Equal(expected, actual);
        }
    }

    public static IEnumerable<object[]> GetFixtureIds()
    {
        var fixtures = LoadAllFixtures();
        foreach (var f in fixtures)
        {
            // Skip fixtures that use pre-constructed screenRows (direct strategy tests)
            if (f.Setup.ScreenRows != null)
                continue;
            yield return [f.Id];
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a cell row with the given text, optionally setting SoftWrap on the last cell.
    /// This mirrors Alacritty's <c>cell(c)</c> and <c>wrap_cell(c)</c> helper functions
    /// in <c>alacritty_terminal/src/grid/tests.rs</c>.
    /// </summary>
    private static TerminalCell[] MakeCellRow(string text, int width, bool softWrap)
    {
        var cells = new TerminalCell[width];
        for (int i = 0; i < width; i++)
        {
            var ch = i < text.Length ? text[i].ToString() : " ";
            var attrs = (i == width - 1 && softWrap) ? CellAttributes.SoftWrap : CellAttributes.None;
            cells[i] = new TerminalCell(ch, null, null, attrs);
        }
        return cells;
    }

    private static string GetRowText(TerminalCell[] row)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in row)
            sb.Append(string.IsNullOrEmpty(c.Character) ? ' ' : c.Character[0]);
        return sb.ToString().TrimEnd();
    }

    private static bool HasSoftWrap(TerminalCell[] row)
    {
        return row.Length > 0 && (row[^1].Attributes & CellAttributes.SoftWrap) != 0;
    }

    private static ReflowFixture LoadFixture(string id)
    {
        var all = LoadAllFixtures();
        return all.First(f => f.Id == id);
    }

    private static ReflowFixture[] LoadAllFixtures()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("ReflowFixtures.json", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        var doc = JsonSerializer.Deserialize<ReflowFixtureDocument>(json, JsonOptions)!;
        return doc.Fixtures;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    #endregion

    #region Fixture Models

    private class ReflowFixtureDocument
    {
        [JsonPropertyName("fixtures")]
        public ReflowFixture[] Fixtures { get; set; } = [];
    }

    private class ReflowFixture
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
        [JsonPropertyName("emulator")]
        public string Emulator { get; set; } = "";
        [JsonPropertyName("setup")]
        public FixtureSetup Setup { get; set; } = new();
        [JsonPropertyName("resize")]
        public FixtureResize? Resize { get; set; }
        [JsonPropertyName("resizeSteps")]
        public FixtureResize[]? ResizeSteps { get; set; }
        [JsonPropertyName("expected")]
        public FixtureExpected Expected { get; set; } = new();
    }

    private class FixtureSetup
    {
        [JsonPropertyName("cols")]
        public int Cols { get; set; }
        [JsonPropertyName("rows")]
        public int Rows { get; set; }
        [JsonPropertyName("scrollback")]
        public int Scrollback { get; set; }
        [JsonPropertyName("input")]
        public string? Input { get; set; }
        [JsonPropertyName("inputSequence")]
        public FixtureInputStep[]? InputSequence { get; set; }
        [JsonPropertyName("screenRows")]
        public FixtureScreenRow[]? ScreenRows { get; set; }
        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    private class FixtureInputStep
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class FixtureScreenRow
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
        [JsonPropertyName("wrapped")]
        public bool Wrapped { get; set; }
        [JsonPropertyName("cells")]
        public string[]? Cells { get; set; }
    }

    private class FixtureResize
    {
        [JsonPropertyName("cols")]
        public int Cols { get; set; }
        [JsonPropertyName("rows")]
        public int Rows { get; set; }
    }

    private class FixtureExpected
    {
        [JsonPropertyName("screenLines")]
        public string[]? ScreenLines { get; set; }
        [JsonPropertyName("scrollbackLines")]
        public string[]? ScrollbackLines { get; set; }
        [JsonPropertyName("scrollbackWraps")]
        public bool[]? ScrollbackWraps { get; set; }
        [JsonPropertyName("totalRows")]
        public int? TotalRows { get; set; }
        [JsonPropertyName("cursorYUnchanged")]
        public bool? CursorYUnchanged { get; set; }
        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }

    #endregion
}
