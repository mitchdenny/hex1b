using System.Runtime.CompilerServices;
using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Handle returned by <see cref="Hex1bFlowContext.Step"/> that controls a running
/// inline step. Use <see cref="Invalidate"/> to trigger re-renders from background
/// work, <see cref="Complete()"/> to finish the step, and <c>await</c> to wait for
/// the step's cleanup (yield widget rendering and cursor advancement) to finish.
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
    /// A <see cref="System.Threading.Tasks.Task"/> that completes when the step finishes,
    /// including yield widget rendering and cursor advancement.
    /// </summary>
    public Task Task => _tcs.Task;

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
    /// Requests that focus be moved to a node matching the predicate.
    /// </summary>
    public void RequestFocus(Func<Hex1bNode, bool> predicate) => _app?.RequestFocus(predicate);

    /// <summary>
    /// Gets an awaiter so the step can be awaited directly: <c>await step;</c>
    /// </summary>
    public TaskAwaiter GetAwaiter() => Task.GetAwaiter();
}
