using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace WebMuxerDemo.Cli;

/// <summary>
/// Hex1bApp shell for the WebMuxerDemo CLI viewer. Renders three modes:
/// <list type="bullet">
/// <item>
/// <c>primary</c> — we hold the role; embed the inner terminal full-screen
/// because the producer's PTY tracks our host dims.
/// </item>
/// <item>
/// <c>viewer-fit</c> — host >= producer dims; embed the inner terminal so
/// the user can see what the primary is doing.
/// </item>
/// <item>
/// <c>viewer-too-small</c> — host &lt; producer dims; show a centered "doesn't
/// fit" panel offering to take control.
/// </item>
/// </list>
/// Hotkeys use a tmux-style chord prefix, <see cref="Hex1bKey.B"/> with Ctrl
/// (Ctrl+B), to avoid clashing with normal input forwarded to the embedded
/// terminal in primary mode.
/// </summary>
internal sealed class CliViewerApp
{
    // Panel background colour matching the WebMuxerDemo browser UI's
    // --panel CSS variable (#161b22). When the producer's grid is smaller
    // than the host TTY, the surrounding framing area is filled with this
    // colour so the terminal's edges are visible against a contrasting
    // backdrop — same visual idiom as the web's terminal card.
    private static readonly Hex1bColor PanelColor = Hex1bColor.FromRgb(0x16, 0x1b, 0x22);

    // Terminal background, matching the browser's --bg CSS variable
    // (#0d1117). This is applied to TerminalWidget so the embedded grid
    // owns its surface; without it, default-bg cells inside the terminal
    // are transparent and the surrounding PanelColor bleeds through every
    // blank cell, making the terminal indistinguishable from its frame.
    private static readonly Hex1bColor TerminalBackground = Hex1bColor.FromRgb(0x0d, 0x11, 0x17);

    private readonly IHmp1ConnectionHandle _connection;
    private readonly IHex1bTerminalWorkloadAdapter _workload;
    private readonly string _sessionName;
    private Hex1bApp? _app;
    private Hex1bTerminal? _embedded;
    private TerminalWidgetHandle? _handle;
    private CancellationTokenSource? _embeddedCts;

    // Locally-tracked inner terminal dimensions. Hex1bTerminal doesn't
    // expose its current grid size, so we track it here. Updated whenever
    // we resize the inner terminal in response to RoleChanged or to a
    // drift detected at render time.
    private int _innerWidth;
    private int _innerHeight;

    // Last host TTY dims we broadcast to the producer while we held the
    // primary role. We use this to detect host SIGWINCH (Windows Terminal
    // resize) and re-broadcast the new dims so the producer's PTY follows.
    // -1 means "no broadcast in flight"; reset whenever we lose the role.
    private int _lastBroadcastWidth = -1;
    private int _lastBroadcastHeight = -1;

    // Single-flight gate so SIGWINCH bursts (typical from a mouse drag-
    // resize) collapse to one in-flight RequestPrimaryAsync at a time.
    // We always remember the most recent target and re-broadcast when the
    // current call completes if the target moved while we were waiting.
    private int _resizeInFlight; // 0 = idle, 1 = a request is in flight

    /// <summary>
    /// Constructs the viewer app from the two HMP1 surfaces it needs:
    /// an <see cref="IHmp1ConnectionHandle"/> for runtime queries +
    /// events + <c>RequestPrimaryAsync</c>, and an
    /// <see cref="IHex1bTerminalWorkloadAdapter"/> to feed
    /// <c>WithWorkload</c> when constructing the inner embedded
    /// terminal. Today both are satisfied by the same
    /// <see cref="Hmp1WorkloadAdapter"/> instance — the caller passes it
    /// twice. The split keeps the consumer code honest about which
    /// surface it's reaching for: every call site here goes through
    /// <c>_connection</c> except the single <c>WithWorkload</c> hop.
    /// </summary>
    public CliViewerApp(
        IHmp1ConnectionHandle connection,
        IHex1bTerminalWorkloadAdapter workload,
        string sessionName)
    {
        _connection = connection;
        _workload = workload;
        _sessionName = sessionName;
    }

