namespace Hex1b.Tests;

using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Tokens;
using Xunit;

public class LineFeedBandingTest
{
    [Fact]
    public void MapsciiPattern_CrLfCr_NoBlankLines()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 10, 10); // 10 wide, 10 tall (enough room)

        // Simulate mapscii pattern: chars, \r\n\r, chars, \r\n\r...
        // With deferred wrap (pending wrap):
        // - Write 10 chars: cursor at column 9, pending wrap = true
        // - \r clears pending wrap, cursor to column 0
        // - \n moves to next row
        // - \r does nothing (already at column 0)
        // Net effect: each row of content is on consecutive rows
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAAAAAAA\r\n\rBBBBBBBBBB\r\n\rCCCCCCCCCC\r\n\r"));

        var snapshot = terminal.CreateSnapshot();
        
        // Capture for visual inspection
        TestCaptureHelper.Capture(terminal, "linefeed-test");
        
        // Build row info for error message
        var rowInfo = "";
        for (int y = 0; y < 10; y++)
        {
            rowInfo += $"\nRow {y}: '{snapshot.GetLineTrimmed(y)}'";
        }
        
        // With deferred wrap:
        // Row 0: AAAAAAAAAA (cursor at col 9, pending wrap = true)
        // \r clears pending wrap, cursor to col 0 (still row 0)
        // \n moves to row 1
        // \r does nothing
        // Row 1: BBBBBBBBBB
        // Row 2: CCCCCCCCCC
        
        // Row 0 should have "AAAAAAAAAA"
        Assert.True(snapshot.GetCell(0, 0).Character == "A", $"Expected 'A' at (0,0), got '{snapshot.GetCell(0, 0).Character}'{rowInfo}");
        
        // Row 1 should have "BBBBBBBBBB" (consecutive, no banding!)
        Assert.True(snapshot.GetCell(0, 1).Character == "B", $"Expected 'B' at (0,1), got '{snapshot.GetCell(0, 1).Character}'{rowInfo}");
        
        // Row 2 should have "CCCCCCCCCC" (consecutive, no banding!)
        Assert.True(snapshot.GetCell(0, 2).Character == "C", $"Expected 'C' at (0,2), got '{snapshot.GetCell(0, 2).Character}'{rowInfo}");
    }
    
    [Fact]
    public void DeferredWrap_WritingAtEndOfLine_DoesNotWrapUntilNextChar()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 5, 3); // 5 wide, 3 tall

        // Write exactly 5 characters - should fill row 0, cursor at col 4 (last column)
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("AAAAA"));

        // Cursor should be at last column (4), not wrapped to next line
        Assert.Equal(4, terminal.CursorX);
        Assert.Equal(0, terminal.CursorY);
        
        // Now write one more character - this should trigger the deferred wrap
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("B"));
        
        // After writing B, cursor should be at column 1 (or column 0 with pending wrap)
        // Actually B is at row 1, col 0, then cursor moves to col 1
        Assert.Equal(1, terminal.CursorY); // Should be on row 1 now
        
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("B", snapshot.GetCell(0, 1).Character); // B is on row 1
    }
}
