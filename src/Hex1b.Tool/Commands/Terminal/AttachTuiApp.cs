using System.IO.Pipelines;
using System.Text;
using Hex1b;
using Hex1b.Theming;
using Hex1b.Tokens;
using Hex1b.Tool.Infrastructure;
using Hex1b.Widgets;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Runs the TUI-based attach experience. Creates a Hex1b TUI app that embeds
/// the remote terminal via TerminalWidget, with an InfoBar showing status and key bindings.
/// </summary>
internal sealed class AttachTuiApp : IAsyncDisposable
{
    private readonly IAttachTransport _transport;
    private readonly string _displayId;
    private readonly TerminalClient _client;
    private readonly bool _initialResize;
    private readonly bool _claimLead;

    // Pipe bridging network output → StreamWorkloadAdapter
    private readonly Pipe _outputPipe = new();

    // Stream that captures input from the embedded terminal before sending to network
    private InputInterceptStream? _inputIntercept;

    // Shared state
    private volatile bool _isLeader;
    private volatile bool _shutdownRequested;
    private int _remoteWidth;
    private int _remoteHeight;
    private int _displayWidth;
    private int _displayHeight;
    private Hex1bApp? _app;
    private Hex1bTerminal? _embeddedTerminal;
    private TerminalWidgetHandle? _handle;
    private CancellationTokenSource? _appCts;

