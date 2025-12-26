using Hex1b.Terminal;
using Hex1b.Theming;
using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the OptimizedPresentationFilter.
/// </summary>
public class OptimizedPresentationFilterTests
{
    [Fact]
    public async Task Filter_FirstWrite_ForwardsAsIs()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer = CreateEmptyBuffer(80, 24);
        screenBuffer[0, 0] = new TerminalCell("H", null, null);
        
        var originalOutput = Encoding.UTF8.GetBytes("H");

        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,
            24,
            TimeSpan.Zero);

        // Assert - First write should be forwarded as-is
        Assert.Equal(originalOutput.Length, result.Length);
    }

    [Fact]
    public async Task Filter_NoChanges_SuppressesOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer = CreateEmptyBuffer(80, 24);
        var originalOutput = Encoding.UTF8.GetBytes("Test");

        // First write to establish baseline
        await filter.TransformOutputAsync(originalOutput, screenBuffer, 80, 24, TimeSpan.Zero);

        // Act - Second write with same buffer (no changes)
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should suppress output (no changes)
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_CellChange_GeneratesOptimizedOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        var originalOutput1 = Encoding.UTF8.GetBytes("A");

        // First write
        await filter.TransformOutputAsync(originalOutput1, screenBuffer1, 80, 24, TimeSpan.Zero);

        // Create new buffer with change
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("B", null, null);
        
        var originalOutput2 = Encoding.UTF8.GetBytes("\x1b[1;1HB");

        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput2,
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should generate output for the change
        Assert.False(result.IsEmpty);
        var output = Encoding.UTF8.GetString(result.Span);
        Assert.Contains("B", output);
    }

    [Fact]
    public async Task Filter_MultipleChanges_GeneratesOptimizedOutput()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        await filter.TransformOutputAsync(Array.Empty<byte>(), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Create buffer with multiple changes
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("H", null, null);
        screenBuffer2[0, 1] = new TerminalCell("i", null, null);
        screenBuffer2[1, 0] = new TerminalCell("!", null, null);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("Hi\n!"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert
        Assert.False(result.IsEmpty);
        var output = Encoding.UTF8.GetString(result.Span);
        
        // Should contain the changed characters
        Assert.Contains("H", output);
        Assert.Contains("i", output);
        Assert.Contains("!", output);
    }

    [Fact]
    public async Task Filter_Resize_HandlesCorrectly()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Act - Resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        var screenBuffer2 = CreateEmptyBuffer(100, 30);
        screenBuffer2[0, 0] = new TerminalCell("B", null, null);
        
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("B"),
            screenBuffer2,
            100,
            30,
            TimeSpan.Zero);

        // Assert - Should handle resize correctly
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_ColorChange_DetectsChange()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Change color
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", Hex1bColor.FromRgb(255, 0, 0), null);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("\x1b[31mA"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should detect color change
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_AttributeChange_DetectsChange()
    {
        // Arrange
        var filter = new OptimizedPresentationFilter();
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer1 = CreateEmptyBuffer(80, 24);
        screenBuffer1[0, 0] = new TerminalCell("A", null, null, CellAttributes.None);
        
        await filter.TransformOutputAsync(Encoding.UTF8.GetBytes("A"), screenBuffer1, 80, 24, TimeSpan.Zero);

        // Change attribute
        var screenBuffer2 = CreateEmptyBuffer(80, 24);
        screenBuffer2[0, 0] = new TerminalCell("A", null, null, CellAttributes.Bold);

        // Act
        var result = await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("\x1b[1mA"),
            screenBuffer2,
            80,
            24,
            TimeSpan.Zero);

        // Assert - Should detect attribute change
        Assert.False(result.IsEmpty);
    }

    private static TerminalCell[,] CreateEmptyBuffer(int width, int height)
    {
        var buffer = new TerminalCell[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                buffer[y, x] = TerminalCell.Empty;
            }
        }
        return buffer;
    }
}
