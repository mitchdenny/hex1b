using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for copy mode on TerminalWidgetHandle — output queuing, enter/exit lifecycle,
/// selection integration, and text extraction.
/// </summary>
public class TerminalCopyModeTests
{
    private static TerminalWidgetHandle CreateHandle(int width = 80, int height = 24)
    {
        return new TerminalWidgetHandle(width, height);
    }

    private static IReadOnlyList<AppliedToken> MakeCellImpacts(string text, int row = 0, int startCol = 0)
    {
        var impacts = new List<CellImpact>();
        for (int i = 0; i < text.Length; i++)
        {
            impacts.Add(new CellImpact(startCol + i, row,
                new TerminalCell(text[i].ToString(), null, null)));
        }
        
        return new List<AppliedToken>
        {
            new(new TextToken(text), impacts, startCol, row, startCol + text.Length, row)
        };
    }

    [Fact]
    public void EnterCopyMode_SetsIsInCopyMode()
    {
        var handle = CreateHandle();
        Assert.False(handle.IsInCopyMode);
        
        handle.EnterCopyMode();
        Assert.True(handle.IsInCopyMode);
    }

    [Fact]
    public void EnterCopyMode_CreatesSelection()
    {
        var handle = CreateHandle(80, 24);
        handle.EnterCopyMode();
        
        Assert.NotNull(handle.Selection);
        // Cursor should be at bottom-left of screen
        Assert.Equal(23, handle.Selection!.Cursor.Row); // scrollbackCount(0) + height(24) - 1
        Assert.Equal(0, handle.Selection.Cursor.Column);
    }

    [Fact]
    public void ExitCopyMode_ClearsState()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        handle.ExitCopyMode();
        
        Assert.False(handle.IsInCopyMode);
        Assert.Null(handle.Selection);
    }

    [Fact]
    public void EnterCopyMode_DoubleEntry_NoOp()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        var selection1 = handle.Selection;
        
        handle.EnterCopyMode(); // should be no-op
        Assert.Same(selection1, handle.Selection);
    }

    [Fact]
    public async Task OutputQueuing_WhenInCopyMode_DoesNotApplyToBuffer()
    {
        var handle = CreateHandle(10, 5);
        
        // Write initial content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello", row: 0));
        Assert.Equal("H", handle.GetCell(0, 0).Character);
        
        // Enter copy mode
        handle.EnterCopyMode();
        
        // Write new content — should be queued, not applied
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World", row: 0));
        
        // Buffer should still show original content
        Assert.Equal("H", handle.GetCell(0, 0).Character);
    }

    [Fact]
    public async Task OutputQueuing_OnExit_FlushesQueuedOutput()
    {
        var handle = CreateHandle(10, 5);
        
        // Write initial content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello", row: 0));
        
        // Enter copy mode and write new content
        handle.EnterCopyMode();
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World", row: 0));
        
        // Exit copy mode — queued output should be flushed
        handle.ExitCopyMode();
        
        Assert.Equal("W", handle.GetCell(0, 0).Character);
        Assert.Equal("o", handle.GetCell(1, 0).Character);
    }

    [Fact]
    public async Task OutputQueuing_MultipleChunks_AllFlushedInOrder()
    {
        var handle = CreateHandle(10, 5);
        handle.EnterCopyMode();
        
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("AA", row: 0));
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("BB", row: 0)); // overwrites
        
        handle.ExitCopyMode();
        
        // Second write should overwrite first
        Assert.Equal("B", handle.GetCell(0, 0).Character);
        Assert.Equal("B", handle.GetCell(1, 0).Character);
    }

    [Fact]
    public void CopyModeChanged_FiredOnEnterAndExit()
    {
        var handle = CreateHandle();
        var events = new List<bool>();
        handle.CopyModeChanged += (value) => events.Add(value);
        
        handle.EnterCopyMode();
        handle.ExitCopyMode();
        
        Assert.Equal(2, events.Count);
        Assert.True(events[0]);   // enter
        Assert.False(events[1]);  // exit
    }

    [Fact]
    public async Task CopySelection_ExtractsTextAndExits()
    {
        var handle = CreateHandle(10, 3);
        
        // Write content
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("Hello!", row: 0));
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("World!", row: 1));
        
        // Enter copy mode
        handle.EnterCopyMode();
        
        // Select "Hello!" on row 0
        var sel = handle.Selection!;
        sel.MoveCursor(new BufferPosition(0, 0));
        sel.StartSelection(SelectionMode.Character);
        sel.MoveCursor(new BufferPosition(0, 5));
        
        string? copiedText = null;
        handle.TextCopied += (text) => copiedText = text;
        
        var result = handle.CopySelection();
        
        Assert.Equal("Hello!", result);
        Assert.Equal("Hello!", copiedText);
        Assert.False(handle.IsInCopyMode);
    }

    [Fact]
    public void CopySelection_NoSelection_ReturnsNull()
    {
        var handle = CreateHandle();
        handle.EnterCopyMode();
        // Don't start selection
        
        var result = handle.CopySelection();
        Assert.Null(result);
        Assert.False(handle.IsInCopyMode);
    }

    [Fact]
    public void VirtualBufferHeight_ReturnsScrollbackPlusScreen()
    {
        var handle = CreateHandle(80, 24);
        // No scrollback configured (no terminal attached), so scrollback count = 0
        Assert.Equal(24, handle.VirtualBufferHeight);
    }

    [Fact]
    public async Task GetVirtualCell_ScreenRegion_ReturnsCorrectCell()
    {
        var handle = CreateHandle(10, 5);
        await handle.WriteOutputWithImpactsAsync(MakeCellImpacts("ABC", row: 2));
        
        // Screen rows start at scrollbackCount (0 when no terminal attached)
        var cell = handle.GetVirtualCell(2, 0);
        Assert.NotNull(cell);
        Assert.Equal("A", cell!.Value.Character);
        
        var cell2 = handle.GetVirtualCell(2, 2);
        Assert.NotNull(cell2);
        Assert.Equal("C", cell2!.Value.Character);
    }

    [Fact]
    public void GetVirtualCell_OutOfBounds_ReturnsNull()
    {
        var handle = CreateHandle(10, 5);
        
        Assert.Null(handle.GetVirtualCell(-1, 0));
        Assert.Null(handle.GetVirtualCell(0, -1));
        Assert.Null(handle.GetVirtualCell(0, 10)); // width = 10
        Assert.Null(handle.GetVirtualCell(5, 0));  // height = 5
    }

    [Fact]
    public void CopyModeCursorPosition_WhenNotInCopyMode_ReturnsNull()
    {
        var handle = CreateHandle();
        Assert.Null(handle.CopyModeCursorPosition);
    }

    [Fact]
    public void CopyModeCursorPosition_WhenInCopyMode_ReturnsCursorPos()
    {
        var handle = CreateHandle(80, 24);
        handle.EnterCopyMode();
        
        var pos = handle.CopyModeCursorPosition;
        Assert.NotNull(pos);
        Assert.Equal(23, pos!.Value.Row);
        Assert.Equal(0, pos.Value.Column);
    }
}
