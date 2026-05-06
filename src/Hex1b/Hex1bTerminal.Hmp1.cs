namespace Hex1b;

public sealed partial class Hex1bTerminal
{
    /// <summary>
    /// When this terminal was constructed with an HMP1 workload (e.g. via
    /// <see cref="Hmp1BuilderExtensions.WithHmp1UdsClient(Hex1bTerminalBuilder, string)"/>),
    /// returns the underlying <see cref="Hmp1WorkloadAdapter"/> so callers
    /// can inspect peer-roster state, request primary, and observe role changes.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the workload is not an HMP1 client
    /// (for example, when the terminal hosts a local PTY or a Hex1b app).
    /// </remarks>
    public Hmp1WorkloadAdapter? Hmp1 => _workload as Hmp1WorkloadAdapter;
}
