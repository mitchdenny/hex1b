using Hex1b.Terminal;
using System.Text;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the OptimizedPresentationAdapter.
/// </summary>
public class OptimizedPresentationAdapterTests
{
    [Fact]
    public void Constructor_ValidatesInnerAdapter()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OptimizedPresentationAdapter(null!));
    }

    [Fact]
    public void Constructor_ForwardsProperties()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);

        // Act
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Assert
        Assert.Equal(80, optimizedAdapter.Width);
        Assert.Equal(24, optimizedAdapter.Height);
        Assert.Equal(mockAdapter.Capabilities, optimizedAdapter.Capabilities);
    }

    [Fact]
    public async Task WriteOutputAsync_FirstWrite_ForwardsToInner()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);
        var output = Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        await optimizedAdapter.WriteOutputAsync(output);

        // Assert
        Assert.Single(mockAdapter.WrittenData);
        Assert.Equal("Hello, World!", Encoding.UTF8.GetString(mockAdapter.WrittenData[0].Span));
    }

    [Fact]
    public async Task WriteOutputAsync_DuplicateWrite_Suppressed()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);
        var output = Encoding.UTF8.GetBytes("Hello");

        // Act
        await optimizedAdapter.WriteOutputAsync(output);
        await optimizedAdapter.WriteOutputAsync(output); // Same output again

        // Assert
        // First write should go through, second should be suppressed
        Assert.Single(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_DifferentContent_BothForwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Hello"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("World"));

        // Assert
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task WriteOutputAsync_CursorMovement_Suppressed()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write text, then move cursor to same position multiple times
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Text"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1H")); // Move to 1,1
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1H")); // Move to 1,1 again

        // Assert - Only first write and first cursor movement should go through
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task WriteOutputAsync_ClearScreen_OnlyFirstForwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Clear screen twice
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[2J"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[2J"));

        // Assert - Only first clear should go through
        Assert.Single(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_OverwriteSamePosition_Suppressed()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write at position, move back, write same content
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1HA"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1HA"));

        // Assert - Second write should be suppressed
        Assert.Single(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_OverwriteDifferentContent_Forwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write at position, move back, write different content
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1HA"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1HB"));

        // Assert - Both writes should go through
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task WriteOutputAsync_ColorChange_Forwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write text with color, then same text with different color
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[31mRed"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1H\x1b[32mRed"));

        // Assert - Both should go through (different colors)
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task WriteOutputAsync_SameColorChange_Suppressed()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Set color twice
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[31m"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[31m"));

        // Assert - Second should be suppressed
        Assert.Single(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_AttributeChange_Forwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write text, then same text with bold
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1HText"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\x1b[1;1H\x1b[1mText"));

        // Assert - Both should go through (different attributes)
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task EnterTuiModeAsync_ResetsCache()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);
        
        // Write some data
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Test"));

        // Act - Enter TUI mode (should reset cache)
        await optimizedAdapter.EnterTuiModeAsync();
        
        // Same data should now be forwarded since cache was reset
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Test"));

        // Assert
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    [Fact]
    public async Task Resize_UpdatesCacheSize()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);
        var resizeReceived = false;
        int newWidth = 0, newHeight = 0;

        optimizedAdapter.Resized += (w, h) =>
        {
            resizeReceived = true;
            newWidth = w;
            newHeight = h;
        };

        // Act
        mockAdapter.TriggerResize(100, 30);

        // Assert
        Assert.True(resizeReceived);
        Assert.Equal(100, newWidth);
        Assert.Equal(30, newHeight);
    }

    [Fact]
    public async Task Disconnected_EventForwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);
        var disconnectReceived = false;

        optimizedAdapter.Disconnected += () => disconnectReceived = true;

        // Act
        mockAdapter.TriggerDisconnect();

        // Assert
        Assert.True(disconnectReceived);
    }

    [Fact]
    public async Task WriteOutputAsync_EmptyData_NotForwarded()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act
        await optimizedAdapter.WriteOutputAsync(ReadOnlyMemory<byte>.Empty);

        // Assert
        Assert.Empty(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_MultipleLines_TrackedCorrectly()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write multiline text
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Line1\nLine2\nLine3"));
        
        // Write same multiline text again
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Line1\nLine2\nLine3"));

        // Assert - Second write should be suppressed
        Assert.Single(mockAdapter.WrittenData);
    }

    [Fact]
    public async Task WriteOutputAsync_NewlinesBetweenWrites_TrackedCorrectly()
    {
        // Arrange
        var mockAdapter = new MockPresentationAdapter(80, 24);
        var optimizedAdapter = new OptimizedPresentationAdapter(mockAdapter);

        // Act - Write text, then write same text on next line
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("Test"));
        await optimizedAdapter.WriteOutputAsync(Encoding.UTF8.GetBytes("\nTest"));

        // Assert - Both should go through (different positions)
        Assert.Equal(2, mockAdapter.WrittenData.Count);
    }

    // Mock adapter for testing
    private class MockPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly int _width;
        private readonly int _height;

        public MockPresentationAdapter(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public List<ReadOnlyMemory<byte>> WrittenData { get; } = new();

        public int Width => _width;
        public int Height => _height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;

        public event Action<int, int>? Resized;
        public event Action? Disconnected;

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WrittenData.Add(data);
            return ValueTask.CompletedTask;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void TriggerResize(int width, int height)
        {
            Resized?.Invoke(width, height);
        }

        public void TriggerDisconnect()
        {
            Disconnected?.Invoke();
        }
    }
}
