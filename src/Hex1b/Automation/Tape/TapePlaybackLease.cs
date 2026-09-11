using System.Runtime.CompilerServices;

namespace Hex1b.Automation;

internal sealed class TapePlaybackLease : IDisposable
{
    // A weak, terminal-keyed gate also coordinates different TapePlayer instances.
    private static readonly ConditionalWeakTable<Hex1bTerminal, SemaphoreSlim> Gates = new();
    private SemaphoreSlim? _gate;

    private TapePlaybackLease(SemaphoreSlim gate) => _gate = gate;

    internal static TapePlaybackLease Acquire(Hex1bTerminal terminal)
    {
        var gate = Gates.GetValue(terminal, static _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(0))
            throw new InvalidOperationException("A Tape playback is already active on this terminal.");
        return new(gate);
    }

    public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
}
