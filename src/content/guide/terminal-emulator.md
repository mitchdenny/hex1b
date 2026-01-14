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

::: tip Tutorial: Build a Web Terminal
For a hands-on walkthrough building a multi-terminal web app with WebSocket streaming and xterm.js, see [Using the Emulator](./using-the-emulator).
:::

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
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "-i")
    .Build();

await terminal.RunAsync();
```

```csharp
// Or run a command without PTY (for build tools, scripts)
await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithProcess("dotnet", "build")
    .WithHeadless()
    .Build();

await terminal.RunAsync();
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
- **`WebSocketPresentationAdapter`** — Stream to WebSocket clients (xterm.js, etc.)
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

- [Using the Emulator](./using-the-emulator) — Step-by-step tutorial
- [Presentation Adapters](./presentation-adapters) — Custom display handling
- [Workload Adapters](./workload-adapters) — Custom workload types
- [MCP Server](/guide/mcp-server) — Expose terminals to AI agents
- [Testing](/guide/testing) — Automate terminal interactions
