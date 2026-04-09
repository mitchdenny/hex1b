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
