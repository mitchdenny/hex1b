using System.Text;
using Hex1b.Terminal;

namespace Hex1b.Tests;

public class ResizeDebounceFilterTests
{
    [Fact]
    public async Task Filter_NoResize_PassesThroughOutput()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var screenBuffer = CreateEmptyBuffer(80, 24);
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,
            24,
            TimeSpan.Zero);

        // Assert - No resize, so output passes through
        Assert.Equal(originalOutput.Length, result.Length);
    }

    [Fact]
    public async Task Filter_DuringResize_SuppressesOutput()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Trigger resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Immediately try to transform (within debounce window)
        var screenBuffer = CreateEmptyBuffer(100, 30);
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            100,
            30,
            TimeSpan.Zero);

        // Assert - During debounce, output should be suppressed
        Assert.True(result.IsEmpty, "Output should be suppressed during resize debounce window");
    }

    [Fact]
    public async Task Filter_AfterDebounceExpires_GeneratesFullRedraw()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Trigger resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Wait for debounce to expire
        await Task.Delay(60);
        
        var screenBuffer = CreateEmptyBuffer(100, 30);
        screenBuffer[0, 0] = new TerminalCell("X", null, null);
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            100,
            30,
            TimeSpan.Zero);

        // Assert - After debounce expires, should generate full redraw (not pass through)
        Assert.False(result.IsEmpty);
        var output = Encoding.UTF8.GetString(result.Span);
        // Should contain clear screen sequence and the cell content
        Assert.Contains("\x1b[2J", output);
        Assert.Contains("X", output);
    }

    [Fact]
    public async Task Filter_AfterFullRedraw_PassesThroughNormally()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Trigger resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Wait for debounce to expire
        await Task.Delay(60);
        
        var screenBuffer = CreateEmptyBuffer(100, 30);
        
        // First call generates full redraw
        await filter.TransformOutputAsync(
            Encoding.UTF8.GetBytes("First"),
            screenBuffer,
            100,
            30,
            TimeSpan.Zero);
        
        // Act - Second call should pass through normally
        var originalOutput = Encoding.UTF8.GetBytes("Second");
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            100,
            30,
            TimeSpan.Zero);

        // Assert - Should pass through the original output
        Assert.Equal(originalOutput.Length, result.Length);
    }

    [Fact]
    public async Task Filter_RapidResize_SuppressesAllOutput()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        var suppressedCount = 0;
        
        // Simulate rapid resizing
        for (int i = 0; i < 10; i++)
        {
            await filter.OnResizeAsync(80 + i, 24 + i, TimeSpan.FromMilliseconds(i * 5));
            
            var screenBuffer = CreateEmptyBuffer(80 + i, 24 + i);
            var originalOutput = Encoding.UTF8.GetBytes($"Frame {i}");
            
            var result = await filter.TransformOutputAsync(
                originalOutput,
                screenBuffer,
                80 + i,
                24 + i,
                TimeSpan.FromMilliseconds(i * 5));
            
            if (result.IsEmpty)
                suppressedCount++;
        }

        // Assert - All frames during rapid resize should be suppressed
        Assert.Equal(10, suppressedCount);
    }

    [Fact]
    public async Task Filter_CustomDebounceMs_Respected()
    {
        // Arrange - Use a very short debounce
        var filter = new ResizeDebounceFilter(debounceMs: 10);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Trigger resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Wait longer than the short debounce
        await Task.Delay(20);
        
        var screenBuffer = CreateEmptyBuffer(100, 30);
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            100,
            30,
            TimeSpan.Zero);

        // Assert - After short debounce expires, should generate output (full redraw)
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public async Task Filter_DimensionMismatch_SuppressesOutput()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Trigger resize to new size
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Wait for debounce to expire
        await Task.Delay(60);
        
        // But provide a buffer with wrong dimensions (simulating stale render)
        var screenBuffer = CreateEmptyBuffer(80, 24);  // Old size
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            80,  // Wrong dimensions
            24,
            TimeSpan.Zero);

        // Assert - Dimension mismatch should suppress output
        Assert.True(result.IsEmpty, "Output should be suppressed when dimensions don't match pending resize");
    }

    [Fact]
    public async Task Filter_MultipleResizeEvents_ExtendsDebounce()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // First resize
        await filter.OnResizeAsync(100, 30, TimeSpan.Zero);
        
        // Wait 30ms (not enough for debounce to expire)
        await Task.Delay(30);
        
        // Second resize - should reset the debounce timer
        await filter.OnResizeAsync(110, 35, TimeSpan.Zero);
        
        // Wait another 30ms (40ms since first resize, but only 30ms since second)
        await Task.Delay(30);
        
        var screenBuffer = CreateEmptyBuffer(110, 35);
        var originalOutput = Encoding.UTF8.GetBytes("Hello");
        
        // Act
        var result = await filter.TransformOutputAsync(
            originalOutput,
            screenBuffer,
            110,
            35,
            TimeSpan.Zero);

        // Assert - Still within debounce window of second resize
        Assert.True(result.IsEmpty, "Debounce should be extended by second resize");
    }

    [Fact]
    public async Task Filter_RapidResize_DoesNotThrow()
    {
        // Arrange
        var filter = new ResizeDebounceFilter(debounceMs: 50);
        await filter.OnSessionStartAsync(80, 24, DateTimeOffset.UtcNow);
        
        // Simulate rapid resizing from multiple threads
        var tasks = new List<Task>();
        
        for (int i = 0; i < 100; i++)
        {
            var iteration = i;
            tasks.Add(Task.Run(async () =>
            {
                var width = 80 + (iteration % 20);
                var height = 24 + (iteration % 10);
                await filter.OnResizeAsync(width, height, TimeSpan.FromMilliseconds(iteration));
            }));
            
            tasks.Add(Task.Run(async () =>
            {
                var width = 80 + (iteration % 20);
                var height = 24 + (iteration % 10);
                var buffer = CreateEmptyBuffer(width, height);
                
                await filter.TransformOutputAsync(
                    Encoding.UTF8.GetBytes($"{iteration}"),
                    buffer,
                    width,
                    height,
                    TimeSpan.FromMilliseconds(iteration));
            }));
        }

        // Act & Assert - Should complete without exceptions
        await Task.WhenAll(tasks);
    }

    private static TerminalCell[,] CreateEmptyBuffer(int width, int height)
    {
        var buffer = new TerminalCell[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                buffer[y, x] = new TerminalCell(" ", null, null);
            }
        }
        return buffer;
    }
}
