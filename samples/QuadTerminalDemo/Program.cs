using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

// Serve the HTML page with 4 xterm.js terminals in a grid
app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset="utf-8">
        <title>Quad Terminal Demo</title>
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.min.css">
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            html, body { height: 100%; background: #0a0a12; overflow: hidden; }
            .grid { display: grid; grid-template-columns: 1fr 1fr; grid-template-rows: 1fr 1fr; height: 100%; gap: 8px; padding: 8px; }
            .terminal-pane { background: #0f0f1a; overflow: hidden; border-radius: 6px; padding: 8px; }
            .terminal-pane .label { color: #666; font-family: monospace; font-size: 11px; padding-bottom: 4px; }
        </style>
    </head>
    <body>
        <div class="grid">
            <div id="term1" class="terminal-pane"><div class="label">Star Wars (telnet)</div></div>
            <div id="term2" class="terminal-pane"><div class="label">CMatrix</div></div>
            <div id="term3" class="terminal-pane"><div class="label">Pipes</div></div>
            <div id="term4" class="terminal-pane"><div class="label">Asciiquarium</div></div>
        </div>
        <script type="module">
            import { Terminal } from 'https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/+esm';
            import { FitAddon } from 'https://cdn.jsdelivr.net/npm/@xterm/addon-fit@0.10.0/+esm';

            const theme = { background: '#0f0f1a', foreground: '#e0e0e0' };
            const terminals = [];

            function createTerminal(containerId, endpoint) {
                const container = document.getElementById(containerId);
                const termDiv = document.createElement('div');
                termDiv.style.height = 'calc(100% - 20px)';
                container.appendChild(termDiv);
                
                const term = new Terminal({ cursorBlink: true, theme });
                const fitAddon = new FitAddon();
                term.loadAddon(fitAddon);
                term.open(termDiv);
                fitAddon.fit();

                const ws = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}${endpoint}`);
                ws.onmessage = e => term.write(e.data);
                ws.onopen = () => ws.send(JSON.stringify({ type: 'resize', cols: term.cols, rows: term.rows }));
                term.onData(data => ws.send(data));
                
                // Send resize when terminal dimensions actually change
                term.onResize(({ cols, rows }) => {
                    if (ws.readyState === WebSocket.OPEN) {
                        ws.send(JSON.stringify({ type: 'resize', cols, rows }));
                    }
                });

                return { term, fitAddon, ws };
            }

            terminals.push(createTerminal('term1', '/ws/starwars'));
            terminals.push(createTerminal('term2', '/ws/cmatrix'));
            terminals.push(createTerminal('term3', '/ws/pipes'));
            terminals.push(createTerminal('term4', '/ws/asciiquarium'));

            window.onresize = () => terminals.forEach(({ fitAddon }) => fitAddon.fit());
        </script>
    </body>
    </html>
    """, "text/html"));

// Helper to create WebSocket terminal endpoint
async Task HandleTerminalWebSocket(HttpContext context, params string[] command)
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await using var presentation = new WebSocketPresentationAdapter(webSocket, 80, 24);
    
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess(command[0], command[1..])
        .Build();

    await terminal.RunAsync(context.RequestAborted);
}

// Star Wars ASCII movie via SSH
app.Map("/ws/starwars", context => 
    HandleTerminalWebSocket(context, "ssh", "starwarstel.net"));

// CMatrix - the Matrix-style falling code
app.Map("/ws/cmatrix", context => 
    HandleTerminalWebSocket(context, "docker", "run", "-it", "--rm", "--log-driver", "none", "--net", "none", "--read-only", "--cap-drop=ALL", "willh/cmatrix"));

// Pipes - animated pipes screensaver
app.Map("/ws/pipes", context => 
    HandleTerminalWebSocket(context, "docker", "run", "--rm", "-it", "joonas/pipes.sh"));

// Asciiquarium - underwater ASCII art
app.Map("/ws/asciiquarium", context => 
    HandleTerminalWebSocket(context, "docker", "run", "-it", "--rm", "vanessa/asciiquarium"));

app.Run();
