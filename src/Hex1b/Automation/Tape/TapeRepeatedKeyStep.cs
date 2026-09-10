using Hex1b.Input;

namespace Hex1b.Automation;

internal sealed record TapeRepeatedKeyStep(
    Hex1bKey Key, string Text, Hex1bModifiers Modifiers, ulong Count, TimeSpan Delay) : TestStep
{
    internal override async Task ExecuteAsync(Hex1bTerminal terminal, Hex1bTerminalInputSequenceOptions options, CancellationToken ct)
    {
        var key = new KeyInputStep(Key, Text, Modifiers);
        for (ulong i = 0; i < Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await key.ExecuteAsync(terminal, options, ct);
            await DelayAsync(options.TimeProvider ?? TimeProvider.System, Delay, ct);
        }
    }
}
