# Hex1b.Analyzer

A terminal analyzer tool that provides passthrough emulation with a web-based terminal state viewer.

## Overview

Hex1b.Analyzer launches a child process with PTY passthrough (similar to running a command in a terminal), while simultaneously exposing a local web server that provides:
- A real-time terminal view using xterm.js with SignalR streaming
- An API endpoint for SVG snapshots of the terminal state

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
# Launch bash with the analyzer (default port 5050)
hex1b-analyzer run -- bash

# Launch with a specific port for the web server
hex1b-analyzer run --port 8080 -- /bin/bash --norc

# Launch any command
hex1b-analyzer run -- htop
```

When started, the analyzer outputs the URL with a clickable hyperlink (using OSC 8 escape codes in supported terminals).

## Web Interface

Once running, open the displayed URL in a browser to see:
- **Home page (/)**: Real-time terminal view using xterm.js with live updates via SignalR
- **SVG endpoint (/getsvg)**: Returns an SVG snapshot of the current terminal state

The web terminal automatically reconnects and syncs state if the connection is interrupted.

## API

### GET /getsvg

Returns an SVG snapshot of the current terminal state.

```bash
curl http://localhost:5050/getsvg > terminal.svg
```

## Architecture

Hex1b.Analyzer uses a multiheaded presentation adapter architecture:
- **ConsolePresentationAdapter**: Handles passthrough to the host terminal
- **BlazorPresentationAdapter**: Captures output for web streaming
- **MultiheadedPresentationAdapter**: Combines both, broadcasting output to all adapters

The web interface uses SignalR for real-time streaming of terminal output to xterm.js in the browser.

## Command Line Reference

```
hex1b-analyzer run [options] -- <command> [args...]

Options:
  --port <port>  Port for the web server (default: 5050)
  -?, -h, --help Show help and usage information

Arguments:
  <command>      The command and arguments to run
```

## Requirements

- Linux or macOS (Windows is not currently supported)
- .NET 10.0 or later

## License

MIT License - See [LICENSE](https://github.com/mitchdenny/hex1b/blob/main/LICENSE) for details.
