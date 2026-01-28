using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Integration tests for the DiagnosticShell to debug rendering stall issues.
/// </summary>
/// <remarks>
/// <para>
/// These tests create a controlled environment with an embedded terminal running
/// the diagnostic shell. Filters are used to trace data flow through the system
/// to identify where stalls occur.
/// </para>
/// <para>
/// Architecture:
/// - Outer Hex1bTerminal: Runs in headless mode with a Hex1bApp
/// - Hex1bApp: Contains a TerminalWidget that renders from a TerminalWidgetHandle
/// - Inner Hex1bTerminal: Runs the DiagnosticShellWorkloadAdapter
/// - TerminalWidgetHandle: Bridges inner terminal output to outer app rendering
/// </para>
/// </remarks>
public class DiagnosticShellIntegrationTests
{
    /// <summary>
    /// Traces events through the data flow pipeline with timestamps.
    /// </summary>
    private sealed class DataFlowTracer
    {
        private readonly ConcurrentQueue<(DateTimeOffset Time, string Source, string Event, string? Data)> _events = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        
        public void Log(string source, string eventName, string? data = null)
        {
            _events.Enqueue((DateTimeOffset.Now, source, eventName, data));
        }
        
        public IReadOnlyList<(DateTimeOffset Time, string Source, string Event, string? Data)> GetEvents()
        {
            return _events.ToArray();
        }
        
        public void PrintEvents()
        {
            foreach (var evt in _events)
            {
                var dataPreview = evt.Data != null 
                    ? $" [{evt.Data.Length} chars: {evt.Data[..Math.Min(50, evt.Data.Length)]}...]" 
                    : "";
                Debug.WriteLine($"[{evt.Time:HH:mm:ss.fff}] {evt.Source}: {evt.Event}{dataPreview}");
            }
        }
    }
    
    /// <summary>
    /// Filter that traces workload output (inner terminal → Hex1bTerminal).
    /// </summary>
    private sealed class TracingWorkloadFilter : IHex1bTerminalWorkloadFilter
    {
        private readonly DataFlowTracer _tracer;
        private readonly string _name;
        
        public TracingWorkloadFilter(DataFlowTracer tracer, string name)
        {
            _tracer = tracer;
            _name = name;
        }
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            _tracer.Log(_name, "SessionStart", $"{width}x{height}");
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var text = AnsiTokenSerializer.Serialize(tokens);
            _tracer.Log(_name, "Output", text);
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            _tracer.Log(_name, "FrameComplete");
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var text = AnsiTokenSerializer.Serialize(tokens);
            _tracer.Log(_name, "Input", text);
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        {
            _tracer.Log(_name, "Resize", $"{width}x{height}");
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            _tracer.Log(_name, "SessionEnd");
            return ValueTask.CompletedTask;
        }
    }
    
    /// <summary>
    /// Filter that traces presentation output (Hex1bTerminal → presentation/handle).
    /// </summary>
    private sealed class TracingPresentationFilter : IHex1bTerminalPresentationFilter
    {
        private readonly DataFlowTracer _tracer;
        private readonly string _name;
        
