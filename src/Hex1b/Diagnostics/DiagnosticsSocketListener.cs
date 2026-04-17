using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Diagnostics;

/// <summary>
/// A presentation filter that exposes diagnostics via a Unix domain socket.
/// Allows MCP tools to capture terminal state as ANSI or SVG, and inject input.
/// </summary>
/// <remarks>
/// <para>
/// Add this filter using <see cref="Hex1bTerminalBuilder.WithDiagnostics"/>.
/// The filter creates a Unix domain socket at ~/.hex1b/sockets/[pid].diagnostics.socket
/// that MCP tools can connect to for:
/// </para>
/// <list type="bullet">
///   <item>Querying terminal info (dimensions, app name)</item>
///   <item>Capturing terminal state as ANSI or SVG</item>
///   <item>Injecting input characters</item>
/// </list>
/// </remarks>
public sealed class McpDiagnosticsPresentationFilter : ITerminalAwarePresentationFilter, IAsyncDisposable
{
    private readonly string _appName;
    private readonly DateTimeOffset _startTime;
    private readonly string _socketPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<byte> _pendingInput = [];
    private readonly object _inputLock = new();
    private readonly List<AttachSession> _sessions = [];
    private readonly object _attachLock = new();
    private AttachSession? _leaderSession;
    
    // Terminal mode state for attach replay
    private bool _mouseTrackingEnabled;
    private bool _sgrMouseModeEnabled;
    private bool _bracketedPasteEnabled;
    
    private Hex1bTerminal? _terminal;
    private AsciinemaRecorder? _recorder;
    private Socket? _listenerSocket;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// Gets the socket path for this diagnostics filter.
    /// </summary>
    public string SocketPath => _socketPath;

    /// <summary>
    /// Gets the application name.
    /// </summary>
    public string AppName => _appName;

    /// <summary>
    /// Gets the current terminal width, or 0 if not initialized.
    /// </summary>
    public int TerminalWidth => _terminal?.Width ?? 0;

    /// <summary>
    /// Gets the current terminal height, or 0 if not initialized.
    /// </summary>
    public int TerminalHeight => _terminal?.Height ?? 0;

    /// <summary>
    /// Gets a token that is cancelled when a shutdown request is received.
    /// </summary>
    internal CancellationToken ShutdownToken => _cts.Token;

    /// <summary>
    /// Creates a new MCP diagnostics presentation filter.
    /// </summary>
    /// <param name="appName">Optional application name. Defaults to the entry assembly name.</param>
    public McpDiagnosticsPresentationFilter(string? appName = null)
    {
        _appName = appName 
            ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name 
            ?? "Hex1bApp";
        _startTime = DateTimeOffset.UtcNow;
        _socketPath = GetSocketPath();
    }

    /// <summary>
    /// Sets the terminal reference. Called by the terminal during construction.
    /// </summary>
    public void SetTerminal(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }

    /// <summary>
    /// Sets the asciinema recorder reference for remote recording control.
    /// </summary>
    internal void SetRecorder(AsciinemaRecorder recorder)
    {
        _recorder = recorder;
    }

