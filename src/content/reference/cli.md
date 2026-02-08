# CLI Reference

Complete reference for the Hex1b CLI tool (`dotnet hex1b` / `hex1b`).

For an introduction and usage guide, see [CLI Tool](/guide/cli).

## Global Options

These options apply to all commands:

| Option | Type | Description |
|--------|------|-------------|
| `--json` | flag | Output results as JSON |

---

## `terminal`

Manage terminal lifecycle, metadata, and connections.

### `terminal list`

List all known terminals.

```bash
hex1b terminal list
```

Discovers terminals via their diagnostics sockets. Shows terminal ID, dimensions, process info, and recording status.

### `terminal start`

Start a hosted terminal.

```bash
hex1b terminal start [options] -- <command...>
```

| Argument | Description |
|----------|-------------|
| `command` | Command and arguments to run (after `--`) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--width` | int | `120` | Terminal width in columns |
| `--height` | int | `30` | Terminal height in rows |
| `--cwd` | string | | Working directory for the command |
| `--record` | string | | Record session to an asciinema `.cast` file |
| `--attach` | flag | | Immediately attach to the terminal after starting |

**Examples:**

```bash
# Start bash with default dimensions
hex1b terminal start -- bash

# Start htop in a custom-sized terminal
hex1b terminal start --width 160 --height 50 -- htop

# Start and attach immediately
hex1b terminal start --attach -- vim README.md

# Start with recording
hex1b terminal start --record session.cast -- bash
```

### `terminal stop`

Stop a hosted terminal.

```bash
hex1b terminal stop <id>
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

### `terminal info`

Show terminal details.

