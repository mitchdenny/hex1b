# Using the Emulator

This tutorial walks you through building a web-based terminal application step by step. By the end, you'll have a page with four terminals running different processes—all powered by Hex1b's terminal emulator, WebSocket streaming, and xterm.js.

## What You'll Build

A web app with four independent terminals in a grid:
- **Star Wars** — SSH to `starwarstel.net` for the ASCII movie
- **CMatrix** — Matrix-style falling code
- **Pipes** — Animated pipes screensaver  
- **Asciiquarium** — Underwater ASCII art

Each terminal runs its own process in a PTY, streams output over WebSocket, and renders in the browser with xterm.js.

## Prerequisites

- .NET 10 SDK
- Docker (for CMatrix, Pipes, Asciiquarium)
- SSH client (for Star Wars)

## Step 1: Create the Project

Start with a minimal ASP.NET Core web app:

```bash
dotnet new web -n QuadTerminalDemo
cd QuadTerminalDemo
dotnet add package Hex1b
```

## Step 2: Set Up the WebSocket Backend

The backend needs to:
1. Accept WebSocket connections
2. Create a terminal with a PTY process
3. Bridge the terminal to the WebSocket using a presentation adapter

Here's the core pattern:

```csharp
using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

app.Map("/ws/terminal", async context =>
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
        .WithPtyProcess("bash", "-i")
        .Build();

    await terminal.RunAsync(context.RequestAborted);
});

app.Run();
```

Let's break this down:

### WebSocketPresentationAdapter

This adapter bridges `Hex1bTerminal` to a WebSocket:
- **Sends** terminal output (ANSI sequences) to the WebSocket as text messages
- **Receives** user input from the WebSocket and forwards to the terminal
- **Handles resize** messages (JSON) to resize the PTY

### WithPtyProcess

Creates a pseudo-terminal (PTY) and runs the specified command inside it. The PTY provides:
- Proper terminal semantics (line discipline, signals)
- Correct handling of interactive programs
- Support for terminal resize

## Step 3: Create the Frontend

Create `wwwroot/index.html` with xterm.js:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Terminal Demo</title>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.min.css">
    <style>
        html, body { height: 100%; margin: 0; background: #0a0a12; }
        #terminal { height: 100%; padding: 8px; }
    </style>
</head>
<body>
    <div id="terminal"></div>
    <script type="module">
        import { Terminal } from 'https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/+esm';
        import { FitAddon } from 'https://cdn.jsdelivr.net/npm/@xterm/addon-fit@0.10.0/+esm';

        const term = new Terminal({ cursorBlink: true });
        const fitAddon = new FitAddon();
        term.loadAddon(fitAddon);
        term.open(document.getElementById('terminal'));
        fitAddon.fit();

        const ws = new WebSocket(`ws://${location.host}/ws/terminal`);
        
        // Terminal output from server
        ws.onmessage = e => term.write(e.data);
        
        // Send initial size on connect
        ws.onopen = () => ws.send(JSON.stringify({ 
            type: 'resize', 
            cols: term.cols, 
            rows: term.rows 
        }));
        
        // User input to server
        term.onData(data => ws.send(data));
        
        // Handle resize
        term.onResize(({ cols, rows }) => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'resize', cols, rows }));
            }
        });
        
        window.onresize = () => fitAddon.fit();
    </script>
</body>
</html>
```

Key points:
- **FitAddon** automatically sizes the terminal to fill its container
- **onResize** fires when terminal dimensions change (after `fitAddon.fit()`)
- Resize messages are JSON: `{ "type": "resize", "cols": 80, "rows": 24 }`

## Step 4: Add Multiple Terminals

Now let's scale to four terminals with different processes.

### Update Program.cs

Create a helper function and add endpoints for each terminal type:

```csharp
using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();
app.UseDefaultFiles();
app.UseStaticFiles();

// Helper to create WebSocket terminal endpoint
async Task HandleTerminal(HttpContext context, params string[] command)
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
app.Map("/ws/starwars", ctx => HandleTerminal(ctx, "ssh", "starwarstel.net"));

// CMatrix - Matrix-style falling code
app.Map("/ws/cmatrix", ctx => HandleTerminal(ctx, 
    "docker", "run", "-it", "--rm", "--log-driver", "none", 
    "--net", "none", "--read-only", "--cap-drop=ALL", "willh/cmatrix"));

// Pipes - animated pipes screensaver
app.Map("/ws/pipes", ctx => HandleTerminal(ctx, "docker", "run", "--rm", "-it", "joonas/pipes.sh"));

// Asciiquarium - underwater ASCII art
app.Map("/ws/asciiquarium", ctx => HandleTerminal(ctx, "docker", "run", "-it", "--rm", "vanessa/asciiquarium"));

app.Run();
```

### Update wwwroot/index.html

Create a grid layout with four terminal panes:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>Quad Terminal Demo</title>
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/css/xterm.min.css">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { height: 100%; background: #0a0a12; overflow: hidden; }
        .grid { 
            display: grid; 
            grid-template-columns: 1fr 1fr; 
            grid-template-rows: 1fr 1fr; 
            height: 100%; 
            gap: 8px; 
            padding: 8px; 
        }
        .terminal-pane { 
            background: #0f0f1a; 
            overflow: hidden; 
            border-radius: 6px; 
            padding: 8px; 
        }
        .terminal-pane .label { 
            color: #666; 
            font-family: monospace; 
            font-size: 11px; 
            padding-bottom: 4px; 
        }
    </style>
</head>
<body>
    <div class="grid">
        <div id="term1" class="terminal-pane"><div class="label">Star Wars (SSH)</div></div>
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
```

## Step 5: Run It

```bash
dotnet run
```

Open `http://localhost:5000` and you'll see four terminals, each running a different process. Resize the browser window and all terminals resize together.

## How It Works

The architecture:

```
┌─────────────────────────────────────────────────────────────┐
│                        Browser                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  xterm.js   │  │  xterm.js   │  │  xterm.js   │  ...    │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
└─────────┼────────────────┼────────────────┼────────────────┘
          │ WebSocket      │ WebSocket      │ WebSocket
┌─────────┼────────────────┼────────────────┼────────────────┐
│         ▼                ▼                ▼        ASP.NET │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ WebSocket   │  │ WebSocket   │  │ WebSocket   │         │
│  │ Presentation│  │ Presentation│  │ Presentation│         │
│  │   Adapter   │  │   Adapter   │  │   Adapter   │         │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
│         │                │                │                 │
│  ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐         │
│  │ Hex1bTerminal│  │ Hex1bTerminal│  │ Hex1bTerminal│        │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
│         │                │                │                 │
│  ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐         │
│  │     PTY     │  │     PTY     │  │     PTY     │         │
│  │   Process   │  │   Process   │  │   Process   │         │
│  │   (ssh)     │  │  (docker)   │  │  (docker)   │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
└─────────────────────────────────────────────────────────────┘
```

Each WebSocket connection gets:
1. Its own `WebSocketPresentationAdapter` — bridges terminal ↔ WebSocket
2. Its own `Hex1bTerminal` — processes ANSI/VT sequences, maintains screen buffer
3. Its own PTY process — runs the actual command

The terminals are completely independent—each has its own process, its own screen buffer, and its own WebSocket connection.

## Next Steps

- [Presentation Adapters](./presentation-adapters) — Build custom adapters for other UIs
- [Workload Adapters](./workload-adapters) — Run TUI apps instead of child processes
- [Terminal Emulator](./terminal-emulator) — Deep dive into terminal capabilities
