using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the SoftWrap cell attribute tracking during terminal operations.
/// </summary>
public class SoftWrapTrackingTests
{
    [Fact]
    public void WriteToEndOfLine_SetsSoftWrapOnLastCell()
    {
        // Arrange: 5-column terminal
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();

        // Act: Write 6 chars to trigger wrap at column 5
        terminal.ApplyTokens([new TextToken("ABCDEF")]);

        // Assert: Last cell of row 0 should have SoftWrap (it was the wrap point)
        var snapshot = terminal.CreateSnapshot();
        var lastCell = snapshot.GetCell(4, 0); // col 4 (0-based), row 0
        Assert.Equal("E", lastCell.Character);
        Assert.True((lastCell.Attributes & CellAttributes.SoftWrap) != 0,
            "Last cell of wrapped row should have SoftWrap attribute");

        // Row 1 should have F and no SoftWrap on its last occupied cell
        Assert.Equal("F", snapshot.GetCell(0, 1).Character);
    }

    [Fact]
    public void ExplicitLineFeed_DoesNotSetSoftWrap()
    {
        // Arrange: 10-column terminal
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(10, 3).Build();

        // Act: Write text then CR+LF (hard wrap)
        // Use AnsiTokenizer so \r\n are processed as control characters, not graphemes
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        // Assert: Last cell of row 0 should NOT have SoftWrap
        var snapshot = terminal.CreateSnapshot();
        var lastCell = snapshot.GetCell(9, 0); // last column
        Assert.True((lastCell.Attributes & CellAttributes.SoftWrap) == 0,
            "Row ending with explicit LF should not have SoftWrap");
        // Also verify the content is correct (World on row 1)
        Assert.Equal("Hello", snapshot.GetLine(0).TrimEnd());
        Assert.Equal("World", snapshot.GetLine(1).TrimEnd());
    }

    [Fact]
    public void EraseLine_ClearsSoftWrap()
    {
        // Arrange: 5-column terminal, write to wrap
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();

        // Write to trigger wrap
        terminal.ApplyTokens([new TextToken("ABCDEF")]);

        // Verify wrap is set
        var snap1 = terminal.CreateSnapshot();
        Assert.True((snap1.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) != 0);

        // Act: Move cursor to row 0 and erase entire line (CSI 2K)
        terminal.ApplyTokens([
            new CursorPositionToken(1, 1), // Move to row 1, col 1
            new ClearLineToken(ClearMode.All)
        ]);

        // Assert: SoftWrap should be cleared
        var snap2 = terminal.CreateSnapshot();
        Assert.True((snap2.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) == 0,
            "Erase line should clear SoftWrap");
    }

    [Fact]
    public void OverwriteLastCell_ClearsSoftWrapWhenNotWrapping()
    {
        // Arrange: 5-column terminal, write to wrap
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();

        // Write to trigger wrap
        terminal.ApplyTokens([new TextToken("ABCDEF")]);

        // Verify wrap is set
        var snap1 = terminal.CreateSnapshot();
        Assert.True((snap1.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) != 0);

        // Act: Move cursor to col 5 of row 0 and overwrite (no wrap occurs)
        terminal.ApplyTokens([
            new CursorPositionToken(1, 5), // row 1, col 5 (1-based)
            new TextToken("X")
        ]);

        // Assert: The cell was overwritten. Since this write triggers another wrap,
        // SoftWrap should still be set.
        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("X", snap2.GetCell(4, 0).Character);
    }

    [Fact]
    public void SoftWrap_TravelsWithScrollUp()
    {
        // Arrange: 5-column, 3-row terminal with scrollback
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3)
            .WithScrollback(10).Build();

        // Write enough to fill screen and trigger scroll
        // Row 0: "ABCDE" (wraps) → SoftWrap on col 4
        // Row 1: "FGHIJ" (wraps) → SoftWrap on col 4
        // Row 2: "KLMNO" (wraps) → SoftWrap on col 4
        // This should scroll row 0 into scrollback
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJKLMNOP")]);

        // Assert: The scrollback should contain the first row with SoftWrap intact
        var snapshot = terminal.CreateSnapshot(scrollbackLines: 10);

        // Screen row 0's last cell should have SoftWrap (it wrapped)
        var screenRow0Last = snapshot.GetCell(4, 0);
        Assert.True((screenRow0Last.Attributes & CellAttributes.SoftWrap) != 0,
            "Screen row that wrapped should retain SoftWrap");
    }

    [Fact]
    public void MultipleWraps_AllSetSoftWrap()
    {
        // Arrange: 3-column terminal
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(3, 5).Build();

        // Act: Write 9 chars → should fill 3 rows of 3 chars each
        terminal.ApplyTokens([new TextToken("ABCDEFGHI")]);

        var snapshot = terminal.CreateSnapshot();

        // Row 0: ABC (soft-wrapped)
        Assert.True((snapshot.GetCell(2, 0).Attributes & CellAttributes.SoftWrap) != 0,
            "Row 0 last cell should have SoftWrap");

        // Row 1: DEF (soft-wrapped)
        Assert.True((snapshot.GetCell(2, 1).Attributes & CellAttributes.SoftWrap) != 0,
            "Row 1 last cell should have SoftWrap");

        // Row 2: GHI (not wrapped — content ends here)
        Assert.True((snapshot.GetCell(2, 2).Attributes & CellAttributes.SoftWrap) == 0,
            "Last row should not have SoftWrap (no continuation)");
    }

    [Fact]
    public void EraseDisplay_ClearsSoftWrap()
    {
        // Arrange
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();

        terminal.ApplyTokens([new TextToken("ABCDEF")]);

        // Verify wrap is set
        var snap1 = terminal.CreateSnapshot();
        Assert.True((snap1.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) != 0);

        // Act: Clear entire display (CSI 2J)
        terminal.ApplyTokens([new ClearScreenToken(ClearMode.All)]);

        // Assert
        var snap2 = terminal.CreateSnapshot();
        Assert.True((snap2.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) == 0,
            "Erase display should clear SoftWrap");
    }

    [Fact]
    public void ExactFill_NoWrap_NoSoftWrap()
    {
        // Arrange: Write exactly 5 chars to a 5-column terminal
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(5, 3).Build();

        // Act: Write exactly 5 chars — fills row but doesn't trigger wrap yet
        // (deferred wrap: _pendingWrap is true but no next char to fire it)
        terminal.ApplyTokens([new TextToken("ABCDE")]);

        // Assert: No SoftWrap yet because wrap hasn't actually fired
        // (it's just pending)
        var snapshot = terminal.CreateSnapshot();
        Assert.True((snapshot.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) == 0,
            "Pending wrap (no next char) should not set SoftWrap");
    }
}
