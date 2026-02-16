using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Comprehensive per-terminal reflow strategy test matrix. Each terminal emulator's strategy
/// is tested for its specific behavioral characteristics:
/// <list type="bullet">
///   <item><b>Bottom-fill (preserveCursorRow: false):</b> Alacritty, Windows Terminal</item>
///   <item><b>Cursor-anchored, no saved cursor:</b> Kitty, WezTerm</item>
///   <item><b>Cursor-anchored + saved cursor reflow:</b> VTE, Ghostty, Foot</item>
///   <item><b>No reflow (crop only):</b> xterm, iTerm2</item>
/// </list>
/// </summary>
/// <remarks>
/// Tests verify: soft-wrap merge/split, hard-wrap preservation, alternate screen bypass,
/// round-trip stability, cursor tracking, bottom-fill vs cursor-anchored positioning,
/// and saved cursor (DECSC) reflow behavior.
/// </remarks>
public class PerTerminalStrategyTests
{
    #region Helper Methods

    /// <summary>
    /// Creates a row of terminal cells from a string, with optional soft-wrap on last cell.
    /// </summary>
    private static TerminalCell[] MakeRow(string text, int width, bool softWrap = false)
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

    /// <summary>
    /// Extracts visible text from a row, trimming trailing spaces.
    /// </summary>
    private static string GetRowText(TerminalCell[] row)
    {
        var chars = row.Select(c => c.Character ?? " ").ToArray();
        return string.Concat(chars).TrimEnd();
    }

