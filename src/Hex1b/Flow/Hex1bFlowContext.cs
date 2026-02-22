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

    private static Hex1bFlowStepOptions? BuildOptions(Action<Hex1bFlowStepOptions>? configure)
    {
        if (configure == null) return null;
        var options = new Hex1bFlowStepOptions();
        configure(options);
        return options;
    }

    /// <summary>
    /// Runs an inline interactive step in the normal terminal buffer.
    /// The step reserves space from the current cursor position down and supports
    /// full interactivity (focus, keyboard navigation, etc.).
    /// </summary>
    /// <param name="builder">Widget builder for the interactive TUI content.</param>
    /// <param name="options">Optional callback to configure step options.</param>
    public Task StepAsync(
        Func<RootContext, Hex1bWidget> builder,
        Action<Hex1bFlowStepOptions>? options = null)
    {
        return _runner.RunStepAsync(builder, BuildOptions(options));
    }

    /// <summary>
    /// Runs an inline interactive step, providing access to <see cref="Hex1bStepContext"/>
    /// for programmatic control. Use <see cref="Hex1bStepContext.Complete"/> to set the
    /// frozen output rendered after the step completes, or <see cref="Hex1bStepContext.RequestStop"/>
    /// to exit without output.
    /// </summary>
    /// <param name="configure">
    /// Configuration callback that receives the step context and returns the widget builder.
    /// The step context can be captured for use in event handlers and background tasks.
    /// </param>
    /// <param name="options">Optional callback to configure step options.</param>
    public Task StepAsync(
        Func<Hex1bStepContext, Func<RootContext, Hex1bWidget>> configure,
        Action<Hex1bFlowStepOptions>? options = null)
    {
        return _runner.RunStepAsync(configure, BuildOptions(options));
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
