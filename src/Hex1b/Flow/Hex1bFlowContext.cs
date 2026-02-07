using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Context passed to a flow callback. Provides methods to run inline slices
/// and full-screen TUI applications as sequential steps in a flow.
/// </summary>
public sealed class Hex1bFlowContext
{
    private readonly Hex1bFlowRunner _runner;

    internal Hex1bFlowContext(Hex1bFlowRunner runner)
    {
        _runner = runner;
    }

    /// <summary>
    /// Runs an inline micro-TUI slice in the normal terminal buffer.
    /// The slice reserves space from the current cursor position down and supports
    /// full interactivity (focus, keyboard navigation, etc.).
    /// </summary>
    /// <param name="builder">Widget builder for the interactive TUI content.</param>
    /// <param name="yield">
    /// Optional widget builder for the "yield" state — rendered after the slice completes.
    /// This widget is re-rendered on terminal resize and remains visible as the flow progresses.
    /// </param>
    /// <param name="options">Optional configuration for the slice.</param>
    public Task SliceAsync(
        Func<RootContext, Hex1bWidget> builder,
        Func<RootContext, Hex1bWidget>? @yield = null,
        Hex1bFlowSliceOptions? options = null)
    {
        return _runner.RunSliceAsync(builder, @yield, options);
    }

    /// <summary>
    /// Runs an inline micro-TUI slice, providing access to the underlying <see cref="Hex1bApp"/>
    /// for programmatic control (e.g., <see cref="Hex1bApp.Invalidate"/> from background tasks,
    /// or <see cref="Hex1bApp.RequestStop"/> to end the slice).
    /// </summary>
    /// <param name="configure">
    /// Configuration callback that receives the app instance and returns the widget builder.
    /// The app reference can be captured for use in background tasks.
    /// </param>
    /// <param name="yield">
    /// Optional widget builder for the "yield" state — rendered after the slice completes.
    /// </param>
    /// <param name="options">Optional configuration for the slice.</param>
    public Task SliceAsync(
        Func<Hex1bApp, Func<RootContext, Hex1bWidget>> configure,
        Func<RootContext, Hex1bWidget>? @yield = null,
        Hex1bFlowSliceOptions? options = null)
    {
        return _runner.RunSliceAsync(configure, @yield, options);
    }

    /// <summary>
    /// Runs a full-screen TUI application in the alternate screen buffer.
    /// Inline state is saved before entering and restored after exiting.
    /// </summary>
    /// <param name="configure">
    /// Configuration callback that receives the app and options, returning the widget builder.
    /// Same pattern as the builder's WithHex1bApp method.
    /// </param>
    public Task FullScreenAsync(
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1bWidget>> configure)
    {
        return _runner.RunFullScreenAsync(configure);
    }
}

/// <summary>
/// Options for configuring an inline flow slice.
/// </summary>
public sealed class Hex1bFlowSliceOptions
{
    /// <summary>
    /// Maximum height in rows for the slice. If null, defaults to terminal height.
    /// </summary>
    public int? MaxHeight { get; init; }
}
