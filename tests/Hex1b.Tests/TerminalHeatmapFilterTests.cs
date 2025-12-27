using Hex1b.Terminal;
using Hex1b.Widgets;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

public class TerminalHeatmapFilterTests
{
    [Fact]
    public void Constructor_InitializesHeatmapDataStructures()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        
        // Act
        var filter = new TerminalHeatmapFilter(innerAdapter);
        
        // Assert
        Assert.False(filter.IsEnabled);
        Assert.Equal(80, filter.Width);
        Assert.Equal(24, filter.Height);
    }

    [Fact]
    public void Enable_SetsEnabledState()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        
        // Act
        filter.Enable();
        
        // Assert
        Assert.True(filter.IsEnabled);
    }

    [Fact]
    public void Disable_ClearsEnabledState()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        filter.Enable();
        
        // Act
        filter.Disable();
        
        // Assert
        Assert.False(filter.IsEnabled);
    }

    [Fact]
    public async Task WriteOutputAsync_PassesThroughWhenDisabled()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        var data = new byte[] { 0x1b, 0x5b, 0x48 }; // ESC[H
        
        // Act
        await filter.WriteOutputAsync(data, TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Single(innerAdapter.WrittenOutputs);
        Assert.Equal(data, innerAdapter.WrittenOutputs[0].ToArray());
    }

    [Fact]
    public async Task WriteOutputAsync_ShowsHeatmapWhenEnabledWithTerminal()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        var workload = new Hex1bAppWorkloadAdapter(filter.Capabilities);
        var terminal = new Hex1bTerminal(filter, workload, 80, 24);
        filter.AttachTerminal(terminal);
        
        filter.Enable();
        
        // Wait for Enable() to complete its async render
        await innerAdapter.WaitForOutputAsync();
        innerAdapter.WrittenOutputs.Clear(); // Clear the heatmap render from Enable()
        
        var data = new byte[] { 0x1b, 0x5b, 0x48 }; // ESC[H
        
        // Act
        await filter.WriteOutputAsync(data, TestContext.Current.CancellationToken);
        
        // Assert - should have heatmap output instead of original
        Assert.Single(innerAdapter.WrittenOutputs);
        var output = System.Text.Encoding.UTF8.GetString(innerAdapter.WrittenOutputs[0].Span);
        Assert.Contains("\x1b[H\x1b[2J", output); // Should have clear screen
        
        terminal.Dispose();
    }

    [Fact]
    public async Task ReadInputAsync_PassesThroughInput()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        var inputData = new byte[] { 0x1b, 0x5b, 0x41 }; // ESC[A (up arrow)
        innerAdapter.QueueInput(inputData);
        
        // Act
        var result = await filter.ReadInputAsync(TestContext.Current.CancellationToken);
        
        // Assert
        Assert.Equal(inputData, result.ToArray());
    }

    [Fact]
    public async Task EnterTuiModeAsync_ForwardsToInnerAdapter()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        
        // Act
        await filter.EnterTuiModeAsync(TestContext.Current.CancellationToken);
        
        // Assert
        Assert.True(innerAdapter.InTuiMode);
    }

    [Fact]
    public async Task ExitTuiModeAsync_ForwardsToInnerAdapter()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        await filter.EnterTuiModeAsync(TestContext.Current.CancellationToken);
        
        // Act
        await filter.ExitTuiModeAsync(TestContext.Current.CancellationToken);
        
        // Assert
        Assert.False(innerAdapter.InTuiMode);
    }

    [Fact]
    public void Resized_EventIsForwarded()
    {
        // Arrange
        var innerAdapter = new TestPresentationAdapter(80, 24);
        var filter = new TerminalHeatmapFilter(innerAdapter);
        int? newWidth = null;
        int? newHeight = null;
        filter.Resized += (w, h) =>
        {
            newWidth = w;
            newHeight = h;
        };
        
        // Act
        innerAdapter.TriggerResize(100, 40);
        
        // Assert
        Assert.Equal(100, newWidth);
        Assert.Equal(40, newHeight);
    }

    // Helper class for testing
    private class TestPresentationAdapter : IHex1bTerminalPresentationAdapter
    {
        private readonly Queue<ReadOnlyMemory<byte>> _inputQueue = new();
        private readonly TaskCompletionSource _outputReceived = new();
        private int _width;
        private int _height;

        public TestPresentationAdapter(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public List<ReadOnlyMemory<byte>> WrittenOutputs { get; } = new();
        public bool InTuiMode { get; private set; }

        public int Width => _width;
        public int Height => _height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;

        public event Action<int, int>? Resized;
        public event Action? Disconnected;

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WrittenOutputs.Add(data);
            _outputReceived.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public async Task WaitForOutputAsync()
        {
            // Wait for at least one output to be written
            await _outputReceived.Task.ConfigureAwait(false);
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            if (_inputQueue.Count > 0)
            {
                return ValueTask.FromResult(_inputQueue.Dequeue());
            }
            return ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        }

        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

        public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
        {
            InTuiMode = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
        {
            InTuiMode = false;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void QueueInput(byte[] data)
        {
            _inputQueue.Enqueue(data);
        }

        public void TriggerResize(int newWidth, int newHeight)
        {
            _width = newWidth;
            _height = newHeight;
            Resized?.Invoke(newWidth, newHeight);
        }

        public void TriggerDisconnect()
        {
            Disconnected?.Invoke();
        }
    }
}
