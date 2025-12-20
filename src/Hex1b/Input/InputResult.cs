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
