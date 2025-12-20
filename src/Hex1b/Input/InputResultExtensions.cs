namespace Hex1b.Input;

/// <summary>
/// Extension methods for InputResult.
/// </summary>
public static class InputResultExtensions
{
    /// <summary>
    /// Returns true if the input was handled.
    /// </summary>
    public static bool WasHandled(this InputResult result) => result == InputResult.Handled;

    /// <summary>
    /// Converts a boolean to an InputResult.
    /// </summary>
    public static InputResult ToInputResult(this bool handled) 
        => handled ? InputResult.Handled : InputResult.NotHandled;
}
