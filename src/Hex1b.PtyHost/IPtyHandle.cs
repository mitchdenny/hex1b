namespace Hex1b;

internal interface IPtyHandle : IAsyncDisposable
{
    int ProcessId { get; }

    Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct);

    ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);

    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);

    void Resize(int width, int height);

    void Kill(int signal);

    Task<int> WaitForExitAsync(CancellationToken ct);
}
