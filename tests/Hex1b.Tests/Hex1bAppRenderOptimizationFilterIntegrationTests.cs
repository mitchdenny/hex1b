using System.Runtime.CompilerServices;
using Hex1b.Input;
using Hex1b.Terminal;
using Hex1b.Terminal.Automation;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the Hex1bAppRenderOptimizationFilter to verify it only sends changed cells.
/// </summary>
/// <remarks>
/// These tests are timing-sensitive and use Collection to ensure they don't run in parallel
/// with other tests, which can cause CPU contention and timeouts.
/// </remarks>
public class Hex1bAppRenderOptimizationFilterIntegrationTests
{
    /// <summary>
    /// A null presentation adapter that just discards output.
    /// Required to trigger the presentation filter pipeline.
    /// </summary>
    private class NullPresentationAdapter : IHex1bTerminalPresentationAdapter, IDisposable
    {
        private readonly int _width;
        private readonly int _height;
        private readonly TaskCompletionSource _disconnected = new();
        
        public NullPresentationAdapter(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public int Width => _width;
        public int Height => _height;
        public TerminalCapabilities Capabilities => TerminalCapabilities.Minimal;
#pragma warning disable CS0067 // Event is never used - required by interface
        public event Action<int, int>? Resized;
        public event Action? Disconnected;
#pragma warning restore CS0067

        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
        {
            // Wait indefinitely until cancelled
            try
            {
                await _disconnected.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException) { }
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask FlushAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask EnterTuiModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask ExitTuiModeAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disconnected?.Invoke();
            _disconnected.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            Disconnected?.Invoke();
            _disconnected.TrySetResult();
        }
    }

    /// <summary>
    /// A test presentation filter that captures the token stream after delta encoding.
    /// Placed after the Hex1bAppRenderOptimizationFilter to see what actually gets sent.
    /// Uses the DEC 2026 synchronized update end sequence (?2026l) to detect frame completion.
    /// </summary>
    private class TokenCapturePresentationFilter : IHex1bTerminalPresentationFilter
    {
        private readonly object _lock = new();
        private TaskCompletionSource _frameCompleted = new();
        private int _pendingCellCount;
        
        public List<List<AnsiToken>> FrameTokens { get; } = new();
        public List<int> FrameCellCounts { get; } = new();
        
        /// <summary>
        /// Gets the number of complete frames captured.
        /// A frame is complete when we see PrivateModeToken(2026, false) - the ?2026l sequence.
        /// </summary>
        public int CompleteFrameCount { get; private set; }
        
        /// <summary>
        /// Clears the captured frame data. Call this before the action you want to measure
        /// to avoid race conditions with background renders.
        /// </summary>
        public void ClearCounts()
        {
            lock (_lock)
            {
                FrameTokens.Clear();
                FrameCellCounts.Clear();
                CompleteFrameCount = 0;
                _pendingCellCount = 0;
            }
        }
        
