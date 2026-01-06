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

### Utility

- **ping** - Verify the MCP server is running

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
