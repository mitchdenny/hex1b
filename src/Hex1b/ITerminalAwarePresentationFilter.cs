namespace Hex1b;

/// <summary>
/// Presentation filter that requires a reference to the terminal.
/// </summary>
/// <remarks>
/// Filters implementing this interface will receive the terminal reference
/// during terminal construction, before the session starts.
/// </remarks>
public interface ITerminalAwarePresentationFilter : IHex1bTerminalPresentationFilter
{
    /// <summary>
    /// Called when the terminal is created, before the session starts.
    /// </summary>
    /// <param name="terminal">The terminal instance.</param>
    void SetTerminal(Hex1bTerminal terminal);
}
