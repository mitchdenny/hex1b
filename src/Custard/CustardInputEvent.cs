namespace Custard;

/// <summary>
/// Represents an input event from the terminal.
/// </summary>
public abstract record CustardInputEvent;

public sealed record KeyInputEvent(ConsoleKey Key, char KeyChar, bool Shift, bool Alt, bool Control) : CustardInputEvent;
