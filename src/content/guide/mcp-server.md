# MCP Server

Expose terminal sessions to AI agents via the Model Context Protocol (MCP). Let LLMs interact with your terminal applications programmatically, enabling powerful automation and AI-assisted workflows.

## What is MCP?

The [Model Context Protocol](https://github.com/anthropics/model-context-protocol) is an open standard that allows AI models to interact with external tools and data sources. Hex1b implements an MCP server that exposes terminal capabilities to AI agents.

## Why MCP for Terminals?

AI agents are increasingly being used to automate development workflows. With Hex1b's MCP server, AI can:

- **Run commands** — Execute shell commands and observe output
- **Navigate TUIs** — Interact with terminal user interfaces
- **Debug applications** — Inspect terminal state and diagnose issues
- **Automate workflows** — Build AI-powered automation pipelines
- **Test applications** — Use AI for exploratory testing

## Key Features

### Terminal Session Management

AI agents can create, manage, and interact with terminal sessions:

```
Tools exposed via MCP:
- start_bash_terminal    → Create new bash session
- start_pwsh_terminal    → Create new PowerShell session
- send_terminal_input    → Send text/commands to terminal
- send_terminal_key      → Send special keys (Enter, Tab, Arrows)
- capture_terminal       → Get current screen content
- list_terminals         → List active sessions
- stop_terminal          → Stop a session
```

### Screen Capture

AI agents can read the current terminal state:

- **Text capture** — Get screen content as plain text
- **Accessibility snapshot** — Structured representation of UI elements
- **SVG capture** — Visual representation with colors and styling

### Input Simulation

Send any input to the terminal:

- Text input with proper encoding
- Special keys (Enter, Tab, Escape, Arrow keys, Function keys)
- Key modifiers (Ctrl, Alt, Shift)

## Getting Started

### Install the MCP Server

The Hex1b MCP server is available as a .NET tool:

```bash
dotnet tool install -g Hex1b.McpServer
```

### Configure Your AI Client

Add the MCP server to your AI client's configuration. For example, with Claude:

```json
{
  "mcpServers": {
    "hex1b": {
      "command": "hex1b-mcp",
      "args": []
    }
  }
}
```

### Use with AI Agents

Once configured, AI agents can interact with terminals:

```
User: "Run the tests and tell me if they pass"

AI: I'll start a terminal session and run the tests.
    [Uses start_bash_terminal]
    [Uses send_terminal_input: "dotnet test"]
    [Uses capture_terminal to read output]
    
    The tests completed successfully. All 42 tests passed.
```

## Architecture

The MCP server runs as a separate process that AI clients communicate with via JSON-RPC:

```
┌─────────────┐     JSON-RPC     ┌──────────────┐     PTY     ┌──────────┐
│  AI Client  │ ◄──────────────► │  MCP Server  │ ◄─────────► │ Terminal │
│  (Claude)   │                  │ (Hex1b.Mcp)  │             │ (bash)   │
└─────────────┘                  └──────────────┘             └──────────┘
```

### Session Management

The MCP server maintains a pool of terminal sessions:

- Sessions persist across tool calls
- Each session has a unique ID
- Sessions can be stopped and cleaned up
- Multiple concurrent sessions supported

## Use Cases

### AI-Assisted Development

Let AI agents help with development tasks:

```
"Set up a new .NET project with xUnit tests"
"Run the build and fix any errors you find"
"Deploy this to the staging environment"
```

### Automated Testing

Use AI for exploratory or regression testing:

```
"Test all the menu items in this TUI application"
"Try to break this CLI by entering unexpected input"
"Verify the application handles errors gracefully"
```

### DevOps Automation

Automate infrastructure and deployment:

```
"Check the status of all running containers"
"Tail the logs and alert me if you see errors"
"Scale the service to 3 replicas"
```

### Learning & Documentation

AI can explore and document CLI tools:

```
"What commands does this CLI support?"
"Show me examples of using the --format flag"
"Document the configuration options"
```

## Security Considerations

When exposing terminals to AI agents, consider:

| Concern | Mitigation |
|---------|------------|
| **Command execution** | Run in sandboxed environments |
| **Credential exposure** | Use environment variables, not inline secrets |
| **Resource limits** | Set timeouts and memory limits |
| **Network access** | Restrict network in sensitive environments |

### Best Practices

1. **Use dedicated environments** — Don't expose production terminals
2. **Audit AI actions** — Log all commands executed by AI
3. **Set boundaries** — Configure AI with clear scope limitations
4. **Review outputs** — Human review for sensitive operations

## API Reference

### Tools

| Tool | Description |
|------|-------------|
| `start_bash_terminal` | Start a new bash session |
| `start_pwsh_terminal` | Start a new PowerShell session |
| `send_terminal_input` | Send text input to terminal |
| `send_terminal_key` | Send special key (Enter, Tab, etc.) |
| `capture_terminal` | Capture screen as text or SVG |
| `wait_for_terminal_text` | Wait for specific text to appear |
| `list_terminals` | List all active sessions |
| `stop_terminal` | Stop a terminal session |
| `remove_session` | Clean up a stopped session |

### Resources

| Resource | Description |
|----------|-------------|
| `terminal://sessions` | List of active terminal sessions |
| `terminal://session/{id}` | Current state of a specific session |

## Related Topics

- [Terminal Emulator](/guide/terminal-emulator) — The underlying terminal technology
- [Testing](/guide/testing) — Use MCP for automated testing
- [Model Context Protocol](https://github.com/anthropics/model-context-protocol) — Official MCP specification
