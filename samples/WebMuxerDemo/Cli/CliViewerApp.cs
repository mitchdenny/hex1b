using Hex1b;
using Hex1b.Input;
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
    private readonly Hmp1WorkloadAdapter _adapter;
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

    public CliViewerApp(Hmp1WorkloadAdapter adapter, string sessionName)
    {
        _adapter = adapter;
        _sessionName = sessionName;
    }

    public async Task RunAsync()
    {
        // Build the embedded inner terminal that consumes the HMP1 byte
        // stream. Initial size = producer's current dims at handshake.
        _innerWidth = Math.Max(1, _adapter.RemoteWidth);
        _innerHeight = Math.Max(1, _adapter.RemoteHeight);

        _embedded = Hex1bTerminal.CreateBuilder()
            .WithDimensions(_innerWidth, _innerHeight)
            .WithWorkload(_adapter)
            .WithScrollback()
            .WithTerminalWidget(out var handle)
            .Build();
        _handle = handle;

        _embeddedCts = new CancellationTokenSource();
        _ = _embedded.RunAsync(_embeddedCts.Token);

        // Re-render the outer app whenever the producer roster or role
        // changes. Resize-only broadcasts (no role change) silently update
        // the adapter's RemoteWidth/Height; we catch those at render time
        // by comparing against _innerWidth/_innerHeight.
        _adapter.RoleChanged += OnRoleChanged;
        _adapter.PeerJoined += OnPeerEvent;
        _adapter.PeerLeft += OnPeerEvent;

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
            _adapter.RoleChanged -= OnRoleChanged;
            _adapter.PeerJoined -= OnPeerEvent;
            _adapter.PeerLeft -= OnPeerEvent;

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
        // Catch silent producer-resize broadcasts that happen without an
        // accompanying RoleChange (e.g. the current primary resized their
        // host TTY). Cheap O(1) compare; resize is a no-op when sizes
        // already match.
        EnsureInnerSize(_adapter.RemoteWidth, _adapter.RemoteHeight);

        // Available widget space ~= host TTY minus the InfoBar (1 row).
        // Console.WindowWidth / WindowHeight reflect the live host TTY
        // size including SIGWINCH; the outer Hex1bTerminal is bound to
        // those dims when running interactively.
        var availW = Math.Max(1, Console.WindowWidth);
        var availH = Math.Max(1, Console.WindowHeight - 1);

        var producerW = _adapter.RemoteWidth;
        var producerH = _adapter.RemoteHeight;

        var isPrimary = _adapter.IsPrimary;
        var fits = producerW <= availW && producerH <= availH;
        var showTerminal = isPrimary || fits;

        Hex1bWidget body = showTerminal
            ? BuildTerminalView(ctx)
            : BuildDoesntFitView(ctx, producerW, producerH, availW, availH);

        var info = BuildInfoBar(ctx, isPrimary, producerW, producerH, availW, availH);

        var content = ctx.VStack(v => [body, info]);

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
        return ctx.Align(
            Alignment.Center,
            ctx.Terminal(_handle)
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
        var peers = _adapter.Peers.Count + 1;
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
            await _adapter.RequestPrimaryAsync(availW, availH, CancellationToken.None);
        }
        catch (Exception)
        {
            // Best-effort; if the producer is gone we'll see Disconnected
            // shortly. Don't escalate — Hex1bApp surface should never
            // unwind a binding action with an exception.
        }
    }
}
