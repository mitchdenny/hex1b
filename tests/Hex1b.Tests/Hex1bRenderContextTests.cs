using Hex1b.Layout;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for Hex1bRenderContext functionality.
/// </summary>
public class Hex1bRenderContextTests
{
    #region ClearRegion Tests

    [Fact]
    public void ClearRegion_WritesSpacesToRegion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        
        // First, write some content
        context.SetCursorPosition(5, 2);
        context.Write("Hello World");
        terminal.FlushOutput();
        Assert.True(terminal.CreateSnapshot().ContainsText("Hello World"));
        
        // Clear the region where the text is
        context.ClearRegion(new Rect(5, 2, 11, 1));
        terminal.FlushOutput();
        
        // Text should be gone (replaced with spaces)
        Assert.False(terminal.CreateSnapshot().ContainsText("Hello World"));
    }

    [Fact]
    public void ClearRegion_MultipleRows()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        
        // Write content on multiple rows
        context.SetCursorPosition(0, 0);
        context.Write("Line 0");
        context.SetCursorPosition(0, 1);
        context.Write("Line 1");
        context.SetCursorPosition(0, 2);
        context.Write("Line 2");
        terminal.FlushOutput();
        
        // Clear a region spanning all three rows
        context.ClearRegion(new Rect(0, 0, 10, 3));
        terminal.FlushOutput();
        
        var snapshot = terminal.CreateSnapshot();
        Assert.False(snapshot.ContainsText("Line 0"));
        Assert.False(snapshot.ContainsText("Line 1"));
        Assert.False(snapshot.ContainsText("Line 2"));
    }

    [Fact]
    public void ClearRegion_PartialClear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(0, 0);
        context.Write("ABCDEFGHIJ");
        terminal.FlushOutput();
        
        // Clear only the middle portion
        context.ClearRegion(new Rect(3, 0, 4, 1));
        terminal.FlushOutput();
        
        var line = terminal.CreateSnapshot().GetLine(0);
        Assert.Equal("ABC    HIJ", line.Substring(0, 10));
    }

    [Fact]
    public void ClearRegion_ClampsToTerminalBounds()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 20, 5);
        var context = new Hex1bRenderContext(workload);
        
        // Try to clear a region that extends beyond terminal bounds
        // This should not throw and should clear only the valid portion
        context.ClearRegion(new Rect(15, 3, 100, 100));
        terminal.FlushOutput();
        
        // If we got here without exception, the clamping worked
        Assert.True(true);
    }

    [Fact]
    public void ClearRegion_EmptyRect_DoesNothing()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(5, 2);
        context.Write("Should remain");
        terminal.FlushOutput();
        
        // Clear with zero dimensions
        context.ClearRegion(new Rect(5, 2, 0, 0));
        terminal.FlushOutput();
        
        Assert.True(terminal.CreateSnapshot().ContainsText("Should remain"));
    }

    [Fact]
    public void ClearRegion_NegativePosition_ClampsToZero()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = new Hex1bTerminal(workload, 40, 10);
        var context = new Hex1bRenderContext(workload);
        
        context.SetCursorPosition(0, 0);
        context.Write("Hello");
        terminal.FlushOutput();
        
        // Clear with negative position (should clamp to 0)
        context.ClearRegion(new Rect(-5, -5, 10, 10));
        terminal.FlushOutput();
        
        // Content at origin should be cleared
        Assert.False(terminal.CreateSnapshot().ContainsText("Hello"));
    }

    #endregion
}
