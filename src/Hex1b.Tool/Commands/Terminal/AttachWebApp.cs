using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Runs a web-based attach experience using xterm.js.
/// Starts a Kestrel web server that bridges browser WebSocket connections
/// to the remote terminal's diagnostics socket.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class AttachWebApp : IAsyncDisposable
{
    private readonly string _socketPath;
    private readonly string _displayId;
    private readonly TerminalClient _client;
    private readonly int _port;

    public AttachWebApp(string socketPath, string displayId, TerminalClient client, int port)
    {
        _socketPath = socketPath;
        _displayId = displayId;
        _client = client;
        _port = port;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        // Verify the terminal exists before starting the web server
        var response = await _client.SendAsync(_socketPath,
            new DiagnosticsRequest { Method = "info" }, cancellationToken);
        if (response is not { Success: true })
        {
            Console.Error.WriteLine($"Error: Cannot connect to terminal {_displayId}");
            return 1;
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, _port);
        });
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.UseWebSockets();

        app.MapGet("/", () => Results.Content(GetHtmlPage(), "text/html"));
        app.Map("/ws/terminal", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await BridgeWebSocketAsync(ws, context.RequestAborted);
        });

        await app.StartAsync(cancellationToken);

        var url = $"http://localhost:{app.Urls.Select(u => new Uri(u).Port).First()}";
        Console.Error.WriteLine($"Web attach for {_displayId} at: {url}");
        Console.Error.WriteLine("Press Ctrl+C to detach.");

        // Try to open the browser
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Not critical if browser doesn't open
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) { }

        Console.Error.WriteLine($"Detached from {_displayId}.");
        await app.StopAsync();
        return 0;
    }

    /// <summary>
    /// Bridges a browser WebSocket to the remote terminal's diagnostics socket.
    /// The browser is always the leader — its resize events control the remote terminal.
    /// </summary>
    private async Task BridgeWebSocketAsync(WebSocket ws, CancellationToken ct)
    {
        // Connect to the remote terminal
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);
        }
        catch
        {
            await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Cannot connect to terminal", ct);
            socket.Dispose();
            return;
        }

        var networkStream = new NetworkStream(socket, ownsSocket: true);
        var reader = new StreamReader(networkStream, Encoding.UTF8);
        var writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };

        try
        {
            // Send attach request
            var request = new DiagnosticsRequest { Method = "attach" };
            var requestJson = JsonSerializer.Serialize(request, DiagnosticsJsonOptions.Default);
            await writer.WriteLineAsync(requestJson.AsMemory(), ct);

            var responseLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(responseLine))
            {
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Empty response", ct);
                return;
            }

            var response = JsonSerializer.Deserialize<DiagnosticsResponse>(responseLine, DiagnosticsJsonOptions.Default);
            if (response is not { Success: true })
            {
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, response?.Error ?? "Attach failed", ct);
                return;
            }

            // Claim leadership — browser always leads
            await writer.WriteLineAsync("lead".AsMemory(), ct);
            await reader.ReadLineAsync(ct); // consume leader:true

            // Send initial screen content to browser
            if (response.Data != null)
            {
                var initialBytes = Encoding.UTF8.GetBytes(response.Data);
                await ws.SendAsync(initialBytes, WebSocketMessageType.Text, true, ct);
            }

            // Bridge: remote → browser and browser → remote
            using var bridgeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var remoteToWeb = PumpRemoteToWebAsync(reader, ws, bridgeCts);
            var webToRemote = PumpWebToRemoteAsync(ws, writer, bridgeCts);

            await Task.WhenAny(remoteToWeb, webToRemote);
            await bridgeCts.CancelAsync();

            try { await Task.WhenAll(remoteToWeb, webToRemote); }
            catch (OperationCanceledException) { }
        }
        finally
        {
            try { await writer.WriteLineAsync("detach"); } catch { }
            reader.Dispose();
            await writer.DisposeAsync();
            await networkStream.DisposeAsync();
        }
    }

    /// <summary>
    /// Pumps frames from the remote terminal to the browser WebSocket.
    /// </summary>
    private static async Task PumpRemoteToWebAsync(StreamReader reader, WebSocket ws, CancellationTokenSource bridgeCts)
    {
        try
        {
            while (!bridgeCts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(bridgeCts.Token);
                if (line == null || line == "exit")
                {
                    await bridgeCts.CancelAsync();
                    return;
                }

                if (line.StartsWith("o:"))
                {
                    var bytes = Convert.FromBase64String(line[2..]);
                    var text = Encoding.UTF8.GetString(bytes);
                    await ws.SendAsync(
                        Encoding.UTF8.GetBytes(text),
                        WebSocketMessageType.Text, true, bridgeCts.Token);
                }
                // leader:true/false and r: frames are server-only; browser doesn't need them
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { await bridgeCts.CancelAsync(); }
        catch (IOException) { await bridgeCts.CancelAsync(); }
    }

    /// <summary>
    /// Pumps input from the browser WebSocket to the remote terminal.
    /// Handles both raw terminal input and JSON resize messages from xterm.js.
    /// </summary>
    private static async Task PumpWebToRemoteAsync(WebSocket ws, StreamWriter writer, CancellationTokenSource bridgeCts)
    {
        var buffer = new byte[4096];
        try
        {
            while (!bridgeCts.Token.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, bridgeCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await bridgeCts.CancelAsync();
                    return;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // Check for JSON resize messages from xterm.js
                if (text.StartsWith("{") && text.Contains("\"resize\""))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("type", out var type) && type.GetString() == "resize"
                            && root.TryGetProperty("cols", out var cols) && root.TryGetProperty("rows", out var rows))
                        {
                            var resizeFrame = $"r:{cols.GetInt32()},{rows.GetInt32()}";
                            await writer.WriteLineAsync(resizeFrame.AsMemory(), bridgeCts.Token);
                        }
                    }
                    catch (JsonException) { }
                }
                else
                {
                    // Regular terminal input
                    var b64 = Convert.ToBase64String(buffer, 0, result.Count);
                    await writer.WriteLineAsync($"i:{b64}".AsMemory(), bridgeCts.Token);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { await bridgeCts.CancelAsync(); }
    }

    private string GetHtmlPage() => """
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8">
            <title>Hex1b - __DISPLAY_ID__</title>
            <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.min.css">
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                html, body {
                    height: 100%;
                    background: #1a1a2e;
                    overflow: hidden;
                    font-family: system-ui, -apple-system, sans-serif;
                }
                .container {
                    display: flex;
                    flex-direction: column;
                    height: 100%;
                    padding: 12px;
                    gap: 8px;
                }
                .header {
                    color: #888;
                    font-size: 13px;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }
                .header h1 {
                    font-size: 15px;
                    color: #ccc;
                    font-weight: 500;
                }
                .status {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                }
                .status-dot {
                    width: 8px;
                    height: 8px;
                    border-radius: 50%;
                    background: #666;
                }
                .status-dot.connected { background: #4ade80; }
                .status-dot.disconnected { background: #ef4444; }
                .terminal-container {
                    flex: 1;
                    background: #0f0f1a;
                    border-radius: 8px;
                    border: 1px solid #333;
                    padding: 8px;
                    overflow: hidden;
                }
                #terminal {
                    height: 100%;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1>hex1b attach &mdash; __DISPLAY_ID__</h1>
                    <div class="status">
                        <span id="status-text">Connecting...</span>
                        <div id="status-dot" class="status-dot"></div>
                    </div>
                </div>
                <div class="terminal-container">
                    <div id="terminal"></div>
                </div>
            </div>

            <script type="module">
                import { Terminal } from 'https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/+esm';
                import { FitAddon } from 'https://cdn.jsdelivr.net/npm/@xterm/addon-fit@0.10.0/+esm';
                import { ImageAddon } from 'https://cdn.jsdelivr.net/npm/@xterm/addon-image@0.8.0/+esm';

                const statusDot = document.getElementById('status-dot');
                const statusText = document.getElementById('status-text');

                function setStatus(connected) {
                    statusDot.className = 'status-dot ' + (connected ? 'connected' : 'disconnected');
                    statusText.textContent = connected ? 'Connected' : 'Disconnected';
                }

                const term = new Terminal({
                    cursorBlink: true,
                    theme: {
                        background: '#0f0f1a',
                        foreground: '#e0e0e0',
                        cursor: '#e0e0e0'
                    },
                    fontFamily: 'Menlo, Monaco, "Courier New", monospace',
                    fontSize: 14,
                    allowProposedApi: true
                });

                const fitAddon = new FitAddon();
                const imageAddon = new ImageAddon();
                term.loadAddon(fitAddon);
                term.loadAddon(imageAddon);
                term.open(document.getElementById('terminal'));
                fitAddon.fit();

                const wsProtocol = location.protocol === 'https:' ? 'wss' : 'ws';
                const ws = new WebSocket(`${wsProtocol}://${location.host}/ws/terminal`);

                ws.onopen = () => {
                    setStatus(true);
                    ws.send(JSON.stringify({
                        type: 'resize',
                        cols: term.cols,
                        rows: term.rows
                    }));
                };

                ws.onmessage = e => term.write(e.data);

                ws.onclose = () => {
                    setStatus(false);
                    term.write('\r\n\x1b[31mDisconnected\x1b[0m\r\n');
                };

                ws.onerror = () => {
                    setStatus(false);
                };

                term.onData(data => {
                    if (ws.readyState === WebSocket.OPEN) ws.send(data);
                });

                term.onResize(({ cols, rows }) => {
                    if (ws.readyState === WebSocket.OPEN) {
                        ws.send(JSON.stringify({ type: 'resize', cols, rows }));
                    }
                });

                window.addEventListener('resize', () => fitAddon.fit());
                setTimeout(() => fitAddon.fit(), 100);
            </script>
        </body>
        </html>
        """.Replace("__DISPLAY_ID__", _displayId);

    public ValueTask DisposeAsync() => default;
}
