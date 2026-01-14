# Terminal Emulator

## Why a Terminal Emulator with an API?

When automating or testing terminal applications, many tools rely on simple stdin/stdout redirection. While this works for basic scenarios, it quickly falls short for real-world terminal applications:

- **No screen state**: Stdin/stdout gives you a stream of bytes, not a structured view of what's on screen. You can't easily ask "what text is at row 5, column 10?" or "is the cursor blinking?"
- **No timing context**: Terminal applications use escape sequences to position text, change colors, and manage the display. Raw byte streams don't tell you when a "frame" is complete.
- **No interactivity**: Testing a TUI application means simulating user input and verifying visual output—you need something that understands terminal semantics.
- **No embedability**: If you want to host a shell or another TUI inside your application (think VS Code's integrated terminal), you need a terminal emulator that maintains screen state in-process.

Hex1b's `Hex1bTerminal` is a **headless terminal emulator**—it processes ANSI/VT escape sequences and maintains an in-memory screen buffer, but doesn't render to any display by itself. This separation is intentional:

> **Headless by design**: `Hex1bTerminal` owns the terminal state (screen buffer, cursor, attributes). **Presentation adapters** bridge that state to whatever UI you're targeting—the local console, a web browser, a GUI window, or nothing at all (for testing).

This architecture enables:
- **Automation**: Programmatically read screen content, wait for specific text, inject keystrokes
- **Testing**: Run TUI applications in CI/CD without a real terminal
- **Embedding**: Host shells, editors, or other terminal programs inside your application
- **Remote terminals**: Stream terminal state to web clients or other processes

## Quick Start: Quad Terminal Demo

Here's a web app with 4 independent terminals in a grid—each WebSocket connection spawns its own Docker container. Resize the browser and all terminals resize together:

```csharp
using System.Net.WebSockets;
using Hex1b;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

// Serve HTML with 4 xterm.js terminals in a 2x2 grid
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
            .grid { display: grid; grid-template-columns: 1fr 1fr; grid-template-rows: 1fr 1fr; height: 100%; gap: 2px; }
            .terminal-pane { background: #0f0f1a; overflow: hidden; }
        </style>
    </head>
    <body>
        <div class="grid">
            <div id="term1" class="terminal-pane"></div>
            <div id="term2" class="terminal-pane"></div>
            <div id="term3" class="terminal-pane"></div>
            <div id="term4" class="terminal-pane"></div>
        </div>
        <script type="module">
            import { Terminal } from 'https://cdn.jsdelivr.net/npm/@xterm/xterm@5.5.0/+esm';
            import { FitAddon } from 'https://cdn.jsdelivr.net/npm/@xterm/addon-fit@0.10.0/+esm';

            const theme = { background: '#0f0f1a', foreground: '#e0e0e0' };
            const terminals = [];

            function createTerminal(containerId) {
                const term = new Terminal({ cursorBlink: true, theme });
                const fitAddon = new FitAddon();
                term.loadAddon(fitAddon);
                term.open(document.getElementById(containerId));
                fitAddon.fit();

                const ws = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`);
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

            ['term1', 'term2', 'term3', 'term4'].forEach(id => terminals.push(createTerminal(id)));

            window.onresize = () => terminals.forEach(({ fitAddon }) => fitAddon.fit());
        </script>
    </body>
    </html>
    """, "text/html"));

// WebSocket endpoint - each connection gets its own Docker container
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    await using var presentation = new WebSocketPresentationAdapter(webSocket, 80, 24);
    
    using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPresentation(presentation)
        .WithPtyProcess("docker", "run", "--rm", "-it", "joonas/pipes.sh")
        .Build();

    await terminal.RunAsync(context.RequestAborted);
});

app.Run();
```

This demonstrates:
- **Multiple independent terminals**: Each WebSocket spawns its own PTY process
- **Grid layout with resize**: All 4 terminals resize together when the window changes
- **WebSocket streaming**: Real-time terminal output to xterm.js
- **Isolated containers**: 4 Docker containers running independently

See the full working sample in [`samples/QuadTerminalDemo`](https://github.com/mitchdenny/hex1b/tree/main/samples/QuadTerminalDemo).

## Key Features

### Full VT/ANSI Support

Hex1b's terminal emulator supports the full range of terminal escape sequences:

- **Text formatting** — Colors (16, 256, and 24-bit), bold, italic, underline, strikethrough
- **Cursor control** — Positioning, visibility, shape changes
- **Screen management** — Scrolling regions, alternate screen buffer
- **Mouse support** — Click, drag, and scroll event reporting
- **Unicode** — Full Unicode support including emoji and complex scripts

### Child Process Integration

Run any command with proper PTY (pseudo-terminal) support:

```csharp
// Start an interactive shell with PTY
await Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "-i")
    .RunAsync();

// Or run a command without PTY (for build tools, scripts)
await Hex1bTerminal.CreateBuilder()
    .WithProcess("dotnet", "build")
    .WithHeadless()
    .RunAsync();
```

### Programmatic Control

Read and write to the terminal programmatically:

```csharp
// Send input to the terminal
terminal.SendInput("ls -la\n");

// Read the current screen content
var screen = terminal.GetScreenContent();

// Wait for specific output
await terminal.WaitForTextAsync("$");
```

## Architecture

The terminal emulator consists of several components:

| Component | Purpose |
|-----------|---------|
| `Hex1bTerminal` | Headless terminal emulator — maintains screen state |
| `Hex1bTerminal.CreateBuilder()` | Fluent configuration builder |
| `IHex1bTerminalPresentationAdapter` | Bridges terminal state to a display |
| `IHex1bTerminalWorkloadAdapter` | Manages what runs inside the terminal |

### Presentation Adapters

Since `Hex1bTerminal` is headless, you need a presentation adapter to display its content:

- **`ConsolePresentationAdapter`** — Render to `System.Console` (the default)
- **`HeadlessPresentationAdapter`** — No rendering (for testing/automation)
- **Custom adapters** — Implement your own for GUIs, web, etc.

See [Presentation Adapters](./presentation-adapters) for details.

## Use Cases

### Embedding in a TUI

Combine the terminal emulator with Hex1b's TUI framework:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithSize(80, 24)
    .Build();

var app = new Hex1bApp(ctx =>
    ctx.VStack(v => [
        v.Text("My Terminal App"),
        v.Terminal(terminal).Fill(),
        v.InfoBar("Ctrl+D: Exit")
    ])
);
```

### Building Dev Tools

Create development tools with integrated terminals:

```csharp
var app = new Hex1bApp(ctx =>
    ctx.Splitter(
        left: ctx.Border(b => [
            b.Text("Files"),
            b.List(files)
        ]),
        right: ctx.Terminal(terminal)
    )
);
```

### Automation & Testing

See the [Automation & Testing](/guide/testing) guide for using the terminal emulator in testing scenarios.

## Related Topics

- [Pluggable Terminal Emulator](./pluggable-terminal-emulator) — Architecture overview
- [Presentation Adapters](./presentation-adapters) — Custom display handling
- [Workload Adapters](./workload-adapters) — Custom workload types
- [MCP Server](/guide/mcp-server) — Expose terminals to AI agents
- [Testing](/guide/testing) — Automate terminal interactions
