namespace Hex1b;

/// <summary>
/// Reports program status through an application's terminal output.
/// </summary>
/// <remarks>
/// Reports and clears are serialized with rendering and work without invalidating the UI.
/// Await sequential async calls to preserve their order; simultaneous callers have no defined
/// relative order. Completion means output has been accepted by the workload adapter, not
/// presented by the terminal. Cancellation before acceptance emits nothing; cancellation
/// after acceptance does not retract output. Disposal rejects new calls and cancels pending
/// async calls. No status is automatically emitted when callbacks finish or the app stops.
/// Async calls use their explicitly supplied cancellation token; use the callback context's
/// cancellation token when reports should be canceled with the application run.
/// Custom workload adapters retain their synchronous write semantics; cancellation cannot
/// interrupt a custom write already in progress.
/// Reentrant emission from rendering callbacks is unsupported and throws
/// <see cref="InvalidOperationException"/> instead of waiting for the active frame.
/// Emit from widget builders, input callbacks, or independent background work instead.
/// Background work inheriting a render callback's execution context can emit once that
/// frame has finished, but is rejected while the inherited render scope is active.
/// </remarks>
/// <example>
/// <code>
/// using Hex1b;
///
/// await using var app = new Hex1bApp(context =&gt;
///     context.Button("Run").OnClick(async e =&gt;
///     {
///         await e.Context.ProgramStatus.ReportAsync(
///             new(TerminalProgramStatusState.Working) { Title = "Running" },
///             e.Context.CancellationToken);
///         await Task.Delay(1000, e.Context.CancellationToken);
///         await e.Context.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Done));
///     }));
/// await app.RunAsync();
/// </code>
/// </example>
public sealed class TerminalProgramStatusReporter
{
    private readonly Action<string> _write;
    private readonly Func<string, CancellationToken, Task> _writeAsync;

    internal TerminalProgramStatusReporter(
        Action<string> write,
        Func<string, CancellationToken, Task> writeAsync)
    {
        _write = write;
        _writeAsync = writeAsync;
    }

    /// <summary>Replaces a program-status record.</summary>
    /// <param name="report">The complete replacement record.</param>
    /// <exception cref="ArgumentException">The report violates the OSC 7501 contract.</exception>
    /// <exception cref="ObjectDisposedException">The application has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Called reentrantly from a render callback.</exception>
    /// <remarks>May block under output backpressure. Prefer <see cref="ReportAsync"/> in async callbacks.</remarks>
    public void Report(TerminalProgramStatusReport report)
        => _write(TerminalProgramStatusCodec.Serialize(report));

    /// <summary>Asynchronously replaces a program-status record.</summary>
    /// <param name="report">The complete replacement record.</param>
    /// <param name="cancellationToken">Cancels waiting for output capacity.</param>
    /// <returns>A task completing when output has been accepted.</returns>
    /// <exception cref="ArgumentException">The report violates the OSC 7501 contract.</exception>
    /// <exception cref="ObjectDisposedException">The application has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Called reentrantly from a render callback.</exception>
    public Task ReportAsync(TerminalProgramStatusReport report, CancellationToken cancellationToken = default)
        => _writeAsync(TerminalProgramStatusCodec.Serialize(report), cancellationToken);

    /// <summary>Clears a program-status record without reporting a replacement state.</summary>
    /// <param name="id">The record identifier, or null or empty for the root record.</param>
    /// <exception cref="ArgumentException">The identifier violates the OSC 7501 contract.</exception>
    /// <exception cref="ObjectDisposedException">The application has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Called reentrantly from a render callback.</exception>
    public void Clear(string? id = null)
        => _write(TerminalProgramStatusCodec.SerializeClear(id));

    /// <summary>Asynchronously clears a program-status record.</summary>
    /// <param name="id">The record identifier, or null or empty for the root record.</param>
    /// <param name="cancellationToken">Cancels waiting for output capacity.</param>
    /// <returns>A task completing when output has been accepted.</returns>
    /// <exception cref="ArgumentException">The identifier violates the OSC 7501 contract.</exception>
    /// <exception cref="ObjectDisposedException">The application has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Called reentrantly from a render callback.</exception>
    public Task ClearAsync(string? id = null, CancellationToken cancellationToken = default)
        => _writeAsync(TerminalProgramStatusCodec.SerializeClear(id), cancellationToken);
}
