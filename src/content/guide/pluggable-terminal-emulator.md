# Pluggable Terminal Emulator

Hex1b's terminal emulation layer is designed to be modular and extensible. At its core, the library uses a pluggable architecture that separates the concerns of **presentation** (how things are displayed) from **workload** (what work is being done).

## Architecture Overview

The terminal emulation system consists of three main components:

1. **`Hex1bTerminal`** - The main terminal abstraction that coordinates between adapters
2. **Presentation Adapters** - Handle rendering and display
3. **Workload Adapters** - Handle the actual work being performed (TUI apps, child processes, etc.)

```
┌─────────────────────────────────────────────────┐
│                  Hex1bTerminal                  │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌─────────────────┐    ┌─────────────────┐    │
│  │   Presentation  │    │     Workload    │    │
│  │     Adapter     │◄──►│     Adapter     │    │
│  └─────────────────┘    └─────────────────┘    │
│                                                 │
└─────────────────────────────────────────────────┘
```

## The Builder Pattern

Use `Hex1bTerminalBuilder` to configure and create terminal instances:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()  // Use console for display
    .WithAppWorkload(BuildUI)   // Run a TUI app
    .Build();

await terminal.RunAsync();
```

## Key Interfaces

### IHex1bTerminalPresentationAdapter

Defines how the terminal content is displayed:

```csharp
public interface IHex1bTerminalPresentationAdapter
{
    void Initialize(Hex1bTerminal terminal);
    void Render(ReadOnlySpan<CellAttributes> buffer, int width, int height);
    void SetCursorPosition(int x, int y);
    void SetCursorVisible(bool visible);
    // ... additional display methods
}
```

### IHex1bTerminalWorkloadAdapter

Defines what work the terminal performs:

```csharp
public interface IHex1bTerminalWorkloadAdapter
{
    void Initialize(Hex1bTerminal terminal);
    Task RunAsync(CancellationToken cancellationToken);
    void HandleInput(Hex1bKeyEvent keyEvent);
    void HandleResize(int width, int height);
}
```

## Common Configurations

### Console TUI Application

The most common setup for building terminal UIs:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()
    .WithAppWorkload(BuildUI)
    .Build();
```

### Headless Testing

Run without any display for automated testing:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithHeadlessPresentation(80, 24)
    .WithAppWorkload(BuildUI)
    .Build();
```

### Child Process Hosting

Host an external process inside the terminal:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()
    .WithChildProcessWorkload("bash", "-i")
    .Build();
```

## Next Steps

- Learn about [Presentation Adapters](./presentation-adapters) for custom display handling
- Learn about [Workload Adapters](./workload-adapters) for custom workload types
