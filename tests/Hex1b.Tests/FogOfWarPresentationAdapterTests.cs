using Hex1b.Terminal;
using Hex1b.Theming;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the FogOfWarPresentationAdapter.
/// </summary>
public class FogOfWarPresentationAdapterTests
{
    [Fact]
    public async Task FogOfWar_PassesThroughNonColorSequences()
    {
        // Arrange
        var mock = new MockPresentationAdapter();
        var fogAdapter = new FogOfWarPresentationAdapter(mock, maxDistance: 10.0);
        var clearScreen = System.Text.Encoding.UTF8.GetBytes("\x1b[2J");

        // Act
        await fogAdapter.WriteOutputAsync(clearScreen);

        // Assert
        Assert.Single(mock.WrittenOutputs);
        var written = System.Text.Encoding.UTF8.GetString(mock.WrittenOutputs[0].Span);
        Assert.Equal("\x1b[2J", written);
    }

    [Fact]
    public async Task FogOfWar_ModifiesColorWhenMousePositionKnown()
    {
        // Arrange
        var mock = new MockPresentationAdapter();
        var fogAdapter = new FogOfWarPresentationAdapter(mock, maxDistance: 10.0);
        
        // Simulate mouse input at position (5, 5)
        var mouseInput = System.Text.Encoding.UTF8.GetBytes("\x1b[<0;6;6M"); // SGR mouse format (1-based)
        await fogAdapter.ReadInputAsync();
        mock.InputQueue.Enqueue(mouseInput);
        await fogAdapter.ReadInputAsync();

        // Simulate cursor position and RGB color output at (5, 5) - should be bright
        var output = System.Text.Encoding.UTF8.GetBytes("\x1b[6;6H\x1b[38;2;255;0;0mX");
        
        // Act
        await fogAdapter.WriteOutputAsync(output);

        // Assert
        Assert.Single(mock.WrittenOutputs);
        var written = System.Text.Encoding.UTF8.GetString(mock.WrittenOutputs[0].Span);
        
        // At distance 0, color should be mostly preserved (fog factor = 1.0)
        Assert.Contains("38;2;", written); // Should still have RGB color
        Assert.Contains("X", written); // Character should be preserved
    }

    [Fact]
    public async Task FogOfWar_DarkensColorAwayFromMouse()
    {
        // Arrange
        var mock = new MockPresentationAdapter();
        var fogAdapter = new FogOfWarPresentationAdapter(mock, maxDistance: 10.0);
        
        // Simulate mouse input at position (0, 0)
        var mouseInput = System.Text.Encoding.UTF8.GetBytes("\x1b[<0;1;1M");
        mock.InputQueue.Enqueue(mouseInput);
        await fogAdapter.ReadInputAsync();

        // Simulate cursor position and RGB color output at (20, 20) - far from mouse
        var output = System.Text.Encoding.UTF8.GetBytes("\x1b[21;21H\x1b[38;2;255;255;255mX");
        
        // Act
        await fogAdapter.WriteOutputAsync(output);

        // Assert
        Assert.Single(mock.WrittenOutputs);
        var written = System.Text.Encoding.UTF8.GetString(mock.WrittenOutputs[0].Span);
        
        // At large distance, color should be very dark (close to 0;0;0)
        Assert.Contains("38;2;", written); // Should still have RGB format
        Assert.Contains("X", written); // Character should be preserved
        
        // Colors should be significantly reduced (fog effect)
        var match = System.Text.RegularExpressions.Regex.Match(written, @"38;2;(\d+);(\d+);(\d+)");
        Assert.True(match.Success);
        var r = int.Parse(match.Groups[1].Value);
        var g = int.Parse(match.Groups[2].Value);
        var b = int.Parse(match.Groups[3].Value);
        
        // Color values should be much darker than original (255, 255, 255)
        Assert.True(r < 100, $"Red channel {r} should be darkened");
        Assert.True(g < 100, $"Green channel {g} should be darkened");
        Assert.True(b < 100, $"Blue channel {b} should be darkened");
    }

    [Fact]
    public async Task FogOfWar_PassesThroughBeforeMousePosition()
    {
        // Arrange
        var mock = new MockPresentationAdapter();
        var fogAdapter = new FogOfWarPresentationAdapter(mock, maxDistance: 10.0);
        
        // Output before mouse position is known
        var output = System.Text.Encoding.UTF8.GetBytes("\x1b[38;2;255;0;0mHello");
        
        // Act
        await fogAdapter.WriteOutputAsync(output);

        // Assert
        Assert.Single(mock.WrittenOutputs);
        var written = System.Text.Encoding.UTF8.GetString(mock.WrittenOutputs[0].Span);
        
        // Should pass through unchanged when mouse position is unknown
        Assert.Equal("\x1b[38;2;255;0;0mHello", written);
    }

    [Fact]
    public void FogOfWar_DimensionsMatchInner()
    {
        // Arrange
        var mock = new MockPresentationAdapter { Width = 100, Height = 50 };
        var fogAdapter = new FogOfWarPresentationAdapter(mock);

        // Assert
        Assert.Equal(100, fogAdapter.Width);
        Assert.Equal(50, fogAdapter.Height);
    }

    [Fact]
    public void FogOfWar_CapabilitiesMatchInner()
    {
        // Arrange
        var mock = new MockPresentationAdapter();
        var fogAdapter = new FogOfWarPresentationAdapter(mock);

        // Assert
        Assert.Equal(mock.Capabilities, fogAdapter.Capabilities);
    }

    // Mock presentation adapter for testing
    private class MockPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        public int Width { get; set; } = 80;
        public int Height { get; set; } = 24;
        public TerminalCapabilities Capabilities { get; set; } = TerminalCapabilities.Modern;
        
        public event Action<int, int>? Resized;
        public event Action? Disconnected;

        public List<ReadOnlyMemory<byte>> WrittenOutputs { get; } = new();
        public Queue<ReadOnlyMemory<byte>> InputQueue { get; } = new();

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WrittenOutputs.Add(data);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            if (InputQueue.Count > 0)
            {
                return ValueTask.FromResult(InputQueue.Dequeue());
            }
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask EnterTuiModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask ExitTuiModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void TriggerResize(int width, int height)
        {
            Width = width;
            Height = height;
            Resized?.Invoke(width, height);
        }

        public void TriggerDisconnect()
        {
            Disconnected?.Invoke();
        }
    }
}
