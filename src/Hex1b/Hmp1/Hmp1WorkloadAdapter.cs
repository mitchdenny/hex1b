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
/// <see cref="IsPrimary"/> is false). Set <see cref="IHmp1ConnectionHandle.OnRoleChanged"/>,
/// <see cref="IHmp1ConnectionHandle.OnPeerJoined"/>, and
/// <see cref="IHmp1ConnectionHandle.OnPeerLeft"/> to drive UX state.
/// </para>
/// </remarks>
public sealed class Hmp1WorkloadAdapter : IHex1bTerminalWorkloadAdapter, IHmp1ConnectionHandle
{
    private readonly Hmp1ClientOptions _options;
    private readonly string _localDisplayName;
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
    // Completed early in the read-pump finally — *before* invoking the
    // user's OnDisconnected — so internal "wait for transport disconnect"
    // consumers (e.g. WithHmp1Client's runCallback) don't get coupled to
    // user-handler duration. See WaitForDisconnectAsync.
    private readonly TaskCompletionSource _disconnectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Creates a muxer workload adapter from the supplied options bag.
    /// </summary>
    /// <param name="options">
    /// Transport (<see cref="Hmp1ClientOptions.StreamFactory"/>),
    /// optional <see cref="Hmp1ClientOptions.StreamTransform"/>,
    /// handshake hints, and event hooks.
    /// </param>
    /// <remarks>
    /// Most consumers should use the
    /// <see cref="Hmp1BuilderExtensions"/> family instead — the
    /// extensions construct and wire this adapter for you, and deliver
    /// an <see cref="IHmp1ConnectionHandle"/> via
    /// <see cref="Hmp1ClientOptions.OnConnected"/>. Construct this
    /// adapter directly only when you need to drive
    /// <see cref="ConnectAsync"/> before assembling the surrounding
    /// terminal builder (typically because the builder needs to read
    /// producer dimensions before <c>Build()</c>).
    /// </remarks>
    public Hmp1WorkloadAdapter(Hmp1ClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _localDisplayName = options.DisplayName ?? Hmp1Base36.GenerateDisplayName();
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

        // Pre-populate adapter callback properties from the options bag.
        // Single assignment per property. Consumers who construct this
        // adapter directly may later compose additional handlers via
        // `+=` on the IHmp1ConnectionHandle properties — Hmp1AsyncCallback
        // iterates the invocation list so multicast composition behaves
        // exactly as a developer migrating from C# events would expect.
        OnConnected = options.OnConnected;
        OnRoleChanged = options.OnRoleChanged;
        OnPeerJoined = options.OnPeerJoined;
        OnPeerLeft = options.OnPeerLeft;
        OnRemoteResized = options.OnRemoteResized;
        OnDisconnected = options.OnDisconnected;
    }

    /// <summary>
    /// The display name this adapter sent in its
    /// <see cref="Hmp1FrameType.ClientHello"/> frame. If the caller
    /// did not supply one, this returns the auto-generated value.
    /// </summary>
    public string LocalDisplayName => _localDisplayName;

    /// <summary>
    /// The role hint this adapter sent in its ClientHello, or
    /// <see langword="null"/> if no hint was supplied.
    /// </summary>
    public Hmp1Role? DefaultRole => _options.DefaultRole;

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
    public Func<CancellationToken, Task>? OnDisconnected { get; set; }

    /// <inheritdoc />
    public Func<Hmp1ConnectedEventArgs, CancellationToken, Task>? OnConnected { get; set; }

    /// <inheritdoc />
    public Func<RoleChangedEventArgs, CancellationToken, Task>? OnRoleChanged { get; set; }

    /// <inheritdoc />
    public Func<PeerJoinEventArgs, CancellationToken, Task>? OnPeerJoined { get; set; }

    /// <inheritdoc />
    public Func<PeerLeaveEventArgs, CancellationToken, Task>? OnPeerLeft { get; set; }

    /// <inheritdoc />
    public Func<RemoteResizedEventArgs, CancellationToken, Task>? OnRemoteResized { get; set; }

    /// <summary>
    /// Raised when the underlying transport closes. Required by the
    /// <see cref="IHex1bTerminalWorkloadAdapter"/> contract so the
    /// hosting <c>Hex1bTerminal</c> can observe workload exit. HMP1
    /// consumers that want async-callback semantics should set
    /// <see cref="OnDisconnected"/> instead — the event remains for
    /// the workload-adapter contract only.
    /// </summary>
    public event Action? Disconnected;

