using Hex1b.Input;

namespace Hex1b.Automation;

/// <summary>
/// A step that sends a mouse event at a specific position.
/// </summary>
public sealed record MouseInputStep(
    MouseButton Button, 
    MouseAction Action, 
    int X, 
    int Y, 
    Hex1bModifiers Modifiers,
    int ClickCount = 1) : TestStep
{
    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        return terminal.SendEventAsync(new Hex1bMouseEvent(Button, Action, X, Y, Modifiers, ClickCount), ct);
    }
}