    public async Task RunAsync()
    {
        // Build the embedded inner terminal that consumes the HMP1 byte
        // stream. Initial size = producer's current dims at handshake.
        _innerWidth = Math.Max(1, _connection.RemoteWidth);
        _innerHeight = Math.Max(1, _connection.RemoteHeight);

        _embedded = Hex1bTerminal.CreateBuilder()
            .WithDimensions(_innerWidth, _innerHeight)
            .WithWorkload(_workload)
            .WithScrollback()
            .WithTerminalWidget(out var handle)
            .Build();
        _handle = handle;

        _embeddedCts = new CancellationTokenSource();
        _ = _embedded.RunAsync(_embeddedCts.Token);

        // Re-render the outer app whenever the producer roster or role
        // changes. Pure-Resize broadcasts (no role change, e.g. the current
        // primary resized their host) come through RemoteResized — that
        // event was added so consumers don't have to poll RemoteWidth /
        // RemoteHeight every render.
        _connection.RoleChanged += OnRoleChanged;
        _connection.RemoteResized += OnRemoteResized;
        _connection.PeerJoined += OnPeerEvent;
        _connection.PeerLeft += OnPeerEvent;

        try
        {
            await using var outer = Hex1bTerminal.CreateBuilder()
                .WithMouse()
                .WithHex1bApp((app, _) =>
                {
                    _app = app;
                    return ctx => Render(ctx);
                })
                .Build();

            await outer.RunAsync();
        }
        finally
        {
            _connection.RoleChanged -= OnRoleChanged;
            _connection.RemoteResized -= OnRemoteResized;
            _connection.PeerJoined -= OnPeerEvent;
            _connection.PeerLeft -= OnPeerEvent;

            try
            {
                if (_embeddedCts is not null)
                {
                    await _embeddedCts.CancelAsync();
                    _embeddedCts.Dispose();
                }
            }
            catch { }

            // Bound the embedded terminal teardown so a stuck adapter read
            // pump can't keep the process alive forever after detach.
            try
            {
                if (_embedded is not null)
                {
                    var disposeTask = _embedded.DisposeAsync().AsTask();
                    var timeout = Task.Delay(TimeSpan.FromSeconds(2));
                    await Task.WhenAny(disposeTask, timeout);
                }
            }
            catch { }
        }
    }

    private void OnRoleChanged(object? sender, RoleChangedEventArgs e)
    {
        // RoleChange always carries the current dims. Resize the inner
        // terminal to match; this is the cleanest signal we get for
        // "producer's PTY is now N x M".
        EnsureInnerSize(e.Width, e.Height);

        // If we no longer hold the primary role, drop the broadcast tracker
        // so a future re-take starts from scratch and immediately resyncs.
        if (!_connection.IsPrimary)
        {
            _lastBroadcastWidth = -1;
            _lastBroadcastHeight = -1;
        }

        _app?.Invalidate();
    }

    private void OnRemoteResized(object? sender, RemoteResizedEventArgs e)
    {
        // Producer's PTY just changed dims (either we requested it as primary
        // or another peer is driving it). Resize the embedded terminal and
        // re-render so viewer-fit / doesn't-fit recomputes.
        EnsureInnerSize(e.Width, e.Height);
        _app?.Invalidate();
    }

    private void OnPeerEvent(object? sender, EventArgs e)
    {
        // Roster changed; re-render so the InfoBar peer count is fresh.
        _app?.Invalidate();
    }

