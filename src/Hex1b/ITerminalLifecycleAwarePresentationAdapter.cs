namespace Hex1b;

/// <summary>
/// Extended presentation adapter interface that receives terminal lifecycle notifications.
/// </summary>
/// <remarks>
/// <para>
/// When a presentation adapter implements this interface, the terminal will call
/// the lifecycle methods at appropriate points during terminal execution.
/// </para>
/// <para>
/// This is useful for presentation adapters that need to track the terminal's state,
/// forward input to the terminal, or trigger UI updates when the terminal starts or stops.
/// </para>
/// </remarks>
public interface ITerminalLifecycleAwarePresentationAdapter : IHex1bTerminalPresentationAdapter
{
    /// <summary>
    /// Called when the terminal instance is created and associated with this adapter.
    /// </summary>
    /// <param name="terminal">The terminal instance.</param>
    /// <remarks>
    /// This is called during terminal construction, before the terminal is started.
    /// The adapter can use this to store a reference to the terminal for input forwarding.
    /// </remarks>
    void TerminalCreated(Hex1bTerminal terminal);

    /// <summary>
    /// Called when the terminal has started and is ready to process I/O.
    /// </summary>
    /// <remarks>
    /// This is called after the terminal's I/O pumps have started and the workload
    /// is connected. The adapter can use this to transition from a "not started" state
    /// to an active state.
    /// </remarks>
    void TerminalStarted();

    /// <summary>
    /// Called when the terminal has completed execution.
    /// </summary>
    /// <param name="exitCode">The exit code from the terminal's run callback or workload.</param>
    /// <remarks>
    /// This is called after the terminal's workload has disconnected and the run callback
    /// (if any) has completed. The adapter can use this to transition to a "completed" state
    /// and display appropriate UI (e.g., exit code, restart button).
    /// </remarks>
    void TerminalCompleted(int exitCode);
}
