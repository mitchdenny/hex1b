namespace Hex1b.Input;

/// <summary>
/// The result of input handling, indicating whether the input was consumed.
/// </summary>
public enum InputResult
{
    /// <summary>
    /// The input was not handled and should continue to be processed.
    /// </summary>
    NotHandled,

    /// <summary>
    /// The input was handled and should not be processed further.
    /// </summary>
    Handled,
}

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
