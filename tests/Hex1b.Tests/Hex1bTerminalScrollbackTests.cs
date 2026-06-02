using Hex1b.Automation;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for terminal scrollback buffer support.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void ScrollUp_CapturesRowInScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Fill 3 rows and then force a scroll by adding a 4th line
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Line1\r\nLine2\r\nLine3\r\nLine4"));

        Assert.IsNotNull(terminal.Scrollback);
        Assert.AreEqual(1, terminal.Scrollback.Count);

        var lines = terminal.Scrollback.GetLines(1);
        Assert.AreEqual("L", lines[0].Cells[0].Character);
        Assert.AreEqual("i", lines[0].Cells[1].Character);
        Assert.AreEqual("n", lines[0].Cells[2].Character);
        Assert.AreEqual("e", lines[0].Cells[3].Character);
        Assert.AreEqual("1", lines[0].Cells[4].Character);
    }

    [TestMethod]
    public void ScrollUp_MultipleRows_CapturesAllInOrder()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write 5 lines into a 3-row terminal -> 2 rows scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD\r\nEEE"));

        Assert.AreEqual(2, terminal.Scrollback!.Count);

        var lines = terminal.Scrollback.GetLines(2);
        Assert.AreEqual("A", lines[0].Cells[0].Character);
        Assert.AreEqual("B", lines[1].Cells[0].Character);
    }

    [TestMethod]
    public void ScrollUp_InAlternateScreen_DoesNotCaptureScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Enter alternate screen, then scroll
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.AreEqual(0, terminal.Scrollback!.Count);
    }

    [TestMethod]
    public void ScrollUp_ResumesAfterExitingAlternateScreen()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write some lines that scroll off in normal mode
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("X\r\nY\r\nZ\r\nW"));
        Assert.AreEqual(1, terminal.Scrollback!.Count);

        // Enter alternate screen — no scrollback
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049h"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD"));
        Assert.AreEqual(1, terminal.Scrollback.Count);

        // Exit alternate screen — scrollback resumes
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1049l"));
        // Original content restored (X, Y, Z, W from before) — scroll more
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\r\n\r\n\r\nQ"));
        Assert.IsTrue(terminal.Scrollback.Count > 1);
    }

    [TestMethod]
    public void NoScrollback_WhenNotConfigured()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(10, 3)
            .Build();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.IsNull(terminal.Scrollback);
    }

    [TestMethod]
    public void CSI3J_ClearsScrollbackBuffer()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Scroll some lines off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));
        Assert.IsTrue(terminal.Scrollback!.Count > 0);

        // Send CSI 3J (Erase Display with param 3)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3J"));
        Assert.AreEqual(0, terminal.Scrollback.Count);
    }

    [TestMethod]
    public void Callback_InvokedWhenRowScrollsOff()
    {
        var callbackRows = new List<ScrollbackRowEventArgs>();
        using var terminal = CreateTerminal(
            width: 10, height: 3,
            callback: args => callbackRows.Add(args));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD"));

        TestSeq.Single(callbackRows);
        Assert.AreEqual(10, callbackRows[0].OriginalWidth);
        Assert.AreSame(terminal, callbackRows[0].Terminal);
        Assert.AreEqual("A", callbackRows[0].Cells.Span[0].Character);
    }

    [TestMethod]
    public void Callback_ReceivesMultipleRows()
    {
        var callbackRows = new List<ScrollbackRowEventArgs>();
        using var terminal = CreateTerminal(
            width: 10, height: 3,
            callback: args => callbackRows.Add(args));

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        Assert.AreEqual(2, callbackRows.Count);
        Assert.AreEqual("A", callbackRows[0].Cells.Span[0].Character);
        Assert.AreEqual("B", callbackRows[1].Cells.Span[0].Character);
    }

    [TestMethod]
    public void Snapshot_WithScrollbackLines_IncludesHistory()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write enough to push 2 rows into scrollback
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAA\r\nBBB\r\nCCC\r\nDDD\r\nEEE"));

        // Snapshot with scrollback
        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 2);

        // Total height = 2 scrollback + 3 visible
        Assert.AreEqual(5, snapshot.Height);
        Assert.AreEqual(2, snapshot.ScrollbackLineCount);

        // First two rows should be scrollback (AAA, BBB)
        Assert.AreEqual("A", snapshot.GetCell(0, 0).Character);
        Assert.AreEqual("B", snapshot.GetCell(0, 1).Character);

        // Visible area follows
        Assert.AreEqual("C", snapshot.GetCell(0, 2).Character);
        Assert.AreEqual("D", snapshot.GetCell(0, 3).Character);
        Assert.AreEqual("E", snapshot.GetCell(0, 4).Character);
    }

    [TestMethod]
    public void Snapshot_WithScrollbackLines_Zero_EqualsDefaultSnapshot()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        using var defaultSnapshot = terminal.CreateSnapshot();
        using var withZero = terminal.CreateSnapshot(scrollbackLines: 0);

        Assert.AreEqual(defaultSnapshot.Width, withZero.Width);
        Assert.AreEqual(defaultSnapshot.Height, withZero.Height);
        Assert.AreEqual(0, withZero.ScrollbackLineCount);
    }

    [TestMethod]
    public void Snapshot_WithScrollbackWidth_CurrentTerminal_TruncatesToTerminalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write at width 10, scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));

        // Resize to narrower
        terminal.Resize(5, 3);

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1, scrollbackWidth: ScrollbackWidth.CurrentTerminal);

        // Snapshot width should be the current terminal width (5)
        Assert.AreEqual(5, snapshot.Width);
    }

    [TestMethod]
    public void Snapshot_WithScrollbackWidth_Original_PreservesOriginalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write at width 10, scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));

        // Resize to narrower
        terminal.Resize(5, 3);

        using var snapshot = terminal.CreateSnapshot(scrollbackLines: 1, scrollbackWidth: ScrollbackWidth.Original);

        // Snapshot width should be max of original scrollback width (10) and current terminal width (5)
        Assert.AreEqual(10, snapshot.Width);
    }

    [TestMethod]
    public void Snapshot_VoidCell_FillsGapsWithSpecifiedCell()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        // Write at width 10, scroll off
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));

        // Resize to narrower
        terminal.Resize(5, 3);

        var voidCell = new TerminalCell("·", null, null);
        using var snapshot = terminal.CreateSnapshot(
            scrollbackLines: 1,
            scrollbackWidth: ScrollbackWidth.Original,
            voidCell: voidCell);

        // Visible area is 5 wide, snapshot is 10 wide — columns 5-9 in visible rows should be void cell
        Assert.AreEqual("·", snapshot.GetCell(5, snapshot.ScrollbackLineCount).Character);
        Assert.AreEqual("·", snapshot.GetCell(9, snapshot.ScrollbackLineCount).Character);
    }

    [TestMethod]
    public void Snapshot_VoidCell_DefaultsToEmpty()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC\r\nDDDDDDDDDD"));
        terminal.Resize(5, 3);

        // No voidCell specified — should default to TerminalCell.Empty (a space)
        using var snapshot = terminal.CreateSnapshot(
            scrollbackLines: 1,
            scrollbackWidth: ScrollbackWidth.Original);

        Assert.AreEqual(" ", snapshot.GetCell(5, snapshot.ScrollbackLineCount).Character);
    }

    [TestMethod]
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
        Assert.AreEqual(3, snapshot.Height);
        Assert.AreEqual(0, snapshot.ScrollbackLineCount);
    }

    [TestMethod]
    public void WithScrollback_ThrowsForZeroCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithScrollback(0));
    }

    [TestMethod]
    public void WithScrollback_ThrowsForNegativeCapacity()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Hex1bTerminal.CreateBuilder().WithScrollback(-5));
    }

    [TestMethod]
    public void ScrollbackRow_PreservesOriginalWidth()
    {
        using var terminal = CreateTerminal(width: 10, height: 3);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD"));

        var lines = terminal.Scrollback!.GetLines(1);
        Assert.AreEqual(10, lines[0].OriginalWidth);
    }

    [TestMethod]
    public void PartialScrollRegion_DoesNotCaptureScrollback()
    {
        using var terminal = CreateTerminal(width: 10, height: 5);

        // Set scroll region to rows 2-4 (not starting at 0)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;5r")); // DECSTBM: rows 3-5 (1-based)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[3;1H")); // Move cursor to row 3

        // Fill the scroll region and cause scroll
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("A\r\nB\r\nC\r\nD\r\nE"));

        // Should not capture to scrollback since scroll region doesn't start at row 0
        Assert.AreEqual(0, terminal.Scrollback!.Count);
    }
}
