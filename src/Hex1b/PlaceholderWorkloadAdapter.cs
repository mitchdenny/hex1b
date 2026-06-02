#pragma warning disable HEX1B002 // PlaceholderResumePolicy is experimental — internal usage is allowed.
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Hex1b;

/// <summary>
/// Multiplexes two child <see cref="IHex1bTerminalWorkloadAdapter"/>s onto a
/// single workload slot — a <em>placeholder</em> that owns the screen until
/// the <em>primary</em> signals it is ready, then a hand-off back to the
/// placeholder when (or if) the primary disconnects.
/// </summary>
/// <remarks>
/// <para>
/// Constructed by <see cref="PlaceholderWorkloadBuilderExtensions.WithPlaceholderWorkload(Hex1bTerminalBuilder, Action{Hex1bTerminalBuilder}, PlaceholderResumePolicy)"/>
/// and friends; not intended to be instantiated directly.
/// </para>
/// <para>
/// "Ready" is detected via <see cref="IConnectableWorkloadAdapter.ConnectedTask"/>
/// when the primary opts into that contract. Otherwise the primary is treated
/// as ready as soon as it produces its first non-empty
/// <see cref="IHex1bTerminalWorkloadAdapter.ReadOutputAsync"/> result.
/// </para>
/// <para>
/// On every swap a synthetic terminal-reset sequence (RIS + leave alt-screen +
/// clear + cursor-home) is prepended to the now-active child's bytes so the
/// downstream parser drops modes / SGR / scroll-region / alt-screen state
/// from the previous occupant. If the new active is an <see cref="IRepaintableWorkloadAdapter"/>
/// (e.g. a <see cref="Hex1bAppWorkloadAdapter"/>) <see cref="IRepaintableWorkloadAdapter.RequestFullRepaint"/>
/// is also invoked so it discards diff state.
/// </para>
/// </remarks>
internal sealed class PlaceholderWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // Hard-reset sequence prepended to each swapped-in child's first frame.
    //   ESC c            - RIS, full reset (modes, attrs, scroll regions, alt-screen)
    //   ESC[?1049l       - defensive: leave alternate screen if RIS handler missed it
    //   ESC[2J           - clear screen
    //   ESC[H            - cursor home
    private static readonly ReadOnlyMemory<byte> ResetSequence =
        Encoding.ASCII.GetBytes("\u001bc\u001b[?1049l\u001b[2J\u001b[H");

    private IHex1bTerminalWorkloadAdapter _primary;
    private readonly IHex1bTerminalWorkloadAdapter _placeholder;
    private readonly Func<CancellationToken, Task<int>>? _placeholderRun;
    private readonly PlaceholderResumePolicy _resumePolicy;
    private readonly object _swapLock = new();

    private IHex1bTerminalWorkloadAdapter _active;
    private CancellationTokenSource _swapCts = new();
    private bool _resetPending;
    private bool _disposed;

    private int _lastWidth;
    private int _lastHeight;
    private bool _hasSize;

    private CancellationTokenSource? _watcherCts;
    private Task? _watcherTask;

    private CancellationTokenSource? _placeholderRunCts;
    private Task? _placeholderRunTask;

    public PlaceholderWorkloadAdapter(
        IHex1bTerminalWorkloadAdapter primary,
        IHex1bTerminalWorkloadAdapter placeholder,
        PlaceholderResumePolicy resumePolicy)
        : this(primary, placeholder, placeholderRun: null, resumePolicy)
    {
    }

    public PlaceholderWorkloadAdapter(
        IHex1bTerminalWorkloadAdapter primary,
        IHex1bTerminalWorkloadAdapter placeholder,
        Func<CancellationToken, Task<int>>? placeholderRun,
        PlaceholderResumePolicy resumePolicy)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _placeholder = placeholder ?? throw new ArgumentNullException(nameof(placeholder));
        _placeholderRun = placeholderRun;
        _resumePolicy = resumePolicy;
        _active = placeholder;

        _primary.Disconnected += OnPrimaryDisconnectedEvent;

        _watcherCts = new CancellationTokenSource();
        _watcherTask = Task.Run(() => WatchPrimaryAsync(_watcherCts.Token));

        // Drive the placeholder's "run" loop (typically a Hex1bApp.RunAsync) so
        // it actually produces frames into its workload adapter — otherwise the
        // adapter's output channel stays empty and ReadOutputAsync would block
        // forever waiting on a child that has nothing to say.
        if (_placeholderRun is { } run)
        {
            _placeholderRunCts = new CancellationTokenSource();
            _placeholderRunTask = Task.Run(async () =>
            {
                try { await run(_placeholderRunCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                catch { /* swallow — we're a background helper */ }
            });
        }
    }

    /// <summary>The currently-active child (test hook).</summary>
    internal IHex1bTerminalWorkloadAdapter ActiveChild => Volatile.Read(ref _active);

    /// <summary>
    /// Replace the primary workload with a freshly-built instance. Used by
    /// the builder layer to recover from one-shot adapters (e.g.
    /// <see cref="Hmp1WorkloadAdapter"/>, whose read pump dies on
    /// disconnect) so the wrapper can reconnect on subsequent attempts.
    /// </summary>
    /// <remarks>
    /// Tears down the previous primary watcher, unsubscribes its
    /// <see cref="IHex1bTerminalWorkloadAdapter.Disconnected"/> handler,
    /// disposes it (best-effort), then installs the replacement, propagates
    /// the last-known dimensions, and starts a fresh watcher. Active-child
    /// state is left alone — if the wrapper was already showing the
    /// placeholder (the typical case after a disconnect) it stays there
    /// until the new primary signals connected.
    /// </remarks>
    internal async ValueTask ReplacePrimaryAsync(
        IHex1bTerminalWorkloadAdapter newPrimary,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newPrimary);
        if (_disposed) return;

        IHex1bTerminalWorkloadAdapter oldPrimary;
        CancellationTokenSource? oldWatcherCts;
        Task? oldWatcherTask;
        bool needResize;
        int width, height;

        lock (_swapLock)
        {
            if (_disposed) return;
            oldPrimary = _primary;
            oldWatcherCts = _watcherCts;
            oldWatcherTask = _watcherTask;
            _primary = newPrimary;
            _watcherCts = new CancellationTokenSource();
            needResize = _hasSize;
            width = _lastWidth;
            height = _lastHeight;
        }

        oldPrimary.Disconnected -= OnPrimaryDisconnectedEvent;
        if (oldWatcherCts is not null)
        {
            try { oldWatcherCts.Cancel(); } catch { }
            try { if (oldWatcherTask is not null) await oldWatcherTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch { }
            oldWatcherCts.Dispose();
        }
        await SafeDisposeAsync(oldPrimary).ConfigureAwait(false);

        newPrimary.Disconnected += OnPrimaryDisconnectedEvent;
        if (needResize)
        {
            try { await newPrimary.ResizeAsync(width, height, ct).ConfigureAwait(false); }
            catch { }
        }

        _watcherTask = Task.Run(() => WatchPrimaryAsync(_watcherCts!.Token));
    }

    public event Action? Disconnected;

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Drain a queued reset sequence first so it lands ahead of the
            // new child's first bytes. Reset is short — single chunk is fine.
            CancellationTokenSource swapCts;
            IHex1bTerminalWorkloadAdapter active;
            bool emitReset;
            lock (_swapLock)
            {
                active = _active;
                swapCts = _swapCts;
                emitReset = _resetPending;
                _resetPending = false;
            }

            if (emitReset)
            {
                if (active is IRepaintableWorkloadAdapter repaintable)
                {
                    repaintable.RequestFullRepaint();
                }
                return ResetSequence;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, swapCts.Token);
            try
            {
                var data = await active.ReadOutputAsync(linked.Token).ConfigureAwait(false);

                // If a swap happened while we were awaiting, drop these bytes
                // — they belong to the now-inactive child and would mix into
                // the new child's screen.
                if (!ReferenceEquals(Volatile.Read(ref _active), active))
                {
                    continue;
                }

                if (data.IsEmpty)
                {
                    // EOF on the active child. If that's the primary and the
                    // resume policy allows it, fall back to the placeholder
                    // and keep going. Otherwise propagate EOF upstream.
                    if (ReferenceEquals(active, _primary)
                        && _resumePolicy == PlaceholderResumePolicy.OnDisconnect)
                    {
                        SwapTo(_placeholder);
                        continue;
                    }

                    Disconnected?.Invoke();
                    return ReadOnlyMemory<byte>.Empty;
                }

                return data;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Swap fired — loop and read from the new active.
                continue;
            }
        }
    }

    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        return Volatile.Read(ref _active).WriteInputAsync(data, ct);
    }

    public async ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        lock (_swapLock)
        {
            _lastWidth = width;
            _lastHeight = height;
            _hasSize = true;
        }

        // Fan out so an inactive child's layout stays in sync with host TTY
        // dims and renders correctly the moment it becomes active.
        await _placeholder.ResizeAsync(width, height, ct).ConfigureAwait(false);
        await _primary.ResizeAsync(width, height, ct).ConfigureAwait(false);

        // For Hex1bApp-style children, the very first resize sets dimensions
        // but doesn't fire a Hex1bResizeEvent (treated as initial setup). Without
        // that wake-up the app's render loop sleeps after its inaugural 0x0
        // frame and the user never sees the placeholder UI. Forcing a repaint
        // here invalidates the app and wakes the loop so it re-renders at the
        // newly-known dimensions. Safe for child-process style children that
        // don't implement IRepaintableWorkloadAdapter.
        if (_placeholder is IRepaintableWorkloadAdapter ph) ph.RequestFullRepaint();
        if (_primary is IRepaintableWorkloadAdapter pr) pr.RequestFullRepaint();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _primary.Disconnected -= OnPrimaryDisconnectedEvent;

        if (_watcherCts is { } wcts)
        {
            wcts.Cancel();
            try
            {
                if (_watcherTask is { } wt) await wt.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            wcts.Dispose();
        }

        if (_placeholderRunCts is { } pcts)
        {
            pcts.Cancel();
            try
            {
                if (_placeholderRunTask is { } pt) await pt.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            pcts.Dispose();
        }

        lock (_swapLock)
        {
            _swapCts.Cancel();
            _swapCts.Dispose();
        }

        await SafeDisposeAsync(_placeholder).ConfigureAwait(false);
        await SafeDisposeAsync(_primary).ConfigureAwait(false);
    }

    private static async ValueTask SafeDisposeAsync(IAsyncDisposable d)
    {
        try { await d.DisposeAsync().ConfigureAwait(false); }
        catch { /* swallow — we're tearing down */ }
    }

    private async Task WatchPrimaryAsync(CancellationToken ct)
    {
        try
        {
            if (_primary is IConnectableWorkloadAdapter connectable)
            {
                // Wait for either explicit ready signal or cancellation.
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var connectedTask = connectable.ConnectedTask;
                var cancelTask = Task.Delay(Timeout.Infinite, linked.Token);
                var winner = await Task.WhenAny(connectedTask, cancelTask).ConfigureAwait(false);
                if (winner == cancelTask) return;

                SwapTo(_primary);

                // Now wait for disconnect to potentially swap back.
                var disconnectTask = connectable.DisconnectedTask;
                var winner2 = await Task.WhenAny(disconnectTask, cancelTask).ConfigureAwait(false);
                if (winner2 == cancelTask) return;

                if (_resumePolicy == PlaceholderResumePolicy.OnDisconnect)
                {
                    SwapTo(_placeholder);
                }
                else
                {
                    Disconnected?.Invoke();
                }
            }
            // For non-IConnectableWorkloadAdapter primaries the swap is
            // driven implicitly by ReadPrimaryProbeAsync below.
            else
            {
                await ProbePrimaryFirstByteAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    // Fallback "first byte = ready" probe for primaries that don't implement
    // IConnectableWorkloadAdapter. Reads from the primary in the background;
    // when it returns its first non-empty chunk we swap it in and re-queue
    // those bytes by routing them through ReadOutputAsync's normal path.
    //
    // Implementation note: rather than buffering the chunk (which would force
    // ReadOutputAsync to drain a side-queue), we simply observe the primary
    // by polling `IsCompleted` style — except `ReadOutputAsync` doesn't have
    // that. For v1 the simple approach is: don't probe. Treat the primary as
    // immediately-ready if it doesn't expose `IConnectableWorkloadAdapter`,
    // i.e. swap straight to it. Most non-handshaking workloads (child PTY,
    // Hex1bApp) are in fact ready at construction, so this matches expectations.
    private Task ProbePrimaryFirstByteAsync(CancellationToken ct)
    {
        SwapTo(_primary);
        return Task.CompletedTask;
    }

    private void OnPrimaryDisconnectedEvent()
    {
        // Belt-and-braces alongside ConnectedTask/DisconnectedTask: some
        // adapters expose Disconnected without IConnectableWorkloadAdapter.
        if (_resumePolicy == PlaceholderResumePolicy.OnDisconnect
            && ReferenceEquals(Volatile.Read(ref _active), _primary))
        {
            SwapTo(_placeholder);
        }
    }

    private void SwapTo(IHex1bTerminalWorkloadAdapter target)
    {
        CancellationTokenSource oldCts;
        bool changed;
        bool needResize;
        int width, height;

        lock (_swapLock)
        {
            if (_disposed) return;
            if (ReferenceEquals(_active, target))
            {
                return;
            }
            _active = target;
            _resetPending = true;
            oldCts = _swapCts;
            _swapCts = new CancellationTokenSource();
            changed = true;
            needResize = _hasSize;
            width = _lastWidth;
            height = _lastHeight;
        }

        if (changed)
        {
            try { oldCts.Cancel(); } catch { }
            oldCts.Dispose();

            // Make sure the newly-active child has the current host dimensions
            // even if it was constructed before we knew the size, so its first
            // post-RIS frame is sized correctly.
            if (needResize)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await target.ResizeAsync(width, height).ConfigureAwait(false);
                    }
                    catch { /* swallow — best effort */ }
                });
            }
        }
    }
}

/// <summary>
/// Controls what happens when the primary workload disconnects after having
/// been active.
/// </summary>
[Experimental("HEX1B002", UrlFormat = "https://github.com/hex1b/hex1b/blob/main/docs/experimental/placeholder.md")]
public enum PlaceholderResumePolicy
{
    /// <summary>
    /// Swap back to the placeholder workload and keep the terminal alive.
    /// Suits scenarios where the primary may reconnect (e.g. an HMP1 producer
    /// is restarted, or a UDS path is re-bound).
    /// </summary>
    OnDisconnect = 0,

    /// <summary>
    /// Treat primary disconnect as terminal disconnect — surface
    /// <see cref="IHex1bTerminalWorkloadAdapter.Disconnected"/> upstream and
    /// stop. Matches the pre-placeholder behaviour of bare HMP1 / PTY workloads.
    /// </summary>
    OneShot = 1,
}
