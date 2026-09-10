using Hex1b.Automation;

namespace Hex1b;

/// <summary>
/// Owns viewport and active-buffer-start snapshots taken atomically with an observation boundary.
/// Ansi is populated for attachment and visible resynchronization only.
/// </summary>
internal sealed class TerminalCaptureBoundary(
    Hex1bTerminalSnapshot snapshot, Hex1bTerminalSnapshot bufferStartSnapshot,
    string ansi, long sequence, TimeSpan elapsed) : IDisposable
{
    internal Hex1bTerminalSnapshot Snapshot { get; } = snapshot;
    internal Hex1bTerminalSnapshot BufferStartSnapshot { get; } = bufferStartSnapshot;
    internal string Ansi { get; } = ansi;
    internal long Sequence { get; } = sequence;
    internal TimeSpan Elapsed { get; } = elapsed;

    public void Dispose()
    {
        Snapshot.Dispose();
        BufferStartSnapshot.Dispose();
    }
}
