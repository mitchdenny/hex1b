using Hex1b.Input;

namespace Hex1b.Automation;

/// <summary>
/// A step that sends a single key event.
/// </summary>
public sealed record KeyInputStep(Hex1bKey Key, string Text, Hex1bModifiers Modifiers) : TestStep
{
    /// <summary>Gets the type of key event to send.</summary>
    public Hex1bKeyEventType EventType { get; init; }

    /// <summary>Gets the unshifted Unicode or functional key code, when known.</summary>
    public int? UnicodeKeyCode { get; init; }

    /// <summary>Gets the shifted key code, when known.</summary>
    public int? ShiftedKeyCode { get; init; }

    /// <summary>Gets the base-layout key code, when known.</summary>
    public int? BaseLayoutKeyCode { get; init; }

    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        return terminal.SendEventAsync(new Hex1bKeyEvent(Key, Text, Modifiers)
        {
            EventType = EventType,
            UnicodeKeyCode = UnicodeKeyCode,
            ShiftedKeyCode = ShiftedKeyCode,
            BaseLayoutKeyCode = BaseLayoutKeyCode
        }, ct);
    }
}