    /// <summary>
    /// Completes when the read pump has observed disconnection (clean
    /// transport close, stream error, or local disposal). Completed
    /// <em>before</em> the <see cref="OnDisconnected"/> callback is
    /// awaited so framework consumers that just want to know "the
    /// transport went away" do not get coupled to user-handler duration.
    /// </summary>
    /// <remarks>
    /// Used by <c>Hmp1BuilderExtensions.WithHmp1Client</c>'s internal
    /// <c>runCallback</c> as the "wait for disconnect" signal — replaces
    /// the previous pattern of subscribing to a multicast event, which
    /// is no longer available now that callbacks are async-callback
    /// properties.
    /// </remarks>
    public Task DisconnectedTask => _disconnectedTcs.Task;

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
        var rawStream = await _options.StreamFactory(ct).ConfigureAwait(false);

        // Apply optional stream wrap (TLS, compression, framing) between
        // the raw transport and the HMP1 handshake. This is what makes
        // the EncryptedMuxerDemo "easy path" possible without forcing
        // the caller to write a custom StreamFactory.
        _stream = _options.StreamTransform is { } transform
            ? await transform(rawStream).ConfigureAwait(false)
            : rawStream;

        // Send ClientHello first. The producer reads this before sending its
        // Hello, so a slow server cannot deadlock here.
        await Hmp1Protocol.WriteClientHelloAsync(
            _stream,
            _localDisplayName,
            _options.DefaultRole?.ToWireString(),
            ct).ConfigureAwait(false);

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

        // Snapshot connected-state under the lock, raise the event without
        // holding it. Handlers run on the connecting thread and are
        // awaited inline so ConnectAsync only returns after they
        // complete (matches the previous synchronous-event semantic).
        if (OnConnected is not null)
        {
            string peerId;
            string? primaryPeerId;
            int width;
            int height;
            PeerInfo[] peers;
            lock (_stateLock)
            {
                peerId = _peerId;
                primaryPeerId = _primaryPeerId;
                width = _currentWidth;
                height = _currentHeight;
                peers = [.. _peers];
            }
            var args = new Hmp1ConnectedEventArgs(this, peerId, primaryPeerId, peers, width, height);
            await Hmp1AsyncCallback.InvokeAsync(OnConnected, args, ct).ConfigureAwait(false);
        }
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
    /// The server always grants in this iteration; observe
    /// <see cref="IHmp1ConnectionHandle.OnRoleChanged"/>
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
        Func<RoleChangedEventArgs, CancellationToken, Task> handler = (_, _) =>
        {
            if (IsPrimary == primary) tcs.TrySetResult(true);
            return Task.CompletedTask;
        };
        OnRoleChanged += handler;
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
            OnRoleChanged -= handler;
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
                        bool resizeChanged;
                        bool resizeIsPrimary;
                        lock (_stateLock)
                        {
                            resizeChanged = _currentWidth != width || _currentHeight != height;
                            _currentWidth = width;
                            _currentHeight = height;
                            resizeIsPrimary = _primaryPeerId != null && _primaryPeerId == _peerId;
                        }
                        if (resizeChanged)
                        {
                            await Hmp1AsyncCallback.InvokeAsync(
                                OnRemoteResized,
                                new RemoteResizedEventArgs(width, height, resizeIsPrimary),
                                ct).ConfigureAwait(false);
                        }
                        break;

                    case Hmp1FrameType.RoleChange:
                        await HandleRoleChangeAsync(frame.Payload, ct).ConfigureAwait(false);
                        break;

                    case Hmp1FrameType.PeerJoin:
                        await HandlePeerJoinAsync(frame.Payload, ct).ConfigureAwait(false);
                        break;

                    case Hmp1FrameType.PeerLeave:
                        await HandlePeerLeaveAsync(frame.Payload, ct).ConfigureAwait(false);
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
            // Complete the disconnect task BEFORE invoking the user
            // callback so framework consumers awaiting DisconnectedTask
            // don't get blocked behind a slow user OnDisconnected
            // handler. The TCS is one-shot — only the first disconnect
            // observation completes it.
            _disconnectedTcs.TrySetResult();
            // Fire the IHex1bTerminalWorkloadAdapter contract event so
            // the hosting Hex1bTerminal can react to workload exit.
            // Wrap in try/catch so a misbehaving subscriber can't
            // suppress the OnDisconnected callback below.
            try { Disconnected?.Invoke(); } catch { }
            // Pass CancellationToken.None: by this point _readCts has
            // (likely) been cancelled, and a naive handler that throws
            // on a cancelled token would skip the cleanup work the
            // disconnect callback exists to perform.
            await Hmp1AsyncCallback.InvokeAsync(OnDisconnected, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleRoleChangeAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var p = Hmp1Protocol.ParseRoleChange(payload);
        bool nowPrimary;
        bool previouslyPrimary;
        bool dimsChanged;
        lock (_stateLock)
        {
            previouslyPrimary = _primaryPeerId != null && _primaryPeerId == _peerId;
            _primaryPeerId = p.PrimaryPeerId;
            dimsChanged = _currentWidth != p.Width || _currentHeight != p.Height;
            _currentWidth = p.Width;
            _currentHeight = p.Height;
            nowPrimary = _primaryPeerId != null && _primaryPeerId == _peerId;
        }
        await Hmp1AsyncCallback.InvokeAsync(
            OnRoleChanged,
            new RoleChangedEventArgs(p.PrimaryPeerId, p.Width, p.Height, p.Reason, previouslyPrimary, nowPrimary),
            ct).ConfigureAwait(false);
        if (dimsChanged)
        {
            await Hmp1AsyncCallback.InvokeAsync(
                OnRemoteResized,
                new RemoteResizedEventArgs(p.Width, p.Height, nowPrimary),
                ct).ConfigureAwait(false);
        }
    }