    /// <summary>
    /// Creates a terminal with the specified strategy, dimensions, and scrollback capacity.
    /// </summary>
    private static Hex1bTerminal CreateTerminal(ITerminalReflowProvider strategy, int cols, int rows, int scrollback = 0)
    {
        var adapter = new HeadlessPresentationAdapter(cols, rows).WithReflow(strategy);
        using var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(cols, rows);
        if (scrollback > 0)
            builder = builder.WithScrollback(scrollback);
        return builder.Build();
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Alacritty — Bottom-fill reflow, no saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Alacritty merges soft-wrapped rows when the terminal is widened.
    /// </summary>
    [Fact]
    public void Alacritty_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(AlacrittyReflowStrategy.Instance, 5, 3);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Alacritty splits long lines when the terminal is narrowed.
    /// </summary>
    [Fact]
    public void Alacritty_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(AlacrittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Alacritty does not merge hard-wrapped (newline-separated) lines.
    /// </summary>
    [Fact]
    public void Alacritty_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(AlacrittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Alacritty does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void Alacritty_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Alacritty round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void Alacritty_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(AlacrittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Alacritty tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void Alacritty_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(AlacrittyReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGH")]);

        var snap1 = terminal.CreateSnapshot();
        Assert.Equal(3, snap1.CursorX);
        Assert.Equal(1, snap1.CursorY);

        terminal.Resize(10, 5);

        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGH", snap2.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Alacritty uses bottom-fill: when narrowing pushes content down, the screen
    /// shows the bottom of the buffer and cursor shifts upward.
    /// </summary>
    [Fact]
    public void Alacritty_BottomFill_ContentPushedUp()
    {
        int width = 10, height = 3;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width),
        };

        // Cursor at row 1, col 3. Narrow to 5: 20-char line A → 4 rows, line B → 2 rows = 6 total.
        // Bottom-fill: screen shows rows 3-5, cursor maps to row 0 or higher.
        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 1, false);

        var result = AlacrittyReflowStrategy.Instance.Reflow(context);

        // Bottom-fill: cursor Y should be pushed toward the top of visible screen
        Assert.True(result.CursorY <= 1,
            $"Bottom-fill should push cursor toward top; got CursorY={result.CursorY}");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Windows Terminal — Bottom-fill reflow, no saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Windows Terminal merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void WindowsTerminal_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(WindowsTerminalReflowStrategy.Instance, 5, 3);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Windows Terminal splits rows when narrowed.
    /// </summary>
    [Fact]
    public void WindowsTerminal_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(WindowsTerminalReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Windows Terminal does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void WindowsTerminal_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(WindowsTerminalReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Windows Terminal does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void WindowsTerminal_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = WindowsTerminalReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Windows Terminal round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void WindowsTerminal_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(WindowsTerminalReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Windows Terminal tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void WindowsTerminal_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(WindowsTerminalReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGH")]);

        var snap1 = terminal.CreateSnapshot();
        Assert.Equal(3, snap1.CursorX);
        Assert.Equal(1, snap1.CursorY);

        terminal.Resize(10, 5);

        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGH", snap2.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Windows Terminal uses bottom-fill: when narrowing pushes content down, the
    /// screen shows the bottom and cursor shifts upward.
    /// </summary>
    [Fact]
    public void WindowsTerminal_BottomFill_ContentPushedUp()
    {
        int width = 10, height = 3;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 1, false);

        var result = WindowsTerminalReflowStrategy.Instance.Reflow(context);

        Assert.True(result.CursorY <= 1,
            $"Bottom-fill should push cursor toward top; got CursorY={result.CursorY}");
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Kitty — Cursor-anchored reflow, no saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kitty merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void Kitty_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(KittyReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Kitty splits rows when narrowed.
    /// </summary>
    [Fact]
    public void Kitty_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(KittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Kitty does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void Kitty_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(KittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Kitty does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void Kitty_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = KittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Kitty round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void Kitty_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(KittyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Kitty tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void Kitty_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(KittyReflowStrategy.Instance, 5, 5, scrollback: 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // cursor on 'F' at (0,1)

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Kitty anchors the cursor to its visual row: the cursor stays at the same
    /// screen row position rather than shifting to the bottom.
    /// </summary>
    [Fact]
    public void Kitty_CursorAnchored_StaysOnVisualRow()
    {
        int width = 10, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width, softWrap: true),
            MakeRow("BBBBBBBBBB", width),
            MakeRow("CCCCCCCCCC", width),
        };

        // Cursor at row 2, col 3 (middle of screen).
        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 2, false);

        var kittyResult = KittyReflowStrategy.Instance.Reflow(context);
        var alacrittyResult = AlacrittyReflowStrategy.Instance.Reflow(context);

        // Kitty anchors: cursor stays at visual row 2
        Assert.Equal(2, kittyResult.CursorY);
        // Alacritty bottom-fills: cursor shifts differently
        Assert.NotEqual(kittyResult.CursorY, alacrittyResult.CursorY);
    }

    /// <summary>
    /// Kitty does NOT reflow the saved cursor (DECSC) — returns null.
    /// </summary>
    [Fact]
    public void Kitty_SavedCursor_NotReflowed()
    {
        int width = 5, height = 5;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 2, SavedCursorY: 0);

        var result = KittyReflowStrategy.Instance.Reflow(context);

        Assert.Null(result.NewSavedCursorX);
        Assert.Null(result.NewSavedCursorY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region WezTerm — Cursor-anchored reflow, no saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// WezTerm merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void WezTerm_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(WezTermReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// WezTerm splits rows when narrowed.
    /// </summary>
    [Fact]
    public void WezTerm_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(WezTermReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// WezTerm does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void WezTerm_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(WezTermReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// WezTerm does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void WezTerm_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = WezTermReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// WezTerm round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void WezTerm_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(WezTermReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// WezTerm tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void WezTerm_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(WezTermReflowStrategy.Instance, 5, 5, scrollback: 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // cursor on 'F' at (0,1)

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// WezTerm anchors the cursor to its visual row (same as Kitty behavior).
    /// </summary>
    [Fact]
    public void WezTerm_CursorAnchored_StaysOnVisualRow()
    {
        int width = 10, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width, softWrap: true),
            MakeRow("BBBBBBBBBB", width),
            MakeRow("CCCCCCCCCC", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 2, false);

        var wezResult = WezTermReflowStrategy.Instance.Reflow(context);
        var alacrittyResult = AlacrittyReflowStrategy.Instance.Reflow(context);

        // WezTerm anchors: cursor stays at visual row 2
        Assert.Equal(2, wezResult.CursorY);
        Assert.NotEqual(wezResult.CursorY, alacrittyResult.CursorY);
    }

    /// <summary>
    /// WezTerm does NOT reflow the saved cursor (DECSC) — returns null.
    /// </summary>
    [Fact]
    public void WezTerm_SavedCursor_NotReflowed()
    {
        int width = 5, height = 5;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 2, SavedCursorY: 0);

        var result = WezTermReflowStrategy.Instance.Reflow(context);

        Assert.Null(result.NewSavedCursorX);
        Assert.Null(result.NewSavedCursorY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region VTE — Cursor-anchored reflow + saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// VTE merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void Vte_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// VTE splits rows when narrowed.
    /// </summary>
    [Fact]
    public void Vte_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// VTE does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void Vte_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// VTE does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void Vte_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = VteReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// VTE round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void Vte_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// VTE tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void Vte_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(VteReflowStrategy.Instance, 5, 5, scrollback: 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // cursor on 'F' at (0,1)

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// VTE anchors the cursor to its visual row.
    /// </summary>
    [Fact]
    public void Vte_CursorAnchored_StaysOnVisualRow()
    {
        int width = 10, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width, softWrap: true),
            MakeRow("BBBBBBBBBB", width),
            MakeRow("CCCCCCCCCC", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 2, false);

        var vteResult = VteReflowStrategy.Instance.Reflow(context);
        var alacrittyResult = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, vteResult.CursorY);
        Assert.NotEqual(vteResult.CursorY, alacrittyResult.CursorY);
    }

    /// <summary>
    /// VTE reflowed the saved cursor (DECSC) position on resize — it tracks to the
    /// same character position after reflow.
    /// </summary>
    [Fact]
    public void Vte_SavedCursor_ReflowedOnResize()
    {
        int width = 5, height = 5;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 0, SavedCursorY: 1);

        var result = VteReflowStrategy.Instance.Reflow(context);

        Assert.NotNull(result.NewSavedCursorX);
        Assert.NotNull(result.NewSavedCursorY);
        // 'F' was at (0,1) in width-5, maps to (5,0) in width-10
        Assert.Equal(5, result.NewSavedCursorX!.Value);
        Assert.Equal(0, result.NewSavedCursorY!.Value);
    }

    /// <summary>
    /// VTE saved cursor tracks to the correct character position after narrowing.
    /// </summary>
    [Fact]
    public void Vte_SavedCursor_TracksCharacterPosition()
    {
        int width = 10, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDEFGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        // Saved cursor at (7,0) = 'H'. Narrow to width 5: offset 7 → row=1, col=2.
        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            0, 0, false,
            SavedCursorX: 7, SavedCursorY: 0);

        var result = VteReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, result.NewSavedCursorX);
        Assert.Equal(1, result.NewSavedCursorY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Ghostty — Cursor-anchored reflow + saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ghostty merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void Ghostty_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Ghostty splits rows when narrowed.
    /// </summary>
    [Fact]
    public void Ghostty_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Ghostty does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void Ghostty_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Ghostty does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void Ghostty_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = GhosttyReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Ghostty round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void Ghostty_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Ghostty tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void Ghostty_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(GhosttyReflowStrategy.Instance, 5, 5, scrollback: 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // cursor on 'F' at (0,1)

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Ghostty anchors the cursor to its visual row.
    /// </summary>
    [Fact]
    public void Ghostty_CursorAnchored_StaysOnVisualRow()
    {
        int width = 10, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width, softWrap: true),
            MakeRow("BBBBBBBBBB", width),
            MakeRow("CCCCCCCCCC", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 2, false);

        var ghosttyResult = GhosttyReflowStrategy.Instance.Reflow(context);
        var alacrittyResult = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, ghosttyResult.CursorY);
        Assert.NotEqual(ghosttyResult.CursorY, alacrittyResult.CursorY);
    }

    /// <summary>
    /// Ghostty reflowed the saved cursor (DECSC) position on resize.
    /// </summary>
    [Fact]
    public void Ghostty_SavedCursor_ReflowedOnResize()
    {
        int width = 5, height = 5;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 0, SavedCursorY: 1);

        var result = GhosttyReflowStrategy.Instance.Reflow(context);

        Assert.NotNull(result.NewSavedCursorX);
        Assert.NotNull(result.NewSavedCursorY);
        Assert.Equal(5, result.NewSavedCursorX!.Value);
        Assert.Equal(0, result.NewSavedCursorY!.Value);
    }

    /// <summary>
    /// Ghostty saved cursor tracks to the correct character position after narrowing.
    /// </summary>
    [Fact]
    public void Ghostty_SavedCursor_TracksCharacterPosition()
    {
        int width = 10, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDEFGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            0, 0, false,
            SavedCursorX: 7, SavedCursorY: 0);

        var result = GhosttyReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, result.NewSavedCursorX);
        Assert.Equal(1, result.NewSavedCursorY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Foot — Cursor-anchored reflow + saved cursor
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Foot merges soft-wrapped rows when widened.
    /// </summary>
    [Fact]
    public void Foot_NarrowToWider_MergesSoftWrappedRows()
    {
        using var terminal = CreateTerminal(FootReflowStrategy.Instance, 5, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Foot splits rows when narrowed.
    /// </summary>
    [Fact]
    public void Foot_WiderToNarrow_SplitsRows()
    {
        using var terminal = CreateTerminal(FootReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Foot does not merge hard-wrapped lines.
    /// </summary>
    [Fact]
    public void Foot_HardWrappedLines_NotMerged()
    {
        using var terminal = CreateTerminal(FootReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// Foot does NOT reflow content in the alternate screen buffer.
    /// </summary>
    [Fact]
    public void Foot_AlternateScreen_NoReflow()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, InAlternateScreen: true);

        var result = FootReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
    }

    /// <summary>
    /// Foot round-trips: narrow then widen restores original content.
    /// </summary>
    [Fact]
    public void Foot_RoundTrip_NarrowAndRestore()
    {
        using var terminal = CreateTerminal(FootReflowStrategy.Instance, 10, 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 5);
        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
    }

    /// <summary>
    /// Foot tracks cursor position correctly through reflow.
    /// </summary>
    [Fact]
    public void Foot_CursorPosition_TrackedCorrectly()
    {
        using var terminal = CreateTerminal(FootReflowStrategy.Instance, 5, 5, scrollback: 5);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // cursor on 'F' at (0,1)

        terminal.Resize(10, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap.GetLine(0).TrimEnd());
        Assert.Equal(5, snap.CursorX);
        Assert.Equal(0, snap.CursorY);
    }

    /// <summary>
    /// Foot anchors the cursor to its visual row.
    /// </summary>
    [Fact]
    public void Foot_CursorAnchored_StaysOnVisualRow()
    {
        int width = 10, height = 5;

        var screen = new TerminalCell[][] {
            MakeRow("AAAAAAAAAA", width, softWrap: true),
            MakeRow("AAAAAAAAAA", width),
            MakeRow("BBBBBBBBBB", width, softWrap: true),
            MakeRow("BBBBBBBBBB", width),
            MakeRow("CCCCCCCCCC", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            3, 2, false);

        var footResult = FootReflowStrategy.Instance.Reflow(context);
        var alacrittyResult = AlacrittyReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, footResult.CursorY);
        Assert.NotEqual(footResult.CursorY, alacrittyResult.CursorY);
    }

    /// <summary>
    /// Foot reflowed the saved cursor (DECSC) position on resize.
    /// </summary>
    [Fact]
    public void Foot_SavedCursor_ReflowedOnResize()
    {
        int width = 5, height = 5;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false,
            SavedCursorX: 0, SavedCursorY: 1);

        var result = FootReflowStrategy.Instance.Reflow(context);

        Assert.NotNull(result.NewSavedCursorX);
        Assert.NotNull(result.NewSavedCursorY);
        Assert.Equal(5, result.NewSavedCursorX!.Value);
        Assert.Equal(0, result.NewSavedCursorY!.Value);
    }

    /// <summary>
    /// Foot saved cursor tracks to the correct character position after narrowing.
    /// </summary>
    [Fact]
    public void Foot_SavedCursor_TracksCharacterPosition()
    {
        int width = 10, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDEFGHIJ", width),
            MakeRow("", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 5, height,
            0, 0, false,
            SavedCursorX: 7, SavedCursorY: 0);

        var result = FootReflowStrategy.Instance.Reflow(context);

        Assert.Equal(2, result.NewSavedCursorX);
        Assert.Equal(1, result.NewSavedCursorY);
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region Xterm — No reflow (crop only)
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Xterm does NOT reflow: content is cropped when narrowed, not re-wrapped.
    /// </summary>
    [Fact]
    public void Xterm_NoReflow_ContentCropped()
    {
        using var terminal = CreateTerminal(XtermReflowStrategy.Instance, 10, 3);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd()); // No overflow — content is cropped
    }

    /// <summary>
    /// Xterm ignores SoftWrap flags — they are not processed for reflow.
    /// </summary>
    [Fact]
    public void Xterm_NoReflow_SoftWrapIgnored()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false);

        var result = XtermReflowStrategy.Instance.Reflow(context);

        // Soft-wrapped rows should NOT be merged — xterm does not reflow
        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
        Assert.Equal("FGHIJ", GetRowText(result.ScreenRows[1]));
    }

    #endregion

    // ════════════════════════════════════════════════════════════════════════════
    #region iTerm2 — No reflow (crop only)
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// iTerm2 does NOT reflow: content is cropped when narrowed, not re-wrapped.
    /// </summary>
    [Fact]
    public void ITerm2_NoReflow_ContentCropped()
    {
        using var terminal = CreateTerminal(ITerm2ReflowStrategy.Instance, 10, 3);
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        terminal.Resize(5, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    /// <summary>
    /// iTerm2 ignores SoftWrap flags — they are not processed for reflow.
    /// </summary>
    [Fact]
    public void ITerm2_NoReflow_SoftWrapIgnored()
    {
        int width = 5, height = 3;
        var screen = new TerminalCell[][] {
            MakeRow("ABCDE", width, softWrap: true),
            MakeRow("FGHIJ", width),
            MakeRow("", width),
        };

        var context = new ReflowContext(
            screen, Array.Empty<ReflowScrollbackRow>(),
            width, height, 10, height,
            0, 1, false);

        var result = ITerm2ReflowStrategy.Instance.Reflow(context);

        Assert.Equal("ABCDE", GetRowText(result.ScreenRows[0]));
        Assert.Equal("FGHIJ", GetRowText(result.ScreenRows[1]));
    }

    #endregion
}
