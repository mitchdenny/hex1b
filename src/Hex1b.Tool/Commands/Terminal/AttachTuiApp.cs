using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Hex1b;
using Hex1b.Diagnostics;
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
    private readonly string _socketPath;
    private readonly string _displayId;
    private readonly TerminalClient _client;
    private readonly bool _initialResize;
    private readonly bool _claimLead;

    // Network state
    private Socket? _socket;
    private NetworkStream? _networkStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

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

    public AttachTuiApp(string socketPath, string displayId, TerminalClient client, bool resize, bool lead)
    {
        _socketPath = socketPath;
        _displayId = displayId;
        _client = client;
        _initialResize = resize;
        _claimLead = lead;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        // 1. Connect to the remote terminal
        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await _socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Cannot connect: {ex.Message}");
            _socket.Dispose();
            return 1;
        }

        _networkStream = new NetworkStream(_socket, ownsSocket: true);
        _reader = new StreamReader(_networkStream, Encoding.UTF8);
        _writer = new StreamWriter(_networkStream, Encoding.UTF8) { AutoFlush = true };

        // 2. Send attach request and get initial state
        var request = new DiagnosticsRequest { Method = "attach" };
        var requestJson = JsonSerializer.Serialize(request, DiagnosticsJsonOptions.Default);
        await _writer.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

        var responseLine = await _reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(responseLine))
        {
            Console.Error.WriteLine("Error: Empty response from terminal");
            return 1;
        }

        var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, DiagnosticsJsonOptions.Default);
        if (response is not { Success: true })
        {
            Console.Error.WriteLine($"Error: {response?.Error ?? "Attach failed"}");
            return 1;
        }

        _isLeader = response.Leader == true;
        _remoteWidth = response.Width ?? 80;
        _remoteHeight = response.Height ?? 24;

        // 3. Claim leadership if requested
        if (_claimLead && !_isLeader)
        {
            await _writer.WriteLineAsync("lead".AsMemory(), cancellationToken);
            var leadResponse = await _reader.ReadLineAsync(cancellationToken);
            if (leadResponse == "leader:true")
                _isLeader = true;
        }

        // 5. Write initial screen content into the output pipe so the embedded terminal parses it
        if (response.Data != null)
        {
            var initialBytes = Encoding.UTF8.GetBytes(response.Data);
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

        // Start the embedded terminal's output pump in the background
        var embeddedRunTask = _embeddedTerminal.RunAsync(_appCts.Token);

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
                try { await _writer.WriteLineAsync("shutdown"); } catch { }
                Console.Error.WriteLine($"Terminated remote session {_displayId}.");
            }
            else
            {
                try { await _writer.WriteLineAsync("detach"); } catch { }
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
                        try { await _writer!.WriteLineAsync("lead"); } catch { }
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
                        try { await _writer!.WriteLineAsync("lead"); } catch { }
                    }),
                    m.Separator(),
                    m.MenuItem("Stop").OnActivated(async _ =>
                    {
                        _shutdownRequested = true;
                        try { await _writer!.WriteLineAsync("shutdown"); } catch { }
                        _app?.RequestStop();
                    }),
                    m.Separator(),
                    m.MenuItem("Exit (Detach)").OnActivated(async _ =>
                    {
                        try { await _writer!.WriteLineAsync("detach"); } catch { }
                        _app?.RequestStop();
                    })
                ])
            ]),

            v.ThemePanel(
                theme => theme.Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(40, 40, 40)),
                v.Border(content, title: title)).Fill(),

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
    /// Reads o:/leader:/exit frames from the network and writes decoded output into the pipe
    /// for the embedded terminal to parse.
    /// </summary>
    private async Task PumpNetworkOutputAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader!.ReadLineAsync(ct);
                if (line == null)
                {
                    _app?.RequestStop();
                    return;
                }

                if (line.StartsWith("o:"))
                {
                    var bytes = Convert.FromBase64String(line[2..]);
                    await _outputPipe.Writer.WriteAsync(bytes, ct);
                    await _outputPipe.Writer.FlushAsync(ct);
                }
                else if (line.StartsWith("r:"))
                {
                    // Remote terminal was resized (by leader) — update embedded terminal
                    var parts = line[2..].Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                    {
                        _remoteWidth = w;
                        _remoteHeight = h;
                        _handle?.Resize(w, h);
                        _embeddedTerminal?.Resize(w, h);
                        _app?.Invalidate();
                    }
                }
                else if (line == "exit")
                {
                    _app?.RequestStop();
                    return;
                }
                else if (line == "leader:true")
                {
                    _isLeader = true;
                    await SendResizeForCurrentDisplayAsync();
                    _app?.Invalidate();
                }
                else if (line == "leader:false")
                {
                    _isLeader = false;
                    _app?.Invalidate();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { _app?.RequestStop(); }
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
                try { await _writer!.WriteLineAsync("lead"); } catch { }
                break;

            case (byte)'q':
                _shutdownRequested = true;
                _app?.RequestStop();
                break;

            case 0x1D: // literal Ctrl+]
                var b64 = Convert.ToBase64String([0x1D]);
                try { await _writer!.WriteLineAsync($"i:{b64}"); } catch { }
                break;
        }
    }

    /// <summary>
    /// Sends input bytes to the remote terminal as i: frames.
    /// </summary>
    internal async Task SendInputAsync(byte[] data, int offset, int count)
    {
        var b64 = Convert.ToBase64String(data, offset, count);
        try { await _writer!.WriteLineAsync($"i:{b64}"); } catch { }
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

        _reader?.Dispose();
        if (_writer != null)
            await _writer.DisposeAsync();
        if (_networkStream != null)
            await _networkStream.DisposeAsync();
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

        try { await _writer!.WriteLineAsync($"r:{termWidth},{termHeight}"); } catch { }
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
