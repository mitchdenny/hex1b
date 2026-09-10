using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Sixel;
using Hex1b.Tokens;
using Hex1b.Automation;

namespace Hex1b;

/// <summary>
/// Presents authoritative terminal state to Hex1b's first-party browser client using HWT1.
/// </summary>
/// <remarks>
/// Attach one adapter to one terminal and one ordered, message-preserving connection.
/// Send each result of <see cref="ReadFrameAsync"/> as a complete binary message, and
/// concurrently deliver client JSON messages to <see cref="HandleMessageAsync"/> in
/// transport order. The adapter owns projection, resources, acknowledgements, resync,
/// and mode-aware input; the host owns transport, authentication, and terminal lifetime.
/// No ASP.NET or WebSocket dependency is required. HWT1 is internal state transfer,
/// not a supported contract for third-party frontend implementations. Keep the server
/// and first-party client in sync and upgrade them together. The wire format may
/// change without backward-compatibility or deprecation guarantees; its name and
/// version field do not imply cross-release compatibility. This API is also experimental.
/// The current client-requested profile uses 10x20 logical-pixel cells,
/// 20..300 columns, and 10..100 rows. A directly attached
/// <see cref="Hmp1WorkloadAdapter"/> supplies authoritative geometry and primary
/// ownership instead; producer dimensions are not clamped to this request profile.
/// Frames support up to 1024 columns, 512 rows, and 262144 total cells.
/// Larger authoritative grids fail projection without resizing the producer.
/// Reconnection requires a new adapter; resync repairs the existing connection only.
/// Producer-backed views can be created with <see cref="Hmp1PresentationAdapter.CreateBrowserViewAsync"/>.
/// Each connection owns its viewport and selection. Historical rendering is text-only,
/// bounded to the producer's current grid; older, wider rows are cropped visually, but
/// logical-line and normal multirow extraction retain their original widths and soft wraps.
/// Resize, reflow, reset, and buffer switches invalidate selection coordinates explicitly.
/// Erased or discarded selected rows also invalidate the whole selection. Output does not
/// move a retained historical anchor. Keyboard input and paste clear selection and return live.
/// Rewriting selected live text invalidates the selection before extension or copying;
/// character writes and cell-level erases outside the selected span preserve its captured intent.
/// Structural row operations conservatively invalidate selections touching affected rows.
/// Selected text is bounded to 512 Ki UTF-16 code units (including padding before trimming).
/// Word and logical-line expansion enforce this bound during traversal. An oversized
/// selection is explicitly invalidated rather than copied partially.
/// Frames also carry the captured <see cref="Hex1bTerminal.WindowTitle"/>, including an empty
/// title. Title-only output can produce a frame with no changed cells. Titles follow
/// the same coalescing and synchronized-output rules as other state; frames are not
/// a lossless stream of individual title-setting sequences.
/// Progress and shell-integration state use the same snapshot and coalescing rules.
/// These fields are current even when the view is displaying historical text.
/// </remarks>
public sealed class Hwt1PresentationAdapter :
    ICellImpactAwarePresentationAdapter, ITerminalLifecycleAwarePresentationAdapter
{
    private readonly Channel<bool> _dirty = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly Hwt1RenderProjection _projection = new();
    private readonly object _ackLock = new();
    private readonly CancellationTokenSource _disposedCancellation = new();
    private readonly TimeProvider _timeProvider;
    private readonly long _started = Stopwatch.GetTimestamp();
    private Hex1bTerminal? _terminal;
    private IHmp1TerminalOutputSource? _hmp1OutputSource;
    private Hmp1WorkloadAdapter? Hmp1Workload => _hmp1OutputSource?.Hmp1Workload;
    private Hmp1PresentationAdapter? _muxer;
    private Hmp1PresentationAdapter.Hmp1ClientSession? _session;
    private readonly Hwt1ViewState _view = new();
    private TaskCompletionSource? _ack;
    private uint _awaitedRevision;
    private int _forceFull = 1;
    private int _reading;
    private int _disposed;
    private bool _isReadOnly;
    private long _outputBatches;
    private int _width;
    private int _height;
    private TimeSpan _acknowledgementTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Creates an HWT1 presentation adapter with the initial grid dimensions.</summary>
    /// <param name="width">Initial width, from 20 to 300 columns.</param>
    /// <param name="height">Initial height, from 10 to 100 rows.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside the draft profile's bounds.</exception>
    public Hwt1PresentationAdapter(int width = 80, int height = 24)
        : this(width, height, TimeProvider.System)
    {
    }

    internal Hwt1PresentationAdapter(int width, int height, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 300);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 10);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, 100);
        _width = width;
        _height = height;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets or sets whether this view ignores client commands that modify the producer.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// The host can set this before attachment or change it while the view is connected.
    /// When enabled, valid input, paste, key, mouse, resize, and requestPrimary messages
    /// are ignored. Message validation still applies. Acknowledgements, resync, history
    /// navigation, selection, copying, and output continue normally.
    /// This setting does not change peer identity, primary ownership, terminal geometry,
    /// or terminal lifetime. Other views and direct terminal input, including automation,
    /// are unaffected. The browser client's setReadOnly method is a user-experience
    /// control only; the host must use this property to enforce read-only access.
    /// Changes apply when a validated command is checked for dispatch. Enabling read-only
    /// access does not retract commands already accepted or cancel writes in progress.
    /// </remarks>
    public bool IsReadOnly
    {
        get => Volatile.Read(ref _isReadOnly);
        set => Volatile.Write(ref _isReadOnly, value);
    }

    /// <summary>
    /// Gets the maximum time the next frame read waits for the previous acknowledgement.
    /// Defaults to two minutes.
    /// </summary>
    /// <remarks>The host must also bound its transport writes. Infinite waits are not supported.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is nonpositive or exceeds the timer's supported range.</exception>
    public TimeSpan AcknowledgementTimeout
    {
        get => _acknowledgementTimeout;
        init
        {
            if (value <= TimeSpan.Zero || value.TotalMilliseconds > uint.MaxValue - 1)
                throw new ArgumentOutOfRangeException(nameof(value));
            _acknowledgementTimeout = value;
        }
    }

    /// <inheritdoc />
    public int Width => _terminal is { Width: > 0 } terminal ? terminal.Width : Volatile.Read(ref _width);

    /// <inheritdoc />
    public int Height => _terminal is { Height: > 0 } terminal ? terminal.Height : Volatile.Read(ref _height);

    /// <inheritdoc />
    public TerminalCapabilities Capabilities { get; } = new()
    {
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true,
        SupportsSixel = true,
        SixelSupport = SixelPresentationSupport.Headless,
        SupportsKgp = true,
        SupportsStyledUnderlines = true,
        SupportsUnderlineColor = true,
        CellPixelWidth = 10,
        CellPixelHeight = 20,
        DefaultForeground = 0xdedede,
        DefaultBackground = 0x181818,
        SixelCellMetrics = new(10, 20, SixelCellMetricsSource.Direct, SixelCellMetricsReliability.Authoritative)
    };

    /// <inheritdoc />
    public event Action<int, int>? Resized;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <summary>Reads the next complete HWT1 state frame.</summary>
    /// <param name="cancellationToken">Cancels this read without acknowledging any frame.</param>
    /// <returns>The complete binary frame, whose memory remains valid after subsequent reads.</returns>
    /// <remarks>
    /// Only one reader is permitted. A read waits for the previous frame's acknowledgement
    /// and a state invalidation before capturing a snapshot. Output processing continues
    /// while the reader waits. Send every returned frame or dispose this connection; do not
    /// discard frames locally. The host must cancel its read loop when the terminal ends.
    /// DEC mode 2026 defers capture until synchronized output ends or its one-second
    /// watchdog expires. The browser keeps its previous frame while capture is deferred.
    /// Projection and transport failures are fatal to the connection; they are not retried.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The adapter is unattached or already has a reader.</exception>
    /// <exception cref="InvalidDataException">Terminal graphics or text exceed the draft profile's limits.</exception>
    /// <exception cref="ObjectDisposedException">The adapter was already disposed when the read began.</exception>
    /// <exception cref="TimeoutException">The previous frame was not acknowledged in time.</exception>
    /// <exception cref="OperationCanceledException">The read was cancelled or the adapter was disposed.</exception>
    public async ValueTask<ReadOnlyMemory<byte>> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var terminal = _terminal ?? throw new InvalidOperationException("Attach the adapter to a terminal before reading frames.");
        if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0)
            throw new InvalidOperationException("Only one HWT1 frame reader is supported.");
        var consumedInvalidation = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposedCancellation.Token);
            await terminal.WaitForHmp1InitialReplayAsync(linked.Token).ConfigureAwait(false);
            Task? acknowledgement;
            lock (_ackLock)
                acknowledgement = _ack?.Task;
            if (acknowledgement is not null)
                await acknowledgement.WaitAsync(AcknowledgementTimeout, _timeProvider, linked.Token);

            _ = await _dirty.Reader.ReadAsync(linked.Token);
            consumedInvalidation = true;
            linked.Token.ThrowIfCancellationRequested();
            long snapshotStarted;
            Hex1bTerminalSnapshot? snapshot;
            Hwt1History? history;
            Hwt1Peer peer;
            var outputLock = _muxer is not null || _hmp1OutputSource is not null ? terminal.Hmp1OutputStateLock : null;
            while (true)
            {
                linked.Token.ThrowIfCancellationRequested();
                while (_dirty.Reader.TryRead(out _)) { }
                snapshotStarted = Stopwatch.GetTimestamp();
                if (outputLock is not null)
                    await outputLock.WaitAsync(linked.Token).ConfigureAwait(false);
                Task? pendingUpdate;
                try
                {
                    peer = _muxer is not null ? _muxer.GetBrowserPeer(_session!) : Hwt1Peer.Standalone;
                    if (terminal.TryCaptureBrowserSnapshot(_view, out snapshot, out history,
                        out var remoteState, out pendingUpdate))
                    {
                        if (_muxer is null && (Hmp1Workload is not null || remoteState is not null))
                            peer = remoteState is null ? Hwt1Peer.Unconnected :
                                new Hwt1Peer(remoteState.PeerId, remoteState.PrimaryPeerId, remoteState.IsPrimary);
                        break;
                    }
                }
                finally
                {
                    outputLock?.Release();
                }
                // Never hold the producer output lock while waiting for its end marker.
                await pendingUpdate.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            using var capturedSnapshot = snapshot;
            var snapshotMs = Stopwatch.GetElapsedTime(snapshotStarted).TotalMilliseconds;
            var bytes = _projection.Encode(snapshot, Capabilities, terminal.OutputBytesRead,
                Interlocked.Read(ref _outputBatches), Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
                Interlocked.Exchange(ref _forceFull, 0) != 0, snapshotMs, peer, history);
            lock (_ackLock)
            {
                _awaitedRevision = _projection.Revision;
                _ack = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return bytes;
        }
        catch (OperationCanceledException) when (consumedInvalidation)
        {
            InvalidatePresentation();
            throw;
        }
        finally
        {
            Volatile.Write(ref _reading, 0);
        }
    }

    /// <summary>Processes one complete UTF-8 HWT1 client JSON message.</summary>
    /// <param name="utf8Json">An acknowledgement, resync, resize, requestPrimary, input, paste, key, mouse,
    /// viewport, selection, or copy message, at most 64 KiB.</param>
    /// <param name="cancellationToken">Cancels processing and workload input writes.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    /// <remarks>
    /// Deliver messages serially, independently of the frame reader so acknowledgements
    /// can release backpressure. Unknown commands, including sample-only pause and rate
    /// controls, are rejected. Invalid JSON and field types raise parsing exceptions;
    /// invalid command values raise <see cref="InvalidDataException"/>.
    /// Stale acknowledgements and mouse events excluded by the live terminal mode are ignored.
    /// For an HMP1 workload, resize requests require the primary role and take effect
    /// only after producer confirmation. RequestPrimary asks HMP1 for that role;
    /// secondary peers can still send keyboard, mouse, and paste input.
    /// When <see cref="IsReadOnly"/> is enabled, producer-mutating commands are validated
    /// but ignored without changing this view's viewport or selection.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The adapter is unattached or a JSON field has the wrong type.</exception>
    /// <exception cref="InvalidDataException">The command, its values, or its size are unsupported.</exception>
    /// <exception cref="JsonException">The message is not valid JSON.</exception>
    /// <exception cref="ObjectDisposedException">The adapter was already disposed.</exception>
    public async Task HandleMessageAsync(ReadOnlyMemory<byte> utf8Json, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var terminal = _terminal ?? throw new InvalidOperationException("Attach the adapter to a terminal before handling messages.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposedCancellation.Token);
        linked.Token.ThrowIfCancellationRequested();
        if (utf8Json.Length > 64 * 1024)
            throw new InvalidDataException("HWT1 input exceeds 64 KiB.");
        using var document = JsonDocument.Parse(utf8Json);
        var command = document.RootElement;
        switch (command.GetProperty("type").GetString())
        {
            case "ack":
                var revision = command.GetProperty("revision").GetUInt32();
                lock (_ackLock)
                {
                    if (revision > _awaitedRevision)
                        throw new InvalidDataException("Acknowledgement is ahead of the server.");
                    if (revision == _awaitedRevision)
                        _ack?.TrySetResult();
                }
                break;
            case "resync":
                Interlocked.Exchange(ref _forceFull, 1);
                InvalidatePresentation();
                break;
            case "resize":
                var columns = ReadBounded(command, "columns", 20, 300);
                var rows = ReadBounded(command, "rows", 10, 100);
                if (IsReadOnly)
                    break;
                if (_muxer is not null)
                    await _muxer.ResizeBrowserAsync(_session!, columns, rows, primary: false, linked.Token);
                else if (Hmp1Workload is { } remote)
                    await remote.ResizeAsync(columns, rows, linked.Token);
                else
                    Resize(columns, rows);
                break;
            case "requestPrimary":
                var primaryColumns = ReadBounded(command, "columns", 20, 300);
                var primaryRows = ReadBounded(command, "rows", 10, 100);
                if (IsReadOnly)
                    break;
                if (_muxer is not null)
                    await _muxer.ResizeBrowserAsync(_session!, primaryColumns, primaryRows, primary: true, linked.Token);
                else if (Hmp1Workload is { IsConnected: true } candidate)
                    await candidate.RequestPrimaryAsync(primaryColumns, primaryRows, linked.Token);
                else if (Hmp1Workload is null)
                    Resize(primaryColumns, primaryRows);
                break;
            case "input":
            case "paste":
                var text = command.GetProperty("text").GetString() ?? throw new InvalidDataException("Missing text.");
                if (command.GetProperty("type").GetString() == "paste")
                    text = Hwt1Input.EncodePaste(text, terminal);
                if (IsReadOnly)
                    break;
                terminal.ResetBrowserView(_view);
                InvalidatePresentation();
                await SendInputAsync(Encoding.UTF8.GetBytes(text), linked.Token);
                break;
            case "key":
                var key = Hwt1Input.EncodeKey(command, terminal);
                if (IsReadOnly)
                    break;
                terminal.ResetBrowserView(_view);
                InvalidatePresentation();
                await SendInputAsync(Encoding.UTF8.GetBytes(key), linked.Token);
                break;
            case "mouse":
                var mouse = Hwt1Input.EncodeMouse(command, terminal);
                if (IsReadOnly)
                    break;
                if (mouse.Length > 0)
                    await SendInputAsync(mouse, linked.Token);
                break;
            case "viewport":
            case "selection":
            case "copy":
                terminal.HandleBrowserHistoryMessage(_view, command);
                InvalidatePresentation();
                break;
            default:
                throw new InvalidDataException("Unknown HWT1 command.");
        }
    }

    private async Task SendInputAsync(byte[] input, CancellationToken cancellationToken)
    {
        if (_muxer is not null)
            _muxer.SendBrowserInput(_session!, input);
        else
            await _terminal!.SendInputAsync(input, cancellationToken);
    }

    internal void AttachProducer(Hex1bTerminal terminal, Hmp1PresentationAdapter muxer,
        Hmp1PresentationAdapter.Hmp1ClientSession session)
    {
        _muxer = muxer;
        _session = session;
        TerminalCreated(terminal);
    }

    /// <inheritdoc />
    public void TerminalCreated(Hex1bTerminal terminal)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(terminal);
        if (Interlocked.CompareExchange(ref _terminal, terminal, null) is not null)
            throw new InvalidOperationException("An HWT1 adapter can only be attached to one terminal.");
        _hmp1OutputSource = terminal.Workload as IHmp1TerminalOutputSource;
        terminal.PresentationInvalidated += InvalidatePresentation;
        InvalidatePresentation();
    }

    /// <inheritdoc />
    public void TerminalStarted() => InvalidatePresentation();

    /// <inheritdoc />
    public void TerminalCompleted(int exitCode) => InvalidatePresentation();

    private void Resize(int columns, int rows)
    {
        Volatile.Write(ref _width, columns);
        Volatile.Write(ref _height, rows);
        Resized?.Invoke(columns, rows);
        InvalidatePresentation();
    }

    /// <inheritdoc />
    public void InvalidatePresentation() => _dirty.Writer.TryWrite(true);

    internal void RecordOutputBatch() => Interlocked.Increment(ref _outputBatches);

    /// <inheritdoc />
    public ValueTask WriteOutputWithImpactsAsync(IReadOnlyList<AppliedToken> appliedTokens, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _outputBatches);
        InvalidatePresentation();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        InvalidatePresentation();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        // HandleMessageAsync encodes input against server modes and injects it directly.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposedCancellation.Token);
        await Task.Delay(Timeout.Infinite, linked.Token);
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

    /// <summary>Disconnects this adapter and cancels pending frame, input, and acknowledgement waits.</summary>
    /// <returns>An operation that completes after the view's HMP1 peer, if any, is disconnected.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_terminal is { } terminal)
            terminal.PresentationInvalidated -= InvalidatePresentation;
        _disposedCancellation.Cancel();
        _dirty.Writer.TryComplete();
        Disconnected?.Invoke();
        if (_muxer is not null)
            await _muxer.RemoveSessionAsync(_session!).ConfigureAwait(false);
        _disposedCancellation.Dispose();
    }

    private static int ReadBounded(JsonElement command, string name, int min, int max)
    {
        var value = command.GetProperty(name).GetInt32();
        return value >= min && value <= max ? value : throw new InvalidDataException($"{name} must be {min}..{max}.");
    }
}
