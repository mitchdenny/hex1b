using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Progress Widget Documentation: Indeterminate Mode
/// Demonstrates animated indeterminate progress bars using a reactive example.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the indeterminateCode sample in:
/// src/content/guide/widgets/progress.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ProgressIndeterminateExample(ILogger<ProgressIndeterminateExample> logger) : ReactiveExample
{
    private readonly ILogger<ProgressIndeterminateExample> _logger = logger;

    public override string Id => "progress-indeterminate";
    public override string Title => "Progress Widget - Indeterminate";
    public override string Description => "Demonstrates animated indeterminate progress bars";

    public override async Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting indeterminate progress example");

        var animationPos = 0.0;

        var app = new Hex1bApp(
            ctx =>
            {
                return ctx.VStack(v => [
                    v.Text("Loading..."),
                    v.ProgressIndeterminate(animationPos)
                ]);
            },
            new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
        );

        // Animation task
        var animationTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(cancellationToken))
                        break;
                    
                    animationPos = (animationPos + 0.02) % 1.0;
                    app.Invalidate();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cancellationToken);

        try
        {
            await app.RunAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}