        /// <summary>
        /// Waits for a complete frame to be rendered.
        /// Uses the ?2026l sequence (end of synchronized update) as the frame completion signal.
        /// If a frame has already completed (CompleteFrameCount > 0), returns immediately.
        /// </summary>
        public async Task WaitForFrameAsync(CancellationToken ct = default)
        {
            // Check if a frame has already completed
            lock (_lock)
            {
                if (CompleteFrameCount > 0)
                    return;
            }
            
            // Add a timeout to help diagnose issues
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                // Spin until we observe a frame completion
                // This avoids the TCS race where frame completes between getting TCS and awaiting it
                while (true)
                {
                    TaskCompletionSource tcs;
                    int startFrameCount;
                    lock (_lock)
                    {
                        startFrameCount = CompleteFrameCount;
                        tcs = _frameCompleted;
                        
                        // Double-check in case a frame completed between the first check and now
                        if (startFrameCount > 0)
                            return;
                    }
                    
                    // Wait briefly for the TCS to be signaled
                    var completedTask = await Task.WhenAny(
                        tcs.Task,
                        Task.Delay(50, linkedCts.Token));
                    
                    // Check if frame count increased (handles race where we got stale TCS)
                    lock (_lock)
                    {
                        if (CompleteFrameCount > startFrameCount)
                            return;
                    }
                    
                    // If the TCS was signaled, we're done
                    if (completedTask == tcs.Task)
                        return;
                    
                    linkedCts.Token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Get debugging info
                int frameCount, tokenBatchCount;
                List<string> batchDetails;
                lock (_lock)
                {
                    frameCount = CompleteFrameCount;
                    tokenBatchCount = FrameTokens.Count;
                    batchDetails = FrameTokens
                        .Select((batch, i) => $"Batch{i}:[{string.Join(",", batch.Select(t => t.GetType().Name.Replace("Token", "")))}]")
                        .ToList();
                }
                throw new TimeoutException($"FrameCount={frameCount}, Batches={tokenBatchCount}. Batches: {string.Join(" | ", batchDetails)}");
            }
        }
        
        /// <summary>
        /// Waits for at least the specified number of complete frames.
        /// </summary>
        public async Task WaitForFramesAsync(int count, CancellationToken ct = default)
        {
            while (true)
            {
                int current;
                lock (_lock)
                {
                    current = CompleteFrameCount;
                }
                if (current >= count)
                    return;
                await WaitForFrameAsync(ct);
            }
        }
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            ClearCounts();
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(at => at.Token).ToList();
            
            // Count TextTokens as a proxy for cells being updated
            var textTokenCount = tokens.OfType<TextToken>().Sum(t => t.Text.Length);
            
            // Check for frame end signal: PrivateModeToken(2026, false) is ?2026l
            var isFrameEnd = tokens.OfType<PrivateModeToken>()
                .Any(pm => pm.Mode == 2026 && !pm.Enable);
            
            lock (_lock)
            {
                FrameTokens.Add(tokens);
                _pendingCellCount += textTokenCount;
                
                if (isFrameEnd)
                {
                    // Frame is complete - record the cell count and signal
                    FrameCellCounts.Add(_pendingCellCount);
                    _pendingCellCount = 0;
                    CompleteFrameCount++;
                    
                    // Signal waiters and reset for next frame
                    _frameCompleted.TrySetResult();
                    _frameCompleted = new TaskCompletionSource();
                }
            }
            
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DeltaFilter_IdenticalFrames_NoOutputAfterFirst()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new Hex1bAppRenderOptimizationFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(40, 10);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 40,
            Height = 10
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        // Static content that doesn't change
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBlockWidget("Static Content")
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .Wait(50) // Give app time to initialize
            .WaitUntil(s => s.ContainsText("Static Content"), TimeSpan.FromSeconds(5))
            .Wait(100) // Let a few frames pass
            .Capture("final")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // After waiting for content, we should have received at least some frames with text
        Assert.True(captureFilter.FrameTokens.Count >= 1, 
            $"Should have at least one frame. Had {captureFilter.FrameTokens.Count}");
        
        // Find the first frame that actually has text content (skip control-only frames)
        var framesWithContent = captureFilter.FrameCellCounts.Where(c => c > 0).ToList();
        Assert.True(framesWithContent.Count > 0, 
            $"At least one frame should have text content. Frame counts: [{string.Join(", ", captureFilter.FrameCellCounts)}]");
        
        var firstContentFrame = framesWithContent[0];
        
        // After the first content render, subsequent frames should have very few cells (delta only)
        if (framesWithContent.Count > 1)
        {
            for (int i = 1; i < framesWithContent.Count; i++)
            {
                Assert.True(framesWithContent[i] <= firstContentFrame / 4,
                    $"Frame {i} should have significantly fewer cells than first content frame. " +
                    $"First frame: {firstContentFrame}, Frame {i}: {framesWithContent[i]}");
            }
        }
    }

