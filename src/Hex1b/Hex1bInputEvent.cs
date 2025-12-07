namespace Hex1b;

/// <summary>
/// Represents an input event from the terminal.
/// </summary>
public abstract record Hex1bInputEvent;

public sealed record KeyInputEvent(ConsoleKey Key, char KeyChar, bool Shift, bool Alt, bool Control) : Hex1bInputEvent;
