namespace Hex1b.Input;

/// <summary>
/// A terminal resize event. Handled by the app to trigger re-layout.
/// </summary>
/// <param name="Width">New terminal width in characters.</param>
/// <param name="Height">New terminal height in lines.</param>
public sealed record Hex1bResizeEvent(int Width, int Height) : Hex1bEvent;
