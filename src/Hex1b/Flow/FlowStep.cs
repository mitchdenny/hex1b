using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Handle returned by <see cref="Hex1bFlowContext.Step"/> that controls a running
/// inline step. Use <see cref="Invalidate"/> to trigger re-renders from background
/// work, and <see cref="Complete()"/> or <see cref="CompleteAsync()"/> to finish the step.
/// </summary>
public sealed class FlowStep
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile Hex1bApp? _app;
    private int _completed; // 0 = active, 1 = completed

    internal FlowStep(int terminalWidth, int terminalHeight, int stepHeight)
    {
        TerminalWidth = terminalWidth;
        TerminalHeight = terminalHeight;
        StepHeight = stepHeight;
    }

    /// <summary>
    /// Gets the completed builder set by <see cref="Complete(Func{RootContext, Hex1bWidget})"/>,
    /// or null if the step exited without one.
    /// </summary>
    internal Func<RootContext, Hex1bWidget>? CompletedBuilder { get; private set; }

    /// <summary>
    /// Width of the terminal in columns.
    /// </summary>
    public int TerminalWidth { get; }

    /// <summary>
    /// Height of the terminal in rows.
    /// </summary>
    public int TerminalHeight { get; }

    /// <summary>
    /// Number of rows allocated to this step.
    /// </summary>
    public int StepHeight { get; internal set; }

    /// <summary>
    /// Sets the underlying app instance. Called by the runner after the app is created.
    /// </summary>
    internal void SetApp(Hex1bApp app) => _app = app;

    /// <summary>
    /// Marks the task as completed. Called by the runner after cleanup.
    /// </summary>
    internal void SetCompleted() => _tcs.TrySetResult();

    /// <summary>
    /// Marks the task as faulted. Called by the runner on error.
    /// </summary>
    internal void SetFaulted(Exception ex) => _tcs.TrySetException(ex);

    /// <summary>
    /// Triggers a re-render of the step's widget tree.
    /// Safe to call from any thread.
    /// </summary>
    public void Invalidate() => _app?.Invalidate();

    /// <summary>
    /// Completes the step without frozen output. The step region is cleared
    /// and the cursor advances past it.
    /// </summary>
    /// <remarks>
    /// This is a fire-and-forget call suitable for use in event handlers.
    /// Use <see cref="CompleteAsync()"/> from the flow callback to also wait
    /// for cleanup to finish.
    /// </remarks>
    public void Complete()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;
        _app?.RequestStop();
    }

    /// <summary>
    /// Completes the step and renders the given widget as frozen terminal output.
    /// The widget is rendered once after the step ends, scrolling naturally into
    /// the scrollback buffer.
    /// </summary>
    /// <remarks>
    /// This is a fire-and-forget call suitable for use in event handlers.
    /// Use <see cref="CompleteAsync(Func{RootContext, Hex1bWidget})"/> from the
    /// flow callback to also wait for cleanup to finish.
    /// </remarks>
    /// <param name="builder">Widget builder for the frozen output.</param>
    public void Complete(Func<RootContext, Hex1bWidget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;
        CompletedBuilder = builder;
        _app?.RequestStop();
    }

    /// <summary>
    /// Completes the step without frozen output and waits for cleanup
    /// (yield widget rendering, cursor advancement) to finish.
    /// </summary>
    /// <returns>A task that completes when the step is fully cleaned up.</returns>
    public Task CompleteAsync()
    {
        Complete();
        return WaitForCompletionAsync();
    }

    /// <summary>
    /// Completes the step with frozen output and waits for cleanup
    /// (yield widget rendering, cursor advancement) to finish.
    /// </summary>
    /// <param name="builder">Widget builder for the frozen output.</param>
    /// <returns>A task that completes when the step is fully cleaned up.</returns>
    public Task CompleteAsync(Func<RootContext, Hex1bWidget> builder)
    {
        Complete(builder);
        return WaitForCompletionAsync();
    }

    /// <summary>
    /// Waits for the step to finish, including yield widget rendering and
    /// cursor advancement. Use this after calling <see cref="Complete()"/> from
    /// the flow callback, or to wait for a step that is completed by user
    /// interaction (e.g., via <see cref="FlowStepContext.Step"/> in an event handler).
    /// </summary>
    /// <returns>A task that completes when the step is fully cleaned up.</returns>
    public Task WaitForCompletionAsync() => _tcs.Task;

    /// <summary>
    /// Requests that focus be moved to a node matching the predicate.
    /// </summary>
    public void RequestFocus(Func<Hex1bNode, bool> predicate) => _app?.RequestFocus(predicate);
}
