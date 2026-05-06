using System.Diagnostics;
using System.Threading.Channels;

namespace Hex1b;

/// <summary>
/// A workload adapter that connects to a remote muxer server over the Hex1b Muxer Protocol (HMP).
/// </summary>
/// <remarks>
/// <para>
/// This adapter reads terminal output from a remote server and forwards local keyboard
/// input and resize events to it. On connection, the client sends a
/// <see cref="Hmp1FrameType.ClientHello"/> frame; the server responds with a
/// <see cref="Hmp1FrameType.Hello"/> (with the assigned peer ID, current primary, and
/// roster) and a <see cref="Hmp1FrameType.StateSync"/>.
/// </para>
/// <para>
/// The adapter is transport-agnostic: provide any bidirectional <see cref="Stream"/>
/// (Unix domain socket, TCP, named pipe, etc.).
/// </para>
/// <para>
/// Multi-head: a connected client is a "secondary" by default. To take control of the
/// underlying PTY's dimensions, call <see cref="RequestPrimaryAsync"/>. Until the role
/// transition is acknowledged, <see cref="ResizeAsync"/> calls are silently dropped at
/// the producer (the local <see cref="ResizeAsync"/> is also a no-op while
/// <see cref="IsPrimary"/> is false). Subscribe to <see cref="RoleChanged"/>,
/// <see cref="PeerJoined"/>, and <see cref="PeerLeft"/> to drive UX state.
/// </para>
/// </remarks>
public sealed class Hmp1WorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    private readonly Func<CancellationToken, Task<Stream>> _streamFactory;
    private readonly string? _displayName;
    private readonly string? _defaultRole;
    private Stream? _stream;
    private readonly Channel<ReadOnlyMemory<byte>> _outputChannel;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private bool _disposed;
    private readonly object _stateLock = new();
    private readonly List<PeerInfo> _peers = [];
    private string _peerId = string.Empty;
    private string? _primaryPeerId;
    private int _currentWidth;
    private int _currentHeight;

    /// <summary>
    /// Creates a muxer workload adapter that connects via the given stream factory.
    /// </summary>
    /// <param name="streamFactory">Factory that creates a bidirectional stream to the server.</param>
    /// <param name="displayName">Optional human-readable label sent to the server in
    /// <see cref="Hmp1FrameType.ClientHello"/>.</param>
    /// <param name="defaultRole">Optional default-role hint
    /// (<c>"viewer"</c> or <c>"interactive"</c>).</param>
    public Hmp1WorkloadAdapter(
        Func<CancellationToken, Task<Stream>> streamFactory,
        string? displayName = null,
        string? defaultRole = null)
    {
        _streamFactory = streamFactory ?? throw new ArgumentNullException(nameof(streamFactory));
        _displayName = displayName;
        _defaultRole = defaultRole;
        // Wait (not DropOldest) so producer back-pressures over the wire when the
        // consumer is slow, instead of silently losing terminal output. Restored
        // from Hex1b PR #308 (Phase 9c) after the Phase 10 rewrite.
        _outputChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });
    }

    /// <summary>
    /// Creates a muxer workload adapter with an already-connected stream.
    /// </summary>
    /// <param name="stream">A bidirectional stream connected to the server.</param>
    /// <param name="displayName">Optional human-readable label.</param>
    /// <param name="defaultRole">Optional default-role hint.</param>
    public Hmp1WorkloadAdapter(Stream stream, string? displayName = null, string? defaultRole = null)
        : this(_ => Task.FromResult(stream), displayName, defaultRole)
    {
        ArgumentNullException.ThrowIfNull(stream);
    }

    /// <summary>
    /// Gets the peer ID assigned by the server in the Hello frame.
    /// </summary>
    public string PeerId
    {
        get { lock (_stateLock) { return _peerId; } }
    }

    /// <summary>
    /// Gets the peer ID of the current primary peer, or <see langword="null"/>
    /// when no peer is primary.
    /// </summary>
    public string? PrimaryPeerId
    {
        get { lock (_stateLock) { return _primaryPeerId; } }
    }

    /// <summary>
    /// Gets whether this adapter is currently the primary peer.
    /// </summary>
    public bool IsPrimary
    {
        get { lock (_stateLock) { return _primaryPeerId != null && _primaryPeerId == _peerId; } }
    }

    /// <summary>
    /// Gets a snapshot of currently-connected peer roster (excluding self).
    /// </summary>
    public IReadOnlyList<PeerInfo> Peers
    {
        get { lock (_stateLock) { return [.. _peers]; } }
    }

    /// <summary>
    /// Gets the current PTY width as known to this adapter (updated on
    /// <see cref="Hmp1FrameType.RoleChange"/> and observed
    /// <see cref="Hmp1FrameType.Hello"/>).
    /// </summary>
    public int CurrentWidth
    {
        get { lock (_stateLock) { return _currentWidth; } }
    }

    /// <summary>
    /// Gets the current PTY height as known to this adapter.
    /// </summary>
    public int CurrentHeight
    {
        get { lock (_stateLock) { return _currentHeight; } }
    }

    /// <summary>
    /// Gets the remote terminal width reported in the Hello frame (preserved for
    /// source compatibility with single-head HMP1 callers).
    /// </summary>
    public int RemoteWidth => CurrentWidth;

    /// <summary>
    /// Gets the remote terminal height reported in the Hello frame (preserved for
    /// source compatibility with single-head HMP1 callers).
    /// </summary>
    public int RemoteHeight => CurrentHeight;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Raised when the primary peer changes (including to <see langword="null"/>
    /// after the previous primary disconnects).
    /// </summary>
    public event EventHandler<RoleChangedEventArgs>? RoleChanged;

    /// <summary>
    /// Raised when another peer joins the same producer.
    /// </summary>
    public event EventHandler<PeerJoinEventArgs>? PeerJoined;

    /// <summary>
    /// Raised when another peer leaves.
    /// </summary>
    public event EventHandler<PeerLeaveEventArgs>? PeerLeft;

    /// <summary>
    /// Connects to the server, sends ClientHello, reads Hello and StateSync,
    /// and starts the background read pump.
    /// </summary>
    /// <remarks>
    /// The supplied <paramref name="ct"/> is used only for the handshake. Once
    /// the handshake completes, the read pump's lifetime is tied exclusively
    /// to <see cref="DisposeAsync"/>. This avoids the historical foot-gun
    /// where a caller's <c>handshakeCts.CancelAfter(...)</c> would also tear
    /// down the read pump after the handshake had already succeeded.
    /// </remarks>
    public async Task ConnectAsync(CancellationToken ct)
    {
        _stream = await _streamFactory(ct).ConfigureAwait(false);

        // Send ClientHello first. The producer reads this before sending its
        // Hello, so a slow server cannot deadlock here.
        await Hmp1Protocol.WriteClientHelloAsync(_stream, _displayName, _defaultRole, ct).ConfigureAwait(false);

        // Read Hello frame
        var helloFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server closed connection before Hello frame.");

        if (helloFrame.Type != Hmp1FrameType.Hello)
            throw new InvalidOperationException($"Expected Hello frame, got {helloFrame.Type}.");

        var hello = Hmp1Protocol.ParseHello(helloFrame.Payload);
        lock (_stateLock)
        {
            _peerId = hello.PeerId ?? string.Empty;
            _primaryPeerId = hello.PrimaryPeerId;
            _currentWidth = hello.Width;
            _currentHeight = hello.Height;
            if (hello.Peers is { } initialPeers)
            {
                foreach (var p in initialPeers)
                {
                    _peers.Add(new PeerInfo(p.PeerId, p.DisplayName));
                }
            }
        }

        // Read StateSync frame
        var syncFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server closed connection before StateSync frame.");

        if (syncFrame.Type != Hmp1FrameType.StateSync)
            throw new InvalidOperationException($"Expected StateSync frame, got {syncFrame.Type}.");

        // Queue the initial screen content so the terminal displays it immediately
        if (!syncFrame.Payload.IsEmpty)
        {
            _outputChannel.Writer.TryWrite(syncFrame.Payload);
        }

        // Start the background read pump. Important: do NOT capture the caller-supplied
        // CancellationToken here. A "handshake timeout" CT must NOT keep cancelling
        // the read pump after the handshake has succeeded. The pump's lifetime is
        // owned by DisposeAsync (and DisposeAsync alone) via _readCts.
        _readCts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadPumpAsync(_readCts.Token));
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return new ValueTask<ReadOnlyMemory<byte>>(ReadOnlyMemory<byte>.Empty);

        return ReadOutputCoreAsync(ct);
    }

    private async ValueTask<ReadOnlyMemory<byte>> ReadOutputCoreAsync(CancellationToken ct)
    {
        try
        {
            if (!await _outputChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
                return ReadOnlyMemory<byte>.Empty;

            if (!_outputChannel.Reader.TryRead(out var first))
                return ReadOnlyMemory<byte>.Empty;

            // Coalesce any other frames that are immediately available into a single
            // returned buffer. Reduces per-frame overhead in the consumer pump (xterm.js,
            // headless emulator, etc.) under high producer throughput. Restored from
            // Hex1b PR #308 (Phase 9c).
            if (!_outputChannel.Reader.TryPeek(out _))
                return first;

            var pending = new List<ReadOnlyMemory<byte>> { first };
            var total = first.Length;
            while (_outputChannel.Reader.TryRead(out var more))
            {
                pending.Add(more);
                total += more.Length;
            }

            var combined = new byte[total];
            var pos = 0;
            foreach (var chunk in pending)
            {
                chunk.Span.CopyTo(combined.AsSpan(pos));
                pos += chunk.Length;
            }
            return combined;
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <inheritdoc />
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || _stream is null) return;

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed || _stream is null) return;
            await Hmp1Protocol.WriteFrameAsync(_stream, Hmp1FrameType.Input, data, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Stream closed
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Sends a Resize frame to the producer when this adapter is the current
    /// primary; silently no-ops (with a debug-tracing line) otherwise.
    /// </summary>
    public async ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        if (_disposed || _stream is null) return;

        if (!IsPrimary)
        {
            Debug.WriteLine($"[Hmp1WorkloadAdapter] Suppressing Resize {width}x{height}: peer {PeerId} is not primary (primary={PrimaryPeerId ?? "<none>"}).");
            return;
        }

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed || _stream is null) return;
            await Hmp1Protocol.WriteResizeAsync(_stream, width, height, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Stream closed
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Asks the server to make this peer the primary at the supplied dimensions.
    /// The server always grants in this iteration; observe <see cref="RoleChanged"/>
    /// (or call <see cref="WaitForRoleAsync"/>) for the acknowledged transition.
    /// </summary>
    public async Task RequestPrimaryAsync(int cols, int rows, CancellationToken ct = default)
    {
        if (_disposed || _stream is null)
            throw new InvalidOperationException("Adapter is not connected.");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed || _stream is null)
                throw new InvalidOperationException("Adapter is not connected.");
            await Hmp1Protocol.WriteRequestPrimaryAsync(_stream, cols, rows, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for this adapter's
    /// <see cref="IsPrimary"/> to match <paramref name="primary"/>. Returns
    /// <see langword="true"/> on success, <see langword="false"/> on timeout.
    /// </summary>
    /// <remarks>
    /// Useful in tests to write
    /// <code>await adapter.RequestPrimaryAsync(...);
    /// var ok = await adapter.WaitForRoleAsync(primary: true, TimeSpan.FromSeconds(1), ct);</code>
    /// without busy-polling <see cref="IsPrimary"/>.
    /// </remarks>
    public async Task<bool> WaitForRoleAsync(bool primary, TimeSpan timeout, CancellationToken ct = default)
    {
        if (IsPrimary == primary) return true;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<RoleChangedEventArgs> handler = (_, _) =>
        {
            if (IsPrimary == primary) tcs.TrySetResult(true);
        };
        RoleChanged += handler;
        try
        {
            // Re-check after subscribing to close the race window.
            if (IsPrimary == primary) return true;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);
            using var registration = timeoutCts.Token.Register(() => tcs.TrySetResult(false));

            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            RoleChanged -= handler;
        }
    }

    /// <summary>
    /// Background pump that reads frames from the server.
    /// </summary>
    private async Task ReadPumpAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream is not null)
            {
                var maybeFrame = await Hmp1Protocol.ReadFrameAsync(_stream, ct).ConfigureAwait(false);
                if (maybeFrame is not { } frame)
                    break; // Server disconnected

                switch (frame.Type)
                {
                    case Hmp1FrameType.Output:
                    case Hmp1FrameType.StateSync:
                        // WriteAsync (not TryWrite) so the bounded channel back-pressures
                        // the network when the consumer is slow, instead of silently
                        // losing frames. Restored from Hex1b PR #308 (Phase 9c).
                        await _outputChannel.Writer.WriteAsync(frame.Payload, ct).ConfigureAwait(false);
                        break;

                    case Hmp1FrameType.Resize:
                        var (width, height) = Hmp1Protocol.ParseResize(frame.Payload);
                        lock (_stateLock)
                        {
                            _currentWidth = width;
                            _currentHeight = height;
                        }
                        break;

                    case Hmp1FrameType.RoleChange:
                        HandleRoleChange(frame.Payload);
                        break;

                    case Hmp1FrameType.PeerJoin:
                        HandlePeerJoin(frame.Payload);
                        break;

                    case Hmp1FrameType.PeerLeave:
                        HandlePeerLeave(frame.Payload);
                        break;

                    case Hmp1FrameType.Exit:
                        // Server terminal has exited
                        return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Stream error
        }
        finally
        {
            _outputChannel.Writer.TryComplete();
            Disconnected?.Invoke();
        }
    }

    private void HandleRoleChange(ReadOnlyMemory<byte> payload)
    {
        var p = Hmp1Protocol.ParseRoleChange(payload);
        bool nowPrimary;
        bool previouslyPrimary;
        lock (_stateLock)
        {
            previouslyPrimary = _primaryPeerId != null && _primaryPeerId == _peerId;
            _primaryPeerId = p.PrimaryPeerId;
            _currentWidth = p.Width;
            _currentHeight = p.Height;
            nowPrimary = _primaryPeerId != null && _primaryPeerId == _peerId;
        }
        RoleChanged?.Invoke(this, new RoleChangedEventArgs(p.PrimaryPeerId, p.Width, p.Height, p.Reason, previouslyPrimary, nowPrimary));
    }

    private void HandlePeerJoin(ReadOnlyMemory<byte> payload)
    {
        var p = Hmp1Protocol.ParsePeerJoin(payload);
        lock (_stateLock)
        {
            _peers.Add(new PeerInfo(p.PeerId, p.DisplayName));
        }
        PeerJoined?.Invoke(this, new PeerJoinEventArgs(p.PeerId, p.DisplayName));
    }

    private void HandlePeerLeave(ReadOnlyMemory<byte> payload)
    {
        var p = Hmp1Protocol.ParsePeerLeave(payload);
        lock (_stateLock)
        {
            for (var i = _peers.Count - 1; i >= 0; i--)
            {
                if (_peers[i].PeerId == p.PeerId)
                {
                    _peers.RemoveAt(i);
                }
            }
        }
        PeerLeft?.Invoke(this, new PeerLeaveEventArgs(p.PeerId));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_readCts is not null)
        {
            await _readCts.CancelAsync().ConfigureAwait(false);
            try
            {
                if (_readTask is not null)
                    await _readTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            _readCts.Dispose();
        }

        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        _outputChannel.Writer.TryComplete();
    }
}

