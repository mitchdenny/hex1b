# Hex1b.McpServer

A Model Context Protocol (MCP) server for terminal session management using Hex1b. This tool provides AI assistants and automation tools with the ability to create, control, and capture terminal sessions.

## Installation

Install as a .NET global tool:

```bash
dotnet tool install -g Hex1b.McpServer
```

## Usage

Run the MCP server:

```bash
hex1b-mcp
```

The server communicates via stdio using the MCP protocol.

## Available Tools

### Session Management

- **start_bash_terminal** - Start a new bash terminal session (Linux/macOS)
- **start_pwsh_terminal** - Start a new PowerShell terminal session (Windows/cross-platform)
- **stop_terminal** - Stop a terminal session's process
- **remove_session** - Remove a terminal session and dispose resources
- **list_terminals** - List all active terminal sessions
- **resize_terminal** - Resize a terminal session

### Input

- **send_terminal_input** - Send text input to a terminal session
- **send_terminal_key** - Send a special key (Enter, Tab, Arrow keys, F1-F12, etc.)

### Capture

- **capture_terminal_text** - Capture the terminal screen as plain text
- **capture_terminal_screenshot** - Capture the terminal screen as an SVG image
- **wait_for_terminal_text** - Wait for specific text to appear on the terminal

### Recording

- **start_asciinema_recording** - Start recording a terminal session to an asciinema file. Captures the current terminal state as the initial frame, then records all subsequent output. Supports `idle_time_limit` parameter (default 2s) to compress long pauses during playback.
- **stop_asciinema_recording** - Stop an active recording and finalize the file. Returns the path to the completed `.cast` file.

### Utility

- **ping** - Verify the MCP server is running

## Recording Example

```
1. start_bash_terminal → returns sessionId
2. send_terminal_input (run some commands)
3. start_asciinema_recording(sessionId, "/path/to/demo.cast")
   → Captures current screen state as initial frame
4. ... interact with terminal ...
5. stop_asciinema_recording(sessionId)
   → Finalizes recording

Play with: asciinema play /path/to/demo.cast
```

## Configuration

### VS Code MCP Configuration

Add to your VS Code settings:

```json
{
  "mcp": {
    "servers": {
      "terminal-mcp": {
        "command": "hex1b-mcp"
      }
    }
  }
}
```

### Claude Desktop Configuration

Add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "terminal-mcp": {
      "command": "hex1b-mcp"
    }
  }
}
```

## Requirements

- .NET 10.0 or later
- Linux or macOS (PTY support via native interop)

## License

MIT - See LICENSE file in the repository.