    [Fact]
    public async Task DeltaFilter_ButtonClick_OnlyUpdatesButtonCell()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new Hex1bAppRenderOptimizationFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(60, 20);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 60,
            Height = 20
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var clickCount = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Header Text That Should Not Change"),
                    new ButtonWidget($"Clicks: {clickCount}")
                        .OnClick(_ => { clickCount++; return Task.CompletedTask; }),
                    new TextBlockWidget("Footer Text That Should Not Change"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Clicks: 0"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait for at least one complete frame (signaled by ?2026l), then clear counts
        await captureFilter.WaitForFrameAsync(TestContext.Current.CancellationToken);
        var frameCountBeforeClear = captureFilter.CompleteFrameCount;
        captureFilter.ClearCounts();
        
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click the focused button
            .WaitUntil(s => s.ContainsText("Clicks: 1"), TimeSpan.FromSeconds(5))
            .Capture("after_click")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // The frames after the click should only update the button, not the header/footer
        // A full repaint of a 60x20 terminal would be ~1200 cells
        // Just updating "Clicks: 0" to "Clicks: 1" should be ~10-20 cells max
        
        for (int i = 0; i < captureFilter.FrameCellCounts.Count; i++)
        {
            // Allow some buffer for cursor position tokens etc, but should be way less than full screen
            Assert.True(captureFilter.FrameCellCounts[i] < 100,
                $"Frame {i} after click should have minimal cells (< 100), but had {captureFilter.FrameCellCounts[i]}. " +
                $"This suggests the delta filter is not working - it's repainting too much.");
        }
    }

    [Fact]
    public async Task DeltaFilter_ListSelection_OnlyUpdatesChangedRows()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new Hex1bAppRenderOptimizationFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(40, 15);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 40,
            Height = 15
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var items = new List<string> { "Item 1", "Item 2", "Item 3", "Item 4", "Item 5" };
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("Static Header"),
                    new ListWidget(items),
                    new TextBlockWidget("Static Footer"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Item 1"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait for at least one complete frame (signaled by ?2026l), then clear counts
        await captureFilter.WaitForFrameAsync(TestContext.Current.CancellationToken);
        captureFilter.ClearCounts();
        
        // Navigate down in the list - should only update 2 rows (old selection, new selection)
        await new Hex1bTerminalInputSequenceBuilder()
            .Down() // Move selection from Item 1 to Item 2
            .Capture("after_nav")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // Moving selection should only update 2 list items (old and new selection)
        // That's roughly 2 * 40 = 80 characters max, but with attributes maybe 100
        // Should definitely NOT repaint header, footer, or unchanged list items
        
        for (int i = 0; i < captureFilter.FrameCellCounts.Count; i++)
        {
            Assert.True(captureFilter.FrameCellCounts[i] < 200,
                $"Frame {i} after list navigation should have < 200 cells, but had {captureFilter.FrameCellCounts[i]}. " +
                $"Only 2 list rows should need updating when changing selection.");
        }
    }

    [Fact]
    public async Task DeltaFilter_Counter_OnlyUpdatesNumberDisplay()
    {
        // Arrange
        var captureFilter = new TokenCapturePresentationFilter();
        var deltaFilter = new Hex1bAppRenderOptimizationFilter();
        
        using var workload = new Hex1bAppWorkloadAdapter();
        using var presentation = new NullPresentationAdapter(50, 10);
        
        var terminalOptions = new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload,
            PresentationAdapter = presentation,
            Width = 50,
            Height = 10
        };
        terminalOptions.PresentationFilters.Add(deltaFilter);
        terminalOptions.PresentationFilters.Add(captureFilter);
        
        using var terminal = new Hex1bTerminal(terminalOptions);
        
