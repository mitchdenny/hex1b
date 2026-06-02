
namespace Hex1b.Tests;

/// <summary>
/// Tests for the terminal filter system.
/// </summary>
[TestClass]
public class TerminalFilterTests
{
    [TestMethod]
    public async Task WorkloadFilter_ReceivesSessionStart()
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
        Assert.IsTrue(filter.SessionStarted);
        Assert.AreEqual(80, filter.Width);
        Assert.AreEqual(24, filter.Height);
    }

    [TestMethod]
    public async Task WorkloadFilter_ReceivesOutput()
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
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Assert
        TestSeq.Single(filter.OutputChunks);
        Assert.Contains("Hello, World!", filter.OutputChunks[0]);
    }

    [TestMethod, Ignore("Frame complete notification currently only fires on channel close, not on drain. See issue with PumpWorkloadOutputAsync design.")]
    public async Task WorkloadFilter_ReceivesFrameComplete()
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
        
        // Wait for the text to appear, then wait for the next pump cycle to complete
        // which will trigger OnFrameCompleteAsync when the channel is drained
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Frame 1"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);
        
        // Write another chunk to ensure pump cycles again and triggers frame complete
        workload.Write("Frame 2");
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Frame 2"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal);

        // Assert - FrameComplete should have been called when channel drained after Frame 1
        Assert.IsTrue(filter.FrameCompleteCount > 0, $"Expected FrameCompleteCount > 0, but got {filter.FrameCompleteCount}. OutputChunks: {filter.OutputChunks.Count}");
    }

    [TestMethod]
    public async Task WorkloadFilter_ReceivesSessionEnd()
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
        Assert.IsTrue(filter.SessionEnded);
    }

    [TestMethod]
    public async Task PresentationFilter_ReceivesSessionStart()
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
        Assert.IsTrue(filter.SessionStarted);
    }

    [TestMethod]
    public async Task TerminalOptions_ValidatesWorkloadAdapter()
    {
        // Arrange
        var options = new Hex1bTerminalOptions
        {
            Width = 80,
            Height = 24,
            WorkloadAdapter = null // Missing required adapter
        };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => new Hex1bTerminal(options));
    }

    [TestMethod]
    public async Task TerminalOptions_Constructor_SetsDefaults()
    {
        // Act
        var options = new Hex1bTerminalOptions();

        // Assert
        Assert.AreEqual(80, options.Width);
        Assert.AreEqual(24, options.Height);
        Assert.IsNull(options.WorkloadAdapter);
        Assert.IsNull(options.PresentationAdapter);
        Assert.IsEmpty(options.WorkloadFilters);
        Assert.IsEmpty(options.PresentationFilters);
    }

    [TestMethod]
    public async Task MultipleFilters_AllReceiveEvents()
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
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Assert
        Assert.IsTrue(filter1.SessionStarted);
        Assert.IsTrue(filter2.SessionStarted);
        TestSeq.Single(filter1.OutputChunks);
        TestSeq.Single(filter2.OutputChunks);
    }

    [TestMethod]
    public async Task TokenBasedFilters_UsesApplyTokensPath()
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
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(TimeSpan.FromMilliseconds(100))
            .Build()
            .ApplyAsync(terminal);

        // Assert
        TestSeq.Single(filter.OutputChunks);
        Assert.Contains("Hello", filter.OutputChunks[0]);
        Assert.Contains("Red", filter.OutputChunks[0]);
        Assert.Contains("World", filter.OutputChunks[0]);
        
        // Verify terminal buffer was updated correctly
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Hello") && s.ContainsText("Red") && s.ContainsText("World"), TimeSpan.FromSeconds(5), "HelloRedWorld visible")
            .Capture("final")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual("HelloRedWorld", snapshot.GetLine(0).TrimEnd());
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

        public ValueTask OnInputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            InputChunks.Add(Hex1b.Tokens.AnsiTokenSerializer.Serialize(tokens));
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

        public ValueTask OnInputAsync(IReadOnlyList<Hex1b.Tokens.AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            InputChunks.Add(Hex1b.Tokens.AnsiTokenSerializer.Serialize(tokens));
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
