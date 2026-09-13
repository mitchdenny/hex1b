using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Hex1b.Tokens;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Diagnostics;
using Hex1b.Sixel;
using Hex1b.Reflow;

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
/// <see cref="Hmp1FrameType.StateSync"/> frame with a full screen snapshot and
/// its mandatory <see cref="Hmp1FrameType.ActivityState"/> checkpoint.
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
public sealed class Hmp1PresentationAdapter : ITerminalLifecycleAwarePresentationAdapter,
    ITerminalReflowProvider, IInternalTerminalReflowProvider
{
    private readonly List<Hmp1ClientSession> _sessions = [];
    private readonly object _sessionsLock = new();
    private readonly Channel<ReadOnlyMemory<byte>> _inputChannel;
    private Hex1bTerminal? _terminal;
    private int _width;
    private int _height;
    private string? _primaryPeerId;
    private bool _disposed;
    private ITerminalReflowProvider _reflowStrategy = NoReflowStrategy.Instance;
    private bool _reflowEnabled;
    private Hmp1SixelStateReplay.ReplayResult? _lastSixelReplayResult;

    internal Hex1bMetrics Metrics { get; set; } = Hex1bMetrics.Default;
    internal Hmp1SixelStateReplay.ReplayResult? LastSixelReplayResult
    {
        get
        {
            lock (_sessionsLock)
            {
                return _lastSixelReplayResult;
            }
        }
    }

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
    /// Enables producer-side reflow using the specified strategy.
    /// By default, resize crops the screen without reflow.
    /// </summary>
    /// <param name="strategy">The reflow strategy; use <see cref="GhosttyReflowStrategy.Instance"/> for shell terminals.</param>
    /// <returns>This adapter for fluent configuration before terminal construction.</returns>
    /// <remarks>
    /// Configure the producer, not individual browser views. Primary-peer resize authority
    /// is unchanged. Ghostty reflows main-screen text and retained scrollback, but not
    /// alternate-screen layouts. Scrollback capacity still bounds retained history.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The strategy is null.</exception>
    public Hmp1PresentationAdapter WithReflow(ITerminalReflowProvider strategy)
    {
        _reflowStrategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _reflowEnabled = true;
        return this;
    }

    /// <inheritdoc/>
    public bool ReflowEnabled => _reflowEnabled;

    /// <inheritdoc/>
    public bool ShouldClearSoftWrapOnAbsolutePosition => _reflowStrategy.ShouldClearSoftWrapOnAbsolutePosition;

    /// <inheritdoc/>
    public ReflowResult Reflow(ReflowContext context) => _reflowStrategy.Reflow(context);

    bool IInternalTerminalReflowProvider.TryReflowWithAnchors(
        ReflowContext context,
        IReadOnlyList<TerminalReflowAnchor> anchors,
        out InternalReflowResult result)
        => InternalTerminalReflow.TryReflow(_reflowStrategy, context, anchors, out result);

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
        SupportsBracketedPaste = true,
        SupportsKgp = true,
        SupportsSixel = true,
        SixelSupport = SixelPresentationSupport.Headless,
        // HMP's producer model uses a canonical virtual grid, not a peer's font.
        CellPixelWidth = 10,
        CellPixelHeight = 20,
        SixelCellMetrics = new(10, 20, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative)
    };

    /// <inheritdoc />
    /// <remarks>
    /// Required by <see cref="IHex1bTerminalPresentationAdapter"/>. Not
    /// surfaced to HMP1 server-side observers; for runtime size changes
    /// driven by primary-peer resize, use <see cref="OnResized"/>.
    /// </remarks>
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    /// <remarks>
    /// Required by <see cref="IHex1bTerminalPresentationAdapter"/>.
    /// Fired by <see cref="DisposeAsync"/> when the presentation layer
    /// is torn down. Not surfaced to HMP1 server-side observers — the
    /// per-client lifecycle is observed via
    /// <see cref="OnClientDisconnected"/> instead.
    /// </remarks>
    public event Action? Disconnected;

    /// <summary>
    /// Invoked when the producer's PTY dimensions change at runtime
    /// (typically as a result of a primary-peer resize). Awaited inline
    /// by the per-client read pump that processed the triggering frame.
    /// Multicast (<c>+=</c>) is supported; each handler is awaited
    /// independently with its own exception isolation.
    /// </summary>
    public Func<Hmp1ServerResizedEventArgs, CancellationToken, Task>? OnResized { get; set; }

    /// <summary>
    /// Invoked when the primary peer changes (including transitions to no primary).
    /// </summary>
    /// <remarks>
    /// Awaited inline by the per-client read pump (or by RemoveSession
    /// when the previous primary disconnects). Multicast supported.
    /// </remarks>
    public Func<Hmp1ServerPrimaryChangedEventArgs, CancellationToken, Task>? OnPrimaryChanged { get; set; }

    /// <summary>
    /// Invoked after a new client completes its ClientHello → Hello →
    /// StateSync → ActivityState handshake. Argument carries the assigned peer ID, the
    /// display name and (parsed) role hint the client supplied.
    /// </summary>
    /// <remarks>
    /// Awaited inline by the per-client write pump after the handshake
    /// frames have been written but before output streaming begins.
    /// </remarks>
    public Func<Hmp1ClientConnectedEventArgs, CancellationToken, Task>? OnClientConnected { get; set; }

    /// <summary>
    /// Invoked when a per-client session ends (clean disconnect or
    /// transport failure). Per-session disposal runs in parallel so a
    /// slow handler does not delay transport cleanup. Receives
    /// <see cref="CancellationToken.None"/>.
    /// </summary>
    public Func<Hmp1ClientDisconnectedEventArgs, CancellationToken, Task>? OnClientDisconnected { get; set; }

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
        Metrics = terminal.DiagnosticsMetrics;
        // The same gate spans a producer output read's forwarding AND application.
        // Composite presentations participate through this lifecycle callback too.
        _ = terminal.Hmp1OutputStateLock;
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
    /// <see cref="Hmp1FrameType.StateSync"/> frame and its activity checkpoint.
    /// </summary>
    /// <param name="stream">A bidirectional stream connected to the client.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A handle that can be disposed to disconnect the client.</returns>
    /// <exception cref="InvalidDataException">The snapshot and saved title state exceed
    /// the 16 MiB StateSync payload limit. Saved titles are not silently discarded.</exception>
    public async Task<Hmp1ClientHandle> AddClient(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ObjectDisposedException.ThrowIf(_disposed, this);

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

        try
        {
            return await CompleteClientHandshakeAsync(session, ct).ConfigureAwait(false);
        }
        catch
        {
            // A peer is registered before Hello/StateSync to avoid losing output.
            // Roll back that registration when no usable handle can be returned.
            await RemoveSessionAsync(session).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Creates an experimental HWT1 view of the producer and retained text history.</summary>
    /// <param name="displayName">An optional label for the peer in the HMP1 roster.</param>
    /// <param name="cancellationToken">Cancels creation; dispose the result to end the peer lifetime.</param>
    /// <returns>An attached browser adapter owning one secondary HMP1 peer.</returns>
    /// <remarks>
    /// Shares producer state without an emulator or ANSI replay. HMP1 remains the authority
    /// for identity, input, and primary-only resize. Local inspection never requests primary.
    /// Disposing the primary leaves no primary. Historical views contain text only; live
    /// graphics are unchanged. The experimental API and HWT1 contract must be upgraded
    /// with the first-party client. See <see cref="Hwt1PresentationAdapter"/> for transport usage.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The muxer is not attached to a producer.</exception>
    /// <exception cref="ObjectDisposedException">The muxer is disposed.</exception>
    public async Task<Hwt1PresentationAdapter> CreateBrowserViewAsync(
        string? displayName = null, CancellationToken cancellationToken = default)
    {
        var terminal = _terminal ?? throw new InvalidOperationException("Attach the muxer to a producer first.");
        var session = new Hmp1ClientSession(Stream.Null, new CancellationTokenSource(),
            GeneratePeerId(), displayName, "secondary");
        var view = new Hwt1PresentationAdapter();
        session.BrowserView = view;
        try
        {
            await terminal.WaitForHmp1InitialReplayAsync(cancellationToken).ConfigureAwait(false);
            await terminal.Hmp1OutputStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_sessionsLock)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    session.RemoteWidth = _width;
                    session.RemoteHeight = _height;
                    view.AttachProducer(terminal, this, session);
                    foreach (var peer in _sessions)
                        EnqueueControlFrameAsync(peer, stream =>
                            Hmp1Protocol.WritePeerJoinAsync(stream, session.PeerId, displayName,
                                CancellationToken.None).AsTask());
                    _sessions.Add(session);
                }
            }
            finally
            {
                terminal.Hmp1OutputStateLock.Release();
            }
            await Hmp1AsyncCallback.InvokeAsync(OnClientConnected,
                new Hmp1ClientConnectedEventArgs(session.PeerId, displayName, Hmp1Role.Secondary),
                cancellationToken).ConfigureAwait(false);
            return view;
        }
        catch
        {
            await RemoveSessionAsync(session).ConfigureAwait(false);
            throw;
        }
    }

    internal Hwt1Peer GetBrowserPeer(Hmp1ClientSession session)
    {
        lock (_sessionsLock)
            return _sessions.Contains(session)
                ? new(session.PeerId, _primaryPeerId, session.PeerId == _primaryPeerId)
                : Hwt1Peer.Unconnected;
    }

    internal void SendBrowserInput(Hmp1ClientSession session, ReadOnlyMemory<byte> input)
    {
        lock (_sessionsLock)
        {
            ObjectDisposedException.ThrowIf(!_sessions.Contains(session), session.BrowserView!);
            _inputChannel.Writer.TryWrite(input.ToArray());
        }
    }

    internal async ValueTask ResizeBrowserAsync(Hmp1ClientSession session, int width, int height, bool primary,
        CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Cts.Token);
        var operation = primary
            ? HandleRequestPrimaryAsync(session, width, height, linked.Token)
            : HandleResizeAsync(session, width, height, linked.Token);
        await operation.AsTask().WaitAsync(linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
    }

    private async Task<Hmp1ClientHandle> CompleteClientHandshakeAsync(Hmp1ClientSession session, CancellationToken ct)
    {
        var stream = session.Stream;
        var sessionCts = session.Cts;
        var peerId = session.PeerId;
        var displayName = session.DisplayName;
        Hmp1ClientSession[] existingPeers;
        byte[] syncBytes;
        Hmp1ActivityState activityState;
        IReadOnlyList<KgpPlacement> kgpPlacements;
        IReadOnlyDictionary<uint, KgpImageData> kgpImages;
        DateTimeOffset? kgpAnimationTimestamp;
        HyperlinkData? activeHyperlink;
        IReadOnlyList<SixelPlacement> sixelPlacements;
        IReadOnlyList<(int Row, int Column, TerminalCell Cell)> sixelDamagedCells;
        int cursorX;
        int cursorY;
        string? primarySnapshot;
        int widthSnapshot;
        int heightSnapshot;
        if (_terminal is not null)
            await _terminal.WaitForHmp1InitialReplayAsync(ct).ConfigureAwait(false);
        var outputStateLock = _terminal?.Hmp1OutputStateLock;
        if (outputStateLock is not null)
            await outputStateLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
        lock (_sessionsLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            existingPeers = [.. _sessions];

            if (_terminal != null)
            {
                // A peer must also receive unplaced images: future output can
                // place or animate retained pixels without transmitting them again.
                using var snap = _terminal.CreateSnapshot(includeAllKgpImages: true, includeSavedTitles: true);
                var prefix = BuildStateReplayPrefix(snap);
                var ansi = snap.ToAnsi(new TerminalAnsiOptions
                {
                    IncludeClearScreen = true,
                    // Critical: the default IncludeTrailingNewline=true is for
                    // FILE output (the caller wants a clean POSIX line termination
                    // at EOF). For a network state-replay it is actively
                    // harmful — IncludeCursorPosition has just emitted CSI to
                    // park the cursor at the workload's actual (col, row) and
                    // a trailing \n would then push it down one row. PSReadLine
                    // and any other readline-style consumer would type the next
                    // byte into the wrong row, then refresh and clobber its own
                    // ghost text on next redraw. (Repro: PowerShell autocomplete
                    // ghosting on every fresh viewer connect or RoleChange-driven
                    // re-StateSync.)
                    IncludeTrailingNewline = false,
                }, includeHyperlinks: true, preserveSoftWrap: true);
                var suffix = BuildStateReplaySuffix(snap);
                activityState = Hmp1ActivityState.Capture(snap);
                var progress = activityState.BuildProgressReplay();
                var titles = Hmp1TitleStateReplay.Build(snap,
                    Hmp1Protocol.MaxPayloadSize - Encoding.UTF8.GetByteCount(prefix) -
                    Encoding.UTF8.GetByteCount(ansi) - Encoding.UTF8.GetByteCount(suffix) -
                    Encoding.UTF8.GetByteCount(progress));
                syncBytes = Encoding.UTF8.GetBytes(prefix + titles + progress + ansi + suffix);
                kgpPlacements = snap.KgpPlacements;
                kgpImages = snap.KgpImages;
                kgpAnimationTimestamp = snap.KgpAnimationTimestamp;
                activeHyperlink = snap.ActiveHyperlink;
                sixelPlacements = snap.SixelPlacements;
                sixelDamagedCells = CaptureSixelDamagedCells(snap, sixelPlacements);
                cursorX = snap.CursorX;
                cursorY = snap.CursorY;
            }
            else
            {
                activityState = Hmp1ActivityState.Default;
                syncBytes = [];
                kgpPlacements = [];
                kgpImages = new Dictionary<uint, KgpImageData>();
                kgpAnimationTimestamp = null;
                activeHyperlink = null;
                sixelPlacements = [];
                sixelDamagedCells = [];
                cursorX = 0;
                cursorY = 0;
            }

            primarySnapshot = _primaryPeerId;
            widthSnapshot = _width;
            heightSnapshot = _height;

            if (kgpImages.Count > 0)
            {
                EnqueueControlFrameAsync(session, stream =>
                    Hmp1KgpStateReplay.WriteAsync(
                        stream,
                        kgpPlacements,
                        kgpImages,
                        cursorX,
                        cursorY,
                        session.Cts.Token,
                        kgpAnimationTimestamp));
            }

            // Sixel placements own the character cells they occupy (unlike KGP), but
            // StateSync's CSI-2J clear unconditionally erases every Sixel placement
            // regardless of ordering. So — exactly like KGP — this must be queued
            // after StateSync on the per-client write pump, not written directly
            // before it. See Hmp1SixelStateReplay for the full rationale, including
            // why a trailing damage patch is required.
            if (sixelPlacements.Count > 0)
            {
                EnqueueControlFrameAsync(session, async stream =>
                {
                    var started = Stopwatch.GetTimestamp();
                    var result = await Hmp1SixelStateReplay.WriteAsync(
                        stream,
                        sixelPlacements,
                        sixelDamagedCells,
                        session.Cts.Token,
                        activeHyperlink).ConfigureAwait(false);
                    lock (_sessionsLock)
                    {
                        _lastSixelReplayResult = result;
                    }
                    Metrics.Hmp1SixelReplayDuration.Record(
                        Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    Metrics.Hmp1SixelReplayOutcomes.Add(
                        1,
                        new KeyValuePair<string, object?>(
                            "outcome",
                            result.Outcome.ToString().ToLowerInvariant()),
                        new KeyValuePair<string, object?>("limit", result.Limit),
                        new KeyValuePair<string, object?>(
                            "skipped",
                            result.SkippedPlacements > 0 ? "true" : "false"));
                });
            }

            // Snapshot state covers completed tokens, not an unfinished escape
            // sequence. Seed the new parser after all replay commands and before
            // live output can deliver the remainder of that sequence.
            var pendingAnsi = _terminal?.CapturePendingAnsiOutput() ?? ReadOnlyMemory<byte>.Empty;
            if (!pendingAnsi.IsEmpty)
            {
                EnqueueControlFrameAsync(session, async stream =>
                {
                    for (var offset = 0; offset < pendingAnsi.Length;)
                    {
                        var length = Math.Min(pendingAnsi.Length - offset, Hmp1Protocol.MaxPayloadSize);
                        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.Output,
                            pendingAnsi.Slice(offset, length),
                            session.Cts.Token).ConfigureAwait(false);
                        offset += length;
                    }
                });
            }

            _sessions.Add(session);

            // Enqueue PeerJoin notifications to existing peers atomically with the
            // session being added. Two concurrent AddClient calls must produce the
            // same per-existing-peer ordering as the producer's _sessions list, so
            // a roster replay matches authoritative state.
            var newPeerId = peerId;
            var newDisplayName = displayName;
            foreach (var p in existingPeers)
            {
                EnqueueControlFrameAsync(p, async s =>
                    await Hmp1Protocol.WritePeerJoinAsync(s, newPeerId, newDisplayName, CancellationToken.None).ConfigureAwait(false));
            }
        }
        }
        finally
        {
            outputStateLock?.Release();
        }

        // Build roster (existing peers, excluding the new one).
        var roster = new List<HelloPeerInfo>(existingPeers.Length);
        foreach (var p in existingPeers)
        {
            roster.Add(new HelloPeerInfo { PeerId = p.PeerId, DisplayName = p.DisplayName });
        }

        // Send Hello + StateSync + ActivityState over the raw stream (the per-client
        // pump hasn't started yet). Failures here are propagated because the caller
        // hasn't yet received a handle.
        await Hmp1Protocol.WriteHelloAsync(
            stream, widthSnapshot, heightSnapshot, peerId, primarySnapshot, roster, ct).ConfigureAwait(false);

        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync, syncBytes, ct).ConfigureAwait(false);
        await Hmp1Protocol.WriteActivityStateAsync(stream, activityState, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();
        // Always enter the pumps so their finally blocks clean up even if the
        // session is cancelled between scheduling and delegate execution.
        session.WriteTask = Task.Run(() => WriteClientPumpAsync(session));
        session.ReadTask = Task.Run(() => ReadClientPumpAsync(session));

        // Notify server-side observers AFTER the pumps are spinning so an
        // OnClientConnected handler that turns around and inspects the
        // session sees a fully-live state. Failures here must not propagate
        // to the caller (which already has a working handle) — the
        // Hmp1AsyncCallback helper isolates per-handler exceptions.
        await Hmp1AsyncCallback.InvokeAsync(
            OnClientConnected,
            new Hmp1ClientConnectedEventArgs(
                session.PeerId,
                session.DisplayName,
                Hmp1RoleExtensions.TryParseWireString(session.DefaultRole)),
            ct).ConfigureAwait(false);

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

        if (snapshot.ActiveHyperlink is { } hyperlink)
        {
            sb.Append(AnsiTokenSerializer.Serialize(new OscToken("8",
                hyperlink.Parameters, hyperlink.Uri, UseEscBackslash: true)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Captures the exact content of every cell any of <paramref name="placements"/>
    /// reports as damaged, while <paramref name="snapshot"/> is still valid.
    /// </summary>
    /// <remarks>
    /// Sixel replay must run after StateSync's unconditional erase-display, which
    /// re-blanks these cells when the placement is recreated. The captured cells
    /// let <see cref="Hmp1SixelStateReplay"/> patch them back afterward. Capturing
    /// (rather than deferring the read) is required because the snapshot is
    /// disposed at the end of the caller's <c>using</c> block, well before the
    /// queued replay writer runs.
    /// </remarks>
    private static List<(int Row, int Column, TerminalCell Cell)> CaptureSixelDamagedCells(
        Hex1bTerminalSnapshot snapshot,
        IReadOnlyList<SixelPlacement> placements)
    {
        if (placements.Count == 0)
            return [];

        var damaged = new List<(int Row, int Column, TerminalCell Cell)>();
        foreach (var placement in placements)
        {
            for (var row = placement.PaintedTop; row <= placement.PaintedBottom; row++)
            {
                for (var column = placement.PaintedLeft; column <= placement.PaintedRight; column++)
                {
                    if (placement.IsCellDamaged(row, column))
                    {
                        damaged.Add((row, column, snapshot.GetCell(column, row)));
                    }
                }
            }
        }

        return damaged;
    }

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var replayActivity = _terminal?.Hmp1ReplayActivityState;
        if (_disposed || (data.IsEmpty && replayActivity is null)) return ValueTask.CompletedTask;

        // Copy once; each session's pump consumes the same buffer view.
        var copy = data.ToArray();

        // Enqueue to each client's write channel (non-blocking).
        // Clients that can't keep up will be disconnected.
        lock (_sessionsLock)
        {
            for (var i = _sessions.Count - 1; i >= 0; i--)
            {
                var session = _sessions[i];
                if (session.BrowserView is { } browserView)
                {
                    browserView.RecordOutputBatch();
                    continue;
                }
                var work = replayActivity is null
                    ? new Hmp1OutboundWork(copy, null)
                    : new Hmp1OutboundWork(default, async stream =>
                    {
                        await Hmp1Protocol.WriteFrameAsync(stream, Hmp1FrameType.StateSync, copy, session.Cts.Token)
                            .ConfigureAwait(false);
                        await Hmp1Protocol.WriteActivityStateAsync(stream, replayActivity, session.Cts.Token)
                            .ConfigureAwait(false);
                    });
                if (!session.OutputChannel.Writer.TryWrite(work))
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
            await RemoveSessionAsync(session).ConfigureAwait(false);
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
                        await HandleResizeAsync(session, frame.Payload).ConfigureAwait(false);
                        break;

                    case Hmp1FrameType.RequestPrimary:
                        await HandleRequestPrimaryAsync(session, frame.Payload).ConfigureAwait(false);
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
            await RemoveSessionAsync(session).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleResizeAsync(Hmp1ClientSession session, ReadOnlyMemory<byte> payload)
    {
        var (width, height) = Hmp1Protocol.ParseResize(payload);
        await HandleResizeAsync(session, width, height, session.Cts.Token).ConfigureAwait(false);
    }

    private async ValueTask HandleResizeAsync(Hmp1ClientSession session, int width, int height,
        CancellationToken cancellationToken)
    {
        bool acceptedAsPrimary;
        Hmp1ClientSession[] otherPeers;
        var outputStateLock = _terminal?.Hmp1OutputStateLock;
        if (outputStateLock is not null)
            await outputStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        lock (_sessionsLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_sessions.Contains(session))
                return;
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
            acceptedAsPrimary = true;

            // Broadcast Resize to ALL peers (including the sender) so every
            // adapter's CurrentWidth/Height tracks the producer's authoritative
            // state, not its local optimistic write. Enqueue inside the lock
            // so concurrent HandleResize / HandleRequestPrimary calls observe
            // the same broadcast order as state-mutation order on the per-client
            // channel. Echoing back to the sender costs one extra small frame
            // per resize but eliminates the local/server divergence class of
            // bugs.
            otherPeers = [.. _sessions];
            foreach (var p in otherPeers)
            {
                var w = width;
                var h = height;
                EnqueueControlFrameAsync(p, async s =>
                    await Hmp1Protocol.WriteResizeAsync(s, w, h, CancellationToken.None).ConfigureAwait(false));
            }
            // Apply the producer resize in the same ordered transaction as the
            // authority update and broadcasts, before another transition or output.
            Resized?.Invoke(width, height);
        }
        }
        finally
        {
            outputStateLock?.Release();
        }

        if (acceptedAsPrimary)
        {
            // The HMP-server-specific async callback for observers.
            await Hmp1AsyncCallback.InvokeAsync(
                OnResized,
                new Hmp1ServerResizedEventArgs(width, height),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleRequestPrimaryAsync(Hmp1ClientSession session, ReadOnlyMemory<byte> payload)
    {
        var req = Hmp1Protocol.ParseRequestPrimary(payload);
        await HandleRequestPrimaryAsync(session, req.Cols, req.Rows, session.Cts.Token).ConfigureAwait(false);
    }

    private async ValueTask HandleRequestPrimaryAsync(Hmp1ClientSession session, int requestedColumns, int requestedRows,
        CancellationToken cancellationToken)
    {
        Hmp1ClientSession[] peers;
        bool sizeChanged;
        int cols;
        int rows;
        var outputStateLock = _terminal?.Hmp1OutputStateLock;
        if (outputStateLock is not null)
            await outputStateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        lock (_sessionsLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_sessions.Contains(session))
                return;
            cols = requestedColumns > 0 ? requestedColumns : session.RemoteWidth;
            rows = requestedRows > 0 ? requestedRows : session.RemoteHeight;
            if (cols <= 0) cols = _width;
            if (rows <= 0) rows = _height;

            // Always grant in this iteration (per the design). Even if the
            // requesting peer is already primary, broadcasting the role change
            // is safe — peers will see a no-op transition with the latest
            // dimensions.
            _primaryPeerId = session.PeerId;
            sizeChanged = (_width != cols) || (_height != rows);
            _width = cols;
            _height = rows;

            // Snapshot peers AND enqueue RoleChange to each peer's per-client
            // channel WITHIN the lock. This is the critical correctness point
            // for concurrent take-over: state mutation and broadcast enqueueing
            // must happen atomically so two concurrent RequestPrimary calls
            // produce a consistent global order — peers see RoleChange(A)
            // followed by RoleChange(B) iff the producer also processed
            // A then B. If the foreach were outside the lock, a second
            // call could enqueue before the first, leaving clients converged
            // to a different primary than the producer's authoritative state.
            peers = [.. _sessions];
            var primaryId = session.PeerId;
            var w = cols;
            var h = rows;
            foreach (var p in peers)
            {
                EnqueueControlFrameAsync(p, async s =>
                    await Hmp1Protocol.WriteRoleChangeAsync(s, primaryId, w, h, "RequestPrimary", CancellationToken.None).ConfigureAwait(false));
            }
            if (sizeChanged)
                Resized?.Invoke(cols, rows);
        }
        }
        finally
        {
            outputStateLock?.Release();
        }

        // Async observer callbacks are outside the ordered state transaction.
        if (sizeChanged)
        {
            await Hmp1AsyncCallback.InvokeAsync(
                OnResized,
                new Hmp1ServerResizedEventArgs(cols, rows),
                cancellationToken).ConfigureAwait(false);
        }

        await Hmp1AsyncCallback.InvokeAsync(
            OnPrimaryChanged,
            new Hmp1ServerPrimaryChangedEventArgs(session.PeerId),
            cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask RemoveSessionAsync(Hmp1ClientSession session)
    {
        bool wasPrimary;
        bool actuallyRemoved;
        Hmp1ClientSession[] remainingPeers;
        int widthSnapshot;
        int heightSnapshot;
        lock (_sessionsLock)
        {
            if (!_sessions.Remove(session))
            {
                // Already removed (e.g. WriteClientPumpAsync and ReadClientPumpAsync
                // both finishing). Don't double-broadcast.
                actuallyRemoved = false;
                wasPrimary = false;
                remainingPeers = [];
                widthSnapshot = _width;
                heightSnapshot = _height;
            }
            else
            {
                actuallyRemoved = true;
                wasPrimary = session.PeerId == _primaryPeerId;
                if (wasPrimary)
                {
                    _primaryPeerId = null;
                }
                remainingPeers = [.. _sessions];
                widthSnapshot = _width;
                heightSnapshot = _height;

                // Enqueue PeerLeave (and, if the leaver was primary, RoleChange-to-null)
                // INSIDE the lock so two concurrent removals or a removal racing with
                // HandleRequestPrimary produce a consistent global ordering. See
                // comments in HandleRequestPrimary for the same invariant.
                var leavingId = session.PeerId;
                foreach (var p in remainingPeers)
                {
                    EnqueueControlFrameAsync(p, async s =>
                        await Hmp1Protocol.WritePeerLeaveAsync(s, leavingId, CancellationToken.None).ConfigureAwait(false));
                }

                if (wasPrimary)
                {
                    var w = widthSnapshot;
                    var h = heightSnapshot;
                    foreach (var p in remainingPeers)
                    {
                        EnqueueControlFrameAsync(p, async s =>
                            await Hmp1Protocol.WriteRoleChangeAsync(s, null, w, h, "PrimaryDisconnected", CancellationToken.None).ConfigureAwait(false));
                    }
                }
            }
        }

        // Kick off per-session cleanup in parallel with awaiting observer
        // callbacks: a slow observer must not delay stream/channel teardown.
        var disposeTask = DisposeSessionAsync(session);

        if (actuallyRemoved && wasPrimary)
        {
            await Hmp1AsyncCallback.InvokeAsync(
                OnPrimaryChanged,
                new Hmp1ServerPrimaryChangedEventArgs(null),
                CancellationToken.None).ConfigureAwait(false);
        }

        if (actuallyRemoved)
        {
            await Hmp1AsyncCallback.InvokeAsync(
                OnClientDisconnected,
                new Hmp1ClientDisconnectedEventArgs(session.PeerId, session.DisplayName),
                CancellationToken.None).ConfigureAwait(false);
        }

        await disposeTask.ConfigureAwait(false);
    }

    private static void EnqueueControlFrameAsync(Hmp1ClientSession session, Func<Stream, Task> writer)
    {
        if (session.BrowserView is { } view)
        {
            view.InvalidatePresentation();
            return;
        }
        // Enqueue the control writer onto the per-client write pump so it's
        // serialised with normal output. If the channel is closed (peer is
        // gone), the write is dropped silently.
        session.OutputChannel.Writer.TryWrite(new Hmp1OutboundWork(default, writer));
    }

    private static async Task DisposeSessionAsync(Hmp1ClientSession session)
    {
        if (Interlocked.Exchange(ref session.Disposed, 1) != 0)
            return;
        if (session.BrowserView is { } view)
            await view.DisposeAsync().ConfigureAwait(false);
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
        if (session.BrowserView is { } view)
        {
            view.TerminalCompleted(exitCode);
            return;
        }
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
        public Hwt1PresentationAdapter? BrowserView { get; set; }
        public int Disposed;
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