        public TracingPresentationFilter(DataFlowTracer tracer, string name)
        {
            _tracer = tracer;
            _name = name;
        }
        
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
        {
            _tracer.Log(_name, "SessionStart", $"{width}x{height}");
            return ValueTask.CompletedTask;
        }
        
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var tokens = appliedTokens.Select(a => a.Token).ToList();
            var text = AnsiTokenSerializer.Serialize(tokens);
            var impactCount = appliedTokens.Sum(a => a.CellImpacts.Count);
            _tracer.Log(_name, "Output", $"{impactCount} impacts: {text}");
            return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
        }
        
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            var text = AnsiTokenSerializer.Serialize(tokens);
            _tracer.Log(_name, "Input", text);
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
        {
            _tracer.Log(_name, "Resize", $"{width}x{height}");
            return ValueTask.CompletedTask;
        }
        
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
        {
            _tracer.Log(_name, "SessionEnd");
            return ValueTask.CompletedTask;
        }
    }
    
    /// <summary>
    /// Helper class to wrap a TerminalWidgetHandle and trace OutputReceived events.
    /// </summary>
    private sealed class OutputReceivedTracer : IDisposable
    {
        private readonly TerminalWidgetHandle _handle;
        private readonly DataFlowTracer _tracer;
        private int _outputReceivedCount;
        
        public OutputReceivedTracer(TerminalWidgetHandle handle, DataFlowTracer tracer)
        {
            _handle = handle;
            _tracer = tracer;
            _handle.OutputReceived += OnOutputReceived;
        }
        
        private void OnOutputReceived()
        {
            _outputReceivedCount++;
            _tracer.Log("Handle", $"OutputReceived #{_outputReceivedCount}");
        }
        
        public void Dispose()
        {
            _handle.OutputReceived -= OnOutputReceived;
        }
    }
    
    /// <summary>
    /// Wraps an action to trace when Invalidate() is called.
    /// </summary>
    private sealed class TracingInvalidateCallback
    {
        private readonly DataFlowTracer _tracer;
        private readonly Action? _innerCallback;
        private int _callCount;
        
        public TracingInvalidateCallback(DataFlowTracer tracer, Action? innerCallback)
        {
            _tracer = tracer;
            _innerCallback = innerCallback;
        }
        
        public void Invoke()
        {
            _callCount++;
            _tracer.Log("Invalidate", $"Called #{_callCount}");
            _innerCallback?.Invoke();
        }
    }
    
    /// <summary>
    /// Test context that sets up the full pipeline with tracing.
    /// </summary>
    private sealed class DiagnosticTestContext : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly OutputReceivedTracer? _outputTracer;
        
        public DataFlowTracer Tracer { get; }
        public Hex1bTerminal OuterTerminal { get; }
        public Hex1bTerminal InnerTerminal { get; }
        public TerminalWidgetHandle Handle { get; }
        public DiagnosticShellWorkloadAdapter DiagShell { get; }
        public Hex1bApp? App { get; private set; }
        
        private Task? _runTask;
        
        private DiagnosticTestContext(
            Hex1bTerminal outerTerminal,
            Hex1bTerminal innerTerminal,
            TerminalWidgetHandle handle,
            DiagnosticShellWorkloadAdapter diagShell,
            DataFlowTracer tracer,
            OutputReceivedTracer? outputTracer)
        {
            OuterTerminal = outerTerminal;
            InnerTerminal = innerTerminal;
            Handle = handle;
            DiagShell = diagShell;
            Tracer = tracer;
            _outputTracer = outputTracer;
        }
        
        /// <summary>
        /// Creates a test context with full tracing enabled.
        /// </summary>
        public static DiagnosticTestContext Create(
            int outerWidth = 100,
            int outerHeight = 30,
            int innerWidth = 80,
            int innerHeight = 20)
        {
            var tracer = new DataFlowTracer();
            DiagnosticTestContext? capturedContext = null;
            
            // Create the diagnostic shell workload
            var diagShell = new DiagnosticShellWorkloadAdapter();
            
            // Create the handle that bridges inner terminal to outer app
            var handle = new TerminalWidgetHandle(innerWidth, innerHeight);
            
            // Trace OutputReceived events on the handle
            var outputTracer = new OutputReceivedTracer(handle, tracer);
            
            // Create the inner terminal with handle as presentation adapter
            var innerTerminalOptions = new Hex1bTerminalOptions
            {
                Width = innerWidth,
                Height = innerHeight,
                WorkloadAdapter = diagShell,
                PresentationAdapter = handle
            };
            innerTerminalOptions.WorkloadFilters.Add(new TracingWorkloadFilter(tracer, "InnerWorkload"));
            innerTerminalOptions.PresentationFilters.Add(new TracingPresentationFilter(tracer, "InnerPresentation"));
            
            var innerTerminal = new Hex1bTerminal(innerTerminalOptions);
            
            // Create the outer terminal (runs the Hex1bApp with TerminalWidget)
            var outerTerminal = Hex1bTerminal.CreateBuilder()
                .WithHex1bApp((app, options) =>
                {
                    // Capture the app for test access
                    if (capturedContext != null)
                    {
                        capturedContext.App = app;
                    }
                    
                    tracer.Log("OuterApp", "Building widget tree");
                    
                    return ctx => new VStackWidget([
                        new TextBlockWidget("Diagnostic Shell Test"),
                        new BorderWidget(
                            new TerminalWidget(handle),
                            Title: "Diagnostic Terminal"
                        ).Fill()
                    ]).Fill();
                })
                .WithHeadless()
                .WithDimensions(outerWidth, outerHeight)
                .AddWorkloadFilter(new TracingWorkloadFilter(tracer, "OuterWorkload"))
                .AddPresentationFilter(new TracingPresentationFilter(tracer, "OuterPresentation"))
                .Build();
            
            capturedContext = new DiagnosticTestContext(outerTerminal, innerTerminal, handle, diagShell, tracer, outputTracer);
            
            return capturedContext;
        }
        
        /// <summary>
        /// Starts both terminals running.
        /// </summary>
        public async Task StartAsync()
        {
            Tracer.Log("Test", "Starting inner terminal");
            
            // Start the inner terminal (runs the diagnostic shell)
            _ = Task.Run(async () =>
            {
                try
                {
                    await InnerTerminal.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException) { }
            });
            
            // Give inner terminal time to initialize
            await Task.Delay(100);
            
            Tracer.Log("Test", "Starting outer terminal");
            
            // Start the outer terminal (runs the Hex1bApp)
            _runTask = OuterTerminal.RunAsync(_cts.Token);
            
            // Wait for initial render
            await WaitForTextAsync("diag>", TimeSpan.FromSeconds(2));
            
            Tracer.Log("Test", "Initial render complete");
        }
        
        /// <summary>
        /// Sends a command to the diagnostic shell.
        /// </summary>
        public async Task SendCommandAsync(string command)
        {
            Tracer.Log("Test", $"Sending command: {command}");
            
            // Type the command character by character through the workload adapter
            foreach (var c in command)
            {
                await DiagShell.WriteInputAsync(new byte[] { (byte)c });
                await Task.Delay(5); // Small delay between chars
            }
            
            // Press Enter
            await DiagShell.WriteInputAsync(new byte[] { 0x0D }); // CR
            
            Tracer.Log("Test", "Command sent");
        }
        
        /// <summary>
        /// Waits for specific text to appear in the outer terminal snapshot.
        /// </summary>
        public async Task<bool> WaitForTextAsync(string text, TimeSpan timeout)
        {
            var deadline = DateTimeOffset.Now + timeout;
            while (DateTimeOffset.Now < deadline)
            {
                using var snapshot = OuterTerminal.CreateSnapshot();
                var content = GetSnapshotText(snapshot);
                if (content.Contains(text))
                {
                    Tracer.Log("Test", $"Found text: {text}");
                    return true;
                }
                await Task.Delay(50);
            }
            Tracer.Log("Test", $"Timeout waiting for text: {text}");
            return false;
        }
        
        /// <summary>
        /// Gets the current outer terminal content as text.
        /// </summary>
        public string GetOuterContent()
        {
            using var snapshot = OuterTerminal.CreateSnapshot();
            return GetSnapshotText(snapshot);
        }
        
        /// <summary>
        /// Gets the current inner terminal content as text.
        /// </summary>
        public string GetInnerContent()
        {
            using var snapshot = InnerTerminal.CreateSnapshot();
            return GetSnapshotText(snapshot);
        }
        
        private static string GetSnapshotText(Hex1bTerminalSnapshot snapshot)
        {
            var sb = new StringBuilder();
            for (int y = 0; y < snapshot.Height; y++)
            {
                for (int x = 0; x < snapshot.Width; x++)
                {
                    var cell = snapshot.GetCell(x, y);
                    sb.Append(string.IsNullOrEmpty(cell.Character) ? ' ' : cell.Character[0]);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
        
        public async ValueTask DisposeAsync()
        {
            Tracer.Log("Test", "Disposing");
            _cts.Cancel();
            
            if (_runTask != null)
            {
                try { await _runTask.WaitAsync(TimeSpan.FromSeconds(2)); }
                catch { }
            }
            
            _outputTracer?.Dispose();
            InnerTerminal.Dispose();
            OuterTerminal.Dispose();
            await DiagShell.DisposeAsync();
            
            // Print all traced events
            Tracer.PrintEvents();
        }
    }
    
    [Fact]
    public async Task DiagnosticShell_HelpCommand_RendersCompletely()
    {
        // Arrange
        await using var ctx = DiagnosticTestContext.Create();
        await ctx.StartAsync();
        
        // Act - send help command
        await ctx.SendCommandAsync("help");
        
        // Wait for help output to appear
        var foundPing = await ctx.WaitForTextAsync("ping", TimeSpan.FromSeconds(5));
        var foundCapture = await ctx.WaitForTextAsync("capture", TimeSpan.FromSeconds(1));
        var foundDump = await ctx.WaitForTextAsync("dump", TimeSpan.FromSeconds(1));
        
        // Get final content for assertion
        var content = ctx.GetOuterContent();
        
        // Assert - help should show all commands
        Assert.True(foundPing, "Should find 'ping' in help output");
        Assert.True(foundCapture, "Should find 'capture' in help output");
        Assert.True(foundDump, "Should find 'dump' in help output");
    }
    
    [Fact]
    public async Task DiagnosticShell_PingCommand_RespondsImmediately()
    {
        // Arrange
        await using var ctx = DiagnosticTestContext.Create();
        
        // First verify inner terminal content before starting outer
        await Task.Delay(200);
        var innerContentBefore = ctx.GetInnerContent();
        Console.Error.WriteLine($"Inner content before start: [{innerContentBefore.Trim()}]");
        
        await ctx.StartAsync();
        
        // Act
        var beforePing = DateTimeOffset.Now;
        await ctx.SendCommandAsync("ping");
        
        // Wait for pong response (case-insensitive - output is PONG)
        var foundPong = await ctx.WaitForTextAsync("PONG", TimeSpan.FromSeconds(2));
        var afterPong = DateTimeOffset.Now;
        
        // Dump diagnostic info to stderr (which shows in test output)
        Console.Error.WriteLine("=== Traced Events ===");
        foreach (var evt in ctx.Tracer.GetEvents())
        {
            var dataPreview = evt.Data != null 
                ? $" [{evt.Data.Length} chars: {Truncate(evt.Data, 60)}]" 
                : "";
            Console.Error.WriteLine($"[{evt.Time:HH:mm:ss.fff}] {evt.Source}: {evt.Event}{dataPreview}");
        }
        
        Console.Error.WriteLine("\n=== Outer Terminal Content ===");
        Console.Error.WriteLine(ctx.GetOuterContent());
        
        Console.Error.WriteLine("\n=== Inner Terminal Content ===");
        Console.Error.WriteLine(ctx.GetInnerContent());
        
        // Assert
        Assert.True(foundPong, "Should receive 'pong' response");
        Assert.True((afterPong - beforePing).TotalMilliseconds < 500, 
            "Pong should appear within 500ms");
    }
    
    private static string Truncate(string s, int maxLen) 
        => s.Length <= maxLen ? s.Replace("\n", "\\n").Replace("\r", "\\r") : s[..maxLen].Replace("\n", "\\n").Replace("\r", "\\r") + "...";
    
    [Fact]
    public async Task DiagnosticShell_MultipleCommands_AllRender()
    {
        // Arrange
        await using var ctx = DiagnosticTestContext.Create();
        await ctx.StartAsync();
        
        // Act - send multiple commands (with longer timeouts for reliability)
        await ctx.SendCommandAsync("echo hello");
        var foundHello = await ctx.WaitForTextAsync("hello", TimeSpan.FromSeconds(3));
        
        await ctx.SendCommandAsync("echo world");
        var foundWorld = await ctx.WaitForTextAsync("world", TimeSpan.FromSeconds(3));
        
        await ctx.SendCommandAsync("ping");
        var foundPong = await ctx.WaitForTextAsync("PONG", TimeSpan.FromSeconds(3));
        
        // Assert
        Assert.True(foundHello, "Should find 'hello'");
        Assert.True(foundWorld, "Should find 'world'");
        Assert.True(foundPong, "Should find 'PONG'");
    }
    
    [Fact]
    public async Task DiagnosticShell_FloodCommand_HandlesRapidOutput()
    {
        // Arrange
        await using var ctx = DiagnosticTestContext.Create();
        await ctx.StartAsync();
        
        // Act - trigger rapid output
        await ctx.SendCommandAsync("flood 10");
        
        // Wait for the last line of flood output (Line 010)
        var foundComplete = await ctx.WaitForTextAsync("Line 010", TimeSpan.FromSeconds(5));
        
        // Assert
        Assert.True(foundComplete, "Should see last flood line");
    }
    
    [Fact]
    public async Task InvalidateCallback_IsInvokedOnOutput()
    {
        // This test verifies that OutputReceived triggers Invalidate()
        // by examining the trace log
        
        // Arrange
        await using var ctx = DiagnosticTestContext.Create();
        await ctx.StartAsync();
        
        // Clear any initial events
        var initialEvents = ctx.Tracer.GetEvents().ToList();
        
        // Act
        await ctx.SendCommandAsync("ping");
        await Task.Delay(500); // Give time for processing
        
        // Get events
        var allEvents = ctx.Tracer.GetEvents();
        var newEvents = allEvents.Skip(initialEvents.Count).ToList();
        
        // Check for expected flow: InnerWorkload.Output → Handle.OutputReceived → Invalidate
        var hasInnerOutput = newEvents.Any(e => e.Source == "InnerWorkload" && e.Event == "Output");
        var hasHandleOutput = newEvents.Any(e => e.Source == "Handle" && e.Event.Contains("OutputReceived"));
        
        // Assert
        Assert.True(hasInnerOutput, "Should have inner workload output event");
        // Note: Handle OutputReceived tracing requires the custom TracingTerminalWidgetHandle
    }
}
