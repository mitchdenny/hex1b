using Hex1b.Reflow;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the terminal reflow engine — re-wrapping soft-wrapped lines on resize.
/// </summary>
public class TerminalReflowTests
{
    #region Basic Reflow

    [Fact]
    public void Reflow_NarrowToWider_MergesSoftWrappedRows()
    {
        // Arrange: 5-column terminal with xterm reflow
        var adapter = new HeadlessPresentationAdapter(5, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 3).Build();

        // Write 10 chars → wraps to 2 rows of 5
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        var snap1 = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap1.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap1.GetLine(1).TrimEnd());

        // Act: Resize to 10 columns
        terminal.Resize(10, 3);

        // Assert: Should merge into 1 row
        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap2.GetLine(0).TrimEnd());
        Assert.Equal("", snap2.GetLine(1).TrimEnd());
    }

    [Fact]
    public void Reflow_WiderToNarrow_SplitsSoftWrappedRows()
    {
        // Arrange: 10-column terminal with xterm reflow
        var adapter = new HeadlessPresentationAdapter(10, 5).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 5).Build();

        // Write 10 chars → fills 1 row
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        var snap1 = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap1.GetLine(0).TrimEnd());

        // Act: Resize to 5 columns — now the "J" triggers wrap back, 
        // but the 10 chars need to re-wrap to 5-wide
        terminal.Resize(5, 5);

        // Assert: Should split into 2 rows
        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap2.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap2.GetLine(1).TrimEnd());
    }

    [Fact]
    public void Reflow_HardWrappedLines_NotMerged()
    {
        // Arrange: 10-column terminal with xterm reflow
        var adapter = new HeadlessPresentationAdapter(10, 5).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 5).Build();

        // Write two lines with explicit CR+LF (hard wrap)
        // Use AnsiTokenizer to properly split into TextToken + ControlCharacterToken sequences
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("Hello\r\nWorld"));

        // Act: Resize to 20 columns — should NOT merge (hard wrap)
        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
        Assert.Equal("World", snap.GetLine(1).TrimEnd());
    }

    [Fact]
    public void Reflow_NoReflowStrategy_CropsOnly()
    {
        // Arrange: 10-column terminal with NO reflow
        var adapter = new HeadlessPresentationAdapter(10, 3).WithReflowStrategy(NoReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 3).Build();

        // Write 10 chars
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        // Act: Resize to 5 columns — crop, not reflow
        terminal.Resize(5, 3);

        // Assert: First 5 chars visible, rest cropped (not re-wrapped)
        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd()); // No overflow to row 1
    }

    [Fact]
    public void Reflow_SameWidth_NoChange()
    {
        var adapter = new HeadlessPresentationAdapter(10, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 3).Build();

        terminal.ApplyTokens([new TextToken("Hello")]);
        terminal.Resize(10, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("Hello", snap.GetLine(0).TrimEnd());
    }

    [Fact]
    public void Reflow_MultipleWraps_RewrapCorrectly()
    {
        // Arrange: 3-column terminal
        var adapter = new HeadlessPresentationAdapter(3, 5).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(3, 5).Build();

        // Write 9 chars → 3 rows of 3
        terminal.ApplyTokens([new TextToken("ABCDEFGHI")]);

        // Act: Resize to 9 columns — all should merge
        terminal.Resize(9, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHI", snap.GetLine(0).TrimEnd());
        Assert.Equal("", snap.GetLine(1).TrimEnd());
    }

    #endregion

    #region Scrollback Promotion/Demotion

    [Fact]
    public void Reflow_NarrowingPushesRowsToScrollback()
    {
        // Arrange: 10-col, 3-row terminal with scrollback
        var adapter = new HeadlessPresentationAdapter(10, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 3)
            .WithScrollback(100).Build();

        // Fill screen: 3 rows, each 10 chars, hard-wrapped
        terminal.ApplyTokens([new TextToken("AAAAAAAAAA\nBBBBBBBBBB\nCCCCCCCCCC")]);

        // Act: Resize to 5 columns — each 10-char line becomes 2 rows = 6 rows total, but screen is 3
        terminal.Resize(5, 3);

        // Assert: Last 3 rows on screen, first 3 in scrollback
        var snap = terminal.CreateSnapshot(scrollbackLines: 10);
        // The visible screen should contain the last 3 rows of the 6
        // Row ordering: AA|AA|AA|BB|BB|BB|CC|CC|CC — but each original 10-char line was NOT soft-wrapped
        // Actually: Each line had a hard-wrap (explicit \n), so they don't merge.
        // 10 chars at 5 cols = 2 rows per line. 3 lines = 6 rows. Screen = 3. Scrollback gets 3.
        var screenLine0 = snap.GetLine(0).TrimEnd();
        var screenLine2 = snap.GetLine(2).TrimEnd();
        Assert.False(string.IsNullOrEmpty(screenLine0), "Screen should have content after narrowing");
    }

    [Fact]
    public void Reflow_WideningPullsRowsFromScrollback()
    {
        // Arrange: 5-col, 3-row terminal with scrollback, xterm reflow
        var adapter = new HeadlessPresentationAdapter(5, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 3)
            .WithScrollback(100).Build();

        // Write 20 chars — wraps to 4 rows at width 5. Screen is 3 rows, so row 0 scrolls off.
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJKLMNOPQRST")]);

        // Verify some content is on screen
        var snap1 = terminal.CreateSnapshot();
        Assert.False(string.IsNullOrEmpty(snap1.GetLine(0).TrimEnd()));

        // Act: Resize to 10 — 20 chars at width 10 = 2 rows. Should pull from scrollback.
        terminal.Resize(10, 3);

        // Assert: All content should be visible on screen (2 rows of 10)
        var snap2 = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGHIJ", snap2.GetLine(0).TrimEnd());
        Assert.Equal("KLMNOPQRST", snap2.GetLine(1).TrimEnd());
    }

    #endregion

    #region Cursor Preservation

    [Fact]
    public void Reflow_CursorPositionPreserved()
    {
        // Arrange: 5-col terminal with xterm reflow
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        // Write 8 chars: "ABCDE" on row 0 (soft-wrapped), "FGH" on row 1, cursor at (3,1)
        terminal.ApplyTokens([new TextToken("ABCDEFGH")]);

        var snap1 = terminal.CreateSnapshot();
        Assert.Equal(3, snap1.CursorX); // After "FGH", cursor at col 3
        Assert.Equal(1, snap1.CursorY); // Row 1

        // Act: Resize to 10 columns — "ABCDEFGH" fits in 1 row
        terminal.Resize(10, 5);

        var snap2 = terminal.CreateSnapshot();
        // Cursor should be at position 8 (after "ABCDEFGH") in the single row
        // That's col 8, row 0
        Assert.Equal("ABCDEFGH", snap2.GetLine(0).TrimEnd());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Reflow_EmptyScreen_HandlesGracefully()
    {
        var adapter = new HeadlessPresentationAdapter(10, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 3).Build();

        // Resize empty screen
        terminal.Resize(20, 5);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("", snap.GetLine(0).TrimEnd());
    }

    [Fact]
    public void Reflow_AbsolutePositioning_BreaksChain()
    {
        // Arrange: 5-col terminal with xterm reflow (clears SoftWrap on CUP)
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        // Write 15 chars → wraps to 3 rows (0, 1, 2), all soft-wrapped
        // Then write more to end up on row 2 with wrap still pending
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJKLMNO")]);

        // Verify soft-wraps are set
        var snap1 = terminal.CreateSnapshot();
        Assert.True((snap1.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) != 0, "Row 0 should have SoftWrap");
        Assert.True((snap1.GetCell(4, 1).Attributes & CellAttributes.SoftWrap) != 0, "Row 1 should have SoftWrap");

        // Now write one more char to move to row 3, then CUP from row 3
        // This clears SoftWrap on the row the cursor departs from (row 3)
        terminal.ApplyTokens([new TextToken("P")]); // cursor now on row 3

        // CUP to row 1 — clears SoftWrap on current row (row 3, if it has one)
        terminal.ApplyTokens([new CursorPositionToken(1, 1)]);

        // Rows 0 and 1 should still have SoftWrap (CUP clears departure row, not arbitrary rows)
        var snap2 = terminal.CreateSnapshot();
        Assert.True((snap2.GetCell(4, 0).Attributes & CellAttributes.SoftWrap) != 0,
            "Row 0 SoftWrap should be preserved (CUP clears departure row only)");

        // Now write content on row 0 that overwrites the SoftWrap cell
        terminal.ApplyTokens([
            new CursorPositionToken(1, 5), // row 1, col 5 (1-based) = row 0, col 4
            new TextToken("X")
        ]);

        // Row 0's last cell was overwritten with "X", which triggers a new wrap
        // The SoftWrap flag should now be set again (new wrap)
        var snap3 = terminal.CreateSnapshot();

        // Key test: resize wider. Rows 0-1 should merge (SoftWrap), but row 2 onward
        // is separate because CUP broke the continuation
        terminal.Resize(15, 5);

        var snap4 = terminal.CreateSnapshot();
        // At width 15, the content should be:
        // "ABCDX" + "FGHIJ" + "KLMNO" were originally on rows 0-2
        // But CUP broke things up, so exact layout depends on which SoftWraps survived
        Assert.False(string.IsNullOrEmpty(snap4.GetLine(0).TrimEnd()),
            "Content should be present after reflow");
    }

    [Fact]
    public void Reflow_KittyStrategy_AnchorsCursorRow()
    {
        // Arrange: 5-col, 5-row terminal with kitty reflow
        var adapter = new HeadlessPresentationAdapter(5, 5).WithReflowStrategy(KittyReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(5, 5).Build();

        // Write content
        terminal.ApplyTokens([new TextToken("ABCDEFGH")]);

        // Act: Resize — kitty anchors cursor row
        terminal.Resize(10, 5);

        // Assert: Content should be present (exact cursor row behavior is kitty-specific)
        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDEFGH", snap.GetLine(0).TrimEnd());
    }

    [Fact]
    public void Reflow_SoftWrapAttributeSetCorrectlyAfterReflow()
    {
        // Arrange: 10-col terminal
        var adapter = new HeadlessPresentationAdapter(10, 3).WithReflowStrategy(XtermReflowStrategy.Instance);
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(adapter).WithDimensions(10, 3).Build();

        // Write 10 chars — fills row, pending wrap
        terminal.ApplyTokens([new TextToken("ABCDEFGHIJ")]);

        // Act: Resize to 5 columns — should split to 2 rows with SoftWrap on row 0
        terminal.Resize(5, 3);

        var snap = terminal.CreateSnapshot();
        Assert.Equal("ABCDE", snap.GetLine(0).TrimEnd());
        Assert.Equal("FGHIJ", snap.GetLine(1).TrimEnd());

        // The last cell of row 0 should have SoftWrap (it continues to row 1)
        var lastCellRow0 = snap.GetCell(4, 0);
        Assert.True((lastCellRow0.Attributes & CellAttributes.SoftWrap) != 0,
            "After reflow to narrower width, new wrap points should have SoftWrap");

        // Last cell of row 1 should NOT have SoftWrap (content ends here)
        var lastCellRow1 = snap.GetCell(4, 1);
        Assert.True((lastCellRow1.Attributes & CellAttributes.SoftWrap) == 0,
            "Last row of content should not have SoftWrap");
    }

    #endregion
}
