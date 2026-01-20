<!--
  MIRROR WARNING: The demoCode sample below must stay in sync with:
  src/Hex1b.Website/Examples/TerminalBasicExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/terminal-basic.cs?raw'
import fallbackSnippet from './snippets/terminal-fallback.cs?raw'
import lifecycleSnippet from './snippets/terminal-lifecycle.cs?raw'
import titleSnippet from './snippets/terminal-title.cs?raw'

const demoCode = `using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

// State management
var terminals = new List<TerminalSession>();
var nextTerminalId = 1;
var activeTerminalId = 0;
Hex1bApp? displayApp = null;

// Helper to create and start a terminal
void AddTerminal()
{
    var id = nextTerminalId++;
    var terminal = Hex1bTerminal.CreateBuilder()
        .WithDiagnosticShell()  // Or .WithPtyProcess("bash")
        .WithTerminalWidget(out var handle)
        .Build();

    terminals.Add(new(id, terminal, handle));
    activeTerminalId = id;

    // Start terminal in background
    _ = terminal.RunAsync();
    displayApp?.Invalidate();
}

// Create initial terminal
AddTerminal();

// Build the display app
await using var app = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((hex1bApp, options) =>
    {
        displayApp = hex1bApp;
        return ctx => ctx.VStack(v => [
            // Menu bar for terminal management
            v.MenuBar(m => [
                m.Menu("File", menu => [
                    m.MenuItem("New Terminal").OnActivated(_ => AddTerminal()),
                    m.MenuItem("Quit").OnActivated(_ => hex1bApp.RequestStop())
                ]),
                m.Menu("Terminals", menu => [
                    ..terminals.Select(s => 
                        m.MenuItem(s.Id == activeTerminalId ? $"● Terminal {s.Id}" : $"  Terminal {s.Id}")
                            .OnActivated(_ => { activeTerminalId = s.Id; hex1bApp.Invalidate(); })
                    )
                ])
            ]),

            // Active terminal with fallback for exit handling
            v.Border(
                v.Terminal(terminals.First(t => t.Id == activeTerminalId).Handle)
                    .WhenNotRunning(args => v.VStack(fallback => [
                        fallback.Align(Alignment.Center, fallback.VStack(center => [
                            center.Text($"Exited with code {args.ExitCode}"),
                            center.Button("Restart").OnClick(_ => RestartTerminal())
                        ]))
                    ])),
                title: $"Terminal {activeTerminalId}"
            ).Fill(),

            v.InfoBar(["Ctrl+N", "New", "Ctrl+Q", "Quit"])
        ]);
    })
    .Build();

await app.RunAsync();

record TerminalSession(int Id, Hex1bTerminal Terminal, TerminalWidgetHandle Handle);`

const basicCode = `using Hex1b;

// Create a terminal with an embedded bash session
var bashTerminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var bashHandle)
    .Build();

// Start the child terminal in the background
_ = bashTerminal.RunAsync();

// Create the main TUI app with the embedded terminal
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => 
        ctx.Border(
            ctx.Terminal(bashHandle),
            title: "Embedded Bash Terminal"
        ))
    .Build();

await displayTerminal.RunAsync();`

const multiTerminalCode = `using Hex1b;
using Hex1b.Input;

// Create multiple embedded terminals
var bash = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var bashHandle)
    .Build();

var pwsh = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("pwsh", "-NoProfile", "-NoLogo")
    .WithTerminalWidget(out var pwshHandle)
    .Build();

// Start both terminals
_ = bash.RunAsync();
_ = pwsh.RunAsync();

// Display side-by-side using HStack
await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => 
        ctx.HStack(h => [
            h.Border(
                h.Terminal(bashHandle),
                title: "Bash"
            ).Fill(),
            h.Border(
                h.Terminal(pwshHandle),
                title: "PowerShell"
            ).Fill()
        ]))
    .Build();

await displayTerminal.RunAsync();`

const fallbackCode = `using Hex1b;

var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash", "--norc")
    .WithTerminalWidget(out var bashHandle)
    .Build();

Hex1bApp? app = null;

void RestartTerminal()
{
    bashHandle.Reset();
    _ = terminal.RunAsync();
    app?.Invalidate();
}

_ = terminal.RunAsync();

await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((hex1bApp, options) =>
    {
        app = hex1bApp;
        return ctx => ctx.Border(
            ctx.Terminal(bashHandle)
                .WhenNotRunning(args => ctx.VStack(v => [
                    v.Text(""),
                    v.Align(Alignment.Center, v.VStack(center => [
                        center.Text(\$"Terminal exited with code {args.ExitCode ?? 0}"),
                        center.Text(""),
                        center.HStack(buttons => [
                            buttons.Button("Restart").OnClick(_ => RestartTerminal()),
                            buttons.Text("  "),
                            buttons.Button("Close").OnClick(_ => hex1bApp.RequestStop())
                        ])
                    ]))
                ])),
            title: "Terminal with Fallback"
        );
    })
    .Build();

await displayTerminal.RunAsync();`
</script>

