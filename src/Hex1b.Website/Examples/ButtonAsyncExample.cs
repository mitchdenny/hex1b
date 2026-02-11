using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Button Widget Documentation: Background Work Demo
/// Demonstrates triggering async background work from a button click
/// and updating UI state when the work completes.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the asyncCode sample in:
/// src/content/guide/widgets/button.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ButtonAsyncExample(ILogger<ButtonAsyncExample> logger) : ReactiveExample
{
    private readonly ILogger<ButtonAsyncExample> _logger = logger;

    public override string Id => "button-async";
    public override string Title => "Button Widget - Async Work Demo";
    public override string Description => "Demonstrates triggering background work from a button click";

    /// <summary>
    /// State object that manages background work and notifies when complete.
    /// </summary>
    private class LoaderState
    {
        public string Status { get; private set; } = "Ready";
        public int Progress { get; private set; }
        public bool IsLoading { get; private set; }
        public string? Result { get; private set; }
        
        private Hex1bApp? _app;
        private CancellationToken _cancellationToken;

        public void Initialize(Hex1bApp app, CancellationToken cancellationToken)
        {
            _app = app;
            _cancellationToken = cancellationToken;
        }

        public void StartLoading()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            Status = "Starting...";
            Progress = 0;
            Result = null;
            
            // Fire and forget the background work
            _ = DoBackgroundWorkAsync();
        }

        private async Task DoBackgroundWorkAsync()
        {
            try
            {
                var steps = new[] 
                { 
                    "Connecting...", 
                    "Fetching data...", 
                    "Processing...", 
                    "Finalizing..." 
                };

                for (int i = 0; i < steps.Length; i++)
                {
                    Status = steps[i];
                    Progress = (i + 1) * 25;
                    _app?.Invalidate();
                    
                    // Simulate work
                    await Task.Delay(600, _cancellationToken);
                }

                Status = "Complete!";
                Progress = 100;
                Result = "Successfully loaded 42 items";
                IsLoading = false;
                _app?.Invalidate();
            }
            catch (OperationCanceledException)
            {
                Status = "Cancelled";
                IsLoading = false;
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                IsLoading = false;
                _app?.Invalidate();
            }
        }
    }

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting button async example");

        var state = new LoaderState();

        using var app = new Hex1bApp(
            ctx => BuildUI(ctx, state),
            new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
        );

        // Give the state object access to the app for invalidation
        state.Initialize(app, cancellationToken);

        await app.RunAsync(cancellationToken);
    }

    private static Hex1bWidget BuildUI(RootContext ctx, LoaderState state)
    {
        return ctx.Border(b => [
            b.VStack(v => [
                v.Text("Async Background Work Demo"),
                v.Text(""),
                v.Text($"Status: {state.Status}"),
                v.Progress(state.Progress),
                v.Text(""),
                state.Result != null 
                    ? v.Text($"Result: {state.Result}") 
                    : v.Text(""),
                v.Text(""),
                v.Button(state.IsLoading ? "Loading..." : "Load Data")
                    .OnClick(_ => state.StartLoading())
            ])
        ]).Title("Background Work");
    }
}
