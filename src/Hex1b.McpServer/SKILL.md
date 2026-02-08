# Hex1b MCP Server Skill

This skill provides guidance for AI agents working with the Hex1b TUI (Terminal User Interface) library and its MCP diagnostic tools.

## Overview

**Hex1b** is a .NET library for building terminal user interfaces with a React-inspired declarative API. The library ships to NuGet as `Hex1b`.

- **Documentation**: https://hex1b.dev
- **Repository**: https://github.com/AsciiCraft/hex1b
- **NuGet**: https://www.nuget.org/packages/Hex1b

## MCP Tools Reference

The Hex1b MCP server provides tools for interacting with running Hex1b applications that have diagnostics enabled.

### Discovery Tools

#### `GetHex1bStacksWithDiagnosticsEnabled`
Lists all running Hex1b applications with diagnostics enabled.
- Returns: Process IDs, app names, dimensions, and socket paths
- Use this first to discover available applications to connect to

#### `DiscoverHex1bStacks`
Discovers and connects to all Hex1b applications with diagnostics enabled.
- Returns session IDs for each connected application
- Use session IDs with other tools

### Capture Tools

#### `CaptureHex1bTerminal`
Captures the terminal screen state.
- Parameters:
  - `processId`: Process ID of the Hex1b application
  - `savePath`: File path to save the capture
  - `format`: "ansi", "svg", or "text" (default: "ansi")
- Use for visual debugging and documentation

#### `CaptureTerminalScreen`
Captures the terminal screen in various formats.
- Parameters:
  - `sessionId`: Session ID of connected terminal
  - `format`: "text", "ansi", or "svg"
  - `savePath`: Optional file path to save
- Unified capture for both local and remote terminals

### Input Tools

#### `SendInputToHex1bTerminal`
Sends input characters to a Hex1b application.
- Parameters:
  - `processId`: Process ID of the target application
  - `input`: Text to send (supports `\n`, `\t`, `\x1b` escape sequences)
- Use for simulating user input

#### `SendTerminalKey`
Sends a special key to a terminal.
- Parameters:
  - `sessionId`: Session ID of terminal
  - `key`: Key name (Enter, Tab, Escape, Up, Down, Left, Right, F1-F12, etc.)
  - `modifiers`: Optional array ["Ctrl", "Alt", "Shift"]

#### `SendTerminalMouseClick`
Sends a mouse click to a terminal.
- Parameters:
  - `sessionId`: Session ID of terminal
  - `x`, `y`: Cell coordinates (0-based)
  - `button`: "left", "middle", or "right"

### Diagnostic Tools

#### `GetHex1bTree`
**IMPORTANT**: Use this tool to debug layout, hit testing, and focus issues.
- Parameters:
  - `processId`: Process ID of the Hex1b application
- Returns:
  - `tree`: Full widget/node hierarchy with bounds, hit test bounds, and properties
  - `popups`: Popup stack entries with anchor info and stale status
  - `focusInfo`: All focusable nodes with positions and last hit test debug info
- Essential for understanding why clicks aren't working or focus is wrong

### Session Management

#### `ConnectToHex1bStack`
Connects to a remote Hex1b application by process ID.
- Returns a session ID for use with other tools

#### `ListTerminals`
Lists all active terminal sessions.

#### `ListAllTerminalTargets`
Lists both local terminals and remote Hex1b connections.

## Enabling Diagnostics in Your Application

To enable MCP diagnostics in a Hex1b application:

```csharp
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithDiagnostics()  // Enable MCP diagnostics
    .WithHex1bApp((app, options) => ctx => ctx.Text("Hello!"))
    .Build();

await terminal.RunAsync();
```

**Note**: `WithDiagnostics()` is automatically disabled in Release builds for security. To force enable in Release:

```csharp
.WithMcpDiagnostics(forceEnable: true)
```

## Testing Best Practices

### Unit Testing with Headless Mode

For unit tests, use headless mode to avoid terminal I/O:

```csharp
[Fact]
public async Task MyWidget_Click_PerformsAction()
{
    // Arrange
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithHeadless(80, 24)  // No real terminal
        .WithDiagnostics()  // Enable for debugging if needed
        .WithHex1bApp((app, options) => ctx => 
            ctx.Button("Click me", e => { /* handler */ }))
        .Build();
    
    // Act
    await terminal.StartAsync();
    terminal.SendMouseClick(MouseButton.Left, 5, 0);
    await terminal.ProcessEventsAsync();
    
    // Assert
    var snapshot = terminal.CreateSnapshot();
    Assert.Contains("expected text", snapshot.GetText());
}
```

### Key Testing Patterns

1. **Use `WithHeadless(width, height)`** - Runs without a real terminal
2. **Use `terminal.SendMouseClick()` / `terminal.SendKey()`** - Inject input
3. **Use `terminal.ProcessEventsAsync()`** - Process pending events
4. **Use `terminal.CreateSnapshot()`** - Capture screen for assertions
5. **Use `WithDiagnostics()`** - Enable when debugging test failures

### Debugging Test Failures

When tests fail unexpectedly:

1. Enable `WithDiagnostics()` in the test
2. Add a breakpoint or delay
3. Use `GetHex1bTree` MCP tool to inspect:
   - Node bounds and hit test bounds
   - Focus ring state
   - Popup stack

## Architecture Quick Reference

### Widget/Node Pattern

- **Widgets** (`*Widget`): Immutable records describing what to render
- **Nodes** (`*Node`): Mutable classes managing state and rendering
- Reconciliation diffs widgets against nodes to preserve state

### Common Node Types

| Node | Purpose |
|------|---------|
| `ZStackNode` | Popup host, layers children on Z-axis |
| `VStackNode` / `HStackNode` | Vertical/horizontal layout |
| `ButtonNode` | Clickable button |
| `TextBlockNode` | Text display |
| `TableNode` | Data table with scrolling |
| `PickerNode` | Dropdown selection |
| `MenuNode` | Menu bar item |
| `BackdropNode` | Modal backdrop with click-away |
| `AnchoredNode` | Positions popup relative to anchor |

### Hit Testing

- `FocusRing.HitTest(x, y)` finds the focusable node at a position
- Iterates focusables in **reverse order** (last = topmost)
- Uses `node.HitTestBounds` which may differ from `node.Bounds`
- Nodes with zero `Bounds` return `Rect.Zero` for `HitTestBounds` to prevent ghost hits

## Common Issues and Solutions

### Click not working / wrong element responding
1. Use `GetHex1bTree` to inspect `focusInfo.focusables`
2. Look for nodes with mismatched `bounds` vs `hitTestBounds`
3. Check for nodes with `bounds: (0,0,0,0)` but non-zero `hitTestBounds`

### Popup appearing at wrong position
1. Check `popups` array in tree output
2. Look for `anchorInfo.isStale: true` indicating stale anchor reference
3. Stale anchors happen when the anchor node is replaced during reconciliation

### Focus not on expected element
1. Check `focusInfo.currentFocusIndex` and `focusedNodeType`
2. Verify the element is in `focusables` array
3. Check `isFocusable: true` on the node

## Getting This Skill

If you're an AI agent and want to save this skill for future use:

1. Call `GetHex1bSkill` to get this content
2. Save to `.github/skills/hex1b-mcp.md` or your project's skill directory
3. Reference when working with Hex1b applications or debugging TUI issues