    private async ValueTask HandlePeerJoinAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var p = Hmp1Protocol.ParsePeerJoin(payload);
        lock (_stateLock)
        {
            _peers.Add(new PeerInfo(p.PeerId, p.DisplayName));
        }
        await Hmp1AsyncCallback.InvokeAsync(
            OnPeerJoined,
            new PeerJoinEventArgs(p.PeerId, p.DisplayName),
            ct).ConfigureAwait(false);
    }

    private async ValueTask HandlePeerLeaveAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
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
        await Hmp1AsyncCallback.InvokeAsync(
            OnPeerLeft,
            new PeerLeaveEventArgs(p.PeerId),
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Detect dispose-from-callback: a user handler running on the
        // read pump that calls back into us (e.g.
        // OnRoleChanged = async (e, ct) => await connection.DisposeAsync())
        // would otherwise deadlock waiting on _readTask, which is the
        // task currently executing the callback. Cancel the pump but
        // don't wait for it — the pump will unwind naturally after the
        // callback returns and the next ReadFrameAsync observes
        // cancellation.
        var fromCallback = Hmp1CallbackContext.InCallback;

        if (_readCts is not null)
        {
            await _readCts.CancelAsync().ConfigureAwait(false);
            if (!fromCallback)
            {
                try
                {
                    if (_readTask is not null)
                        await _readTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }
            // Defer Dispose until the pump has actually finished if
            // we're being called from outside it; otherwise the pump's
            // CT.IsCancellationRequested check already saw the cancel
            // signal and the CTS can be disposed by whoever finally
            // does a non-callback Dispose.
            if (!fromCallback)
            {
                _readCts.Dispose();
            }
        }

        if (_stream is not null && !fromCallback)
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
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnRoleChanged"/>
/// callback.
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
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnPeerJoined"/>
/// callback.
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
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnPeerLeft"/>
/// callback.
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

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnConnected"/> callback.
/// Carries the state assembled from the Hello and StateSync handshake frames.
/// </summary>
public sealed class Hmp1ConnectedEventArgs : EventArgs
{
    internal Hmp1ConnectedEventArgs(IHmp1ConnectionHandle connection, string peerId, string? primaryPeerId, IReadOnlyList<PeerInfo> peers, int width, int height)
    {
        Connection = connection;
        PeerId = peerId;
        PrimaryPeerId = primaryPeerId;
        Peers = peers;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// The connection handle for this client. Stash this reference for
    /// later runtime calls (e.g. <see cref="IHmp1ConnectionHandle.RequestPrimaryAsync"/>);
    /// no other public surface delivers it when the easy-path
    /// <c>WithHmp1*</c> builder extensions are used.
    /// </summary>
    public IHmp1ConnectionHandle Connection { get; }

    /// <summary>Peer ID assigned by the server.</summary>
    public string PeerId { get; }

    /// <summary>Peer ID of the current primary, or <see langword="null"/> when no peer is primary.</summary>
    public string? PrimaryPeerId { get; }

    /// <summary>Snapshot of the peer roster at handshake time (excluding self).</summary>
    public IReadOnlyList<PeerInfo> Peers { get; }

    /// <summary>Producer PTY width reported in the Hello frame.</summary>
    public int Width { get; }

    /// <summary>Producer PTY height reported in the Hello frame.</summary>
    public int Height { get; }
}

/// <summary>
/// Arguments for the <see cref="IHmp1ConnectionHandle.OnRemoteResized"/> callback.
/// </summary>
public sealed class RemoteResizedEventArgs : EventArgs
{
    internal RemoteResizedEventArgs(int width, int height, bool causedByLocalPrimary)
    {
        Width = width;
        Height = height;
        CausedByLocalPrimary = causedByLocalPrimary;
    }

    /// <summary>The new producer PTY width.</summary>
    public int Width { get; }

    /// <summary>The new producer PTY height.</summary>
    public int Height { get; }

    /// <summary>
    /// <see langword="true"/> when the receiving adapter was primary at the
    /// moment the resize took effect (typically meaning this client caused
    /// the resize via <see cref="Hmp1WorkloadAdapter.RequestPrimaryAsync"/>
    /// or <see cref="Hmp1WorkloadAdapter.ResizeAsync"/>); <see langword="false"/>
    /// when another peer caused it.
    /// </summary>
    public bool CausedByLocalPrimary { get; }
}
