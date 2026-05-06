namespace Hex1b;

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
    /// Gets the peer ID assigned by the server when this client connected.
    /// </summary>
    public string PeerId => _session.PeerId;

    /// <summary>
    /// Gets the optional human-readable label this client provided in its
    /// <see cref="Hmp1FrameType.ClientHello"/>.
    /// </summary>
    public string? DisplayName => _session.DisplayName;

    /// <summary>
    /// Gets whether this client is currently the primary peer.
    /// </summary>
    public bool IsPrimary => _adapter.PrimaryPeerId == _session.PeerId;

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
