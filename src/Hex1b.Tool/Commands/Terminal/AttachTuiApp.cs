using System.IO.Pipelines;
using System.Runtime.Versioning;
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
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
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

        // 3. Write initial screen content into the output pipe so the embedded terminal parses it
        if (connectResult.InitialScreen != null)
        {
            var initialBytes = Encoding.UTF8.GetBytes(connectResult.InitialScreen);
            await _outputPipe.Writer.WriteAsync(initialBytes, cancellationToken);
        }

        // 6. Build the embedded terminal (parses ANSI from output pipe, writes input to intercept stream)
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

        // Wait for the embedded terminal to process the initial screen data before
        // starting the display TUI. Without this, the display's first render can race
        // ahead of the output pump (which runs on a thread-pool thread) and show a
        // blank terminal until the next OutputReceived event — which only fires when
        // the user interacts with the terminal.
        var initialOutputReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void SignalReady() => initialOutputReady.TrySetResult();

        if (connectResult.InitialScreen != null)
        {
            handle.OutputReceived += SignalReady;
        }
        else
        {
            initialOutputReady.SetResult();
        }

        // Start the embedded terminal's output pump in the background
        var embeddedRunTask = _embeddedTerminal.RunAsync(_appCts.Token);

        // Give the output pump time to process the initial data (piped before RunAsync).
        // 2 seconds is generous — it typically completes in < 10ms.
        await Task.WhenAny(initialOutputReady.Task, Task.Delay(2000, cancellationToken));
        handle.OutputReceived -= SignalReady;

        // 7. Start network output bridge task
        var networkOutputTask = PumpNetworkOutputAsync(_appCts.Token);

        // 8. Build and run the display TUI app
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
            content = ctx.Align(Alignment.Center,
                ctx.Terminal(handle)
                    .FixedWidth(_remoteWidth)
                    .FixedHeight(_remoteHeight));
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
                s.Separator(" | "),
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
    private async Task HandleDisplayResizeAsync(int displayWidth, int displayHeight)
    {
        _displayWidth = displayWidth;
        _displayHeight = displayHeight;

        if (_isLeader)
            await SendResizeForCurrentDisplayAsync();
    }

    /// <summary>
    /// Computes available terminal space from current display dimensions and sends r: frame.
    /// </summary>
    private async Task SendResizeForCurrentDisplayAsync()
    {
        var termWidth = _displayWidth - 2;   // border left + right
        var termHeight = _displayHeight - 4; // menu bar + border top + border bottom + info bar
        if (termWidth < 1 || termHeight < 1) return;
        if (termWidth == _remoteWidth && termHeight == _remoteHeight) return;

        _remoteWidth = termWidth;
        _remoteHeight = termHeight;
        _handle?.Resize(termWidth, termHeight);
        _embeddedTerminal?.Resize(termWidth, termHeight);

        try { await _transport.SendResizeAsync(termWidth, termHeight, CancellationToken.None); } catch { }
        _app?.Invalidate();
    }

    /// <summary>
    /// Presentation filter that detects display terminal resize events.
    /// </summary>
    private sealed class ResizeFilter(AttachTuiApp app) : IHex1bTerminalPresentationFilter
    {
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct)
        {
            // Just track dimensions — don't send resize during initialization
            app._displayWidth = width;
            app._displayHeight = height;
            return default;
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
