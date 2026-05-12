using Hex1b;
using Hex1b.Flow;
using Hex1b.Input;
using Hex1b.Widgets;

namespace FlowDemo;

/// <summary>
/// Shared cancellation state for a single FlowDemo command run. A Ctrl+C
/// binding installed on each step's root widget calls <see cref="Cancel"/>
/// and ends the active step; the flow callback then propagates an
/// <see cref="OperationCanceledException"/> via <see cref="ThrowIfCancelled"/>
/// (or via <see cref="Token"/> on Task.Delay/etc.) so the command unwinds
/// cleanly to the top-level catch in <see cref="FlowCancellationExtensions.RunAsync"/>.
/// </summary>
internal sealed class FlowCancellation
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Token that becomes cancelled when the user presses Ctrl+C. Pass to
    /// <c>Task.Delay</c>, <c>FlowStep.WaitForCompletionAsync</c>, and any
    /// other awaitable that should bail out on cancellation.
    /// </summary>
    public CancellationToken Token => _cts.Token;

    /// <summary>True once the user has pressed Ctrl+C.</summary>
    public bool IsCancelled => _cts.IsCancellationRequested;

    /// <summary>Signal cancellation. Idempotent.</summary>
    public void Cancel()
    {
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>Throws <see cref="OperationCanceledException"/> if cancelled.</summary>
    public void ThrowIfCancelled() => _cts.Token.ThrowIfCancellationRequested();
}

/// <summary>
/// Helpers that wire a global Ctrl+C exit into a flow.
/// </summary>
internal static class FlowCancellationExtensions
{
    /// <summary>
    /// Wraps a step's root widget with a Ctrl+C binding that signals
    /// cancellation on the supplied <see cref="FlowCancellation"/> and
    /// exits the current step.
    /// </summary>
    /// <remarks>
    /// The binding overrides the step app's default Ctrl+C-exits-step
    /// behaviour (added by <c>Hex1bAppOptions.EnableDefaultCtrlCExit</c>)
    /// because <c>InputBindings</c> on the root widget registers after the
    /// default binding and wins via the trie's last-write-wins semantics.
    /// </remarks>
    public static TWidget ExitOnCtrlC<TWidget>(this TWidget widget, FlowCancellation cancel, FlowStepContext ctx)
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

    /// <summary>
    /// Runs a command body, catching cancellation and printing a friendly
    /// goodbye message instead of letting the exception escape to the user.
    /// </summary>
    public static async Task RunAsync(Func<FlowCancellation, Task> body)
    {
        var cancel = new FlowCancellation();
        try
        {
            await body(cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancelled)
        {
            Console.WriteLine();
            Console.WriteLine("✗ Cancelled by user. Goodbye!");
        }
    }
}
