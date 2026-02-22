using Hex1b.Widgets;

namespace Hex1b.Flow;

/// <summary>
/// Context passed to a step's configure callback. Wraps the underlying <see cref="Hex1bApp"/>
/// and adds flow-specific methods like <see cref="Complete"/> for setting the output
/// rendered when the step completes.
/// </summary>
public sealed class Hex1bStepContext
{
    private readonly Hex1bApp _app;

    internal Hex1bStepContext(Hex1bApp app)
    {
        _app = app;
    }

    /// <summary>
    /// Gets the completed builder set by <see cref="Complete"/>, or null if the step
    /// exited without completing.
    /// </summary>
    internal Func<RootContext, Hex1bWidget>? CompletedBuilder { get; private set; }

    /// <summary>
    /// Completes the step and sets the widget to render as frozen terminal output.
    /// The widget is rendered once after the step ends, scrolling naturally into the
    /// scrollback buffer.
    /// </summary>
    /// <param name="builder">Widget builder for the frozen output.</param>
    public void Complete(Func<RootContext, Hex1bWidget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        CompletedBuilder = builder;
        _app.RequestStop();
    }

    /// <summary>
    /// Triggers a re-render of the step's widget tree.
    /// Safe to call from background threads.
    /// </summary>
    public void Invalidate() => _app.Invalidate();

    /// <summary>
    /// Stops the step without setting a completed widget.
    /// </summary>
    public void RequestStop() => _app.RequestStop();

    /// <summary>
    /// Requests that focus be moved to a node matching the predicate.
    /// </summary>
    public void RequestFocus(Func<Hex1bNode, bool> predicate) => _app.RequestFocus(predicate);
}