        var counter = 0;
        
        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget("This header should not repaint"),
                    new HStackWidget(new Hex1bWidget[]
                    {
                        new ButtonWidget("-").OnClick(_ => { counter--; return Task.CompletedTask; }),
                        new TextBlockWidget($"  Count: {counter}  "),
                        new ButtonWidget("+").OnClick(_ => { counter++; return Task.CompletedTask; }),
                    }),
                    new TextBlockWidget("This footer should not repaint"),
                })
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Count: 0"), TimeSpan.FromSeconds(5))
            .Tab() // Focus moves from - button to + button
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Wait for at least one complete frame (signaled by ?2026l), then clear counts
        await captureFilter.WaitForFrameAsync(TestContext.Current.CancellationToken);
        captureFilter.ClearCounts();
        
        // Increment the counter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click +
            .WaitUntil(s => s.ContainsText("Count: 1"), TimeSpan.FromSeconds(5))
            .Capture("after_increment")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;

        // Assert
        // Incrementing counter should only update "Count: 0" -> "Count: 1"
        // That's about 10-15 characters, not the whole 50x10 = 500 cell screen
        
        for (int i = 0; i < captureFilter.FrameCellCounts.Count; i++)
        {
            Assert.True(captureFilter.FrameCellCounts[i] < 100,
                $"Frame {i} after counter increment should have < 100 cells, but had {captureFilter.FrameCellCounts[i]}. " +
                $"Only the counter value should need updating, not the whole screen.");
        }
    }

    /// <summary>
    /// A presentation filter that captures exact cell positions that are updated.
    /// Tracks cursor position tokens followed by text tokens to determine exactly which cells are written.
    /// Uses the DEC 2026 synchronized update end sequence (?2026l) to detect frame completion.
    /// </summary>
    private class CellPositionCapturePresentationFilter : IHex1bTerminalPresentationFilter
    {
        private readonly object _lock = new();
        private TaskCompletionSource _frameCompleted = new();
        private HashSet<(int X, int Y)> _pendingCellPositions = new();
        
        public List<HashSet<(int X, int Y)>> FrameCellPositions { get; } = new();
        
        /// <summary>
        /// Gets the number of complete frames captured.
        /// </summary>
        public int CompleteFrameCount { get; private set; }
        
        /// <summary>
        /// Clears the captured frame data. Call this before the action you want to measure
        /// to avoid race conditions with background renders.
        /// </summary>
        public void ClearCounts()
        {
            lock (_lock)
            {
                FrameCellPositions.Clear();
                CompleteFrameCount = 0;
                _pendingCellPositions.Clear();
            }
        }
        
        /// <summary>
        /// Waits for a complete frame to be rendered.
        /// If a frame has already completed (CompleteFrameCount > 0), returns immediately.
        /// </summary>
        public async Task WaitForFrameAsync(CancellationToken ct = default)
        {
            // Check if a frame has already completed
            lock (_lock)
            {
                if (CompleteFrameCount > 0)
                    return;
            }
            
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            // Spin until we observe a frame completion
            while (true)
            {
                TaskCompletionSource tcs;
                int startFrameCount;
                lock (_lock)
                {
                    startFrameCount = CompleteFrameCount;
                    tcs = _frameCompleted;
                    
                    // Double-check in case a frame completed between the first check and now
                    if (startFrameCount > 0)
                        return;
                }
                
                // Wait briefly for the TCS to be signaled
                var completedTask = await Task.WhenAny(
                    tcs.Task,
                    Task.Delay(50, linkedCts.Token));
                
                // Check if frame count increased
                lock (_lock)
                {
                    if (CompleteFrameCount > startFrameCount)
                        return;
                }
                
                // If the TCS was signaled, we're done
                if (completedTask == tcs.Task)
                    return;
                
                linkedCts.Token.ThrowIfCancellationRequested();
            }
        }
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            ClearCounts();
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(at => at.Token).ToList();
            
            // Parse cursor positions and text to determine exact cell updates
            int cursorX = 0;
            int cursorY = 0;
            
            // Check for frame end signal: PrivateModeToken(2026, false) is ?2026l
            var isFrameEnd = tokens.OfType<PrivateModeToken>()
                .Any(pm => pm.Mode == 2026 && !pm.Enable);
            
            lock (_lock)
            {
                foreach (var token in tokens)
                {
                    switch (token)
                    {
                        case CursorPositionToken cpt:
                            // CursorPositionToken uses 1-based coordinates
                            cursorX = cpt.Column - 1;
                            cursorY = cpt.Row - 1;
                            break;
                            
                        case TextToken tt:
                            // Each character in the text token is a cell update
                            foreach (var ch in tt.Text)
                            {
                                if (ch >= ' ') // Skip control characters
                                {
                                    _pendingCellPositions.Add((cursorX, cursorY));
                                    cursorX++;
                                }
                            }
                            break;
                    }
                }
                
                if (isFrameEnd)
                {
                    // Frame is complete - record positions and signal
                    FrameCellPositions.Add(_pendingCellPositions);
                    _pendingCellPositions = new HashSet<(int X, int Y)>();
                    CompleteFrameCount++;
                    
                    // Signal waiters and reset for next frame
                    _frameCompleted.TrySetResult();
                    _frameCompleted = new TaskCompletionSource();
                }
            }
            
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
        }

        public ValueTask OnInputAsync(ReadOnlyMemory<byte> data, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// When a node is replaced with a smaller one, the old region must be cleared.
    /// This tests the fix for the "ghosting" bug where old content remained visible.
    /// </summary>
    [Fact]
    public async Task DeltaFilter_NodeReplacement_ClearsOldRegion()
    {
        // Arrange
        const int terminalWidth = 50;
        const int terminalHeight = 10;
        
        using var workload = new Hex1bAppWorkloadAdapter();
        
        using var terminal = new Hex1bTerminal(workload, terminalWidth, terminalHeight);
        
        // State that toggles between a large widget and a small widget
        var showLarge = true;
        
        using var app = new Hex1bApp(
            ctx =>
            {
                Hex1bWidget content;
                if (showLarge)
                {
                    // Large widget with more children
                    content = ctx.VStack(v => [
                        v.Text("Line 1: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 2: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 3: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 4: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                        v.Text("Line 5: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX"),
                    ]);
                }
                else
                {
                    // Small widget with fewer children
                    content = ctx.VStack(v => [
                        v.Text("SMALL")
                    ]);
                }
                
                return Task.FromResult<Hex1bWidget>(
                    ctx.VStack(v => [
                        v.Button("Toggle").OnClick(_ => { showLarge = !showLarge; return Task.CompletedTask; }),
                        content
                    ])
                );
            },
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // Act - wait for initial render with large widget (5 lines)
        var runTask = app.RunAsync(TestContext.Current.CancellationToken);
        var beforeToggle = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Line 5"), TimeSpan.FromSeconds(5))
            .Wait(50)
            .Capture("before_toggle")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        
        // Verify initial state has all 5 lines
        Assert.True(beforeToggle.ContainsText("Line 1"), "Should have Line 1 before toggle");
        Assert.True(beforeToggle.ContainsText("Line 5"), "Should have Line 5 before toggle");
        
        // Toggle to small widget by clicking the button
        var afterToggle = await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter) // Click Toggle button
            .WaitUntil(s => s.ContainsText("SMALL"), TimeSpan.FromSeconds(5))
            .Wait(50)
            .Capture("after_toggle")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);
        await runTask;
        
        // Assert - verify the terminal buffer has cleared the old content
        Assert.True(afterToggle.ContainsText("SMALL"), "Should have SMALL after toggle");
        
        // Lines 2-5 should be GONE (cleared to spaces)
        Assert.False(afterToggle.ContainsText("Line 2"), 
            $"Line 2 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 3"), 
            $"Line 3 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 4"), 
            $"Line 4 should be cleared after toggle. Buffer:\n{afterToggle}");
        Assert.False(afterToggle.ContainsText("Line 5"), 
            $"Line 5 should be cleared after toggle. Buffer:\n{afterToggle}");
    }
}
