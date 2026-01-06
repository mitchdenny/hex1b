# Hex1b.Analyzer

A terminal analyzer tool that provides passthrough emulation with a web-based terminal state viewer.

## Overview

Hex1b.Analyzer launches a child process with PTY passthrough (similar to running a command in a terminal), while simultaneously exposing a local web server that provides SVG snapshots of the terminal state.

This is useful for:
- Debugging terminal applications
- Capturing terminal state for documentation
- Remote terminal viewing/monitoring
- Testing terminal rendering

## Installation

```bash
dotnet tool install --global Hex1b.Analyzer
```

## Usage

```bash
# Launch bash with the analyzer
hex1b-analyzer bash

# Launch with a specific port for the web server
hex1b-analyzer --port 8080 /bin/bash

# Launch any command
hex1b-analyzer htop
```

## Web API

Once running, the analyzer exposes a local web server (default port 5050) with the following endpoint:

### GET /getsvg

Returns an SVG snapshot of the current terminal state.

```bash
curl http://localhost:5050/getsvg > terminal.svg
```

Open the SVG in a browser to view the terminal state.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `--port PORT` | 5050 | Port for the web server |

## Requirements

- Linux or macOS (Windows is not currently supported)
- .NET 10.0 or later

## License

MIT License - See [LICENSE](https://github.com/mitchdenny/hex1b/blob/main/LICENSE) for details.
