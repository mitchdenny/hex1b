namespace Hex1b;

/// <summary>
/// Internal capability for in-process workloads whose output channel can be
/// explicitly completed, so the terminal drains it as EOF before shutdown.
/// </summary>
/// <remarks>
/// Keeps <see cref="Hex1bTerminal"/> from referencing concrete app workload types,
/// which would root app infrastructure in terminal-only Native AOT consumers.
/// </remarks>
internal interface ICompletableOutputWorkloadAdapter
{
    /// <summary>Gets whether the producer has signaled that no more output follows.</summary>
    bool OutputCompletionRequested { get; }

    /// <summary>Gets whether all queued output has been read after completion.</summary>
    bool OutputCompleted { get; }
}
