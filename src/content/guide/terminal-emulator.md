# Terminal Emulator

Hex1b includes a fully-featured terminal emulator that you can embed in your .NET applications. Host interactive shells, run commands with full PTY support, and build developer tools with complete terminal control.

## Why Embed a Terminal?

Sometimes you need more than a TUI — you need a real terminal:

- **Developer tools** — Build IDEs, debuggers, or DevOps dashboards with integrated terminals
- **Shell hosting** — Embed bash, PowerShell, or any shell in your application
- **Process management** — Run and monitor child processes with full terminal emulation
- **Remote sessions** — Build SSH clients or container terminal access

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
var terminal = new Hex1bTerminalBuilder()
    .WithSize(80, 24)
    .Build();

// Start an interactive shell
await terminal.StartProcessAsync("bash");

// Or run a specific command
await terminal.StartProcessAsync("htop");
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
| `Hex1bTerminal` | Main terminal emulator class |
| `Hex1bTerminalBuilder` | Fluent configuration builder |
| `IHex1bTerminalPresentationAdapter` | Handles screen rendering |
| `IHex1bTerminalWorkloadAdapter` | Manages child processes |

### Presentation Adapters

Different adapters for different use cases:

- **`ConsolePresentationAdapter`** — Render to `System.Console`
- **`HeadlessPresentationAdapter`** — No rendering (for testing/automation)
- **Custom adapters** — Implement your own for GUIs, web, etc.

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

## Getting Started

1. Create a terminal instance with the builder
2. Configure the size and any adapters
3. Start a process or connect to a PTY
4. Integrate with your application

```csharp
using Hex1b;

// Create and configure the terminal
var terminal = new Hex1bTerminalBuilder()
    .WithSize(120, 40)
    .WithPresentationAdapter(new ConsolePresentationAdapter())
    .Build();

// Start an interactive shell
await terminal.StartProcessAsync("bash");

// Run the terminal
await terminal.RunAsync();
```

## Related Topics

- [Child Process Architecture](/docs/child-process-arch) — Deep dive into PTY handling
- [Terminal Internals](/docs/terminal) — Low-level terminal documentation
- [MCP Server](/guide/mcp-server) — Expose terminals to AI agents
- [Testing](/guide/testing) — Automate terminal interactions
