#pragma warning disable HEX1B_SIXEL // Testing experimental Sixel API

using Hex1b;
using Hex1b.Terminal;
using Hex1b.Tokens;

namespace Hex1b.Tests;

public class TrackedObjectTests
{
    [Fact]
    public void ApplyTokens_WithSixelSequence_CreatesSixelData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Process a Sixel sequence directly
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        
        // Should track the Sixel data
        Assert.Equal(1, terminal.TrackedSixelCount);
        Assert.True(terminal.ContainsSixelData());
        
        // The origin cell should have the Sixel data
        var sixelData = terminal.GetSixelDataAt(0, 0);
        Assert.NotNull(sixelData);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", sixelData.Payload);
    }

    [Fact]
    public void ApplyTokens_WithCursorPositionThenSixel_CreatesSixelData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Process cursor position followed by Sixel sequence
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1H\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        
        // Should track the Sixel data
        Assert.Equal(1, terminal.TrackedSixelCount);
        Assert.True(terminal.ContainsSixelData());
        
        // The origin cell should have the Sixel data
        var sixelData = terminal.GetSixelDataAt(0, 0);
        Assert.NotNull(sixelData);
        Assert.Contains("#0;2;100;0;0#0~~~~~~", sixelData.Payload);
    }

    [Fact]
    public void WorkloadAdapter_WithSixel_TerminalReceivesSixelData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Write through workload adapter (simulating what SixelNode does)
        workload.SetCursorPosition(0, 0);
        workload.Write("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\");
        
        // Flush should process it
        terminal.FlushOutput();
        
        // Should track the Sixel data
        Assert.Equal(1, terminal.TrackedSixelCount);
        Assert.True(terminal.ContainsSixelData());
    }

    [Fact]
    public void WorkloadAdapter_SeparateWrites_TerminalReceivesSixelData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Write cursor position and sixel as SEPARATE writes (like SixelNode does via context)
        workload.Write("\x1b[1;1H");  // SetCursorPosition generates this
        workload.Write("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\");
        
        // Flush should process both
        terminal.FlushOutput();
        
        // Should track the Sixel data
        Assert.Equal(1, terminal.TrackedSixelCount);
        Assert.True(terminal.ContainsSixelData());
    }

    [Fact]
    public void RenderContext_WithSixel_TerminalReceivesSixelData()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        var context = new Hex1bRenderContext(workload);
        
        // Use render context exactly like SixelNode does
        context.SetCursorPosition(0, 0);
        context.Write("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\");
        
        // Flush should process it
        terminal.FlushOutput();
        
        // Should track the Sixel data
        Assert.True(terminal.ContainsSixelData());
        Assert.Equal(1, terminal.TrackedSixelCount);
    }

    [Fact]
    public void TrackedSixel_WhenCellOverwritten_ReleasesReference()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Process a Sixel sequence
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        Assert.Equal(1, terminal.TrackedSixelCount);
        
        // Overwrite the cell with text
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[1;1HXXXXXXXX"));
        
        // Sixel data should be released (refcount reached 0)
        Assert.Equal(0, terminal.TrackedSixelCount);
    }

    [Fact]
    public void TrackedSixel_Deduplication_ReusesSameObject()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Process the same Sixel sequence twice
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // Move cursor to next row
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        
        // Should still only have one unique tracked object
        Assert.Equal(1, terminal.TrackedSixelCount);
        
        // Both cells should reference the same object
        var sixel1 = terminal.GetSixelDataAt(0, 0);
        var sixel2 = terminal.GetSixelDataAt(0, 1);
        Assert.Same(sixel1, sixel2);
    }

    [Fact]
    public void TrackedSixel_RefCount_IncreasesWithDeduplication()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 80, 24);
        
        // Process the same Sixel sequence twice
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[2;1H")); // Move cursor to next row
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1bPq#0;2;100;0;0#0~~~~~~\x1b\\"));
        
        var trackedSixel = terminal.GetTrackedSixelAt(0, 0);
        Assert.NotNull(trackedSixel);
        
        // RefCount should be 2 (one for each cell)
        Assert.Equal(2, trackedSixel.RefCount);
    }
}
