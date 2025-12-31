using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// A widget context for building rescue fallback UI.
/// Provides access to error information and the ability to reset the rescue state.
/// </summary>
public sealed class RescueContext : WidgetContext<VStackWidget>
{
    private readonly Action _reset;

    /// <summary>
    /// The exception that was caught.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// The phase in which the error occurred (Reconcile, Measure, Arrange, Render).
    /// </summary>
    public RescueErrorPhase ErrorPhase { get; }

    /// <summary>
    /// Resets the rescue state, causing the next render cycle to retry the child widget.
    /// This triggers internal cleanup and then invokes any OnReset handler.
    /// </summary>
    public void Reset() => _reset();

    internal RescueContext(Exception exception, RescueErrorPhase phase, Action reset)
    {
        Exception = exception;
        ErrorPhase = phase;
        _reset = reset;
    }
}
