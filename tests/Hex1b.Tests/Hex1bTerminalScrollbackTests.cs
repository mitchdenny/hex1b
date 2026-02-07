using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for terminal scrollback buffer support.
/// </summary>
public class Hex1bTerminalScrollbackTests
{
    private static Hex1bTerminal CreateTerminal(int width = 10, int height = 5, int scrollbackCapacity = 100, Action<ScrollbackRowEventArgs>? callback = null)
    {
        var workload = new Hex1bAppWorkloadAdapter();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(width, height)
            .WithScrollback(scrollbackCapacity, callback);
        return builder.Build();
    }

    [Fact]
    public void ScrollUp_CapturesRowInScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Fill 3 rows and then force a scroll by adding a 4th line
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Line1\r\nLine2\r\nLine3\r\nLine4"));

        Assert.NotNull(terminal.Scrollback);
        Assert.Equal(1, terminal.Scrollback.Count);

        var lines = terminal.Scrollback.GetLines(1);
        Assert.Equal("L", lines[0].Cells[0].Character);
        Assert.Equal("i", lines[0].Cells[1].Character);
        Assert.Equal("n", lines[0].Cells[2].Character);
        Assert.Equal("e", lines[0].Cells[3].Character);
        Assert.Equal("1", lines[0].Cells[4].Character);
    }

    [Fact]
    public void ScrollUp_MultipleRows_CapturesAllInOrder()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write 5 lines into a 3-row terminal -> 2 rows scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD\r\nEEE"));

        Assert.Equal(2, terminal.Scrollback!.Count);

        var lines = terminal.Scrollback.GetLines(2);
        Assert.Equal("A", lines[0].Cells[0].Character);
        Assert.Equal("B", lines[1].Cells[0].Character);
    }

    [Fact]
    public void ScrollUp_InAlternateScreen_DoesNotCaptureScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Enter alternate screen, then scroll
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.Equal(0, terminal.Scrollback!.Count);
    }

    [Fact]
    public void ScrollUp_ResumesAfterExitingAlternateScreen()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write some lines that scroll off in normal mode
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("X\r\nY\r\nZ\r\nW"));
        Assert.Equal(1, terminal.Scrollback!.Count);

        // Enter alternate screen — no scrollback
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD"));
        Assert.Equal(1, terminal.Scrollback.Count);

        // Exit alternate screen — scrollback resumes
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        // Original content restored (X, Y, Z, W from before) — scroll more
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n\r\n\r\nQ"));
        Assert.True(terminal.Scrollback.Count > 1);
    }

    [Fact]
    public void NoScrollback_WhenNotConfigured()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(10, 3)
            .Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.Null(terminal.Scrollback);
    }

    [Fact]
    public void CSI3J_ClearsScrollbackBuffer()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Scroll some lines off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));
        Assert.True(terminal.Scrollback!.Count > 0);

        // Send CSI 3J (Erase Display with param 3)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3J"));
        Assert.Equal(0, terminal.Scrollback.Count);
    }

    [Fact]
    public void Callback_InvokedWhenRowScrollsOff()
    {
        var callbackRows = new List<ScrollbackRowEventArgs>();
        using var terminal = CreateTerminal(
            width: 10, height: 3,
            callback: args => callbackRows.Add(args));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD"));

        Assert.Single(callbackRows);
        Assert.Equal(10, callbackRows[0].OriginalWidth);
        Assert.Same(terminal, callbackRows[0].Terminal);
        Assert.Equal("A", callbackRows[0].Cells.Span[0].Character);
    }

    [Fact]
    public void Callback_ReceivesMultipleRows()
    {
        var callbackRows = new List<ScrollbackRowEventArgs>();
        using var terminal = CreateTerminal(
            width: 10, height: 3,
            callback: args => callbackRows.Add(args));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.Equal(2, callbackRows.Count);
        Assert.Equal("A", callbackRows[0].Cells.Span[0].Character);
        Assert.Equal("B", callbackRows[1].Cells.Span[0].Character);
    }

    [Fact]
    public void Snapshot_WithScrollbackLines_IncludesHistory()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write enough to push 2 rows into scrollback
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD\r\nEEE"));

        // Snapshot with scrollback
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 2);

        // Total height = 2 scrollback + 3 visible
        Assert.Equal(5, snapshot.Height);
        Assert.Equal(2, snapshot.ScrollbackLineCount);

        // First two rows should be scrollback (AAA, BBB)
        Assert.Equal("A", snapshot.GetCell(0, 0).Character);
        Assert.Equal("B", snapshot.GetCell(0, 1).Character);

        // Visible area follows
        Assert.Equal("C", snapshot.GetCell(0, 2).Character);
        Assert.Equal("D", snapshot.GetCell(0, 3).Character);
        Assert.Equal("E", snapshot.GetCell(0, 4).Character);
    }

    [Fact]
    public void Snapshot_WithScrollbackLines_Zero_EqualsDefaultSnapshot()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        using var defaultSnapshot = terminal.CreateSnapshot();
        using var withZero = terminal.CreateSnapshot(scrollbackLines: 0);

        Assert.Equal(defaultSnapshot.Width, withZero.Width);
        Assert.Equal(defaultSnapshot.Height, withZero.Height);
        Assert.Equal(0, withZero.ScrollbackLineCount);
    }

    [Fact]
    public void Snapshot_WithScrollbackWidth_CurrentTerminal_TruncatesToTerminalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write at width 10, scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));

        // Resize to narrower
        terminal.Resize(5, 3);

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1, scrollbackWidth: ScrollbackWidth.CurrentTerminal);

        // Snapshot width should be the current terminal width (5)
        Assert.Equal(5, snapshot.Width);
    }

    [Fact]
    public void Snapshot_WithScrollbackWidth_Original_PreservesOriginalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write at width 10, scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));

        // Resize to narrower
        terminal.Resize(5, 3);

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1, scrollbackWidth: ScrollbackWidth.Original);

        // Snapshot width should be max of original scrollback width (10) and current terminal width (5)
        Assert.Equal(10, snapshot.Width);
    }

    [Fact]
    public void Snapshot_NoScrollbackConfigured_IgnoresScrollbackLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(10, 3)
            .Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD"));

        // Even though we request scrollback lines, none are available
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 10);
        Assert.Equal(3, snapshot.Height);
        Assert.Equal(0, snapshot.ScrollbackLineCount);
    }

    [Fact]
    public void WithScrollback_ThrowsForZeroCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithScrollback(0));
    }

    [Fact]
    public void WithScrollback_ThrowsForNegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithScrollback(-5));
    }

    [Fact]
    public void ScrollbackRow_PreservesOriginalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD"));

        var lines = terminal.Scrollback!.GetLines(1);
        Assert.Equal(10, lines[0].OriginalWidth);
    }

    [Fact]
    public void PartialScrollRegion_DoesNotCaptureScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 5);

        // Set scroll region to rows 2-4 (not starting at 0)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;5r")); // DECSTBM: rows 3-5 (1-based)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H")); // Move cursor to row 3

        // Fill the scroll region and cause scroll
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        // Should not capture to scrollback since scroll region doesn't start at row 0
        Assert.Equal(0, terminal.Scrollback!.Count);
    }
}
