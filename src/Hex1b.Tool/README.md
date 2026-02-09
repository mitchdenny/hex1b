# Hex1b.Tool

A command-line tool for managing and interacting with Hex1b terminal applications. Provides terminal hosting, screen capture, input injection, widget tree inspection, session recording, and more.

[![NuGet](https://img.shields.io/nuget/v/Hex1b.Tool.svg)](https://www.nuget.org/packages/Hex1b.Tool)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

Install as a .NET global tool:

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

Start new terminal sessions that host shell commands:

```bash
# Start a terminal running bash
hex1b terminal start -- bash

# Start with custom dimensions
hex1b terminal start --width 120 --height 40 -- htop

# Start and immediately attach
hex1b terminal start --attach -- vim
```

## Commands

### `terminal` — Manage terminal sessions

| Command | Description |
|---------|-------------|
| `terminal list` | List all discoverable terminals |
| `terminal start` | Start a hosted terminal session |
| `terminal stop` | Stop a hosted terminal |
| `terminal info` | Show terminal metadata and details |
| `terminal attach` | Attach to a terminal with an interactive TUI mirror |
| `terminal resize` | Resize terminal dimensions |
| `terminal clean` | Remove stale diagnostic sockets |

### `capture` — Screenshots and recordings

| Command | Description |
|---------|-------------|
| `capture screenshot` | Capture terminal screen (text, ANSI, SVG, HTML, or PNG) |
| `capture recording start` | Begin recording a session to a `.cast` file |
| `capture recording stop` | Stop an active recording |
| `capture recording status` | Check recording status |
| `capture recording playback` | Play back a `.cast` recording |

### `keys` — Send keyboard input

```bash
hex1b keys <id> --text "hello world"
hex1b keys <id> --key Enter
hex1b keys <id> --key Tab --ctrl
```

### `mouse` — Send mouse input

| Command | Description |
|---------|-------------|
| `mouse click` | Click at coordinates |
| `mouse drag` | Drag between coordinates |

### `app` — Inspect widget trees

| Command | Description |
|---------|-------------|
| `app tree` | Show the widget/node tree of a running application |

### `assert` — Validate terminal content

Assert on terminal content for scripting and CI:

```bash
hex1b assert <id> --text-present "Ready" --timeout 10
hex1b assert <id> --text-absent "Error"
```

### `agent` — AI agent integration

| Command | Description |
|---------|-------------|
| `agent init` | Generate a skill file for AI coding agents |
| `agent mcp` | Start an MCP server |

### Global Options

| Option | Description |
|--------|-------------|
| `--json` | Output results as JSON for scripting |

## Making Your App Discoverable

For the CLI to find your application, call `.WithDiagnostics()` on your terminal builder:

```csharp
using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.Text("Hello from Hex1b!"))
    .WithDiagnostics()
    .Build();

await terminal.RunAsync();
```

## Scripting and CI

The CLI is designed for scriptability. Use `--json` for machine-readable output and `assert` for CI checks:

```bash
hex1b terminal start -- ./my-app
hex1b assert abc --text-present "Welcome" --timeout 30
hex1b keys abc --text "admin"
hex1b keys abc --key Enter
hex1b assert abc --text-present "Dashboard"
hex1b capture screenshot abc --format svg --output result.svg
hex1b capture screenshot abc --format png --output result.png
hex1b terminal stop abc
```

## Requirements

- .NET 10.0 or later
- Linux or macOS (PTY support via native interop)

## Documentation

- [CLI Guide](https://hex1b.dev/guide/cli) — Getting started and use cases
- [CLI Reference](https://hex1b.dev/reference/cli) — Complete command reference
- [GitHub Repository](https://github.com/mitchdenny/hex1b)

## License

MIT — See [LICENSE](https://github.com/mitchdenny/hex1b/blob/main/LICENSE) file in the repository.
