namespace Hex1b;

/// <summary>
/// Platform-specific PTY handle abstraction.
/// </summary>
internal interface IPtyHandle : IAsyncDisposable
{
    /// <summary>
    /// Gets the process ID.
    /// </summary>
    int ProcessId { get; }

    /// <summary>
    /// Starts the process with the given parameters.
    /// </summary>
    /// <remarks>
    /// Failure to establish the requested working directory must fail startup.
    /// Unix startup confirms the exec handshake, not application readiness.
    /// Failed or canceled startup must release provisional descriptors and reap its child;
    /// disposal must remain safe after any startup outcome.
    /// </remarks>
    Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct);

    /// <summary>
    /// Reads output from the PTY master.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);

    /// <summary>
    /// Writes input to the PTY master.
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    /// <summary>
    /// Resizes the PTY.
    /// </summary>
    void Resize(int width, int height);

    /// <summary>
    /// Sends a signal to the process.
    /// </summary>
    void Kill(int signal);

    /// <summary>
    /// Waits for the process to exit.
    /// </summary>
    Task<int> WaitForExitAsync(CancellationToken ct);
}