    private void EnsureInnerSize(int width, int height)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        if (_innerWidth == w && _innerHeight == h)
        {
            return;
        }
        _embedded?.Resize(w, h);
        _innerWidth = w;
        _innerHeight = h;
    }

    private Hex1bWidget Render<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        // Available widget space ~= host TTY minus the InfoBar (1 row).
        // Console.WindowWidth / WindowHeight reflect the live host TTY
        // size including SIGWINCH; the outer Hex1bTerminal is bound to
        // those dims when running interactively.
        var availW = Math.Max(1, Console.WindowWidth);
        var availH = Math.Max(1, Console.WindowHeight - 1);

        var producerW = _connection.RemoteWidth;
        var producerH = _connection.RemoteHeight;

        var isPrimary = _connection.IsPrimary;
        var fits = producerW <= availW && producerH <= availH;
        var showTerminal = isPrimary || fits;

        // While we hold the primary role, follow host SIGWINCH: re-broadcast
        // the new dims so the producer's PTY grows or shrinks with the host
        // terminal (Windows Terminal, iTerm2, tmux pane, ...). Without this
        // a host grow leaves the producer pinned at the original dims and
        // the terminal sits with empty padding around it forever.
        if (isPrimary && (availW != _lastBroadcastWidth || availH != _lastBroadcastHeight))
        {
            BroadcastResize(availW, availH);
        }

        Hex1bWidget body = showTerminal
            ? BuildTerminalView(ctx)
            : BuildDoesntFitView(ctx, producerW, producerH, availW, availH);

        var info = BuildInfoBar(ctx, isPrimary, producerW, producerH, availW, availH);

        // Wrap the body+infobar in a BackgroundPanelWidget so the framing
        // area around a smaller producer grid fills with the panel colour
        // (mirrors the web UI's --panel card). The InfoBar paints its own
        // background on top, so the visible grey appears only in the empty
        // space around the centred terminal grid.
        var content = new BackgroundPanelWidget(PanelColor, ctx.VStack(v => [body, info]));

        return content.WithInputBindings(bindings =>
        {
            // Detach: works in any mode.
            bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                .OverridesCapture()
                .Action(_ => _app?.RequestStop(), "Detach");

            // Take control: only when we're not already primary.
            if (!isPrimary)
            {
                bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.T)
                    .OverridesCapture()
                    .Action(async _ => await TakeControlAsync(availW, availH),
                        "Take Control");
            }
        });
    }

    private Hex1bWidget BuildTerminalView<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        if (_handle is null)
        {
            return ctx.Align(Alignment.Center, ctx.Text("(initialising terminal)")).Fill();
        }

        // Pin the Terminal to the producer's grid dims so AlignNode can
        // actually centre it. Without FixedWidth/Height, TerminalNode happily
        // claims the full bounded constraint (see TerminalNode.MeasureCore)
        // and the Align centring becomes a no-op — the grid just paints at
        // top-left with blank padding around it.
        // Apply an explicit terminal Background so cells with default-bg
        // don't inherit the surrounding PanelColor — see TerminalExtensions.Background.
        return ctx.Align(
            Alignment.Center,
            ctx.Terminal(_handle)
                .Background(TerminalBackground)
                .FixedWidth(Math.Max(1, _innerWidth))
                .FixedHeight(Math.Max(1, _innerHeight))
        ).Fill();
    }

    private Hex1bWidget BuildDoesntFitView<TParent>(
        WidgetContext<TParent> ctx,
        int producerW, int producerH,
        int availW, int availH)
        where TParent : Hex1bWidget
    {
        // .Fill() on the Center makes VStack hand it all the remaining body
        // space so the panel can centre vertically and the InfoBar is pushed
        // to the actual bottom of the screen.
        return ctx.Center(
            ctx.Border(b =>
            [
                b.VStack(v =>
                [
                    v.Text(""),
                    v.Text($"  Producer terminal:  {producerW}\u00d7{producerH}  "),
                    v.Text($"  Your terminal:      {availW}\u00d7{availH}  "),
                    v.Text(""),
                    v.Text("  Press  Ctrl+B  T  to take control  "),
                    v.Text("  (resizes producer to your terminal)  "),
                    v.Text(""),
                    v.Text("  Press  Ctrl+B  D  to detach  "),
                    v.Text(""),
                ])
            ]).Title(" doesn't fit ")).Fill();
    }

    private Hex1bWidget BuildInfoBar<TParent>(
        WidgetContext<TParent> ctx,
        bool isPrimary,
        int producerW, int producerH,
        int availW, int availH)
        where TParent : Hex1bWidget
    {
        var role = isPrimary ? "PRIMARY" : "viewer";
        var peers = _connection.Peers.Count + 1;
        var dims = $"{producerW}\u00d7{producerH}";
        var session = _sessionName;

        return ctx.InfoBar(s =>
        [
            s.Section("Ctrl+B T"),
            s.Section(isPrimary ? "(primary)" : "Take"),
            s.Spacer(),
            s.Section("Ctrl+B D"),
            s.Section("Detach"),
            s.Spacer(),
            s.Section(session),
            s.Section(role),
            s.Section($"peers:{peers}"),
            s.Section(dims),
        ]).Divider(" ");
    }

    private async Task TakeControlAsync(int availW, int availH)
    {
        try
        {
            // Request producer to resize PTY to our available widget area
            // (host TTY minus InfoBar). Producer broadcasts RoleChange +
            // implicit Resize; our RoleChanged handler updates the inner
            // terminal grid + invalidates the app.
            await _connection.RequestPrimaryAsync(availW, availH, CancellationToken.None);

            // Seed the SIGWINCH tracker so the render-time host-resize
            // poll doesn't immediately re-broadcast the dims we just set.
            _lastBroadcastWidth = availW;
            _lastBroadcastHeight = availH;
        }
        catch (Exception)
        {
            // Best-effort; if the producer is gone we'll see Disconnected
            // shortly. Don't escalate — Hex1bApp surface should never
            // unwind a binding action with an exception.
        }
    }

    private void BroadcastResize(int width, int height)
    {
        // Record the target dims up-front so we don't loop on the next
        // Render(): if multiple SIGWINCH events fire while a request is in
        // flight, only the latest pair persists in _lastBroadcastWidth/H.
        _lastBroadcastWidth = width;
        _lastBroadcastHeight = height;

        // Single-flight: bail if a broadcast is already in flight. Future
        // renders will re-detect drift if the host kept resizing while the
        // request was in flight (because the in-flight call captured an
        // older target) and trigger a fresh broadcast then.
        if (Interlocked.CompareExchange(ref _resizeInFlight, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _connection.RequestPrimaryAsync(width, height, CancellationToken.None);
            }
            catch
            {
                // Best-effort; producer may have gone away mid-resize.
            }
            finally
            {
                Volatile.Write(ref _resizeInFlight, 0);
                _app?.Invalidate();
            }
        });
    }
}