    public AttachTuiApp(IAttachTransport transport, string displayId, TerminalClient client, bool resize, bool lead)
    {
        _transport = transport;
        _displayId = displayId;
        _client = client;
        _initialResize = resize;
        _claimLead = lead;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        // 1. Connect to the remote terminal via the transport
        var connectResult = await _transport.ConnectAsync(cancellationToken);
        if (!connectResult.Success)
        {
            Console.Error.WriteLine($"Error: {connectResult.Error}");
            return 1;
        }

        _isLeader = connectResult.IsLeader;
        _remoteWidth = connectResult.Width;
        _remoteHeight = connectResult.Height;

        // 2. Claim leadership if requested
        if (_claimLead && !_isLeader)
        {
            await _transport.ClaimLeadAsync(cancellationToken);

            // Read frames until we get leader confirmation,
            // forwarding any output frames (like mode replay) to the output pipe
            await foreach (var frame in _transport.ReadFramesAsync(cancellationToken))
            {
                if (frame.Kind == AttachFrameKind.LeaderChanged)
                {
                    _isLeader = frame.GetIsLeader();
                    break;
                }

                if (frame.Kind == AttachFrameKind.Output)
                {
                    await _outputPipe.Writer.WriteAsync(frame.Data, cancellationToken);
                }
            }
        }

        var preSizedToDisplay = await TryApplyInitialLeaderResizeBeforeBuildAsync(cancellationToken);
        var deferredInitialScreen = connectResult.InitialScreen;
        var wroteInitialScreen = false;

        // If we just resized the remote to fit the local display, the attach response's
        // initial screen snapshot is now stale. Prefer the post-resize output from the
        // remote shell and only fall back to the original snapshot if nothing arrives.
        if (!preSizedToDisplay && deferredInitialScreen != null)
        {
            await WriteInitialScreenAsync(deferredInitialScreen, cancellationToken);
            wroteInitialScreen = true;
        }

        // 3. Build the embedded terminal (parses ANSI from output pipe, writes input to intercept stream)
        _inputIntercept = new InputInterceptStream(this);

        var workload = new StreamWorkloadAdapter(
            _outputPipe.Reader.AsStream(),
            _inputIntercept)
        {
            Width = _remoteWidth,
            Height = _remoteHeight
        };

        _embeddedTerminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(_remoteWidth, _remoteHeight)
            .WithWorkload(workload)
            .WithTerminalWidget(out var handle)
            .Build();

        _handle = handle;
        _appCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Wait for the embedded terminal to process initial data before starting the
        // display TUI. This avoids the first render racing ahead of the output pump.
        var initialOutputReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void SignalReady() => initialOutputReady.TrySetResult();

        handle.OutputReceived += SignalReady;

        // Start the embedded terminal and network bridge in the background.
        var embeddedRunTask = _embeddedTerminal.RunAsync(_appCts.Token);
        var networkOutputTask = PumpNetworkOutputAsync(_appCts.Token);

        if (preSizedToDisplay)
        {
            var firstOutputTask = initialOutputReady.Task;
            var completed = await Task.WhenAny(firstOutputTask, Task.Delay(750, cancellationToken));
            if (completed != firstOutputTask && deferredInitialScreen != null)
            {
                await WriteInitialScreenAsync(deferredInitialScreen, cancellationToken);
                wroteInitialScreen = true;
            }
        }

        if (wroteInitialScreen || preSizedToDisplay)
        {
            await Task.WhenAny(initialOutputReady.Task, Task.Delay(2000, cancellationToken));
        }

        handle.OutputReceived -= SignalReady;

        // 4. Build and run the display TUI app
        // The resize filter sends r: frames when the leader's display terminal resizes
        await using var displayTerminal = Hex1bTerminal.CreateBuilder()
            .WithMouse()
            .AddPresentationFilter(new ResizeFilter(this))
            .WithHex1bApp((app, options) =>
            {
                _app = app;
                return ctx => BuildWidget(ctx, handle);
            })
            .Build();

        try
        {
            await displayTerminal.RunAsync(_appCts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await _appCts.CancelAsync();

            // Send detach or shutdown
            if (_shutdownRequested)
            {
                try { await _transport.ShutdownAsync(CancellationToken.None); } catch { }
                Console.Error.WriteLine($"Terminated remote session {_displayId}.");
            }
            else
            {
                try { await _transport.DetachAsync(CancellationToken.None); } catch { }
                Console.Error.WriteLine($"Detached from {_displayId}{(_isLeader ? " (leader)" : "")}.");
            }

            // Clean up bridge tasks
            _outputPipe.Writer.Complete();
            try { await networkOutputTask; } catch { }
            try { await embeddedRunTask; } catch { }
        }

        return 0;
    }

    private Hex1bWidget BuildWidget<TParent>(WidgetContext<TParent> ctx, TerminalWidgetHandle handle)
        where TParent : Hex1bWidget
    {
        var leaderStatus = _isLeader ? "leader" : "follower";
        var dims = $"{_remoteWidth}\u00d7{_remoteHeight}";
        var title = $" {_displayId} ({dims}) ";

        // Available space inside the border chrome
        var availWidth = _displayWidth - 2;
        var availHeight = _displayHeight - 4;
        var tooSmall = !_isLeader && (availWidth < _remoteWidth || availHeight < _remoteHeight);

        Hex1bWidget content;
        if (tooSmall)
        {
            content = ctx.Align(Alignment.Center,
                ctx.VStack(v =>
                [
                    v.Text($"Terminal too small to display remote session"),
                    v.Text($"Remote: {_remoteWidth}\u00d7{_remoteHeight}  Available: {availWidth}\u00d7{availHeight}"),
                    v.Text(""),
                    v.Button("Take Lead & Resize").OnClick(async _ =>
                    {
                        try { await _transport.ClaimLeadAsync(CancellationToken.None); } catch { }
                    })
                ]));
        }
        else
        {
            // Let the embedded terminal fill the bordered content area so the widget's
            // arranged bounds stay aligned with the current remote PTY size.
            content = ctx.Terminal(handle).Fill();
        }

        return ctx.VStack(v =>
        [
            v.MenuBar(m =>
            [
                m.Menu("Terminal", m =>
                [
                    m.MenuItem("Lead").OnActivated(async _ =>
                    {
                        try { await _transport.ClaimLeadAsync(CancellationToken.None); } catch { }
                    }),
                    m.Separator(),
                    m.MenuItem("Stop").OnActivated(async _ =>
                    {
                        _shutdownRequested = true;
                        try { await _transport.ShutdownAsync(CancellationToken.None); } catch { }
                        _app?.RequestStop();
                    }),
                    m.Separator(),
                    m.MenuItem("Exit (Detach)").OnActivated(async _ =>
                    {
                        try { await _transport.DetachAsync(CancellationToken.None); } catch { }
                        _app?.RequestStop();
                    })
                ])
            ]),

            v.ThemePanel(
                theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 40, 40)),
                v.Border(content).Title(title)).Fill(),

            v.InfoBar(s =>
            [
                s.Spacer(),
                s.Section(dims),
                s.Divider(" | "),
                s.Section(leaderStatus)
            ])
        ]);
    }

    /// <summary>
    /// Reads frames from the transport and writes decoded output into the pipe
    /// for the embedded terminal to parse.
    /// </summary>
    private async Task PumpNetworkOutputAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in _transport.ReadFramesAsync(ct))
            {
                switch (frame.Kind)
                {
                    case AttachFrameKind.Output:
                        await _outputPipe.Writer.WriteAsync(frame.Data, ct);
                        await _outputPipe.Writer.FlushAsync(ct);
                        _app?.Invalidate();
                        break;

                    case AttachFrameKind.Resize:
                        var (w, h) = frame.GetResize();
                        _remoteWidth = w;
                        _remoteHeight = h;
                        _handle?.Resize(w, h);
                        _embeddedTerminal?.Resize(w, h);
                        _app?.Invalidate();
                        break;

                    case AttachFrameKind.Exit:
                        _app?.RequestStop();
                        return;

                    case AttachFrameKind.LeaderChanged:
                        _isLeader = frame.GetIsLeader();
                        if (_isLeader)
                            await SendResizeForCurrentDisplayAsync();
                        _app?.Invalidate();
                        break;
                }
            }

            // Stream ended — connection closed
            _app?.RequestStop();
        }
        catch (OperationCanceledException) { }
        catch (Exception) { _app?.RequestStop(); }
        finally
        {
            _outputPipe.Writer.Complete();
        }
    }

    /// <summary>
    /// Handles a command byte after Ctrl+].
    /// </summary>
    private async Task HandleCommandByteAsync(byte cmd)
    {
        switch (cmd)
        {
            case (byte)'d':
                _app?.RequestStop();
                break;

            case (byte)'l':
                try { await _transport.ClaimLeadAsync(CancellationToken.None); } catch { }
                break;

            case (byte)'q':
                _shutdownRequested = true;
                _app?.RequestStop();
                break;

            case 0x1D: // literal Ctrl+]
                try { await _transport.SendInputAsync(new byte[] { 0x1D }, CancellationToken.None); } catch { }
                break;
        }
    }

    /// <summary>
    /// Sends input bytes to the remote terminal via the transport.
    /// </summary>
    internal async Task SendInputAsync(byte[] data, int offset, int count)
    {
        try { await _transport.SendInputAsync(data.AsMemory(offset, count), CancellationToken.None); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_appCts != null)
        {
            await _appCts.CancelAsync();
            _appCts.Dispose();
        }

        if (_embeddedTerminal != null)
            await _embeddedTerminal.DisposeAsync();

        await _transport.DisposeAsync();
    }

    /// <summary>
    /// Handles display terminal resize by sending r: frames when leader.
    /// Chrome overhead: menu bar (1 row) + border (2 rows, 2 cols) + info bar (1 row) = 4 rows, 2 cols.
    /// </summary>
    private async Task HandleDisplayResizeAsync(int displayWidth, int displayHeight, bool isInitialSessionStart = false)
    {
        var displayChanged = _displayWidth != displayWidth || _displayHeight != displayHeight;
        _displayWidth = displayWidth;
        _displayHeight = displayHeight;

        if (ShouldSendResizeForDisplayChange(_isLeader, _initialResize, isInitialSessionStart))
            await SendResizeForCurrentDisplayAsync();
        else if (displayChanged)
            _app?.Invalidate();
    }

    /// <summary>
    /// Computes available terminal space from current display dimensions and sends r: frame.
    /// </summary>
    private async Task SendResizeForCurrentDisplayAsync()
    {
        var targetSize = CalculateResizeTarget(_displayWidth, _displayHeight, _remoteWidth, _remoteHeight);
        if (targetSize is not { } size)
            return;

        _remoteWidth = size.Width;
        _remoteHeight = size.Height;
        _handle?.Resize(size.Width, size.Height);
        _embeddedTerminal?.Resize(size.Width, size.Height);

        try { await _transport.SendResizeAsync(size.Width, size.Height, CancellationToken.None); } catch { }
        _app?.Invalidate();
    }

    internal static bool ShouldSendResizeForDisplayChange(bool isLeader, bool initialResizeRequested, bool isInitialSessionStart)
        => isLeader && (!isInitialSessionStart || initialResizeRequested);

    internal static (int Width, int Height)? CalculateResizeTarget(
        int displayWidth,
        int displayHeight,
        int remoteWidth,
        int remoteHeight)
    {
        var termWidth = displayWidth - 2;   // border left + right
        var termHeight = displayHeight - 4; // menu bar + border top + border bottom + info bar
        if (termWidth < 1 || termHeight < 1)
            return null;

        if (termWidth == remoteWidth && termHeight == remoteHeight)
            return null;

        return (termWidth, termHeight);
    }

    private async Task<bool> TryApplyInitialLeaderResizeBeforeBuildAsync(CancellationToken ct)
    {
        if (!ShouldSendResizeForDisplayChange(_isLeader, _initialResize, isInitialSessionStart: true))
            return false;

        var displaySize = TryGetCurrentDisplaySize();
        if (displaySize is not { } size)
            return false;

        _displayWidth = size.Width;
        _displayHeight = size.Height;

        var targetSize = CalculateResizeTarget(size.Width, size.Height, _remoteWidth, _remoteHeight);
        if (targetSize is not { } target)
            return false;

        _remoteWidth = target.Width;
        _remoteHeight = target.Height;

        try { await _transport.SendResizeAsync(target.Width, target.Height, ct); } catch { }
        return true;
    }

    private static (int Width, int Height)? TryGetCurrentDisplaySize()
    {
        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            return width > 0 && height > 0 ? (width, height) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private async Task WriteInitialScreenAsync(string initialScreen, CancellationToken ct)
    {
        var initialBytes = Encoding.UTF8.GetBytes(initialScreen);
        await _outputPipe.Writer.WriteAsync(initialBytes, ct);
        await _outputPipe.Writer.FlushAsync(ct);
    }

    /// <summary>
    /// Presentation filter that detects display terminal resize events.
    /// </summary>
    private sealed class ResizeFilter(AttachTuiApp app) : IHex1bTerminalPresentationFilter
    {
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct)
        {
            return new(app.HandleDisplayResizeAsync(width, height, isInitialSessionStart: true));
        }
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct)
            => new(appliedTokens.Select(t => t.Token).ToList());
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct) => default;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct) => default;

        public async ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct)
            => await app.HandleDisplayResizeAsync(width, height);
    }

    /// <summary>
    /// A stream that intercepts writes from the embedded terminal's workload adapter,
    /// scans for Ctrl+] command sequences, and forwards the rest as i: frames to the network.
    /// </summary>
    private sealed class InputInterceptStream : Stream
    {
        private readonly AttachTuiApp _app;
        private bool _inCommandMode;

        public InputInterceptStream(AttachTuiApp app) => _app = app;

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            const byte CtrlRightBracket = 0x1D;

            var start = offset;
            for (var i = offset; i < offset + count; i++)
            {
                if (_inCommandMode)
                {
                    _inCommandMode = false;
                    await _app.HandleCommandByteAsync(buffer[i]);
                    start = i + 1;
                    continue;
                }

                if (buffer[i] == CtrlRightBracket)
                {
                    // Send everything before the Ctrl+]
                    if (i > start)
                        await _app.SendInputAsync(buffer, start, i - start);

                    _inCommandMode = true;
                    start = i + 1;
                }
            }

            // Send remaining bytes
            if (start < offset + count && !_inCommandMode)
                await _app.SendInputAsync(buffer, start, offset + count - start);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            var array = buffer.ToArray();
            await WriteAsync(array, 0, array.Length, ct);
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

        // Read operations not supported — this is a write-only stream
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}
