using Hex1b.Terminal;
using Hex1b.Terminal.Automation;

namespace Hex1b.Tests;

/// <summary>
/// Tests for the terminal filter system.
/// </summary>
public class TerminalFilterTests
{
    [Fact]
    public void WorkloadFilter_ReceivesSessionStart()
    {
        // Arrange
        var filter = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter);

        // Act
        using var terminal = new Hex1bTerminal(options);

        // Assert
        Assert.True(filter.SessionStarted);
        Assert.Equal(80, filter.Width);
        Assert.Equal(24, filter.Height);
    }

    [Fact]
    public void WorkloadFilter_ReceivesOutput()
    {
        // Arrange
        var filter = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter);
        using var terminal = new Hex1bTerminal(options);

        // Act
        workload.Write("Hello, World!");
        terminal.FlushOutput();

        // Assert
        Assert.Single(filter.OutputChunks);
        Assert.Contains("Hello, World!", filter.OutputChunks[0]);
    }

    [Fact]
    public void WorkloadFilter_ReceivesFrameComplete()
    {
        // Arrange
        var filter = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter);
        using var terminal = new Hex1bTerminal(options);

        // Act
        workload.Write("Frame 1");
        terminal.FlushOutput();

        // Assert
        Assert.True(filter.FrameCompleteCount > 0);
    }

    [Fact]
    public void WorkloadFilter_ReceivesSessionEnd()
    {
        // Arrange
        var filter = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter);
        var terminal = new Hex1bTerminal(options);

        // Act
        terminal.Dispose();

        // Assert
        Assert.True(filter.SessionEnded);
    }

    [Fact]
    public void PresentationFilter_ReceivesSessionStart()
    {
        // Arrange
        var filter = new TestPresentationFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.PresentationFilters.Add(filter);

        // Act
        using var terminal = new Hex1bTerminal(options);

        // Assert
        Assert.True(filter.SessionStarted);
    }

    [Fact]
    public void TerminalOptions_ValidatesWorkloadAdapter()
    {
        // Arrange
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = null // Missing required adapter
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Hex1bTerminal(options));
    }

    [Fact]
    public void TerminalOptions_Constructor_SetsDefaults()
    {
        // Act
        var options = new Hex1bTerminalOptions();

        // Assert
        Assert.Equal(80, options.Width);
        Assert.Equal(24, options.Height);
        Assert.Null(options.WorkloadAdapter);
        Assert.Null(options.PresentationAdapter);
        Assert.Empty(options.WorkloadFilters);
        Assert.Empty(options.PresentationFilters);
    }

    [Fact]
    public void MultipleFilters_AllReceiveEvents()
    {
        // Arrange
        var filter1 = new TestWorkloadFilter();
        var filter2 = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter1);
        options.WorkloadFilters.Add(filter2);
        using var terminal = new Hex1bTerminal(options);

        // Act
        workload.Write("Test");
        terminal.FlushOutput();

        // Assert
        Assert.True(filter1.SessionStarted);
        Assert.True(filter2.SessionStarted);
        Assert.Single(filter1.OutputChunks);
        Assert.Single(filter2.OutputChunks);
    }

    [Fact]
    public void TokenBasedFilters_UsesApplyTokensPath()
    {
        // Arrange
        var filter = new TestWorkloadFilter();
        var workload = new Hex1bAppWorkloadAdapter();
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = workload
        };
        options.WorkloadFilters.Add(filter);
        using var terminal = new Hex1bTerminal(options);

        // Act
        workload.Write("Hello\x1b[31mRed\x1b[0mWorld");
        terminal.FlushOutput();

        // Assert
        Assert.Single(filter.OutputChunks);
        Assert.Contains("Hello", filter.OutputChunks[0]);
        Assert.Contains("Red", filter.OutputChunks[0]);
        Assert.Contains("World", filter.OutputChunks[0]);
        
        // Verify terminal buffer was updated correctly
        var snapshot = terminal.CreateSnapshot();
        Assert.Equal("HelloRedWorld", snapshot.GetLine(0).TrimEnd());
    }

    // Test helpers

    private class TestWorkloadFilter : IHex1bTerminalWorkloadFilter
    {
        public bool SessionStarted { get; private set; }
        public bool SessionEnded { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public List<string> OutputChunks { get; } = new();
        public List<string> InputChunks { get; } = new();
        public int FrameCompleteCount { get; private set; }
        public List<(int Width, int Height)> Resizes { get; } = new();

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            SessionStarted = true;
            Width = width;
            Height = height;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnOutputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            OutputChunks.Add(Hex1b.Tokens.AnsiTokenSerializer.Serialize(tokens));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            FrameCompleteCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
        {
            InputChunks.Add(System.Text.Encoding.UTF8.GetString(data.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        {
            Resizes.Add((width, height));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            SessionEnded = true;
            return ValueTask.CompletedTask;
        }
    }

    private class TestPresentationFilter : IHex1bTerminalPresentationFilter
    {
        public bool SessionStarted { get; private set; }
        public bool SessionEnded { get; private set; }
        public List<string> OutputChunks { get; } = new();
        public List<string> InputChunks { get; } = new();

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            SessionStarted = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<Hex1b.Tokens.AnsiToken>> OnOutputAsync(IReadOnlyList<Hex1b.Tokens.AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(at => at.Token).ToList();
            OutputChunks.Add(Hex1b.Tokens.AnsiTokenSerializer.Serialize(tokens));
            return ValueTask.FromResult<IReadOnlyList<Hex1b.Tokens.AnsiToken>>(tokens);
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
        {
            InputChunks.Add(System.Text.Encoding.UTF8.GetString(data.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            SessionEnded = true;
            return ValueTask.CompletedTask;
        }
    }
}
