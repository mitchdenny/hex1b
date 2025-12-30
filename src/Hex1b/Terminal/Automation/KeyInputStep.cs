using Hex1b.Input;

namespace Hex1b.Terminal.Automation;

/// <summary>
/// A step that sends a single key event.
/// </summary>
public sealed record KeyInputStep(Hex1bKey Key, string Text, Hex1bModifiers Modifiers) : TestStep
{
    internal override Task ExecuteAsync(
        Hex1bTerminal terminal,
        Hex1bTerminalInputSequenceOptions options,
        CancellationToken ct)
    {
        terminal.SendEvent(new Hex1bKeyEvent(Key, Text, Modifiers));
        return Task.CompletedTask;
    }
}