```bash
hex1b terminal info <id>
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

Returns terminal dimensions, cursor position, process information, recording status, and other metadata.

### `terminal attach`

Attach to a terminal with an interactive TUI mirror.

```bash
hex1b terminal attach <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Description |
|--------|------|-------------|
| `--resize` | flag | Resize remote terminal to match local terminal dimensions |
| `--lead` | flag | Claim resize leadership (only the leader's resize events control the remote terminal) |
| `--web` | flag | Attach via a web browser using xterm.js instead of the TUI |
| `--port` | int | Port for the web server (0 for random). Only used with `--web` |

**Examples:**

```bash
# Basic attach
hex1b terminal attach abc123

# Attach and resize the remote terminal to match
hex1b terminal attach abc123 --resize --lead

# Attach via web browser
hex1b terminal attach abc123 --web --port 8080
```

**Keyboard shortcuts (TUI mode):**

| Key | Action |
|-----|--------|
| `Ctrl+]` | Open command menu |

### `terminal resize`

Resize a terminal.

```bash
hex1b terminal resize <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Description |
|--------|------|-------------|
| `--width` | int | New width in columns |
| `--height` | int | New height in rows |

At least one of `--width` or `--height` must be specified.

### `terminal clean`

Remove stale terminal sockets.

```bash
hex1b terminal clean
```

Scans for diagnostics sockets that no longer have a running process and removes them.

### `terminal host`

Run as a terminal host process (internal). This command is used internally by `terminal start` and is not intended for direct use.

---

## `capture`

Capture terminal output including screenshots and recordings.

### `capture screenshot`

Capture a terminal screen screenshot.

```bash
hex1b capture screenshot <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--format` | string | `text` | Output format: `text`, `ansi`, `svg`, or `html` |
| `--output` | string | | Save to file instead of stdout |
| `--wait` | string | | Wait for text to appear before capturing |
| `--timeout` | int | `30` | Timeout in seconds for `--wait` |
| `--scrollback` | int | `0` | Number of scrollback lines to include |

**Examples:**

```bash
# Plain text to stdout
hex1b capture screenshot abc123

# SVG with colors saved to file
hex1b capture screenshot abc123 --format svg --output screen.svg

# Wait for app to be ready, then capture
hex1b capture screenshot abc123 --wait "Ready" --timeout 10 --format ansi

# Include scrollback history
hex1b capture screenshot abc123 --scrollback 100
```

### `capture recording start`

Start recording a terminal session in asciinema `.cast` format.

```bash
hex1b capture recording start <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Description |
|--------|------|-------------|
| `--output` | string | **(required)** Output `.cast` file path |
| `--title` | string | Recording title (embedded in the cast file header) |
| `--idle-limit` | double | Max idle time in seconds between frames |

**Examples:**

```bash
# Basic recording
hex1b capture recording start abc123 --output demo.cast

# With title and idle limiting
hex1b capture recording start abc123 --output demo.cast --title "Setup Guide" --idle-limit 2.0
```

### `capture recording stop`

Stop recording a terminal session.

```bash
hex1b capture recording stop <id>
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

Returns the path of the completed recording file.

### `capture recording status`

Show recording status of a terminal session.

```bash
hex1b capture recording status <id>
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

Shows whether a recording is active and the output file path.

### `capture recording playback`

Play back an asciinema recording.

```bash
hex1b capture recording playback [options]
```

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--file` | string | | **(required)** Path to `.cast` file |
| `--speed` | double | `1.0` | Playback speed multiplier |
| `--player` | flag | | Launch interactive TUI player with controls |

**Examples:**

```bash
# Simple playback to stdout
hex1b capture recording playback --file demo.cast

# Double speed
hex1b capture recording playback --file demo.cast --speed 2.0

# Interactive TUI player
hex1b capture recording playback --file demo.cast --player
```

**TUI player controls (`--player`):**

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `←` | Seek backward 5 seconds |
| `→` | Seek forward 5 seconds |
| `Q` | Quit |

---

## `keys`

Send keystrokes to a terminal.

```bash
hex1b keys <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Description |
|--------|------|-------------|
| `--text` | string | Type text as keystrokes |
| `--key` | string | Named key (see below) |
| `--ctrl` | flag | Ctrl modifier |
| `--shift` | flag | Shift modifier |
| `--alt` | flag | Alt modifier |

Provide either `--text` or `--key` (not both).

**Named keys:** `Enter`, `Tab`, `Escape`, `Backspace`, `Delete`, `Space`, `ArrowUp`, `ArrowDown`, `ArrowLeft`, `ArrowRight`, `Home`, `End`, `PageUp`, `PageDown`, `Insert`, `F1`–`F12`.

**Examples:**

```bash
# Type text
hex1b keys abc123 --text "hello world"

# Send Enter
hex1b keys abc123 --key Enter

# Ctrl+C
hex1b keys abc123 --key c --ctrl

# Alt+Tab
hex1b keys abc123 --key Tab --alt
```

---

## `mouse`

Send mouse input to a terminal.

### `mouse click`

Send a mouse click at coordinates.

```bash
hex1b mouse click <id> <x> <y> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |
| `x` | Column (0-based) |
| `y` | Row (0-based) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--button` | string | `left` | Mouse button: `left`, `right`, or `middle` |

### `mouse drag`

Drag from one coordinate to another.

```bash
hex1b mouse drag <id> <x1> <y1> <x2> <y2> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |
| `x1` | Start column (0-based) |
| `y1` | Start row (0-based) |
| `x2` | End column (0-based) |
| `y2` | End row (0-based) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--button` | string | `left` | Mouse button: `left`, `right`, or `middle` |

---

## `app`

TUI application diagnostics for inspecting the widget/node tree.

### `app tree`

Inspect the widget/node tree of a TUI application.

```bash
hex1b app tree <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Description |
|--------|------|-------------|
| `--focus` | flag | Include focus ring info |
| `--popups` | flag | Include popup stack |
| `--depth` | int | Limit tree depth |

**Examples:**

```bash
# Full widget tree
hex1b app tree abc123

# With focus info, limited depth
hex1b app tree abc123 --focus --depth 3

# As JSON
hex1b app tree abc123 --json
```

---

## `assert`

Assert on terminal content for scripting and CI. Exits with code 0 on success, non-zero on failure.

```bash
hex1b assert <id> [options]
```

| Argument | Description |
|----------|-------------|
| `id` | Terminal ID (or unique prefix) |

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--text-present` | string | | Assert text is visible on screen |
| `--text-absent` | string | | Assert text is NOT visible on screen |
| `--timeout` | int | `5` | How long to wait in seconds |

Provide at least one of `--text-present` or `--text-absent`.

**Examples:**

```bash
# Wait for text to appear
hex1b assert abc123 --text-present "Login successful" --timeout 10

# Verify error is not shown
hex1b assert abc123 --text-absent "Error"

# Use in a CI script
hex1b assert abc123 --text-present "Ready" --timeout 30 || exit 1
```

---

## `agent`

AI agent integration commands.

### `agent init`

Initialize a Hex1b agent skill file in a repository. This generates a skill configuration that AI coding agents (GitHub Copilot, Claude, Cursor, etc.) can use to understand how to work with Hex1b in your project.

```bash
hex1b agent init [options]
```

| Option | Type | Description |
|--------|------|-------------|
| `--path` | string | Explicit repo root path (skips auto-detection) |
| `--stdout` | flag | Write skill file to stdout instead of disk |
| `--force` | flag | Overwrite existing skill file |

### `agent mcp`

Start the MCP server (stdio transport). This is used for integration with AI agents that support the Model Context Protocol.

```bash
hex1b agent mcp
```

::: info Coming Soon
The `agent mcp` command is planned for a future release. For MCP integration today, see the standalone [MCP Server](/guide/mcp-server).
:::
