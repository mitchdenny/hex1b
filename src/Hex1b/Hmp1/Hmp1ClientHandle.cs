namespace Hex1b.Hmp1;

/// <summary>
/// Handle representing a connected muxer client. Dispose to disconnect.
/// </summary>
public sealed class Hmp1ClientHandle : IAsyncDisposable
{
    private readonly Hmp1PresentationAdapter.Hmp1ClientSession _session;
    private readonly Hmp1PresentationAdapter _adapter;
    private bool _disposed;

    internal Hmp1ClientHandle(
        Hmp1PresentationAdapter.Hmp1ClientSession session,
        Hmp1PresentationAdapter adapter)
    {
        _session = session;
        _adapter = adapter;
    }

    /// <summary>
    /// Gets whether the client is still connected.
    /// </summary>
    public bool IsConnected => !_disposed && _session.ReadTask is { IsCompleted: false };

    /// <summary>
    /// Gets the remote client's terminal width (from the last Resize frame received).
    /// </summary>
    public int RemoteWidth => _session.RemoteWidth;

    /// <summary>
    /// Gets the remote client's terminal height (from the last Resize frame received).
    /// </summary>
    public int RemoteHeight => _session.RemoteHeight;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _adapter.RemoveSession(_session);
        return ValueTask.CompletedTask;
    }
}
