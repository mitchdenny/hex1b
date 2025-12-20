using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// State for tracking error status in a RescueWidget.
/// </summary>
public class RescueState
{
    /// <summary>
    /// Whether an error has occurred.
    /// </summary>
    public bool HasError { get; internal set; }
    
    /// <summary>
    /// The exception that was caught, if any.
    /// </summary>
    public Exception? Exception { get; internal set; }
    
    /// <summary>
    /// The phase in which the error occurred (Build, Measure, Arrange, Render).
    /// </summary>
    public RescueErrorPhase ErrorPhase { get; internal set; }
    
    /// <summary>
    /// Clears the error state, allowing the widget to retry rendering the main content.
    /// </summary>
    public void Reset()
    {
        HasError = false;
        Exception = null;
        ErrorPhase = RescueErrorPhase.None;
    }
    
    /// <summary>
    /// Sets the error state with the specified exception and phase.
    /// </summary>
    public void SetError(Exception exception, RescueErrorPhase phase)
    {
        HasError = true;
        Exception = exception;
        ErrorPhase = phase;
    }
}