    /// <summary>
    /// Gets the socket directory path (~/.hex1b/sockets/).
    /// </summary>
    internal static string GetSocketDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hex1b", "sockets");
    }

    /// <summary>
    /// Gets the socket path for the current process.
    /// </summary>
    internal static string GetSocketPath()
    {
        var pid = Environment.ProcessId;
        return Path.Combine(GetSocketDirectory(), $"{pid}.diagnostics.socket");
    }

    // === IHex1bTerminalPresentationFilter ===

    /// <inheritdoc />
    public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        StartListening();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        var tokens = appliedTokens.Select(t => t.Token).ToList();

        // Track terminal mode state for attach replay
        foreach (var token in tokens)
        {
            if (token is PrivateModeToken pm)
            {
                switch (pm.Mode)
                {
                    case 1000 or 1002 or 1003:
                        _mouseTrackingEnabled = pm.Enable;
                        break;
                    case 1006:
                        _sgrMouseModeEnabled = pm.Enable;
                        break;
                    case 2004:
                        _bracketedPasteEnabled = pm.Enable;
                        break;
                }
            }
        }

        // Broadcast to attached clients if any
        lock (_attachLock)
        {
            if (_sessions.Count > 0)
            {
                var ansi = AnsiTokenSerializer.Serialize(tokens);
                if (ansi.Length > 0)
                {
                    var frame = new AttachFrame(AttachFrameType.Output, ansi);
                    for (var i = _sessions.Count - 1; i >= 0; i--)
                    {
                        if (!_sessions[i].Channel.Writer.TryWrite(frame))
                        {
                            // Channel full or completed — remove dead client
                            _sessions.RemoveAt(i);
                        }
                    }
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<AnsiToken>>(tokens);
    }

    /// <inheritdoc />
    public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
    {
        // We observe input but don't modify it
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
    {
        return DisposeAsync();
    }

    // === Socket Handling ===

    private void StartListening()
    {
        // Ensure socket directory exists
        var socketDir = Path.GetDirectoryName(_socketPath)!;
        Directory.CreateDirectory(socketDir);

        // Clean up stale socket file if it exists
        if (File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); }
            catch { /* ignore */ }
        }

        // Create Unix domain socket
        _listenerSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listenerSocket.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listenerSocket.Listen(5);

        // Start accepting connections
        _listenTask = AcceptConnectionsAsync(_cts.Token);
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var clientSocket = await _listenerSocket!.AcceptAsync(ct);
                // Handle each connection in a separate task (fire and forget)
                _ = HandleConnectionAsync(clientSocket, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Continue accepting on other errors
            }
        }
    }

    private async Task HandleConnectionAsync(Socket clientSocket, CancellationToken ct)
    {
        await using var stream = new NetworkStream(clientSocket, ownsSocket: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        try
        {
            // Read request (single line JSON)
            var requestLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(requestLine))
                return;

            var request = JsonSerializer.Deserialize(requestLine, DiagnosticsJsonContext.Default.DiagnosticsRequest);
            if (request == null)
            {
                await WriteErrorAsync(writer, "Invalid request format");
                return;
            }

            // Attach is special — bidirectional streaming, keeps connection open
            if (request.Method?.Equals("attach", StringComparison.OrdinalIgnoreCase) == true)
            {
                await HandleAttachAsync(reader, writer, ct);
                return;
            }

            var response = await HandleRequestAsync(request);
            var responseJson = JsonSerializer.Serialize(response, DiagnosticsJsonContext.Default.DiagnosticsResponse);
            await writer.WriteLineAsync(responseJson);
        }
        catch (Exception ex)
        {
            try
            {
                await WriteErrorAsync(writer, ex.Message);
            }
            catch { /* ignore write failures */ }
        }
    }

    /// <summary>
    /// Creates an attach session that receives terminal output and can send input.
    /// Used by external listeners (e.g., WebSocket) to integrate with the attach system.
    /// </summary>
    /// <returns>An <see cref="AttachSession"/> that must be disposed when the client disconnects.</returns>
    /// <exception cref="InvalidOperationException">The terminal is not initialized.</exception>
    public AttachSession CreateAttachSession()
    {
        if (_terminal == null)
            throw new InvalidOperationException("Terminal not initialized");

        var channel = Channel.CreateBounded<AttachFrame>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // Capture initial screen
        using var snapshot = _terminal.CreateSnapshot();
        var initialAnsi = snapshot.ToAnsi(new TerminalAnsiOptions
        {
            IncludeClearScreen = true,
            IncludeTrailingNewline = true
        });

        bool isLeader;
        var session = new AttachSession(this, channel, _terminal.Width, _terminal.Height, false, initialAnsi);

        lock (_attachLock)
        {
            _sessions.Add(session);
            if (_leaderSession == null)
            {
                _leaderSession = session;
                session.IsLeader = true;
            }
            isLeader = _leaderSession == session;
        }

        // Write mode replay as the first frame
        var modeReplay = BuildModeReplaySequence();
        if (modeReplay.Length > 0)
        {
            channel.Writer.TryWrite(new AttachFrame(AttachFrameType.Output, modeReplay));
        }

        return session;
    }

    /// <summary>
    /// Handles an attach session — bidirectional streaming over a persistent connection.
    /// Protocol: Server sends initial ANSI capture, then streams output lines prefixed with "o:".
    /// Client sends input lines prefixed with "i:". Either side can send "detach" to end.
    /// </summary>
    private async Task HandleAttachAsync(StreamReader reader, StreamWriter writer, CancellationToken ct)
    {
        if (_terminal == null)
        {
            await WriteErrorAsync(writer, "Terminal not initialized");
            return;
        }

        await using var session = CreateAttachSession();

        // Send attach response with terminal dimensions and leader status
        var attachResponse = new DiagnosticsResponse
        {
            Success = true,
            Width = session.Width,
            Height = session.Height,
            Data = session.InitialScreen,
            Leader = session.IsLeader
        };
        var responseJson = JsonSerializer.Serialize(attachResponse, DiagnosticsJsonContext.Default.DiagnosticsResponse);
        await writer.WriteLineAsync(responseJson.AsMemory(), ct);

        // Run two tasks: output streaming and input forwarding
        using var detachCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var outputTask = StreamOutputFromSessionAsync(session, writer, detachCts.Token);
        var inputTask = StreamInputToSessionAsync(session, reader, writer, detachCts);

        // Wait for either task to complete (detach or disconnect)
        await Task.WhenAny(outputTask, inputTask);
        await detachCts.CancelAsync();

        // Allow tasks to finish cleanly
        try { await Task.WhenAll(outputTask, inputTask); }
        catch (OperationCanceledException) { }
    }

    private static async Task StreamOutputFromSessionAsync(AttachSession session, StreamWriter writer, CancellationToken ct)
    {
        try
        {
            await foreach (var frame in session.Frames.ReadAllAsync(ct))
            {
                switch (frame.Type)
                {
                    case AttachFrameType.Output:
                        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(frame.Data ?? ""));
                        await writer.WriteLineAsync($"o:{base64}".AsMemory(), ct);
                        break;
                    case AttachFrameType.Resize:
                        await writer.WriteLineAsync($"r:{frame.Data}".AsMemory(), ct);
                        break;
                    case AttachFrameType.LeaderChanged:
                        await writer.WriteLineAsync($"leader:{frame.Data}".AsMemory(), ct);
                        break;
                    case AttachFrameType.Exit:
                        await writer.WriteLineAsync("exit".AsMemory(), ct);
                        return;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task StreamInputToSessionAsync(AttachSession session, StreamReader reader, StreamWriter writer, CancellationTokenSource detachCts)
    {
        try
        {
            while (!detachCts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(detachCts.Token);
                if (line == null || line == "detach")
                {
                    await detachCts.CancelAsync();
                    return;
                }

                if (line == "shutdown")
                {
                    session.RequestShutdown();
                    await detachCts.CancelAsync();
                    return;
                }

                if (line.StartsWith("i:") && _terminal != null)
                {
                    var inputBase64 = line[2..];
                    var bytes = Convert.FromBase64String(inputBase64);
                    await session.SendInputAsync(bytes);
                }
                else if (line.StartsWith("r:"))
                {
                    var parts = line[2..].Split(',');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
                    {
                        await session.SendResizeAsync(width, height);
                    }
                }
                else if (line == "lead")
                {
                    await session.ClaimLeadAsync();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { await detachCts.CancelAsync(); }
    }

    // === Internal methods called by AttachSession ===

    internal Task SendInputFromSessionAsync(AttachSession session, byte[] data)
    {
        if (_terminal == null) return Task.CompletedTask;
        return _terminal.SendInputAsync(data);
    }

    internal Task SendResizeFromSessionAsync(AttachSession session, int width, int height)
    {
        if (_terminal == null) return Task.CompletedTask;

        bool isLeader;
        lock (_attachLock) { isLeader = _leaderSession == session; }
        if (!isLeader) return Task.CompletedTask;

        _terminal.ResizeWithWorkload(width, height);

        // Broadcast new dimensions to all attached sessions except the sender
        var frame = new AttachFrame(AttachFrameType.Resize, $"{width},{height}");
        lock (_attachLock)
        {
            foreach (var s in _sessions)
            {
                if (s != session)
                    s.Channel.Writer.TryWrite(frame);
            }
        }

        return Task.CompletedTask;
    }

    internal Task ClaimLeadFromSessionAsync(AttachSession session)
    {
        AttachSession? oldLeader;
        lock (_attachLock)
        {
            oldLeader = _leaderSession;
            _leaderSession = session;
            session.IsLeader = true;
        }

        if (oldLeader != null && oldLeader != session)
        {
            oldLeader.IsLeader = false;
            oldLeader.Channel.Writer.TryWrite(new AttachFrame(AttachFrameType.LeaderChanged, "false"));
        }

        session.Channel.Writer.TryWrite(new AttachFrame(AttachFrameType.LeaderChanged, "true"));
        return Task.CompletedTask;
    }

    internal void RequestShutdownFromSession()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            await _cts.CancelAsync();
        });
    }

    internal void RemoveSession(AttachSession session)
    {
        lock (_attachLock)
        {
            _sessions.Remove(session);
            if (_leaderSession == session)
            {
                _leaderSession = _sessions.Count > 0 ? _sessions[0] : null;
                // Notify new leader
                if (_leaderSession != null)
                {
                    _leaderSession.IsLeader = true;
                    _leaderSession.Channel.Writer.TryWrite(new AttachFrame(AttachFrameType.LeaderChanged, "true"));
                }
            }
        }
    }

    private async Task<DiagnosticsResponse> HandleRequestAsync(DiagnosticsRequest request)
    {
        return request.Method?.ToLowerInvariant() switch
        {
            "info" => HandleInfoRequest(),
            "capture" => HandleCaptureRequest(request.Format, request.ScrollbackLines, request.FontFamily),
            "input" => await HandleInputRequestAsync(request.Data),
            "key" => await HandleKeyRequestAsync(request.Key, request.Modifiers),
            "click" => HandleClickRequest(request.X, request.Y, request.Button),
            "drag" => HandleDragRequest(request.X, request.Y, request.X2, request.Y2, request.Button),
            "tree" => HandleTreeRequest(),
            "resize" => HandleResizeRequest(request.X, request.Y),
            "shutdown" => HandleShutdownRequest(),
            "record-start" => await HandleRecordStartRequestAsync(request),
            "record-stop" => await HandleRecordStopRequestAsync(),
            "record-status" => HandleRecordStatusRequest(),
            _ => new DiagnosticsResponse { Success = false, Error = $"Unknown method: {request.Method}" }
        };
    }

    private DiagnosticsResponse HandleInfoRequest()
    {
        return new DiagnosticsResponse
        {
            Success = true,
            AppName = _appName,
            ProcessId = Environment.ProcessId,
            StartTime = _startTime,
            Width = _terminal?.Width ?? 0,
            Height = _terminal?.Height ?? 0,
            Recording = _recorder?.IsRecording,
            RecordingPath = _recorder?.FilePath
        };
    }

    private DiagnosticsResponse HandleCaptureRequest(string? format, int? scrollbackLines, string? fontFamily)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        format = format?.ToLowerInvariant() ?? "ansi";

        var scrollback = Math.Max(0, scrollbackLines ?? 0);
        using var snapshot = scrollback > 0
            ? _terminal.CreateSnapshot(scrollback)
            : _terminal.CreateSnapshot();
        
        string data;
        if (format == "svg")
        {
            var svgOptions = new TerminalSvgOptions { ShowCellGrid = false };
            if (fontFamily != null)
            {
                svgOptions.FontFamily = $"'{fontFamily}'";
            }
            data = snapshot.ToSvg(svgOptions);
        }
        else if (format == "html")
        {
            var htmlOptions = new TerminalSvgOptions { ShowCellGrid = false };
            if (fontFamily != null)
            {
                htmlOptions.FontFamily = $"'{fontFamily}'";
            }
            data = snapshot.ToHtml(htmlOptions);
        }
        else if (format == "ansi")
        {
            data = snapshot.ToAnsi(new TerminalAnsiOptions 
            { 
                IncludeClearScreen = true,
                IncludeTrailingNewline = true
            });
        }
        else if (format == "text")
        {
            // Plain text output - cells in row/column order without ANSI codes
            data = snapshot.GetText();
        }
        else
        {
            return new DiagnosticsResponse { Success = false, Error = $"Unknown format: {format}" };
        }

        return new DiagnosticsResponse
        {
            Success = true,
            Width = _terminal.Width,
            Height = _terminal.Height,
            Data = data
        };
    }

    private async Task<DiagnosticsResponse> HandleInputRequestAsync(string? inputData)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        if (string.IsNullOrEmpty(inputData))
        {
            return new DiagnosticsResponse { Success = false, Error = "No input data provided" };
        }

        // Send input to the terminal
        var bytes = Encoding.UTF8.GetBytes(inputData);
        await _terminal.SendInputAsync(bytes);

        return new DiagnosticsResponse
        {
            Success = true,
            Data = $"Sent {bytes.Length} bytes"
        };
    }

    private async Task<DiagnosticsResponse> HandleKeyRequestAsync(string? keyName, string[]? modifiers)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        if (string.IsNullOrEmpty(keyName))
        {
            return new DiagnosticsResponse { Success = false, Error = "No key name provided" };
        }

        // Parse key name to Hex1bKey
        if (!Enum.TryParse<Hex1bKey>(keyName, ignoreCase: true, out var key))
        {
            // Try single character
            if (keyName.Length == 1)
            {
                var c = keyName[0];
                key = c switch
                {
                    >= 'a' and <= 'z' => (Hex1bKey)((int)Hex1bKey.A + (c - 'a')),
                    >= 'A' and <= 'Z' => (Hex1bKey)((int)Hex1bKey.A + (c - 'A')),
                    _ => Hex1bKey.None
                };
            }
            else
            {
                return new DiagnosticsResponse { Success = false, Error = $"Unknown key: {keyName}" };
            }
        }

        // Parse modifiers
        var mods = Hex1bModifiers.None;
        if (modifiers != null)
        {
            foreach (var mod in modifiers)
            {
                mods |= mod.ToLowerInvariant() switch
                {
                    "alt" => Hex1bModifiers.Alt,
                    "ctrl" or "control" => Hex1bModifiers.Control,
                    "shift" => Hex1bModifiers.Shift,
                    _ => Hex1bModifiers.None
                };
            }
        }

        // Send key event via the workload adapter
        if (_terminal.Workload is Hex1bAppWorkloadAdapter workload)
        {
            workload.SendKey(key, '\0', mods);
            return new DiagnosticsResponse
            {
                Success = true,
                Data = $"Sent key {key} with modifiers {mods}"
            };
        }

        // For non-app workloads (PTY terminals), convert to raw bytes and send via input stream
        var rawBytes = KeyToBytes(key, mods);
        if (rawBytes.Length > 0)
        {
            await _terminal.SendInputAsync(rawBytes);
            return new DiagnosticsResponse
            {
                Success = true,
                Data = $"Sent key {key} with modifiers {mods} ({rawBytes.Length} bytes)"
            };
        }

        return new DiagnosticsResponse { Success = false, Error = $"Cannot convert key {key} to raw input for this terminal type" };
    }

    /// <summary>
    /// Converts a key + modifiers into raw bytes suitable for writing to a PTY input stream.
    /// </summary>
    private static byte[] KeyToBytes(Hex1bKey key, Hex1bModifiers mods)
    {
        var hasCtrl = mods.HasFlag(Hex1bModifiers.Control);
        var hasAlt = mods.HasFlag(Hex1bModifiers.Alt);

        // Single character keys with Ctrl modifier → control codes
        if (hasCtrl && key >= Hex1bKey.A && key <= Hex1bKey.Z)
        {
            byte code = (byte)(key - Hex1bKey.A + 1);
            return hasAlt ? [(byte)'\x1b', code] : [code];
        }

        // Named keys → ANSI escape sequences (or raw bytes)
        string? seq = key switch
        {
            Hex1bKey.Enter => "\r",
            Hex1bKey.Tab => "\t",
            Hex1bKey.Escape => "\x1b",
            Hex1bKey.Backspace => "\x7f",
            Hex1bKey.Delete => "\x1b[3~",
            Hex1bKey.Insert => "\x1b[2~",
            Hex1bKey.Home => "\x1b[H",
            Hex1bKey.End => "\x1b[F",
            Hex1bKey.PageUp => "\x1b[5~",
            Hex1bKey.PageDown => "\x1b[6~",
            Hex1bKey.UpArrow => "\x1b[A",
            Hex1bKey.DownArrow => "\x1b[B",
            Hex1bKey.RightArrow => "\x1b[C",
            Hex1bKey.LeftArrow => "\x1b[D",
            Hex1bKey.F1 => "\x1bOP",
            Hex1bKey.F2 => "\x1bOQ",
            Hex1bKey.F3 => "\x1bOR",
            Hex1bKey.F4 => "\x1bOS",
            Hex1bKey.F5 => "\x1b[15~",
            Hex1bKey.F6 => "\x1b[17~",
            Hex1bKey.F7 => "\x1b[18~",
            Hex1bKey.F8 => "\x1b[19~",
            Hex1bKey.F9 => "\x1b[20~",
            Hex1bKey.F10 => "\x1b[21~",
            Hex1bKey.F11 => "\x1b[23~",
            Hex1bKey.F12 => "\x1b[24~",
            Hex1bKey.Spacebar => " ",
            // Single letter keys without Ctrl
            >= Hex1bKey.A and <= Hex1bKey.Z => ((char)('a' + (key - Hex1bKey.A))).ToString(),
            _ => null
        };

        if (seq == null) return [];

        // Wrap with Alt (ESC prefix)
        if (hasAlt && !seq.StartsWith('\x1b'))
        {
            seq = "\x1b" + seq;
        }

        return Encoding.UTF8.GetBytes(seq);
    }

    private DiagnosticsResponse HandleClickRequest(int? x, int? y, string? button)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        if (x == null || y == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Missing x or y coordinates" };
        }

        // Parse button
        var mouseButton = button?.ToLowerInvariant() switch
        {
            "left" or null => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        // Send mouse event via the workload adapter
        if (_terminal.Workload is Hex1bAppWorkloadAdapter workload)
        {
            // Send press then release for a click
            workload.SendMouse(mouseButton, MouseAction.Down, x.Value, y.Value);
            workload.SendMouse(mouseButton, MouseAction.Up, x.Value, y.Value);
            return new DiagnosticsResponse
            {
                Success = true,
                Data = $"Clicked at ({x}, {y}) with {mouseButton}"
            };
        }

        return new DiagnosticsResponse { Success = false, Error = "Terminal workload does not support direct mouse injection" };
    }

    private DiagnosticsResponse HandleDragRequest(int? x1, int? y1, int? x2, int? y2, string? button)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        if (x1 == null || y1 == null || x2 == null || y2 == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Missing coordinates (x, y, x2, y2 required)" };
        }

        var mouseButton = button?.ToLowerInvariant() switch
        {
            "left" or null => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };

        if (_terminal.Workload is Hex1bAppWorkloadAdapter workload)
        {
            // Mouse down at start
            workload.SendMouse(mouseButton, MouseAction.Down, x1.Value, y1.Value);

            // Interpolate drag events from start to end
            int dx = x2.Value - x1.Value;
            int dy = y2.Value - y1.Value;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps == 0) steps = 1;

            for (int i = 1; i <= steps; i++)
            {
                int cx = x1.Value + (dx * i / steps);
                int cy = y1.Value + (dy * i / steps);
                workload.SendMouse(mouseButton, MouseAction.Drag, cx, cy);
            }

            // Mouse up at end
            workload.SendMouse(mouseButton, MouseAction.Up, x2.Value, y2.Value);

            return new DiagnosticsResponse
            {
                Success = true,
                Data = $"Dragged from ({x1}, {y1}) to ({x2}, {y2}) with {mouseButton}"
            };
        }

        return new DiagnosticsResponse { Success = false, Error = "Terminal workload does not support direct mouse injection" };
    }

    private DiagnosticsResponse HandleTreeRequest()
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        // Get the diagnostic tree provider from the workload adapter
        if (_terminal.Workload is Hex1bAppWorkloadAdapter workload && workload.DiagnosticTreeProvider is { } provider)
        {
            return new DiagnosticsResponse
            {
                Success = true,
                Width = _terminal.Width,
                Height = _terminal.Height,
                Tree = provider.GetDiagnosticTree(),
                Popups = provider.GetDiagnosticPopups(),
                FocusInfo = provider.GetDiagnosticFocusInfo(),
                FrameInfo = provider.GetDiagnosticFrameInfo()
            };
        }

        return new DiagnosticsResponse { Success = false, Error = "No diagnostic tree provider available" };
    }

    private DiagnosticsResponse HandleResizeRequest(int? width, int? height)
    {
        if (_terminal == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Terminal not initialized" };
        }

        var newWidth = width ?? _terminal.Width;
        var newHeight = height ?? _terminal.Height;

        _terminal.ResizeWithWorkload(newWidth, newHeight);

        return new DiagnosticsResponse
        {
            Success = true,
            Width = _terminal.Width,
            Height = _terminal.Height
        };
    }

    private DiagnosticsResponse HandleShutdownRequest()
    {
        // Signal that we should shut down - cancel the listener
        // The host process watches for this and exits gracefully
        _ = Task.Run(async () =>
        {
            await Task.Delay(100); // Allow the response to be sent first
            await _cts.CancelAsync();
        });

        return new DiagnosticsResponse { Success = true, Data = "Shutting down" };
    }

    private static async Task WriteErrorAsync(StreamWriter writer, string error)
    {
        var response = new DiagnosticsResponse { Success = false, Error = error };
        var json = JsonSerializer.Serialize(response, DiagnosticsJsonContext.Default.DiagnosticsResponse);
        await writer.WriteLineAsync(json);
    }

    private string BuildModeReplaySequence()
    {
        var sb = new System.Text.StringBuilder();
        if (_mouseTrackingEnabled)
        {
            sb.Append("\x1b[?1000h");
            sb.Append("\x1b[?1002h");
            sb.Append("\x1b[?1003h");
        }
        if (_sgrMouseModeEnabled)
        {
            sb.Append("\x1b[?1006h");
        }
        if (_bracketedPasteEnabled)
        {
            sb.Append("\x1b[?2004h");
        }
        return sb.ToString();
    }

    private async Task<DiagnosticsResponse> HandleRecordStartRequestAsync(DiagnosticsRequest request)
    {
        if (_recorder == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Recording is not available on this terminal" };
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return new DiagnosticsResponse { Success = false, Error = "filePath is required" };
        }

        if (_recorder.IsRecording)
        {
            return new DiagnosticsResponse
            {
                Success = false,
                Error = $"Already recording to '{_recorder.FilePath}'. Stop it first."
            };
        }

        // Ensure the output directory exists
        var dir = Path.GetDirectoryName(request.FilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new AsciinemaRecorderOptions
        {
            AutoFlush = true,
            Title = request.Title,
            IdleTimeLimit = request.IdleLimit is > 0 ? (float)request.IdleLimit.Value : null
        };

        _recorder.StartRecording(request.FilePath, options);

        // Synthesize current terminal state as the first event
        if (_terminal != null)
        {
            using var snapshot = _terminal.CreateSnapshot();
            var initialAnsi = snapshot.ToAnsi(new TerminalAnsiOptions
            {
                IncludeClearScreen = true,
                IncludeTrailingNewline = true
            });
            await _recorder.WriteInitialStateAsync(initialAnsi);
        }

        return new DiagnosticsResponse
        {
            Success = true,
            Data = $"Recording started: {request.FilePath}",
            Recording = true,
            RecordingPath = request.FilePath
        };
    }

    private async Task<DiagnosticsResponse> HandleRecordStopRequestAsync()
    {
        if (_recorder == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Recording is not available on this terminal" };
        }

        if (!_recorder.IsRecording)
        {
            return new DiagnosticsResponse { Success = false, Error = "Not currently recording" };
        }

        var completedPath = await _recorder.StopRecordingAsync();

        return new DiagnosticsResponse
        {
            Success = true,
            Data = completedPath != null ? $"Recording saved: {completedPath}" : "Recording stopped",
            Recording = false,
            RecordingPath = completedPath
        };
    }

    private DiagnosticsResponse HandleRecordStatusRequest()
    {
        if (_recorder == null)
        {
            return new DiagnosticsResponse { Success = false, Error = "Recording is not available on this terminal" };
        }

        return new DiagnosticsResponse
        {
            Success = true,
            Recording = _recorder.IsRecording,
            RecordingPath = _recorder.FilePath
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Signal cancellation
        await _cts.CancelAsync();

        // Close listener socket (will unblock Accept)
        _listenerSocket?.Close();
        _listenerSocket?.Dispose();

        // Wait for listen task to complete
        if (_listenTask != null)
        {
            try { await _listenTask; }
            catch { /* ignore */ }
        }

        // Clean up socket file
        try
        {
            if (File.Exists(_socketPath))
                File.Delete(_socketPath);
        }
        catch { /* ignore */ }

        _cts.Dispose();
    }
}
