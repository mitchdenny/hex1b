namespace Hex1b.Widgets;

/// <summary>
/// The phase in which an error occurred during rescue handling.
/// </summary>
public enum RescueErrorPhase
{
    None,
    Build,
    Reconcile,
    Measure,
    Arrange,
    Render
}