# TerminalWidget

Embed child terminal sessions within your TUI application.

## Overview

The `TerminalWidget` displays the output of an embedded terminal session, such as a bash shell, PowerShell, or any PTY-based process. This enables building terminal multiplexers, IDE-like environments, or any application that needs to host child terminals.

::: tip Key Concept
The TerminalWidget connects to a `TerminalWidgetHandle` which provides the screen buffer from a running terminal session. The handle is created using `Hex1bTerminalBuilder.WithTerminalWidget()`.
:::

## Live Demo

This interactive demo shows multiple embedded terminals with menu-driven switching and restart functionality. Try typing `help` for available commands, or `colors` for a color test:

<CodeBlock lang="csharp" :code="demoCode" command="dotnet run" example="terminal-basic" exampleTitle="Embedded Terminals Demo" />

::: info Diagnostic Shell
The live demo uses the built-in **diagnostic shell** which simulates a terminal environment without requiring PTY infrastructure. For real applications, you'd use `.WithPtyProcess("bash")` or similar.
:::

## Basic Usage

Create an embedded terminal and display it in your TUI:

```csharp-vue
{{ basicCode }}
```

The key steps are:

1. **Create the child terminal** using `Hex1bTerminal.CreateBuilder()` with `.WithTerminalWidget(out var handle)`
2. **Start the child terminal** with `RunAsync()` in the background
3. **Display the terminal** using `ctx.Terminal(handle)` in your widget tree

## Multiple Terminals

You can embed multiple terminal sessions and arrange them using layout widgets:

```csharp-vue
{{ multiTerminalCode }}
```

## Fallback Widget

When a terminal process exits, you can display a fallback widget using `WhenNotRunning()`:

```csharp-vue
{{ fallbackCode }}
```

The `WhenNotRunning` callback receives a `TerminalNotRunningArgs` object containing:

| Property | Type | Description |
|----------|------|-------------|
| `Handle` | `TerminalWidgetHandle` | The terminal handle |
| `State` | `TerminalState` | Current state (`NotStarted` or `Completed`) |
| `ExitCode` | `int?` | Exit code if completed, null otherwise |

## Terminal States

The `TerminalWidgetHandle` tracks the lifecycle of the child process:

```csharp-vue
{{ lifecycleSnippet }}
```

| State | Description |
|-------|-------------|
| `NotStarted` | Terminal created but not yet started |
| `Running` | Terminal process is actively running |
| `Completed` | Terminal process has exited |

## Window Title Support

Child processes can set the terminal title using OSC escape sequences. Subscribe to title changes:

```csharp-vue
{{ titleSnippet }}
```

The handle provides:

- `WindowTitle` - Current window title (OSC 0/2)
- `IconName` - Current icon name (OSC 0/1)
- `WindowTitleChanged` event - Fired when title changes
- `IconNameChanged` event - Fired when icon name changes

## Input Handling

When a TerminalWidget has focus:

- **Keyboard input** is automatically forwarded to the child process
- **Mouse events** are forwarded if the child process has enabled mouse tracking
- **Focus** is captured while the terminal is running

::: info Focus Behavior
When the child terminal exits, focus is released and can move to the fallback widget's interactive elements (like buttons).
:::

## API Reference

### TerminalWidget Record

```csharp
public sealed record TerminalWidget(TerminalWidgetHandle Handle) : Hex1bWidget
```

### Extension Methods

| Method | Description |
|--------|-------------|
| `ctx.Terminal(handle)` | Creates a TerminalWidget bound to the handle |
| `.WhenNotRunning(builder)` | Sets a fallback widget for when the terminal is not running |

### TerminalWidgetHandle Properties

| Property | Type | Description |
|----------|------|-------------|
| `Width` | `int` | Current width in columns |
| `Height` | `int` | Current height in rows |
| `State` | `TerminalState` | Current lifecycle state |
| `ExitCode` | `int?` | Exit code if completed |
| `IsRunning` | `bool` | Whether the terminal is currently running |
| `WindowTitle` | `string` | Current window title from OSC sequences |
| `CursorX` | `int` | Current cursor X position (0-based) |
| `CursorY` | `int` | Current cursor Y position (0-based) |
| `CursorVisible` | `bool` | Whether cursor is visible |
| `MouseTrackingEnabled` | `bool` | Whether child has enabled mouse tracking |

### TerminalWidgetHandle Methods

| Method | Description |
|--------|-------------|
| `SendEventAsync(evt)` | Send a key or mouse event to the child process |
| `Resize(width, height)` | Resize the terminal buffer |
| `Reset()` | Reset state to NotStarted (for restarting) |
| `GetScreenBuffer()` | Get a copy of the current screen buffer |

## Related

- [Terminal Emulator Guide](/guide/terminal-emulator) — Learn about Hex1b's terminal emulation capabilities
- [Stacks](/guide/widgets/stacks) — Layout multiple terminals with HStack/VStack
- [Border](/guide/widgets/containers) — Add borders and titles to terminal panes
