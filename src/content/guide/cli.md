# CLI Tool

The Hex1b CLI (`dotnet hex1b`) gives you hands-on access to Hex1b's diagnostic capabilities from the command line. Start terminal sessions, attach to running applications, capture screenshots and recordings, send input, and inspect widget trees—all without writing code.

## Why a CLI?

Hex1b applications that call `.WithDiagnostics()` expose a Unix socket for remote interaction. The CLI tool connects to these sockets and provides a human-friendly interface for:

- **Debugging** — Attach to a running TUI app to see exactly what's on screen, inspect the widget tree, or check focus state
- **Automation** — Script terminal interactions in CI/CD pipelines with `keys`, `mouse`, and `assert` commands
- **Recording** — Capture terminal sessions as [asciinema](https://asciinema.org/) recordings for documentation or demos
- **Development** — Spin up hosted terminal sessions to test shell integrations without writing a full app

## Installation

Install as a global .NET tool:

```bash
dotnet tool install -g Hex1b.Tool
```

This makes the `hex1b` command available system-wide. Alternatively, install as a local tool in your project:

```bash
dotnet tool install Hex1b.Tool
```

Then invoke with `dotnet hex1b`.

## Quick Start

### Connect to a running app

Any Hex1b application that uses `.WithDiagnostics()` is automatically discoverable:

```bash
# List all discoverable terminals
hex1b terminal list

# Attach to a terminal (interactive TUI mirror)
hex1b terminal attach <id>

# Take a screenshot
hex1b capture screenshot <id>
```

### Start a hosted terminal

You can also start new terminal sessions that host shell commands:

```bash
# Start a terminal running bash
hex1b terminal start -- bash

# Start with custom dimensions
hex1b terminal start --width 120 --height 40 -- htop

# Start and immediately attach
hex1b terminal start --attach -- vim
```

### Send input

```bash
# Type text
hex1b keys <id> --text "hello world"

# Send special keys
hex1b keys <id> --key Enter
hex1b keys <id> --key Tab --ctrl

# Click at coordinates
hex1b mouse click <id> 10 5
```

### Capture output

```bash
# Screenshot as plain text
hex1b capture screenshot <id>

# Screenshot as SVG with colors
hex1b capture screenshot <id> --format svg --output screen.svg

# Record a session
hex1b capture recording start <id> --output demo.cast
hex1b capture recording stop <id>

# Play it back
hex1b capture recording playback --file demo.cast
```

## Making Your App Discoverable

For the CLI to find your application, call `.WithDiagnostics()` on your terminal builder:

```csharp
using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.Text("Hello from Hex1b!"))
    .WithDiagnostics()  // Enables CLI discovery
    .Build();

await terminal.RunAsync();
```

This creates a Unix socket that the CLI tool uses to communicate with your application. The socket is automatically cleaned up when the application exits.

::: tip Terminal IDs
Each discoverable terminal gets an ID. You can use the full ID or just a unique prefix — `hex1b terminal info abc` will match a terminal with ID `abc123...`.
:::

## Command Overview

The CLI is organized into command groups:

| Command | Purpose |
|---------|---------|
| [`terminal`](/reference/cli#terminal) | Manage terminal sessions — list, start, stop, attach, resize |
| [`capture`](/reference/cli#capture) | Screenshots and recordings |
| [`keys`](/reference/cli#keys) | Send keyboard input |
| [`mouse`](/reference/cli#mouse) | Send mouse input |
| [`app`](/reference/cli#app) | Inspect TUI widget/node trees |
| [`assert`](/reference/cli#assert) | Assert on terminal content for scripting and CI |
| [`agent`](/reference/cli#agent) | AI agent integration |

### Global Options

All commands support:

| Option | Description |
|--------|-------------|
| `--json` | Output results as JSON (useful for scripting) |

## Attaching to Terminals

The `attach` command creates an interactive mirror of a running terminal session:

```bash
hex1b terminal attach <id>
```

This opens a full TUI that mirrors the remote terminal in real-time. You can type, navigate, and interact just as if you were running the application directly.

**Keyboard shortcuts while attached:**

| Key | Action |
|-----|--------|
| `Ctrl+]` | Open command menu |

### Web Attach

You can also attach via a web browser using xterm.js:

```bash
hex1b terminal attach <id> --web
hex1b terminal attach <id> --web --port 8080
```

This starts a local web server and opens a browser tab with a full terminal interface.

### Resize Leadership

When multiple clients are attached, only the leader's resize events affect the remote terminal:

```bash
hex1b terminal attach <id> --resize --lead
```

## Recording Sessions

Record terminal sessions in the [asciinema](https://asciinema.org/) `.cast` format:

```bash
# Start recording
hex1b capture recording start <id> --output demo.cast --title "My Demo"

# Check recording status
hex1b capture recording status <id>

# Stop recording
hex1b capture recording stop <id>
```

### Playback

Play back recordings directly in your terminal:

```bash
# Simple playback to stdout
hex1b capture recording playback --file demo.cast

# Interactive TUI player with controls
hex1b capture recording playback --file demo.cast --player

# Adjust playback speed
hex1b capture recording playback --file demo.cast --speed 2.0
```

The `--player` flag opens an interactive TUI with play/pause, seeking, speed controls, and chapter navigation from recording markers.

### Idle Limiting

Use `--idle-limit` to cap long pauses in recordings:

```bash
hex1b capture recording start <id> --output demo.cast --idle-limit 2.0
```

This ensures no gap between frames exceeds the specified number of seconds, making recordings more watchable.

## Scripting and CI

The CLI is designed for scriptability. Use `--json` for machine-readable output and `assert` for CI checks:

```bash
# Wait for text to appear (with timeout)
hex1b assert <id> --text-present "Ready" --timeout 10

# Assert text is NOT on screen
hex1b assert <id> --text-absent "Error"

# Combine with other commands in scripts
hex1b terminal start -- ./my-app
hex1b assert abc --text-present "Welcome" --timeout 30
hex1b keys abc --text "admin"
hex1b keys abc --key Enter
hex1b assert abc --text-present "Dashboard"
hex1b capture screenshot abc --format svg --output result.svg
hex1b terminal stop abc
```

## Next Steps

- **[CLI Reference](/reference/cli)** — Complete command reference with all options
- **[MCP Server](/guide/mcp-server)** — Expose terminals to AI agents via MCP
- **[Testing](/guide/testing)** — Programmatic testing with the Hex1b library
