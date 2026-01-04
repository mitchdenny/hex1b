namespace Hex1b.Terminal;

/// <summary>
/// Windows PTY implementation using ConPTY APIs.
/// </summary>
/// <remarks>
/// This is a stub implementation. Full ConPTY support will be added later.
/// </remarks>
internal sealed class WindowsPtyHandle : IPtyHandle
{
    public int ProcessId => throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    
    public Task StartAsync(
        string fileName,
        string[] arguments,
        string? workingDirectory,
        Dictionary<string, string> environment,
        int width,
        int height,
        CancellationToken ct)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public void Resize(int width, int height)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public void Kill(int signal)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public Task<int> WaitForExitAsync(CancellationToken ct)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }
    
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
