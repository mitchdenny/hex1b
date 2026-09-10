using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>Owns the final snapshot and describes completed playback.</summary>
/// <remarks>Dispose the result to release the snapshot. This never disposes the terminal.</remarks>
public sealed class TapeExecutionResult : IDisposable
{
    internal TapeExecutionResult(Hex1bTerminalSnapshot snapshot, int count, TimeSpan elapsed,
        IEnumerable<TapeArtifact> artifacts, IEnumerable<TapeDiagnostic> diagnostics)
    {
        FinalSnapshot = snapshot;
        CompletedCommandCount = count;
        Elapsed = elapsed;
        TerminalSize = new(snapshot.Width, snapshot.Height);
        Artifacts = Array.AsReadOnly(artifacts.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    /// <summary>Gets the final terminal snapshot, owned by this result.</summary>
    public Hex1bTerminalSnapshot FinalSnapshot { get; }

    /// <summary>Gets the number of completed source commands, not generated primitive steps.</summary>
    public int CompletedCommandCount { get; }

    /// <summary>Gets elapsed execution time, including hidden intervals.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>Gets the final terminal size in columns and rows.</summary>
    public Size TerminalSize { get; }

    /// <summary>Gets the naturally observed exit code of an owned process, if any.</summary>
    public int? ProcessExitCode { get; internal set; }

    /// <summary>Gets successfully finalized artifact destinations.</summary>
    public IReadOnlyList<TapeArtifact> Artifacts { get; }

    /// <summary>Gets preparation and playback information and warnings.</summary>
    public IReadOnlyList<TapeDiagnostic> Diagnostics { get; }

    /// <summary>Releases the owned snapshot.</summary>
    public void Dispose() => FinalSnapshot.Dispose();
}
