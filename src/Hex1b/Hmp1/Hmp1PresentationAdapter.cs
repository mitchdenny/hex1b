using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;

namespace Hex1b;

/// <summary>
/// A presentation adapter that serves terminal output to multiple remote clients
/// over the Hex1b Muxer Protocol (HMP).
/// </summary>
/// <remarks>
/// <para>
/// This adapter acts as a headless presentation layer. Instead of displaying output
/// on a local console, it multicasts ANSI output to all connected clients via their
/// streams. Use <see cref="AddClient"/> to add new client connections.
/// </para>
/// <para>
/// Each client first sends a <see cref="Hmp1FrameType.ClientHello"/> frame; the
/// server then assigns a peer ID and replies with <see cref="Hmp1FrameType.Hello"/>
/// (carrying the assigned peer ID, current primary, and roster) followed by a
/// <see cref="Hmp1FrameType.StateSync"/> frame with a full screen snapshot.
/// </para>
/// <para>
/// One peer may be the <em>primary</em> at any time. The primary's
/// <see cref="Hmp1FrameType.Resize"/> frames are applied to the underlying PTY;
/// secondaries' Resize frames are silently dropped. Any peer can send a
/// <see cref="Hmp1FrameType.RequestPrimary"/> to take over (always granted in
/// this iteration); the resulting <see cref="Hmp1FrameType.RoleChange"/> and any
/// fresh <see cref="Hmp1FrameType.StateSync"/> are broadcast to all peers.
/// </para>
/// </remarks>
public sealed class Hmp1PresentationAdapter : ITerminalLifecycleAwarePresentationAdapter
{
    private readonly List<Hmp1ClientSession> _sessions = [];
    private readonly object _sessionsLock = new();
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private Hex1bTerminal? _terminal;
    private int _width;
    private int _height;
    private string? _primaryPeerId;
    private bool _disposed;