/// <summary>
/// A peer connected to the same producer as this adapter.
/// </summary>
/// <param name="PeerId">The peer's ID.</param>
/// <param name="DisplayName">Optional human-readable label.</param>
public readonly record struct PeerInfo(string PeerId, string? DisplayName);

/// <summary>
/// Arguments for the <see cref="Hmp1WorkloadAdapter.RoleChanged"/> event.
/// </summary>
public sealed class RoleChangedEventArgs : EventArgs
{
    internal RoleChangedEventArgs(string? primaryPeerId, int width, int height, string reason, bool previouslyPrimary, bool nowPrimary)
    {
        PrimaryPeerId = primaryPeerId;
        Width = width;
        Height = height;
        Reason = reason;
        PreviouslyPrimary = previouslyPrimary;
        NowPrimary = nowPrimary;
    }

    /// <summary>The new primary's peer ID, or null when no peer is primary.</summary>
    public string? PrimaryPeerId { get; }

    /// <summary>The PTY width as of this transition.</summary>
    public int Width { get; }

    /// <summary>The PTY height as of this transition.</summary>
    public int Height { get; }

    /// <summary>Free-form reason string from the producer.</summary>
    public string Reason { get; }

    /// <summary>Whether the receiving adapter was primary before this transition.</summary>
    public bool PreviouslyPrimary { get; }

    /// <summary>Whether the receiving adapter is primary after this transition.</summary>
    public bool NowPrimary { get; }
}

/// <summary>
/// Arguments for the <see cref="Hmp1WorkloadAdapter.PeerJoined"/> event.
/// </summary>
public sealed class PeerJoinEventArgs : EventArgs
{
    internal PeerJoinEventArgs(string peerId, string? displayName)
    {
        PeerId = peerId;
        DisplayName = displayName;
    }

    /// <summary>Peer ID of the joining peer.</summary>
    public string PeerId { get; }

    /// <summary>Optional human-readable label of the joining peer.</summary>
    public string? DisplayName { get; }
}

/// <summary>
/// Arguments for the <see cref="Hmp1WorkloadAdapter.PeerLeft"/> event.
/// </summary>
public sealed class PeerLeaveEventArgs : EventArgs
{
    internal PeerLeaveEventArgs(string peerId)
    {
        PeerId = peerId;
    }

    /// <summary>Peer ID of the leaving peer.</summary>
    public string PeerId { get; }
}
