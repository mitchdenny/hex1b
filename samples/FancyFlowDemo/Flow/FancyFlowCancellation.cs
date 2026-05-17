using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace FancyFlowDemo.Flow;

/// <summary>
/// Shared cancellation state for a single FancyFlowDemo run. A Ctrl+C binding
/// installed on each step's root widget calls <see cref="Cancel"/> and ends
/// the active step; the orchestrator then propagates an
/// <see cref="OperationCanceledException"/> via <see cref="ThrowIfCancelled"/>
/// so the run unwinds cleanly to the top-level catch.
/// </summary>
internal sealed class FancyFlowCancellation
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Token => _cts.Token;
    public bool IsCancelled => _cts.IsCancellationRequested;

    public void Cancel()
    {
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    public void ThrowIfCancelled() => _cts.Token.ThrowIfCancellationRequested();
}

internal static class FancyFlowCancellationExtensions
{
    /// <summary>
    /// Wraps a step's root widget with a Ctrl+C binding that signals
    /// cancellation and exits the current step.
    /// </summary>
    public static TWidget ExitOnCtrlC<TWidget>(this TWidget widget, FancyFlowCancellation cancel, FlowStepContext ctx)
        where TWidget : Hex1bWidget
    {
        return widget.InputBindings(b =>
        {
            b.Ctrl().Key(Hex1bKey.C).Action(_ =>
            {
                cancel.Cancel();
                ctx.Step.Complete();
            }, "Cancel and exit");
        });
    }
}