    /// <summary>
    /// Creates a new muxer presentation adapter with the specified initial dimensions.
    /// </summary>
    /// <param name="width">Initial terminal width in columns. The PTY runs at this size
    /// from <c>t0</c> until a peer takes primary and requests a resize.</param>
    /// <param name="height">Initial terminal height in rows.</param>
    public Hmp1PresentationAdapter(int width = 80, int height = 24)
    {
        _width = width;
        _height = height;
        _inputChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    /// <inheritdoc />
    public int Width => _width;

    /// <inheritdoc />
    public int Height => _height;

    /// <summary>
    /// Gets the peer ID of the current primary, or <see langword="null"/> when
    /// no peer is primary (initial state, or after the previous primary
    /// disconnected with no replacement).
    /// </summary>
    public string? PrimaryPeerId
    {
        get
        {
            lock (_sessionsLock)
            {
                return _primaryPeerId;
            }
        }
    }

    /// <inheritdoc />
    public TerminalCapabilities Capabilities => new()
    {
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>
    /// Raised when the primary peer changes (including transitions to no primary).
    /// Argument is the new <see cref="PrimaryPeerId"/>.
    /// </summary>
    public event Action<string?>? PrimaryChanged;

    /// <summary>
    /// Gets the number of currently connected clients.
    /// </summary>
    public int ClientCount
    {
        get
        {
            lock (_sessionsLock)
            {
                return _sessions.Count;
            }
        }
    }

    /// <inheritdoc />
    public void TerminalCreated(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }

    /// <inheritdoc />
    public void TerminalStarted()
    {
    }

    /// <inheritdoc />
    public void TerminalCompleted(int exitCode)
    {
        // Notify all clients that the terminal has exited
        Hmp1ClientSession[] snapshot;
        lock (_sessionsLock)
        {
            snapshot = [.. _sessions];
        }

        foreach (var session in snapshot)
        {
            _ = TrySendExitAsync(session, exitCode);
        }

        // Complete the input channel so the terminal's input pump exits,
        // allowing RunAsync to return when the workload has disconnected.
        _inputChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Adds a new client connection. The client must first send a
    /// <see cref="Hmp1FrameType.ClientHello"/> frame; the server then writes
    /// the assigned peer ID, current primary, and roster in
    /// <see cref="Hmp1FrameType.Hello"/>, followed by a
    /// <see cref="Hmp1FrameType.StateSync"/> frame.
    /// </summary>
    /// <param name="stream">A bidirectional stream connected to the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A handle that can be disposed to disconnect the client.</returns>
    public async Task<Hmp1ClientHandle> AddClient(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Read the client's hello first. Apply a generous timeout so a stuck
        // peer cannot hold this slot open forever.
        ClientHelloPayload clientHello;
        using (var helloCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            helloCts.CancelAfter(ClientHelloTimeout);
            var maybe = await Hmp1Protocol.ReadFrameAsync(stream, helloCts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Client closed connection before sending ClientHello frame.");
            if (maybe.Type != Hmp1FrameType.ClientHello)
                throw new InvalidOperationException(
                    $"Expected ClientHello frame, got {maybe.Type}. The peer may be using an older HMP1 wire format.");
            clientHello = Hmp1Protocol.ParseClientHello(maybe.Payload);
        }

        var peerId = GeneratePeerId();
        var displayName = clientHello.DisplayName;
        var defaultRole = clientHello.DefaultRole;

        // Atomically: capture snapshot, register session, publish PeerJoin to existing
        // peers — so no output is lost between snapshot creation and registration, and
        // peers see consistent join ordering.
        var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var session = new Hmp1ClientSession(stream, sessionCts, peerId, displayName, defaultRole);

        Hmp1ClientSession[] existingPeers;
        byte[] syncBytes;
        string? primarySnapshot;
        int widthSnapshot;
        int heightSnapshot;
        lock (_sessionsLock)
        {
            existingPeers = [.. _sessions];

            if (_terminal != null)
            {
                using var snap = _terminal.CreateSnapshot();
                var prefix = BuildStateReplayPrefix(snap);
                var ansi = snap.ToAnsi(new TerminalAnsiOptions
                {
                    IncludeClearScreen = true,
                    IncludeTrailingNewline = true
                });
                var suffix = BuildStateReplaySuffix(snap);
                syncBytes = Encoding.UTF8.GetBytes(prefix + ansi + suffix);
            }
            else
            {
                syncBytes = [];
            }

            primarySnapshot = _primaryPeerId;
            widthSnapshot = _width;
            heightSnapshot = _height;

            _sessions.Add(session);
        }

        // Build roster (existing peers, excluding the new one).
        var roster = new List<HelloPeerInfo>(existingPeers.Length);
        foreach (var p in existingPeers)
        {
            roster.Add(new HelloPeerInfo { PeerId = p.PeerId, DisplayName = p.DisplayName });
        }

        // Send Hello + StateSync to the new peer. Failures here are propagated
        // because the caller hasn't yet received a handle.
        await Hmp1Protocol.WriteHelloAsync(
            stream, widthSnapshot, heightSnapshot, peerId, primarySnapshot, roster, ct).ConfigureAwait(false);
        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync, syncBytes, ct).ConfigureAwait(false);

        // Notify existing peers that a new peer has joined. Best-effort —
        // a slow peer's broken pipe won't take down a successful join.
        foreach (var p in existingPeers)
        {
            EnqueueControlFrameAsync(p, async s =>
                await Hmp1Protocol.WritePeerJoinAsync(s, peerId, displayName, CancellationToken.None).ConfigureAwait(false));
        }

        // Start per-client write pump and read pump
        session.WriteTask = Task.Run(() => WriteClientPumpAsync(session), sessionCts.Token);
        session.ReadTask = Task.Run(() => ReadClientPumpAsync(session), sessionCts.Token);

        return new Hmp1ClientHandle(session, this);
    }

    /// <summary>
    /// Builds the leading portion of a StateSync replay. Anything that has to
    /// happen <em>before</em> the cell content is painted (like switching to
    /// the alternate screen so the painter's clear+home target the right
    /// buffer) is emitted here.
    /// </summary>
    private static string BuildStateReplayPrefix(Hex1bTerminalSnapshot snapshot)
    {
        var sb = new StringBuilder();

        // ToAnsi paints onto whichever buffer the viewer is currently on and
        // does its own \x1b[2J + \x1b[H. If the workload had switched to the
        // alternate screen, we have to enter alt screen first so the
        // subsequent clear+home target the alt buffer. DECSET 1049 also saves
        // and restores the cursor as a side-effect, which lines up with how a
        // viewer would have observed the original entry.
        if (snapshot.InAlternateScreen)
        {
            sb.Append("\x1b[?1049h");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the trailing portion of a StateSync replay. Modes are emitted
    /// here so that any ANSI in the cell-content payload (which conceptually
    /// targets a freshly-cleared screen) cannot accidentally clobber them.
    /// Only non-default modes are emitted; the viewer's terminal starts with
    /// the standard defaults already applied.
    /// </summary>
    private static string BuildStateReplaySuffix(Hex1bTerminalSnapshot snapshot)
    {
        var sb = new StringBuilder();

        // Mouse-tracking protocols. These flags are independent in xterm — a
        // workload may legitimately enable more than one — so we mirror that
        // model on replay rather than picking a single "winning" mode.
        if (snapshot.MouseProtocolX10Enabled)        { sb.Append("\x1b[?9h"); }
        if (snapshot.MouseProtocolNormalEnabled)     { sb.Append("\x1b[?1000h"); }
        if (snapshot.MouseProtocolHighlightEnabled)  { sb.Append("\x1b[?1001h"); }
        if (snapshot.MouseProtocolButtonEnabled)     { sb.Append("\x1b[?1002h"); }
        if (snapshot.MouseProtocolAnyEnabled)        { sb.Append("\x1b[?1003h"); }

        // Mouse-encoding modes. Same independence applies.
        if (snapshot.MouseEncodingUtf8Enabled)       { sb.Append("\x1b[?1005h"); }
        if (snapshot.MouseEncodingSgrEnabled)        { sb.Append("\x1b[?1006h"); }
        if (snapshot.MouseEncodingUrxvtEnabled)      { sb.Append("\x1b[?1015h"); }

        // Focus events.
        if (snapshot.FocusEventsEnabled)             { sb.Append("\x1b[?1004h"); }

        // Bracketed paste.
        if (snapshot.BracketedPasteEnabled)          { sb.Append("\x1b[?2004h"); }

        // Application cursor keys (DECCKM).
        if (snapshot.ApplicationCursorKeysEnabled)   { sb.Append("\x1b[?1h"); }

        // Application keypad mode (DECKPAM).
        if (snapshot.ApplicationKeypadEnabled)       { sb.Append("\x1b="); }

        // Cursor visibility — default is visible, so only emit when hidden.
        if (!snapshot.CursorVisible)                 { sb.Append("\x1b[?25l"); }

        // Cursor shape (DECSCUSR). 0 means "default"; only emit when
        // non-default. The space-q trailer is part of the sequence, not a
        // separator.
        if (snapshot.CursorShape > 0)
        {
            sb.Append("\x1b[");
            sb.Append(snapshot.CursorShape);
            sb.Append(" q");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed || data.IsEmpty) return ValueTask.CompletedTask;

        // Copy once; each session's pump consumes the same buffer view.
        var copy = data.ToArray();

        // Enqueue to each client's write channel (non-blocking).
        // Clients that can't keep up will be disconnected.
        lock (_sessionsLock)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                var session = _sessions[i];
                if (!session.OutputChannel.Writer.TryWrite(new Hmp1OutboundWork(copy, null)))
                {
                    // Client can't keep up — disconnect it
                    _sessions.RemoveAt(i);
                    _ = DisposeSessionAsync(session);
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return ReadOnlyMemory<byte>.Empty;

        try
        {
            if (await _inputChannel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                if (_inputChannel.Reader.TryRead(out var data))
                    return data;
            }
        }
        catch (OperationCanceledException) { }
        catch (ChannelClosedException) { }

        return ReadOnlyMemory<byte>.Empty;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public (int Row, int Column) GetCursorPosition() => (0, 0);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Hmp1ClientSession[] snapshot;
        lock (_sessionsLock)
        {
            snapshot = [.. _sessions];
            _sessions.Clear();
        }

        foreach (var session in snapshot)
        {
            await DisposeSessionAsync(session).ConfigureAwait(false);
        }

        _inputChannel.Writer.TryComplete();
        Disconnected?.Invoke();
    }

    /// <summary>
    /// Background pump that writes queued output frames to a client's stream.
    /// Each client has its own write pump to prevent slow clients from blocking others.
    /// Output frames and out-of-band control writers (RoleChange, PeerJoin,
    /// PeerLeave) are interleaved through this pump so per-client ordering is
    /// preserved and no separate write race exists.
    /// </summary>
    private async Task WriteClientPumpAsync(Hmp1ClientSession session)
    {
        try
        {
            await foreach (var work in session.OutputChannel.Reader.ReadAllAsync(session.Cts.Token)
                .ConfigureAwait(false))
            {
                if (work.ControlWriter is { } writer)
                {
                    await writer(session.Stream).ConfigureAwait(false);
                }
                else if (!work.Output.IsEmpty)
                {
                    await Hmp1Protocol.WriteFrameAsync(
                        session.Stream, Hmp1FrameType.Output, work.Output, session.Cts.Token).ConfigureAwait(false);
                }
                // Flush periodically (after draining available items)
                if (session.OutputChannel.Reader.Count == 0)
                {
                    await session.Stream.FlushAsync(session.Cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            RemoveSession(session);
        }
    }

    /// <summary>
    /// Background pump that reads frames from a client and routes input/resize/role
    /// requests through the central state machine.
    /// </summary>
    private async Task ReadClientPumpAsync(Hmp1ClientSession session)
    {
        try
        {
            while (!session.Cts.IsCancellationRequested)
            {
                var maybeFrame = await Hmp1Protocol.ReadFrameAsync(session.Stream, session.Cts.Token)
                    .ConfigureAwait(false);

                if (maybeFrame is not { } frame)
                    break; // Client disconnected

                switch (frame.Type)
                {
                    case Hmp1FrameType.Input:
                        _inputChannel.Writer.TryWrite(frame.Payload);
                        break;

                    case Hmp1FrameType.Resize:
                        HandleResize(session, frame.Payload);
                        break;

                    case Hmp1FrameType.RequestPrimary:
                        HandleRequestPrimary(session, frame.Payload);
                        break;

                    // Unknown frame types are intentionally silently ignored to
                    // allow forward additions without breaking older readers.
                }
            }
        }
        catch (Exception ex) when (
            ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            // Client disconnected or stream error
        }
        finally
        {
            RemoveSession(session);
        }
    }

    private void HandleResize(Hmp1ClientSession session, ReadOnlyMemory<byte> payload)
    {
        var (width, height) = Hmp1Protocol.ParseResize(payload);

        lock (_sessionsLock)
        {
            // Always remember the peer's *requested* dimensions so a later
            // RequestPrimary without explicit dims has something sensible to
            // fall back on (and so tests can observe per-peer last-resize).
            session.RemoteWidth = width;
            session.RemoteHeight = height;

            // Drop silently when sender is not primary. This is the central
            // multi-head guarantee.
            if (session.PeerId != _primaryPeerId)
                return;

            _width = width;
            _height = height;
        }

        Resized?.Invoke(width, height);
    }

    private void HandleRequestPrimary(Hmp1ClientSession session, ReadOnlyMemory<byte> payload)
    {
        var req = Hmp1Protocol.ParseRequestPrimary(payload);
        var cols = req.Cols > 0 ? req.Cols : session.RemoteWidth;
        var rows = req.Rows > 0 ? req.Rows : session.RemoteHeight;
        if (cols <= 0) cols = _width;
        if (rows <= 0) rows = _height;

        Hmp1ClientSession[] peers;
        bool sizeChanged;
        lock (_sessionsLock)
        {
            // Always grant in this iteration (per the design). Even if the
            // requesting peer is already primary, broadcasting the role change
            // is safe — peers will see a no-op transition with the latest
            // dimensions.
            _primaryPeerId = session.PeerId;
            sizeChanged = (_width != cols) || (_height != rows);
            _width = cols;
            _height = rows;

            peers = [.. _sessions];
        }

        // If size actually changed, fire Resized so the underlying PTY follows.
        if (sizeChanged)
        {
            Resized?.Invoke(cols, rows);
        }

        // Notify all peers (including the requester — confirms their new role).
        foreach (var p in peers)
        {
            var primaryId = session.PeerId;
            EnqueueControlFrameAsync(p, async s =>
                await Hmp1Protocol.WriteRoleChangeAsync(s, primaryId, cols, rows, "RequestPrimary", CancellationToken.None).ConfigureAwait(false));
        }

        PrimaryChanged?.Invoke(session.PeerId);
    }

    internal void RemoveSession(Hmp1ClientSession session)
    {
        bool wasPrimary;
        Hmp1ClientSession[] remainingPeers;
        int widthSnapshot;
        int heightSnapshot;
        lock (_sessionsLock)
        {
            if (!_sessions.Remove(session))
            {
                // Already removed (e.g. WriteClientPumpAsync and ReadClientPumpAsync
                // both finishing). Don't double-broadcast.
                _ = DisposeSessionAsync(session);
                return;
            }

            wasPrimary = session.PeerId == _primaryPeerId;
            if (wasPrimary)
            {
                _primaryPeerId = null;
            }
            remainingPeers = [.. _sessions];
            widthSnapshot = _width;
            heightSnapshot = _height;
        }

        // Notify remaining peers that this peer left.
        foreach (var p in remainingPeers)
        {
            var leavingId = session.PeerId;
            EnqueueControlFrameAsync(p, async s =>
                await Hmp1Protocol.WritePeerLeaveAsync(s, leavingId, CancellationToken.None).ConfigureAwait(false));
        }

        // If the leaving peer was primary, broadcast a RoleChange to null so
        // remaining peers stop deferring to a ghost primary.
        if (wasPrimary)
        {
            foreach (var p in remainingPeers)
            {
                EnqueueControlFrameAsync(p, async s =>
                    await Hmp1Protocol.WriteRoleChangeAsync(s, null, widthSnapshot, heightSnapshot, "PrimaryDisconnected", CancellationToken.None).ConfigureAwait(false));
            }
            PrimaryChanged?.Invoke(null);
        }

        _ = DisposeSessionAsync(session);
    }

    private static void EnqueueControlFrameAsync(Hmp1ClientSession session, Func<Stream, Task> writer)
    {
        // Enqueue the control writer onto the per-client write pump so it's
        // serialised with normal output. If the channel is closed (peer is
        // gone), the write is dropped silently.
        session.OutputChannel.Writer.TryWrite(new Hmp1OutboundWork(default, writer));
    }

    private static async Task DisposeSessionAsync(Hmp1ClientSession session)
    {
        session.OutputChannel.Writer.TryComplete();

        try
        {
            await session.Cts.CancelAsync().ConfigureAwait(false);
        }
        catch { }

        session.Cts.Dispose();

        try
        {
            await session.Stream.DisposeAsync().ConfigureAwait(false);
        }
        catch { }
    }

    private static async Task TrySendExitAsync(Hmp1ClientSession session, int exitCode)
    {
        try
        {
            // Enqueue a sentinel, then write exit directly
            session.OutputChannel.Writer.TryComplete();
            await Hmp1Protocol.WriteExitAsync(session.Stream, exitCode).ConfigureAwait(false);
        }
        catch { }
    }

    private static readonly TimeSpan ClientHelloTimeout = TimeSpan.FromSeconds(5);

    private static string GeneratePeerId()
    {
        // 4 bytes → 8 lowercase hex chars. Cheap, locally-unique enough; a
        // collision would only confuse the roster UX, not corrupt routing.
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        return "p" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Per-client outbound work item: either a control frame writer (for
    /// PeerJoin / PeerLeave / RoleChange) or a raw output payload.
    /// </summary>
    internal readonly record struct Hmp1OutboundWork(ReadOnlyMemory<byte> Output, Func<Stream, Task>? ControlWriter);

    /// <summary>
    /// Internal session tracking for a connected client.
    /// </summary>
    internal sealed class Hmp1ClientSession
    {
        public Hmp1ClientSession(
            Stream stream,
            CancellationTokenSource cts,
            string peerId,
            string? displayName,
            string? defaultRole)
        {
            Stream = stream;
            Cts = cts;
            PeerId = peerId;
            DisplayName = displayName;
            DefaultRole = defaultRole;
            OutputChannel = Channel.CreateBounded<Hmp1OutboundWork>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public Stream Stream { get; }
        public CancellationTokenSource Cts { get; }
        public string PeerId { get; }
        public string? DisplayName { get; }
        public string? DefaultRole { get; }
        public Task? ReadTask { get; set; }
        public Task? WriteTask { get; set; }
        public int RemoteWidth { get; set; }
        public int RemoteHeight { get; set; }

        /// <summary>
        /// Per-client outbound queue. When full, the client is disconnected rather than
        /// blocking other clients or dropping frames (which would desync incremental ANSI state).
        /// Carries either Output payloads or out-of-band control writers (RoleChange,
        /// PeerJoin, PeerLeave) so per-client ordering is preserved end-to-end.
        /// </summary>
        public Channel<Hmp1OutboundWork> OutputChannel { get; }

        // Compatibility shim for the existing WriteOutputAsync path which used
        // a ReadOnlyMemory<byte>-typed channel. The new payload type wraps both.
        // (kept here so future call sites that still want to enqueue raw output
        // can do so without re-implementing the wrap.)
    }
}
