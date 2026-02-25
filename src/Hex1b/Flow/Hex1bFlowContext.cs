using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Context passed to a flow callback. Provides methods to run interactive steps
/// and full-screen TUI applications as sequential parts of a flow.
/// </summary>
public sealed class Hex1bFlowContext
{
    private readonly Hex1bFlowRunner _runner;

    internal Hex1bFlowContext(Hex1bFlowRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Width of the terminal in columns.
    /// </summary>
    public int TerminalWidth => _runner.TerminalWidth;

    /// <summary>
    /// Height of the terminal in rows.
    /// </summary>
    public int TerminalHeight => _runner.TerminalHeight;

    /// <summary>
    /// Number of rows available from the current cursor position to the bottom
    /// of the terminal, before any scrolling would occur.
    /// </summary>
    public int AvailableHeight => _runner.AvailableHeight;

    /// <summary>
    /// Cancellation token for the flow. This token is cancelled when the outer
    /// flow runner is stopped (e.g., via Ctrl+C). Pass this to
    /// <see cref="FlowStep.WaitForCompletionAsync(CancellationToken)"/> or
    /// <see cref="FlowStep.CompleteAsync(CancellationToken)"/> to make them cancellable.
    /// </summary>
    public CancellationToken CancellationToken => _runner.CancellationToken;

    private static Hex1bFlowStepOptions? BuildOptions(Action<Hex1bFlowStepOptions>? configure)
    {
        if (configure == null) return null;
        var options = new Hex1bFlowStepOptions();
        configure(options);
        return options;
    }

    /// <summary>
    /// Starts an inline interactive step in the normal terminal buffer and returns
    /// a <see cref="FlowStep"/> handle for controlling it. The step renders immediately
    /// on a background task; use the handle to <see cref="FlowStep.Invalidate">invalidate</see>,
    /// <see cref="FlowStep.Complete()">complete</see>, and
    /// <see cref="FlowStep.WaitForCompletionAsync(CancellationToken)">wait for completion</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only one step may be active at a time. Starting a new step while a previous
    /// step is still active throws <see cref="InvalidOperationException"/>. Call
    /// <see cref="FlowStep.CompleteAsync(CancellationToken)"/> or <see cref="FlowStep.Complete()"/>
    /// followed by <see cref="FlowStep.WaitForCompletionAsync(CancellationToken)"/> before starting the next one.
    /// </para>
    /// <para>
    /// The builder receives a <see cref="FlowStepContext"/> which exposes a
    /// <see cref="FlowStepContext.Step"/> property, so event handlers can access the
    /// step handle without needing a separate variable.
    /// </para>
    /// </remarks>
    /// <param name="builder">Widget builder for the interactive TUI content.</param>
    /// <param name="options">Optional callback to configure step options.</param>
    /// <returns>A <see cref="FlowStep"/> handle for controlling the running step.</returns>
    public FlowStep Step(
        Func<FlowStepContext, Hex1bWidget> builder,
        Action<Hex1bFlowStepOptions>? options = null)
    {
        return _runner.StartStep(builder, BuildOptions(options));
    }

    /// <summary>
    /// Renders a static widget as frozen terminal output and advances the cursor.
    /// Use this for headers, dividers, status lines, or any content that doesn't
    /// need interactivity. The widget is rendered once and scrolls naturally into
    /// the scrollback buffer.
    /// </summary>
    /// <param name="builder">Widget builder for the static content.</param>
    /// <returns>A task that completes when the content has been rendered.</returns>
    public Task ShowAsync(Func<RootContext, Hex1bWidget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return _runner.RenderStaticAsync(builder);
    }

    /// <summary>
    /// Runs a full-screen TUI application in the alternate screen buffer.
    /// Inline state is saved before entering and restored after exiting.
    /// </summary>
    /// <param name="configure">
    /// Configuration callback that receives the app and options, returning the widget builder.
    /// Same pattern as the builder's WithHex1bApp method.
    /// </param>
    public Task FullScreenStepAsync(
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1bWidget>> configure)
    {
        return _runner.RunFullScreenStepAsync(configure);
    }
}

/// <summary>
/// Options for configuring an inline flow step.
/// </summary>
public sealed class Hex1bFlowStepOptions
{
    /// <summary>
    /// Maximum height in rows for the step. If null, defaults to terminal height.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Whether to enable mouse input for this step. Defaults to false.
    /// </summary>
    public bool EnableMouse { get; set; }
}
