namespace Hex1b.Input;

/// <summary>
/// A terminal capability response event (e.g., DA1 response for Sixel detection).
/// Handled by the app to update terminal capabilities.
/// </summary>
/// <param name="Response">The raw terminal response string.</param>
public sealed record Hex1bTerminalEvent(string Response) : Hex1bEvent;
