# Hex1bTerminal Architecture Design

> **Status**: In Progress (Phase 1 Complete)  
> **Date**: December 2025  
> **Author**: Design discussion between maintainer and AI assistant

## Implementation Plan

This section tracks the phased implementation of the terminal architecture.

### Phase 1: Workload Adapter Layer ✅ COMPLETE

**Goal**: Refactor `Hex1bApp` to use an adapter interface instead of `IHex1bTerminal` directly, without breaking existing code.

**Files Created**:
- [Terminal/IHex1bTerminalWorkloadAdapter.cs](../src/Hex1b/Terminal/IHex1bTerminalWorkloadAdapter.cs) - Terminal-side interface (raw bytes)
- [Terminal/IHex1bAppTerminalWorkloadAdapter.cs](../src/Hex1b/Terminal/IHex1bAppTerminalWorkloadAdapter.cs) - App-side interface (extends above)
- [Terminal/TerminalCapabilities.cs](../src/Hex1b/Terminal/TerminalCapabilities.cs) - Capability flags record
- [Terminal/LegacyHex1bAppTerminalWorkloadAdapter.cs](../src/Hex1b/Terminal/LegacyHex1bAppTerminalWorkloadAdapter.cs) - Wraps `IHex1bTerminal`

**Files Modified**:
- [Hex1bAppOptions.cs](../src/Hex1b/Hex1bAppOptions.cs) - Added `WorkloadAdapter` property, marked `Terminal` as legacy
- [Hex1bRenderContext.cs](../src/Hex1b/Hex1bRenderContext.cs) - Uses adapter instead of `IHex1bTerminalOutput`
- [Hex1bApp.cs](../src/Hex1b/Hex1bApp.cs) - Uses `_adapter` instead of `_terminal`

**Result**: All 1134 tests pass. Existing code continues to work via legacy adapter.

---

### Phase 2a: Presentation Adapter Interfaces ✅ COMPLETE

**Goal**: Define the presentation-side interfaces (mirror of workload side).

**Files Created**:
- [Terminal/IHex1bTerminalPresentationAdapter.cs](../src/Hex1b/Terminal/IHex1bTerminalPresentationAdapter.cs) - Raw bytes to/from display

**Interface**:
```csharp
public interface IHex1bTerminalPresentationAdapter : IAsyncDisposable
{
    // Write rendered output to the display
    ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    // Read raw input from the display  
    ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default);
    
    // Display dimensions
    int Width { get; }
    int Height { get; }
    
    // Capabilities
    TerminalCapabilities Capabilities { get; }
    
    // Events
    event Action<int, int>? Resized;
    event Action? Disconnected;
    
    // Lifecycle
    ValueTask FlushAsync(CancellationToken ct = default);
    ValueTask EnterTuiModeAsync(CancellationToken ct = default);
    ValueTask ExitTuiModeAsync(CancellationToken ct = default);
}
```

---

### Phase 2b: Legacy Presentation Adapters ⏳ NEXT

**Goal**: Wrap existing terminals with the new presentation interface.

**Files to Create**:
- `Terminal/LegacyConsolePresentationAdapter.cs` - Wraps `ConsoleHex1bTerminal` I/O
- `Terminal/LegacyWebSocketPresentationAdapter.cs` - Wraps `WebSocketHex1bTerminal` I/O

These extract the "raw I/O" parts from existing terminals without changing them.

---

### Phase 2c: Hex1bTerminalCore (Minimal) ⏳ PLANNED

**Goal**: Create the new terminal that sits between workload and presentation.

**Files to Create**:
- `Terminal/Hex1bTerminalCore.cs`

**Initial Implementation**:
- Implements `IHex1bAppTerminalWorkloadAdapter`
- Takes `IHex1bTerminalPresentationAdapter` in constructor
- Pass-through implementation (no pipeline layers yet)
- ANSI parsing for input events (extract from existing code)

---

### Phase 2d: Integration & Factory ⏳ PLANNED

**Goal**: Wire everything together with convenient factory methods.

**Usage Pattern**:
```csharp
// New way to create a terminal
var presentation = new LegacyConsolePresentationAdapter();
var terminal = new Hex1bTerminalCore(presentation);
var app = new Hex1bApp(builder, new Hex1bAppOptions 
{ 
    WorkloadAdapter = terminal 
});
```

**Factory Methods**:
```csharp
Hex1bTerminalCore.CreateConsole()
Hex1bTerminalCore.CreateWebSocket(webSocket)
```

---

### Phase 2e: Pipeline Layers ⏳ FUTURE

**Goal**: Add processing layers incrementally.

**Layers to Implement**:
1. ANSI parser layer
2. Capability detection layer  
3. Delta rendering layer
4. Virtual device state

---

### Phase 3: Deprecate Legacy ⏳ FUTURE

**Goal**: Mark old types as obsolete once new terminal is stable.

1. Mark `LegacyHex1bAppTerminalWorkloadAdapter` as obsolete
2. Mark `IHex1bTerminal`, `ConsoleHex1bTerminal`, `WebSocketHex1bTerminal` as obsolete
3. Eventually remove them

---

### Progress Summary

| Phase | Description | Status | Risk |
|-------|-------------|--------|------|
| 1 | Workload adapter layer | ✅ Complete | - |
| 2a | Presentation interfaces | ✅ Complete | - |
| 2b | Legacy presentation adapters | ⏳ Next | Low |
| 2c | Hex1bTerminalCore (pass-through) | ⏳ Planned | Medium |
| 2d | Integration & factory | ⏳ Planned | Low |
| 2e | Pipeline layers | ⏳ Future | Medium |
| 3 | Deprecate legacy | ⏳ Future | Low |

---

## Overview

This document describes a proposed architecture for `Hex1bTerminal` that unifies multiple terminal scenarios under a single implementation. The key insight is that `Hex1bTerminal` acts as a **virtual terminal emulator** with two connection points: an upstream **workload** side and a downstream **presentation** side.

## Current State

Today, Hex1b has three separate terminal implementations:

| Type | Purpose | Key Characteristics |
|------|---------|---------------------|
| `ConsoleHex1bTerminal` | Real console I/O | `Console.*` APIs, SIGWINCH, polling input loop |
| `WebSocketHex1bTerminal` | Browser-based TUI | WebSocket transport, JSON control messages |
| `Hex1bTerminal` | Testing/virtual | In-memory buffer, programmatic input injection |

### Problems with Current Approach

1. **Duplicated ANSI parsing logic** across implementations
2. **Inconsistent behavior** (e.g., mouse handling differs)
3. **Limited composability** (can't chain terminals)
4. **Hard to extend** for new transports (SSH, PTY, etc.)

## Hex1bApp Integration

### Abstraction Boundary

`Hex1bApp` and all internal TUI components (nodes, widgets, input router, etc.) communicate **exclusively** through `Hex1bAppWorkloadAdapter`. They never touch `Hex1bTerminal` directly.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Hex1bApp                                        │
│                                                                              │
│   ┌───────────────┐  ┌───────────────┐  ┌───────────────┐                  │
│   │   Widgets     │  │    Nodes      │  │  InputRouter  │                  │
│   └───────┬───────┘  └───────┬───────┘  └───────┬───────┘                  │
│           │                  │                  │                           │
│           └──────────────────┼──────────────────┘                           │
│                              │                                              │
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │ Hex1bAppWorkload    │  ◄── ONLY interface to terminal  │
│                   │ Adapter             │                                   │
│                   └──────────┬──────────┘                                   │
│                              │                                              │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │
                               │ (abstraction boundary)
                               │
┌──────────────────────────────┼──────────────────────────────────────────────┐
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │   Hex1bTerminal     │                                   │
│                   │   [layers, state]   │                                   │
│                   └──────────┬──────────┘                                   │
│                              │                                              │
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │ Presentation        │                                   │
│                   │ Adapter             │                                   │
│                   └─────────────────────┘                                   │
│                                                              (Hex1b infra)  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Workload Adapter: Two Sides

A workload adapter has **two faces** - one adapted for what the workload needs, and one adapted for what the terminal needs:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Workload Adapter                                      │
│                                                                              │
│   ┌───────────────────────────┐       ┌───────────────────────────┐        │
│   │   WORKLOAD SIDE           │       │   TERMINAL SIDE           │        │
│   │                           │       │                           │        │
│   │  (Adapted to what the     │◄─────►│  (Adapted to what the     │        │
│   │   workload expects)       │       │   terminal expects)       │        │
│   │                           │       │                           │        │
│   │  For Hex1bApp:            │       │  Raw byte streams:        │        │
│   │  - Write(string)          │       │  - ReadOutputAsync()      │        │
│   │  - InputEvents channel    │       │  - WriteInputAsync()      │        │
│   │  - Width/Height           │       │  - ResizeAsync()          │        │
│   │  - Capabilities           │       │                           │        │
│   └───────────────────────────┘       └───────────────────────────┘        │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Interface Hierarchy

```csharp
/// <summary>
/// Terminal-side interface: What the Hex1bTerminal will need from any workload.
/// Raw byte streams for maximum flexibility.
/// </summary>
public interface IHex1bTerminalWorkloadAdapter : IAsyncDisposable
{
    /// <summary>
    /// Read output FROM the workload (ANSI sequences).
    /// The terminal calls this to get data to parse and display.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Write input TO the workload (encoded key/mouse events).
    /// The terminal calls this when it receives input from presentation.
    /// </summary>
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Notify workload of resize.
    /// </summary>
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    /// <summary>
    /// Workload has disconnected/exited.
    /// </summary>
    event Action? Disconnected;
}

/// <summary>
/// App-side interface: Adds the higher-level APIs that Hex1bApp needs.
/// Extends the terminal-side interface so the same adapter serves both.
/// </summary>
public interface IHex1bAppTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // === Output (App → Terminal) ===
    
    /// <summary>
    /// Write ANSI-encoded output to the terminal.
    /// </summary>
    void Write(string text);
    
    /// <summary>
    /// Write raw bytes to the terminal.
    /// </summary>
    void Write(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Flush any buffered output.
    /// </summary>
    void Flush();
    
    // === Input (Terminal → App) ===
    
    /// <summary>
    /// Channel of input events from the terminal.
    /// </summary>
    ChannelReader<Hex1bEvent> InputEvents { get; }
    
    // === Terminal Info ===
    
    /// <summary>
    /// Current terminal width.
    /// </summary>
    int Width { get; }
    
    /// <summary>
    /// Current terminal height.
    /// </summary>
    int Height { get; }
    
    /// <summary>
    /// Terminal capabilities (mouse, sixel, colors, etc.).
    /// </summary>
    TerminalCapabilities Capabilities { get; }
}
```

### Migration Path

```
                    IHex1bTerminalWorkloadAdapter
                    (terminal-side: raw bytes)
                                │
                                │ extends
                                ▼
                    IHex1bAppTerminalWorkloadAdapter
                    (app-side: Write, InputEvents, etc.)
                                │
                ┌───────────────┼───────────────┐
                │               │               │
                ▼               ▼               ▼
    LegacyHex1bApp         Hex1bApp          PtyWorkload
    TerminalWorkload       TerminalWorkload   Adapter
    Adapter                Adapter            (future)
    (Phase 1: wraps        (Phase 2: works
    current IHex1bTerminal) with new terminal)
```

## Migration Strategy

### Phase 1: Introduce Adapter Layer (Current Work)

Create `LegacyHex1bAppTerminalWorkloadAdapter` that wraps the existing `IHex1bTerminal`:

```csharp
/// <summary>
/// Adapter that bridges Hex1bApp to the current IHex1bTerminal implementation.
/// This allows us to refactor Hex1bApp internals without changing the terminal.
/// </summary>
public class LegacyHex1bAppTerminalWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter
{
    private readonly IHex1bTerminal _terminal;
    private readonly Pipe _outputPipe = new();
    private readonly bool _ownsTerminal;
    
    public LegacyHex1bAppTerminalWorkloadAdapter(
        IHex1bTerminal terminal,
        bool ownsTerminal = true)
    {
        _terminal = terminal;
        _ownsTerminal = ownsTerminal;
    }
    
    // === IHex1bAppTerminalWorkloadAdapter (app-side) ===
    
    public void Write(string text)
    {
        // Write to current terminal
        _terminal.Write(text);
        
        // Also buffer for terminal-side reads (future proofing)
        _outputPipe.Writer.Write(Encoding.UTF8.GetBytes(text));
    }
    
    public void Write(ReadOnlySpan<byte> data)
    {
        _terminal.Write(Encoding.UTF8.GetString(data));
        _outputPipe.Writer.Write(data);
    }
    
    public void Flush()
    {
        // Current terminal doesn't have explicit flush
        _outputPipe.Writer.FlushAsync().AsTask().Wait();
    }
    
    public ChannelReader<Hex1bEvent> InputEvents => _terminal.InputEvents;
    
    public int Width => _terminal.Width;
    public int Height => _terminal.Height;
    
    public TerminalCapabilities Capabilities => TerminalCapabilities.Modern;
    // TODO: Could probe current terminal for actual capabilities
    
    // === IHex1bTerminalWorkloadAdapter (terminal-side) ===
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct)
    {
        // Read from the pipe that Write() populates
        var result = await _outputPipe.Reader.ReadAsync(ct);
        var data = result.Buffer.ToArray();
        _outputPipe.Reader.AdvanceTo(result.Buffer.End);
        return data;
    }
    
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // For legacy, we don't use this - input comes through InputEvents
        // This is here for interface compliance, will be used by new terminal
        await Task.CompletedTask;
    }
    
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct)
    {
        // Current terminal handles resize internally via events
        return ValueTask.CompletedTask;
    }
    
    public event Action? Disconnected;
    
    public async ValueTask DisposeAsync()
    {
        if (_ownsTerminal && _terminal is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await _outputPipe.Reader.CompleteAsync();
        await _outputPipe.Writer.CompleteAsync();
    }
}
```

### Refactored Hex1bApp (Phase 1)

```csharp
public class Hex1bApp : IDisposable
{
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;
    
    public Hex1bApp(
        Func<RootContext, Hex1bWidget> builder,
        Hex1bAppOptions? options = null)
    {
        options ??= new Hex1bAppOptions();
        
        if (options.WorkloadAdapter != null)
        {
            // Custom adapter provided
            _adapter = options.WorkloadAdapter;
        }
        else if (options.Terminal != null)
        {
            // Legacy: wrap provided terminal
            _adapter = new LegacyHex1bAppTerminalWorkloadAdapter(
                options.Terminal, 
                ownsTerminal: options.OwnsTerminal ?? false);
        }
        else
        {
            // Legacy: create default console terminal and wrap it
            var terminal = new ConsoleHex1bTerminal(enableMouse: options.EnableMouse);
            _adapter = new LegacyHex1bAppTerminalWorkloadAdapter(
                terminal, 
                ownsTerminal: true);
        }
        
        // All internal code now uses _adapter instead of _terminal
        _context = new Hex1bRenderContext(_adapter, initialTheme);
        // ...
    }
}

// Hex1bRenderContext now uses adapter
public class Hex1bRenderContext
{
    private readonly IHex1bAppTerminalWorkloadAdapter _adapter;
    
    public Hex1bRenderContext(IHex1bAppTerminalWorkloadAdapter adapter, Hex1bTheme? theme)
    {
        _adapter = adapter;
        // ...
    }
    
    public void Write(string text) => _adapter.Write(text);
    public int Width => _adapter.Width;
    public int Height => _adapter.Height;
}
```

### Phase 2: New Terminal Implementation

When the new `Hex1bTerminal` is ready, create a new adapter:

```csharp
/// <summary>
/// Adapter that connects Hex1bApp to the new Hex1bTerminal pipeline.
/// </summary>
public class Hex1bAppTerminalWorkloadAdapter : IHex1bAppTerminalWorkloadAdapter
{
    private readonly Hex1bTerminal _terminal;
    
    // Uses the new terminal's proper APIs
    // No legacy IHex1bTerminal involved
}
```

Since both adapters implement `IHex1bAppTerminalWorkloadAdapter`, switching is just a matter of which adapter is constructed - **Hex1bApp code doesn't change**.

### Phase 3: Deprecate Legacy

Once the new terminal is stable:
1. Mark `LegacyHex1bAppTerminalWorkloadAdapter` as obsolete
2. Mark `IHex1bTerminal`, `ConsoleHex1bTerminal`, `WebSocketHex1bTerminal` as obsolete  
3. Eventually remove them

### Updated Hex1bAppOptions

```csharp
public class Hex1bAppOptions
{
    // === New way (preferred) ===
    
    /// <summary>
    /// Custom workload adapter. When set, the app uses this directly.
    /// </summary>
    public IHex1bAppTerminalWorkloadAdapter? WorkloadAdapter { get; set; }
    
    // === Legacy way (still supported in Phase 1) ===
    
    /// <summary>
    /// [Legacy] The terminal to use. Will be wrapped in LegacyHex1bAppTerminalWorkloadAdapter.
    /// Prefer using WorkloadAdapter instead.
    /// </summary>
    public IHex1bTerminal? Terminal { get; set; }
    
    /// <summary>
    /// [Legacy] Whether the app owns the terminal.
    /// Only applies when Terminal is set.
    /// </summary>
    public bool? OwnsTerminal { get; set; }
    
    // === Options for default construction ===
    
    /// <summary>
    /// Whether to enable mouse support.
    /// Used when neither WorkloadAdapter nor Terminal is set.
    /// </summary>
    public bool EnableMouse { get; set; }
    
    // ... other options
}
```

## Alternative: Layered Pipeline Architecture

When you create a `Hex1bApp` with default options, it builds the entire pipeline internally:

```csharp
public class Hex1bApp : IDisposable
{
    private readonly IHex1bAppWorkloadAdapter _workloadAdapter;
    private readonly Hex1bTerminal? _ownedTerminal;
    private readonly ITerminalPresentationAdapter? _ownedPresentation;
    
    public Hex1bApp(
        Func<RootContext, Hex1bWidget> builder,
        Hex1bAppOptions? options = null)
    {
        options ??= new Hex1bAppOptions();
        
        if (options.WorkloadAdapter != null)
        {
            // Custom adapter provided - use it directly
            _workloadAdapter = options.WorkloadAdapter;
        }
        else
        {
            // Build default pipeline: Console presentation → Terminal → Workload adapter
            _ownedPresentation = CreatePlatformPresentationAdapter(options);
            _ownedTerminal = BuildTerminal(_ownedPresentation, options);
            _workloadAdapter = _ownedTerminal.CreateWorkloadAdapter();
        }
        
        // ... rest of initialization using only _workloadAdapter
    }
    
    private static ITerminalPresentationAdapter CreatePlatformPresentationAdapter(
        Hex1bAppOptions options)
    {
        // Platform-specific console setup:
        // - Linux/macOS: termios raw mode, SIGWINCH handling
        // - Windows: ConPTY or legacy console APIs
        // - Handles mouse enable/disable
        // - Manages alternate screen buffer
        
        return new ConsolePresentationAdapter(new ConsolePresentationOptions
        {
            EnableMouse = options.EnableMouse,
            // ... other options
        });
    }
    
    private static Hex1bTerminal BuildTerminal(
        ITerminalPresentationAdapter presentation,
        Hex1bAppOptions options)
    {
        return new TerminalPipelineBuilder()
            .WithPresentation(presentation)
            .UseAnsiParser()
            .UseCapabilityResponses(presentation.Capabilities)
            .UseStateUpdates()
            .Build(presentation.Width, presentation.Height);
    }
}
```

### Hex1bAppOptions (Revised)

```csharp
public class Hex1bAppOptions
{
    // === Custom adapter (replaces entire default pipeline) ===
    
    /// <summary>
    /// Custom workload adapter. When set, the app uses this instead of
    /// creating its own terminal infrastructure.
    /// </summary>
    public IHex1bAppWorkloadAdapter? WorkloadAdapter { get; set; }
    
    // === Options for default pipeline ===
    
    /// <summary>
    /// Whether to enable mouse support.
    /// Only used when WorkloadAdapter is null (default pipeline).
    /// </summary>
    public bool EnableMouse { get; set; }
    
    /// <summary>
    /// Theme for rendering.
    /// </summary>
    public Hex1bTheme? Theme { get; set; }
    
    /// <summary>
    /// Dynamic theme provider.
    /// </summary>
    public Func<Hex1bTheme>? ThemeProvider { get; set; }
    
    /// <summary>
    /// Whether to enable rescue (error boundary).
    /// </summary>
    public bool EnableRescue { get; set; } = true;
    
    // ... other app-level options
}
```

### Scenario: Default Console App

```csharp
// Simple case - defaults handle everything
var app = new Hex1bApp(ctx => ctx.Text("Hello!"));
await app.RunAsync();

// Internally creates:
// 1. ConsolePresentationAdapter (handles termios/ConPTY, stdin/stdout)
// 2. Hex1bTerminal (parses ANSI, maintains state)
// 3. Hex1bAppWorkloadAdapter (what the app sees)
```

### Scenario: WebSocket Terminal

```csharp
// Web scenario - provide custom adapter connected to WebSocket terminal
app.MapGet("/terminal", async (HttpContext ctx) =>
{
    var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    
    // Build custom pipeline for WebSocket
    var presentation = new WebSocketPresentationAdapter(ws);
    var terminal = new TerminalPipelineBuilder()
        .WithPresentation(presentation)
        .UseAnsiParser()
        .UseCapabilityResponses(TerminalCapabilities.Modern)
        .UseStateUpdates()
        .UseDeltaCompression()  // Optimize for network
        .Build(presentation.Width, presentation.Height);
    
    // Create workload adapter from terminal
    var workloadAdapter = terminal.CreateWorkloadAdapter();
    
    // App uses the custom adapter
    var app = new Hex1bApp(
        ctx => BuildMyUI(ctx),
        new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
    );
    
    await app.RunAsync();
});
```

### Scenario: Testing

```csharp
// Testing - get both sides of the terminal
var terminal = new TerminalPipelineBuilder()
    .WithVirtualPresentation()  // No real I/O, just virtual devices
    .UseAnsiParser()
    .UseStateUpdates()
    .Build(80, 24);

var workloadAdapter = terminal.CreateWorkloadAdapter();

var app = new Hex1bApp(
    ctx => ctx.Button("Click me", e => clicked = true),
    new Hex1bAppOptions { WorkloadAdapter = workloadAdapter }
);

// Run app in background
var appTask = app.RunAsync();

// Use virtual devices to interact
await terminal.Input.SendKeyAsync(Hex1bKey.Enter);
await terminal.Display.WaitForTextAsync("Click me");

// Inspect state
Assert.True(terminal.Display.ContainsText("Click me"));

app.RequestStop();
await appTask;
```

### Internal App Components

All internal components use the workload adapter, never the terminal:

```csharp
// Hex1bRenderContext uses workload adapter for output
public class Hex1bRenderContext
{
    private readonly IHex1bAppWorkloadAdapter _adapter;
    
    public void Write(string text) => _adapter.Write(text);
    public void Flush() => _adapter.Flush();
}

// Input handling uses workload adapter's event channel
public class Hex1bApp
{
    private async Task RunInputLoopAsync()
    {
        await foreach (var evt in _workloadAdapter.InputEvents.ReadAllAsync())
        {
            await HandleInputAsync(evt);
        }
    }
}

// Nodes query terminal info via workload adapter
public abstract class Hex1bNode
{
    protected int TerminalWidth => Context.Adapter.Width;
    protected bool MouseEnabled => Context.Adapter.Capabilities.SupportsMouse;
}
```

### Platform Abstraction in ConsolePresentationAdapter

The default `ConsolePresentationAdapter` handles all platform specifics:

```csharp
public sealed class ConsolePresentationAdapter : ITerminalPresentationAdapter
{
    private readonly IConsoleDriver _driver;
    
    public ConsolePresentationAdapter(ConsolePresentationOptions options)
    {
        // Select platform-specific driver
        _driver = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new WindowsConsoleDriver(options)
            : new UnixConsoleDriver(options);
    }
}

// Unix driver handles termios, SIGWINCH, etc.
internal class UnixConsoleDriver : IConsoleDriver
{
    public void EnterRawMode()
    {
        // Save current termios settings
        // Set raw mode (no echo, no canonical, etc.)
        // Enable mouse if requested
    }
    
    public void ExitRawMode()
    {
        // Restore termios settings
        // Disable mouse
    }
}

// Windows driver handles ConPTY or legacy console
internal class WindowsConsoleDriver : IConsoleDriver
{
    public void EnterRawMode()
    {
        // Enable virtual terminal processing
        // Set console mode for raw input
    }
}
```

## Alternative: Layered Pipeline Architecture

```
┌─────────────────────┐         ┌─────────────────────────────────┐         ┌─────────────────────┐
│      WORKLOAD       │  ────►  │         Hex1bTerminal           │  ────►  │    PRESENTATION     │
│   (upstream side)   │  ◄────  │      (Virtual Terminal)         │  ◄────  │  (downstream side)  │
└─────────────────────┘         └─────────────────────────────────┘         └─────────────────────┘
         │                                    │                                      │
   ANSI output flows TO                Screen buffer                        Rendered output TO
   terminal, input FROM                ANSI parsing                         user, input FROM user
         │                             Delta detection                               │
         │                             Test assertions                               │
         ▼                                                                           ▼
   - Hex1b app (direct API)                                                  - Real console
   - External process (PTY)                                                  - WebSocket
   - Network stream                                                          - SSH connection
   - Pipe/stdin                                                              - null (testing only)
```

### Core Concept

`Hex1bTerminal` is analogous to terminal emulators like **xterm** or **kitty**:

1. **Receives** ANSI escape sequences from a workload (application)
2. **Maintains** an in-memory screen buffer with cell state
3. **Optionally forwards** (potentially optimized) output to a presentation layer

## Target Scenarios

### Scenario 1: Testing Hex1b TUI Features

**Workload**: Hex1b app writes directly via `IHex1bTerminal` interface  
**Presentation**: None (buffer inspection only)

```csharp
// Create pure virtual terminal
var terminal = new Hex1bTerminal(width: 80, height: 24);

// Hex1b app connects directly
var app = new Hex1bApp(builder, new Hex1bAppOptions { Terminal = terminal });

// Drive the app and inspect results
await terminal.SendKeyAsync(Hex1bKey.Tab);
Assert.True(terminal.ContainsText("Expected Text"));
Assert.Equal("Submit", terminal.GetLineTrimmed(5));
```

### Scenario 2: Test Harness for External Processes

**Workload**: External TUI process connected via PTY  
**Presentation**: None (buffer inspection only)

```csharp
// Launch external process and connect via PTY
var processAdapter = new PtyWorkloadAdapter("./my-tui-app", args);
var terminal = new Hex1bTerminal(workload: processAdapter, width: 80, height: 24);

await terminal.StartAsync();

// Wait for process to render expected content
await terminal.WaitForTextAsync("Welcome", timeout: TimeSpan.FromSeconds(5));

// Send input to the process
await terminal.SendTextAsync("hello\n");

// Assert on what the process rendered
Assert.True(terminal.ContainsText("You typed: hello"));

// Cleanup
await processAdapter.DisposeAsync();
```

### Scenario 3: Delta Optimization Layer

**Workload**: Hex1b app (or external TUI)  
**Presentation**: WebSocket with delta protocol support

```csharp
// WebSocket client that understands delta protocol
var wsAdapter = new WebSocketPresentationAdapter(webSocket)
{
    Capabilities = new TerminalCapabilities { SupportsDeltaProtocol = true }
};

var terminal = new Hex1bTerminal(
    presentation: wsAdapter,
    width: wsAdapter.Width,
    height: wsAdapter.Height
);

// Hex1b app connects
var app = new Hex1bApp(builder, new Hex1bAppOptions { Terminal = terminal });

// Terminal tracks dirty cells and only sends changes over WebSocket
// Full 80x24 = 1920 cells, but typical update might be 50-100 cells
await app.RunAsync();
```

### Scenario 4: Web Terminal (Current WebSocket Use Case)

**Workload**: Hex1b app (direct API)  
**Presentation**: WebSocket with raw ANSI passthrough

```csharp
var wsAdapter = new WebSocketPresentationAdapter(webSocket);
var terminal = new Hex1bTerminal(presentation: wsAdapter);

var app = new Hex1bApp(builder, new Hex1bAppOptions { Terminal = terminal });
await app.RunAsync();
```

### Scenario 5: Remote Terminal Proxy

**Workload**: External SSH session  
**Presentation**: Local console

```csharp
// Connect to remote system
var sshAdapter = new SshWorkloadAdapter(host, credentials);

// Display on local console
var consoleAdapter = new ConsolePresentationAdapter();

var terminal = new Hex1bTerminal(
    workload: sshAdapter,
    presentation: consoleAdapter
);

// Terminal acts as a smart proxy, potentially caching/optimizing
await terminal.RunAsync();
```

## Interface Definitions

### Workload Adapter

The workload adapter connects to the "application side" - where the TUI application runs.

```csharp
/// <summary>
/// Upstream connection: Where workload output comes FROM and input goes TO.
/// This is the "application side" of the virtual terminal.
/// </summary>
public interface ITerminalWorkloadAdapter : IAsyncDisposable
{
    /// <summary>
    /// Send input (keystrokes, mouse events as ANSI sequences) TO the workload.
    /// </summary>
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Receive output (ANSI sequences) FROM the workload.
    /// Returns empty memory when stream ends.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Signal a resize to the workload (e.g., SIGWINCH for PTY).
    /// </summary>
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    /// <summary>
    /// The workload has exited or disconnected.
    /// </summary>
    event Action? Disconnected;
    
    /// <summary>
    /// For process-based adapters, the exit code when available.
    /// </summary>
    int? ExitCode { get; }
}
```

### Presentation Adapter

The presentation adapter connects to the "user side" - where output is displayed and input originates.

```csharp
/// <summary>
/// Downstream connection: Where rendered output goes TO and user input comes FROM.
/// This is the "user side" of the virtual terminal.
/// </summary>
public interface ITerminalPresentationAdapter : IAsyncDisposable
{
    /// <summary>
    /// Send rendered output TO the presentation layer.
    /// Format depends on capabilities (raw ANSI, delta protocol, etc.).
    /// </summary>
    ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    /// <summary>
    /// Receive input (keystrokes, mouse as ANSI sequences) FROM the user.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Current terminal dimensions. Presentation layer controls this.
    /// </summary>
    int Width { get; }
    int Height { get; }
    
    /// <summary>
    /// Presentation layer was resized by user.
    /// </summary>
    event Action<int, int>? Resized;
    
    /// <summary>
    /// Capability hints that inform optimization strategies.
    /// </summary>
    TerminalCapabilities Capabilities { get; }
    
    /// <summary>
    /// Flush any buffered output immediately.
    /// </summary>
    ValueTask FlushAsync(CancellationToken ct = default);
}
```

### Terminal Capabilities

```csharp
/// <summary>
/// Capabilities that inform how Hex1bTerminal optimizes output
/// and what features are available.
/// </summary>
public record TerminalCapabilities
{
    /// <summary>
    /// Presentation understands Hex1b delta protocol (not raw ANSI).
    /// Enables significant bandwidth optimization.
    /// </summary>
    public bool SupportsDeltaProtocol { get; init; }
    
    /// <summary>
    /// Presentation supports Sixel graphics.
    /// </summary>
    public bool SupportsSixel { get; init; }
    
    /// <summary>
    /// Presentation supports mouse tracking.
    /// </summary>
    public bool SupportsMouse { get; init; }
    
    /// <summary>
    /// Presentation supports true color (24-bit RGB).
    /// </summary>
    public bool SupportsTrueColor { get; init; }
    
    /// <summary>
    /// Presentation supports 256 colors.
    /// </summary>
    public bool Supports256Colors { get; init; }
    
    /// <summary>
    /// Presentation supports alternate screen buffer.
    /// </summary>
    public bool SupportsAlternateScreen { get; init; }
    
    /// <summary>
    /// Presentation supports bracketed paste mode.
    /// </summary>
    public bool SupportsBracketedPaste { get; init; }
    
    /// <summary>
    /// Default capabilities for a modern terminal.
    /// </summary>
    public static TerminalCapabilities Modern => new()
    {
        SupportsMouse = true,
        SupportsTrueColor = true,
        Supports256Colors = true,
        SupportsAlternateScreen = true,
        SupportsBracketedPaste = true
    };
    
    /// <summary>
    /// Minimal capabilities (dumb terminal).
    /// </summary>
    public static TerminalCapabilities Minimal => new();
}
```

## Hex1bTerminal Implementation

### Constructor Overloads

```csharp
public sealed class Hex1bTerminal : IHex1bTerminal, IAsyncDisposable
{
    /// <summary>
    /// Creates a pure virtual terminal for testing.
    /// No adapters - use IHex1bTerminal interface and inspection methods.
    /// </summary>
    public Hex1bTerminal(int width = 80, int height = 24)
    {
        // Buffer only, no external connections
    }
    
    /// <summary>
    /// Creates a terminal with presentation layer only.
    /// Hex1b app connects via IHex1bTerminal interface.
    /// Output forwards to presentation adapter.
    /// </summary>
    public Hex1bTerminal(ITerminalPresentationAdapter presentation)
    {
        // App writes to buffer, buffer forwards to presentation
        // Input from presentation flows to InputEvents channel
    }
    
    /// <summary>
    /// Creates a terminal connected to an external workload.
    /// Terminal acts as display/input for the workload.
    /// </summary>
    public Hex1bTerminal(
        ITerminalWorkloadAdapter workload,
        int width = 80,
        int height = 24)
    {
        // Workload output populates buffer
        // No forwarding - inspection only
    }
    
    /// <summary>
    /// Creates a terminal that bridges workload to presentation.
    /// Full proxy mode with potential optimization.
    /// </summary>
    public Hex1bTerminal(
        ITerminalWorkloadAdapter workload,
        ITerminalPresentationAdapter presentation)
    {
        // Workload output → buffer → presentation (with optimization)
        // Presentation input → workload
    }
}
```

### Internal State

```csharp
public sealed partial class Hex1bTerminal
{
    // Screen buffer
    private TerminalCell[,] _buffer;
    private TerminalCell[,] _previousBuffer;  // For delta detection
    private readonly HashSet<(int X, int Y)> _dirtyCells;
    
    // Cursor state
    private int _cursorX;
    private int _cursorY;
    private bool _cursorVisible;
    
    // Terminal modes
    private bool _inAlternateScreen;
    private bool _mouseTrackingEnabled;
    private bool _bracketedPasteEnabled;
    
    // SGR state (current text attributes)
    private Hex1bColor? _foreground;
    private Hex1bColor? _background;
    private TextAttributes _attributes;
    
    // Adapters (nullable)
    private readonly ITerminalWorkloadAdapter? _workload;
    private readonly ITerminalPresentationAdapter? _presentation;
    
    // Event channel for IHex1bTerminal
    private readonly Channel<Hex1bEvent> _inputChannel;
    
    // Processing tasks
    private Task? _workloadOutputTask;
    private Task? _presentationInputTask;
}
```

### Test Harness API

```csharp
public sealed partial class Hex1bTerminal
{
    // === Screen Inspection ===
    
    /// <summary>
    /// Gets the character at the specified position.
    /// </summary>
    public char GetChar(int x, int y);
    
    /// <summary>
    /// Gets the full cell state at the specified position.
    /// </summary>
    public TerminalCell GetCell(int x, int y);
    
    /// <summary>
    /// Gets a line of text (0-based).
    /// </summary>
    public string GetLine(int lineIndex);
    
    /// <summary>
    /// Gets all screen content as a single string.
    /// </summary>
    public string GetScreenText();
    
    /// <summary>
    /// Checks if the screen contains the specified text.
    /// </summary>
    public bool ContainsText(string text);
    
    /// <summary>
    /// Finds all positions where text appears.
    /// </summary>
    public IReadOnlyList<(int Line, int Column)> FindText(string text);
    
    // === Waiting for State ===
    
    /// <summary>
    /// Waits until the specified text appears on screen.
    /// </summary>
    public Task WaitForTextAsync(
        string text, 
        TimeSpan? timeout = null, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Waits until a condition is met on the screen state.
    /// </summary>
    public Task WaitForConditionAsync(
        Func<Hex1bTerminal, bool> condition,
        TimeSpan? timeout = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Waits for the screen to stabilize (no changes for duration).
    /// </summary>
    public Task WaitForIdleAsync(
        TimeSpan stableDuration,
        TimeSpan? timeout = null,
        CancellationToken ct = default);
    
    // === Input Injection ===
    
    /// <summary>
    /// Sends a key event to the terminal.
    /// Routes to workload if connected, or to InputEvents channel.
    /// </summary>
    public ValueTask SendKeyAsync(Hex1bKey key, Hex1bModifiers modifiers = default);
    
    /// <summary>
    /// Types a string of characters.
    /// </summary>
    public ValueTask SendTextAsync(string text);
    
    /// <summary>
    /// Sends a mouse event.
    /// </summary>
    public ValueTask SendMouseAsync(MouseButton button, MouseAction action, int x, int y);
    
    // === State Queries ===
    
    /// <summary>
    /// Current cursor position.
    /// </summary>
    public (int X, int Y) CursorPosition => (_cursorX, _cursorY);
    
    /// <summary>
    /// Whether alternate screen is active.
    /// </summary>
    public bool InAlternateScreen => _inAlternateScreen;
    
    /// <summary>
    /// Gets the raw output written (for debugging).
    /// </summary>
    public string RawOutput { get; }
}
```

## Data Flow

### Flow 1: Testing Hex1b App (No Adapters)

```
Hex1bApp                     Hex1bTerminal
    │                              │
    │── Write("Hello") ───────────►│
    │                              │── Parse ANSI
    │                              │── Update buffer
    │                              │
    │◄── InputEvents channel ──────│◄── SendKeyAsync(Enter)
    │                              │
    │                              │── ContainsText("Hello") → true
```

### Flow 2: External Process Testing (Workload Adapter Only)

```
External Process        PtyWorkloadAdapter          Hex1bTerminal
      │                        │                          │
      │── ANSI output ────────►│                          │
      │                        │── ReadOutputAsync() ────►│
      │                        │                          │── Parse ANSI
      │                        │                          │── Update buffer
      │                        │                          │
      │                        │◄── WriteInputAsync() ────│◄── SendKeyAsync()
      │◄── stdin ──────────────│                          │
      │                        │                          │
      │                        │                          │── ContainsText() ✓
```

### Flow 3: Web Terminal (Presentation Adapter Only)

```
Hex1bApp          Hex1bTerminal          WebSocketPresentationAdapter          Browser
    │                   │                            │                            │
    │── Write() ───────►│                            │                            │
    │                   │── Parse & buffer           │                            │
    │                   │── Compute delta            │                            │
    │                   │── WriteOutputAsync() ─────►│                            │
    │                   │                            │── WebSocket.Send() ───────►│
    │                   │                            │                            │
    │                   │◄── ReadInputAsync() ───────│◄── WebSocket.Recv() ───────│
    │◄── InputEvents ───│                            │                            │
```

### Flow 4: Full Proxy (Both Adapters)

```
Workload            Hex1bTerminal            Presentation
    │                     │                       │
    │── output ──────────►│                       │
    │                     │── parse, buffer       │
    │                     │── delta compute       │
    │                     │── WriteOutputAsync() ►│
    │                     │                       │
    │                     │◄── ReadInputAsync() ──│
    │◄── WriteInputAsync()│                       │
```

## Adapter Implementations

### Console Presentation Adapter

```csharp
public sealed class ConsolePresentationAdapter : ITerminalPresentationAdapter
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<byte[]> _inputChannel;
    
    public int Width => Console.WindowWidth;
    public int Height => Console.WindowHeight;
    
    public TerminalCapabilities Capabilities => new()
    {
        SupportsMouse = true,  // Most modern terminals
        SupportsTrueColor = true,
        SupportsAlternateScreen = true
    };
    
    public event Action<int, int>? Resized;
    
    public ConsolePresentationAdapter()
    {
        Console.TreatControlCAsInput = true;
        // Register SIGWINCH on Linux/macOS
        // Start input reading task
    }
    
    public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        Console.Write(Encoding.UTF8.GetString(data.Span));
        return ValueTask.CompletedTask;
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct)
    {
        // Read from Console with escape sequence buffering
    }
}
```

### WebSocket Presentation Adapter

```csharp
public sealed class WebSocketPresentationAdapter : ITerminalPresentationAdapter
{
    private readonly WebSocket _webSocket;
    
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TerminalCapabilities Capabilities { get; init; }
    
    public event Action<int, int>? Resized;
    
    public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (Capabilities.SupportsDeltaProtocol)
        {
            // Data is already in delta format
            await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
        }
        else
        {
            // Raw ANSI
            await _webSocket.SendAsync(data, WebSocketMessageType.Text, true, ct);
        }
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct)
    {
        var buffer = new byte[1024];
        var result = await _webSocket.ReceiveAsync(buffer, ct);
        
        // Handle JSON control messages (resize) separately
        // Return ANSI input sequences
    }
}
```

### PTY Workload Adapter

```csharp
public sealed class PtyWorkloadAdapter : ITerminalWorkloadAdapter
{
    private readonly Process _process;
    private readonly Stream _ptyStream;
    
    public event Action? Disconnected;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;
    
    public PtyWorkloadAdapter(string command, string[] args, int width, int height)
    {
        // On Linux: Use forkpty() via P/Invoke or pty.net library
        // On Windows: Use ConPTY
        // On macOS: Use forkpty()
    }
    
    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _ptyStream.WriteAsync(data, ct);
    }
    
    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        var read = await _ptyStream.ReadAsync(buffer, ct);
        return buffer.AsMemory(0, read);
    }
    
    public ValueTask ResizeAsync(int width, int height, CancellationToken ct)
    {
        // Send TIOCSWINSZ ioctl to PTY
        return ValueTask.CompletedTask;
    }
}
```

## Delta Protocol (Future Enhancement)

When `SupportsDeltaProtocol` is true, the terminal sends structured updates instead of raw ANSI:

```csharp
public record TerminalDelta
{
    /// <summary>
    /// Cells that changed since last frame.
    /// </summary>
    public required IReadOnlyList<CellUpdate> Cells { get; init; }
    
    /// <summary>
    /// New cursor position, if changed.
    /// </summary>
    public (int X, int Y)? CursorPosition { get; init; }
    
    /// <summary>
    /// Cursor visibility changed.
    /// </summary>
    public bool? CursorVisible { get; init; }
}

public record CellUpdate(int X, int Y, char Character, Hex1bColor? Fg, Hex1bColor? Bg, TextAttributes Attrs);
```

This could reduce bandwidth significantly for typical TUI updates (typing in a text box, moving selection) from full screen redraws to just changed cells.

## Migration Path

### Phase 1: Define Interfaces (Non-Breaking)
- Add `ITerminalWorkloadAdapter` and `ITerminalPresentationAdapter`
- Add `TerminalCapabilities`
- Keep existing terminals unchanged

### Phase 2: Implement New Hex1bTerminal
- Create new unified `Hex1bTerminal` supporting all modes
- Implement adapter-based architecture
- Ensure feature parity with existing terminals

### Phase 3: Create Adapters
- `ConsolePresentationAdapter`
- `WebSocketPresentationAdapter`
- `PtyWorkloadAdapter`

### Phase 4: Migrate Usages
- Update `Hex1bApp` to work with new terminal
- Update examples and tests
- Deprecate old terminal types

### Phase 5: Cleanup
- Remove deprecated types
- Finalize public API

## Alternative: Layered Pipeline Architecture

A more flexible design treats the terminal as a **bidirectional pipeline** with composable layers. The workload and presentation adapters are just the endpoints, with processing layers in between.

### Conceptual Model

```
                              WORKLOAD DIRECTION
                    (ANSI sequences from app/process)
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           WORKLOAD ENDPOINT                                  │
│                     (ITerminalWorkloadAdapter)                              │
│              Direct API / PTY / Pipe / Network Stream                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           WORKLOAD PUMP                                      │
│              Async reader, buffers, backpressure handling                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ANSI PARSER LAYER                                    │
│           Tokenizes escape sequences, emits structured events               │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      CAPABILITY RESPONSE LAYER                               │
│         Intercepts DA1/DA2/DA3 queries, responds with capabilities          │
│         Handles DECRQSS, OSC queries, Sixel detection, etc.                 │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        TERMINAL STATE (CORE)                                 │
│                                                                              │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │
│   │Screen Buffer│  │ Cursor Pos  │  │ SGR State   │  │ Mode Flags  │       │
│   │ [cells]     │  │ (x, y, vis) │  │ (fg,bg,attr)│  │ (alt,mouse) │       │
│   └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘       │
│                                                                              │
│   • Queryable (for testing/inspection)                                       │
│   • Tracks dirty regions                                                     │
│   • Emits state change events                                                │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         OUTPUT TRANSFORM LAYER                               │
│              Delta computation, compression, format conversion              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION PUMP                                    │
│              Async writer, batching, flush control                          │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        PRESENTATION ENDPOINT                                 │
│                    (ITerminalPresentationAdapter)                           │
│                Console / WebSocket / SSH / null                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                           PRESENTATION DIRECTION
                       (to user's eyes/input device)
```

### Bidirectional Flow

Each layer processes data in **both directions**:

```
WORKLOAD → PRESENTATION (Output flow)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Raw bytes → Parse ANSI → Update state → Transform → Send to display

PRESENTATION → WORKLOAD (Input flow)  
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
User keypress → Encode as ANSI → (layers can intercept) → Send to workload
```

### Layer Interface

```csharp
/// <summary>
/// A bidirectional processing layer in the terminal pipeline.
/// </summary>
public interface ITerminalLayer
{
    /// <summary>
    /// Process data flowing from workload toward presentation.
    /// Called with parsed terminal events (not raw bytes).
    /// </summary>
    /// <param name="event">The terminal event to process.</param>
    /// <param name="state">Current terminal state (readable/writable).</param>
    /// <param name="next">Delegate to pass event to next layer.</param>
    /// <returns>Optionally transformed or filtered events.</returns>
    ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next);
    
    /// <summary>
    /// Process data flowing from presentation toward workload.
    /// Called with input events (keys, mouse, etc.).
    /// </summary>
    /// <param name="event">The input event to process.</param>
    /// <param name="state">Current terminal state (readable).</param>
    /// <param name="next">Delegate to pass event to next layer.</param>
    /// <returns>Optionally transformed or filtered events.</returns>
    ValueTask ProcessInputAsync(
        Hex1bEvent @event,
        ITerminalState state,
        Func<Hex1bEvent, ValueTask> next);
}
```

### Terminal State Interface

```csharp
/// <summary>
/// The central state of the virtual terminal.
/// This is the "core" that layers read from and write to.
/// </summary>
public interface ITerminalState
{
    // === Dimensions ===
    int Width { get; }
    int Height { get; }
    
    // === Screen Buffer ===
    TerminalCell GetCell(int x, int y);
    void SetCell(int x, int y, TerminalCell cell);
    ReadOnlySpan2D<TerminalCell> GetBuffer();
    
    // === Cursor ===
    int CursorX { get; set; }
    int CursorY { get; set; }
    bool CursorVisible { get; set; }
    
    // === SGR (Text Attributes) ===
    Hex1bColor? Foreground { get; set; }
    Hex1bColor? Background { get; set; }
    TextAttributes Attributes { get; set; }
    
    // === Terminal Modes ===
    bool AlternateScreenActive { get; set; }
    bool MouseTrackingEnabled { get; set; }
    MouseTrackingMode MouseMode { get; set; }
    bool BracketedPasteEnabled { get; set; }
    
    // === Dirty Tracking ===
    IReadOnlySet<(int X, int Y)> DirtyCells { get; }
    void MarkDirty(int x, int y);
    void MarkAllDirty();
    void ClearDirty();
    
    // === Events ===
    event Action<TerminalStateChange>? StateChanged;
}
```

### Example Layers

#### 1. ANSI Parser Layer

Converts raw bytes into structured terminal events:

```csharp
public class AnsiParserLayer : ITerminalLayer
{
    private readonly AnsiParser _parser = new();
    
    public async ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next)
    {
        if (@event is RawBytesEvent raw)
        {
            // Parse raw bytes into structured events
            foreach (var parsed in _parser.Parse(raw.Data))
            {
                await next(parsed);
            }
        }
        else
        {
            await next(@event);
        }
    }
    
    public ValueTask ProcessInputAsync(
        Hex1bEvent @event,
        ITerminalState state,
        Func<Hex1bEvent, ValueTask> next)
    {
        // Input flows through unchanged (already structured)
        return next(@event);
    }
}
```

#### 2. Capability Response Layer

Intercepts terminal queries and responds with capabilities:

```csharp
public class CapabilityResponseLayer : ITerminalLayer
{
    private readonly TerminalCapabilities _capabilities;
    
    public CapabilityResponseLayer(TerminalCapabilities capabilities)
    {
        _capabilities = capabilities;
    }
    
    public async ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next)
    {
        if (@event is DeviceAttributesQueryEvent da1)
        {
            // Intercept DA1 query - respond instead of forwarding
            var response = BuildDA1Response(_capabilities);
            // Send response back toward workload (reverse direction!)
            await SendToWorkloadAsync(response);
            return; // Don't forward the query
        }
        
        if (@event is SixelQueryEvent)
        {
            // Similar handling for Sixel capability queries
        }
        
        await next(@event);
    }
    
    private string BuildDA1Response(TerminalCapabilities caps)
    {
        // ESC [ ? 62 ; ... c
        var features = new List<int> { 62 }; // VT220
        if (caps.SupportsSixel) features.Add(4);
        // ... more features
        return $"\x1b[?{string.Join(";", features)}c";
    }
}
```

#### 3. State Update Layer

Applies terminal events to the state:

```csharp
public class StateUpdateLayer : ITerminalLayer
{
    public async ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next)
    {
        switch (@event)
        {
            case PrintEvent print:
                // Write character to buffer at cursor, advance cursor
                state.SetCell(state.CursorX, state.CursorY, 
                    new TerminalCell(print.Char, state.Foreground, state.Background));
                state.MarkDirty(state.CursorX, state.CursorY);
                state.CursorX++;
                WrapCursorIfNeeded(state);
                break;
                
            case CursorMoveEvent move:
                state.CursorX = move.X;
                state.CursorY = move.Y;
                break;
                
            case SgrEvent sgr:
                ApplySgr(state, sgr);
                break;
                
            case ClearScreenEvent clear:
                ClearRegion(state, clear.Mode);
                break;
                
            // ... etc
        }
        
        await next(@event); // Still forward for potential logging/debugging
    }
}
```

#### 4. Delta Transform Layer

Computes minimal updates for presentation:

```csharp
public class DeltaTransformLayer : ITerminalLayer
{
    public async ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next)
    {
        // This layer doesn't process individual events
        // Instead, it's triggered on flush
        await next(@event);
    }
    
    /// <summary>
    /// Called when it's time to send updates to presentation.
    /// </summary>
    public TerminalDelta ComputeDelta(ITerminalState state)
    {
        var changes = new List<CellUpdate>();
        
        foreach (var (x, y) in state.DirtyCells)
        {
            var cell = state.GetCell(x, y);
            changes.Add(new CellUpdate(x, y, cell));
        }
        
        state.ClearDirty();
        
        return new TerminalDelta
        {
            Cells = changes,
            CursorPosition = (state.CursorX, state.CursorY),
            CursorVisible = state.CursorVisible
        };
    }
}
```

### Terminal Pipeline Builder

```csharp
public class TerminalPipelineBuilder
{
    private readonly List<ITerminalLayer> _layers = new();
    private ITerminalWorkloadAdapter? _workload;
    private ITerminalPresentationAdapter? _presentation;
    
    /// <summary>
    /// Set the workload endpoint (where app/process connects).
    /// </summary>
    public TerminalPipelineBuilder WithWorkload(ITerminalWorkloadAdapter adapter)
    {
        _workload = adapter;
        return this;
    }
    
    /// <summary>
    /// Set the workload to be the direct API (no external process).
    /// </summary>
    public TerminalPipelineBuilder WithDirectWorkload()
    {
        _workload = null; // Direct IHex1bTerminal usage
        return this;
    }
    
    /// <summary>
    /// Set the presentation endpoint (where output goes).
    /// </summary>
    public TerminalPipelineBuilder WithPresentation(ITerminalPresentationAdapter adapter)
    {
        _presentation = adapter;
        return this;
    }
    
    /// <summary>
    /// No presentation (testing/inspection only).
    /// </summary>
    public TerminalPipelineBuilder WithNoPresentation()
    {
        _presentation = null;
        return this;
    }
    
    /// <summary>
    /// Add a processing layer.
    /// </summary>
    public TerminalPipelineBuilder Use(ITerminalLayer layer)
    {
        _layers.Add(layer);
        return this;
    }
    
    /// <summary>
    /// Add the standard ANSI parsing layer.
    /// </summary>
    public TerminalPipelineBuilder UseAnsiParser()
    {
        _layers.Add(new AnsiParserLayer());
        return this;
    }
    
    /// <summary>
    /// Add capability response handling.
    /// </summary>
    public TerminalPipelineBuilder UseCapabilityResponses(TerminalCapabilities capabilities)
    {
        _layers.Add(new CapabilityResponseLayer(capabilities));
        return this;
    }
    
    /// <summary>
    /// Add the state update layer (required for most scenarios).
    /// </summary>
    public TerminalPipelineBuilder UseStateUpdates()
    {
        _layers.Add(new StateUpdateLayer());
        return this;
    }
    
    /// <summary>
    /// Add delta compression for output.
    /// </summary>
    public TerminalPipelineBuilder UseDeltaCompression()
    {
        _layers.Add(new DeltaTransformLayer());
        return this;
    }
    
    /// <summary>
    /// Build the terminal with configured pipeline.
    /// </summary>
    public Hex1bTerminal Build(int width = 80, int height = 24)
    {
        return new Hex1bTerminal(_workload, _presentation, _layers, width, height);
    }
}
```

### Usage Examples

#### Testing Scenario

```csharp
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload()           // App writes directly
    .WithNoPresentation()           // No output forwarding
    .UseAnsiParser()                // Parse ANSI sequences
    .UseStateUpdates()              // Update screen buffer
    .Build(80, 24);

var app = new Hex1bApp(builder, new() { Terminal = terminal });

// Test as before
await terminal.SendKeyAsync(Hex1bKey.Enter);
Assert.True(terminal.State.ContainsText("Expected"));
```

#### External Process Testing

```csharp
var terminal = new TerminalPipelineBuilder()
    .WithWorkload(new PtyWorkloadAdapter("./my-app"))
    .WithNoPresentation()
    .UseAnsiParser()
    .UseCapabilityResponses(TerminalCapabilities.Modern)  // Respond to DA1
    .UseStateUpdates()
    .Build(80, 24);

await terminal.StartAsync();
await terminal.WaitForTextAsync("Ready");
```

#### Web Terminal with Delta Optimization

```csharp
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload()
    .WithPresentation(new WebSocketPresentationAdapter(ws))
    .UseAnsiParser()
    .UseCapabilityResponses(TerminalCapabilities.Modern)
    .UseStateUpdates()
    .UseDeltaCompression()          // Only send changes
    .Build(ws.Width, ws.Height);

var app = new Hex1bApp(builder, new() { Terminal = terminal });
await app.RunAsync();
```

#### Logging/Debugging Layer

```csharp
public class LoggingLayer : ITerminalLayer
{
    private readonly ILogger _logger;
    
    public async ValueTask ProcessOutputAsync(
        TerminalEvent @event,
        ITerminalState state,
        Func<TerminalEvent, ValueTask> next)
    {
        _logger.LogDebug("Output event: {Event}", @event);
        await next(@event);
    }
    
    public async ValueTask ProcessInputAsync(
        Hex1bEvent @event,
        ITerminalState state,
        Func<Hex1bEvent, ValueTask> next)
    {
        _logger.LogDebug("Input event: {Event}", @event);
        await next(@event);
    }
}

// Add to pipeline
builder.Use(new LoggingLayer(logger));
```

### Key Insight: Virtual Devices

A terminal with "no presentation" isn't a special case - it's a terminal with **virtual devices** attached:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          VIRTUAL TERMINAL                                    │
│                                                                              │
│   ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐      │
│   │ Virtual Keyboard│     │  Terminal Core  │     │ Virtual Display │      │
│   │   SendKeyAsync()│────►│  (State/Buffer) │────►│  GetScreenText()│      │
│   │ Virtual Mouse   │     │                 │     │  GetCell(x,y)   │      │
│   │  SendMouseAsync()────►│                 │────►│  ContainsText() │      │
│   └─────────────────┘     └─────────────────┘     └─────────────────┘      │
│                                                                              │
│          INPUT                  STATE                   OUTPUT              │
│        (inject)              (queryable)              (inspect)             │
└─────────────────────────────────────────────────────────────────────────────┘
```

This reframes the architecture elegantly:

| Scenario | Input Device | Output Device |
|----------|--------------|---------------|
| **Testing** | Virtual keyboard/mouse | Virtual display (buffer) |
| **Console app** | Console keyboard/mouse | Console display |
| **Web terminal** | WebSocket input | WebSocket output |
| **Process harness** | Virtual keyboard → PTY stdin | PTY stdout → Virtual display |

**The "testing" scenario isn't special** - it's just using virtual devices instead of real ones. The presentation layer that "drops traffic" is really a **virtual display** that captures output for inspection rather than rendering it.

```csharp
// These are equivalent mental models:

// Model 1: "No presentation, inspection only"
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload()
    .WithNoPresentation()  // <-- "drops" output
    .Build();

// Model 2: "Virtual devices attached"
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload()
    .WithVirtualDevices()  // <-- virtual display captures output
    .Build();

// In both cases:
terminal.VirtualKeyboard.SendKey(Hex1bKey.Enter);
var text = terminal.VirtualDisplay.GetScreenText();
```

### Virtual Device Interfaces

```csharp
/// <summary>
/// Virtual input device for programmatic control.
/// </summary>
public interface IVirtualInputDevice
{
    /// <summary>
    /// Inject a key press.
    /// </summary>
    ValueTask SendKeyAsync(Hex1bKey key, Hex1bModifiers modifiers = default);
    
    /// <summary>
    /// Type a string of characters.
    /// </summary>
    ValueTask SendTextAsync(string text);
    
    /// <summary>
    /// Inject a mouse event.
    /// </summary>
    ValueTask SendMouseAsync(MouseButton button, MouseAction action, int x, int y);
    
    /// <summary>
    /// Inject a paste event (with bracketed paste if enabled).
    /// </summary>
    ValueTask SendPasteAsync(string text);
    
    /// <summary>
    /// Inject a resize event.
    /// </summary>
    ValueTask SendResizeAsync(int width, int height);
}

/// <summary>
/// Virtual display for programmatic inspection.
/// </summary>
public interface IVirtualDisplay
{
    /// <summary>
    /// Current dimensions.
    /// </summary>
    int Width { get; }
    int Height { get; }
    
    /// <summary>
    /// Get a specific cell.
    /// </summary>
    TerminalCell GetCell(int x, int y);
    
    /// <summary>
    /// Get all screen content as text.
    /// </summary>
    string GetScreenText();
    
    /// <summary>
    /// Get a specific line.
    /// </summary>
    string GetLine(int lineIndex);
    
    /// <summary>
    /// Check if text exists on screen.
    /// </summary>
    bool ContainsText(string text);
    
    /// <summary>
    /// Find all occurrences of text.
    /// </summary>
    IReadOnlyList<(int Line, int Column)> FindText(string text);
    
    /// <summary>
    /// Wait for text to appear.
    /// </summary>
    Task WaitForTextAsync(string text, TimeSpan? timeout = null);
    
    /// <summary>
    /// Wait for screen to stabilize.
    /// </summary>
    Task WaitForIdleAsync(TimeSpan stableDuration, TimeSpan? timeout = null);
    
    /// <summary>
    /// Current cursor position.
    /// </summary>
    (int X, int Y) CursorPosition { get; }
    
    /// <summary>
    /// Event raised when display content changes.
    /// </summary>
    event Action<DisplayChangedEventArgs>? Changed;
}
```

### Unified Terminal Interface

With virtual devices as first-class concepts:

```csharp
public sealed class Hex1bTerminal : IHex1bTerminal, IAsyncDisposable
{
    // The virtual devices (always available)
    public IVirtualInputDevice Input { get; }
    public IVirtualDisplay Display { get; }
    
    // Core state (the terminal emulator state machine)
    public ITerminalState State { get; }
    
    // The pipeline layers
    private readonly IReadOnlyList<ITerminalLayer> _layers;
    
    // Optional external connections (adapters bridge to real devices)
    private readonly ITerminalWorkloadAdapter? _workloadAdapter;
    private readonly ITerminalPresentationAdapter? _presentationAdapter;
}
```

### How Adapters Fit In

Adapters are **bridges** between virtual devices and real ones:

```
                                    WORKLOAD SIDE
                         (app/process that generates ANSI)
                                        │
                                        ▼
               ┌────────────────────────────────────────────┐
               │          Workload Adapter                  │
               │   (DirectAdapter / PtyAdapter / etc.)      │
               └────────────────────────┬───────────────────┘
                                        │
                                        ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                              Hex1bTerminal                                 │
│                                                                            │
│  ┌──────────────────┐        ┌──────────────┐        ┌─────────────────┐ │
│  │ IVirtualInputDevice│◄─────│TerminalState │───────►│IVirtualDisplay  │ │
│  └────────▲─────────┘        └──────────────┘        └───────┬─────────┘ │
│           │                                                   │           │
└───────────┼───────────────────────────────────────────────────┼───────────┘
            │                                                   │
            │ (bridge)                                          │ (bridge)
            │                                                   │
   ┌────────┴────────┐                                ┌────────┴────────┐
   │  Presentation   │                                │  Presentation   │
   │  Adapter        │                                │  Adapter        │
   │  (input side)   │                                │  (output side)  │
   └────────▲────────┘                                └────────┬────────┘
            │                                                   │
            │                                                   ▼
                                PRESENTATION SIDE
                         (console/websocket/virtual)
```

**Key principle: No special access.** Even a Hex1bApp running in-process uses a workload adapter:

```csharp
/// <summary>
/// Workload adapter for in-process Hex1b apps.
/// Provides an IHex1bTerminal interface that routes through the pipeline.
/// </summary>
public class DirectWorkloadAdapter : ITerminalWorkloadAdapter
{
    private readonly Pipe _outputPipe = new();  // App writes here
    private readonly Pipe _inputPipe = new();   // App reads from here
    
    /// <summary>
    /// The terminal interface that Hex1bApp uses.
    /// This is what gets passed to Hex1bAppOptions.Terminal.
    /// </summary>
    public IHex1bTerminal Terminal { get; }
    
    public DirectWorkloadAdapter(int width, int height)
    {
        Terminal = new DirectWorkloadTerminal(this, width, height);
    }
    
    // ITerminalWorkloadAdapter implementation
    public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct)
        => _outputPipe.Reader.ReadAsync(ct);  // Terminal reads app's output
    
    public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        => _inputPipe.Writer.WriteAsync(data, ct);  // Terminal sends input to app
}

/// <summary>
/// The IHex1bTerminal that the app sees. Routes through the adapter.
/// </summary>
internal class DirectWorkloadTerminal : IHex1bTerminal
{
    private readonly DirectWorkloadAdapter _adapter;
    
    public void Write(string text)
    {
        // Write to the pipe that the terminal reads from
        _adapter.OutputPipe.Write(Encoding.UTF8.GetBytes(text));
    }
    
    public ChannelReader<Hex1bEvent> InputEvents => _adapter.InputChannel.Reader;
    
    // ... etc
}
```

### Uniform Data Flow

With this design, **all scenarios use identical data flow**:

```
┌─────────────────┐      ┌────────────────┐      ┌─────────────────────────┐
│   Hex1bApp      │      │ DirectWorkload │      │                         │
│                 │─────►│ Adapter        │─────►│                         │
│ app.Write(...)  │      │                │      │                         │
└─────────────────┘      └────────────────┘      │                         │
                                                  │     Hex1bTerminal       │
┌─────────────────┐      ┌────────────────┐      │                         │
│ External Process│      │ PtyWorkload    │      │  [layers, state,        │
│ (vim, htop)     │─────►│ Adapter        │─────►│   virtual devices]      │
│                 │      │                │      │                         │
└─────────────────┘      └────────────────┘      │                         │
                                                  │                         │
┌─────────────────┐      ┌────────────────┐      │                         │
│ Network Stream  │      │ StreamWorkload │      │                         │
│ (SSH session)   │─────►│ Adapter        │─────►│                         │
│                 │      │                │      │                         │
└─────────────────┘      └────────────────┘      └─────────────────────────┘
```

The terminal doesn't know or care what's on the other side of the workload adapter. It just:
1. Reads ANSI output from the adapter
2. Parses and updates state
3. Accepts input from virtual keyboard (which adapters can feed)
4. Sends input back through the adapter to the workload

### Complete Scenario Table

| Scenario | Workload Adapter | Presentation Adapter |
|----------|------------------|---------------------|
| **Unit testing Hex1b app** | `DirectWorkloadAdapter` | None (virtual display) |
| **Console app** | `DirectWorkloadAdapter` | `ConsolePresentationAdapter` |
| **Web terminal** | `DirectWorkloadAdapter` | `WebSocketPresentationAdapter` |
| **Testing external process** | `PtyWorkloadAdapter` | None (virtual display) |
| **Terminal proxy** | `PtyWorkloadAdapter` | `ConsolePresentationAdapter` |
| **SSH client** | `SshWorkloadAdapter` | `ConsolePresentationAdapter` |

### Updated Pipeline Builder

```csharp
public class TerminalPipelineBuilder
{
    private ITerminalWorkloadAdapter? _workload;
    private ITerminalPresentationAdapter? _presentation;
    private readonly List<ITerminalLayer> _layers = new();
    
    /// <summary>
    /// Use a direct (in-process) workload adapter for Hex1bApp.
    /// Returns the IHex1bTerminal to pass to the app.
    /// </summary>
    public TerminalPipelineBuilder WithDirectWorkload(out IHex1bTerminal appTerminal)
    {
        var adapter = new DirectWorkloadAdapter();
        _workload = adapter;
        appTerminal = adapter.Terminal;
        return this;
    }
    
    /// <summary>
    /// Connect to an external process via PTY.
    /// </summary>
    public TerminalPipelineBuilder WithPtyWorkload(string command, params string[] args)
    {
        _workload = new PtyWorkloadAdapter(command, args);
        return this;
    }
    
    /// <summary>
    /// Use a custom workload adapter.
    /// </summary>
    public TerminalPipelineBuilder WithWorkload(ITerminalWorkloadAdapter adapter)
    {
        _workload = adapter;
        return this;
    }
    
    /// <summary>
    /// No presentation - use virtual display only.
    /// </summary>
    public TerminalPipelineBuilder WithVirtualPresentation()
    {
        _presentation = null;
        return this;
    }
    
    /// <summary>
    /// Use console for presentation.
    /// </summary>
    public TerminalPipelineBuilder WithConsolePresentation()
    {
        _presentation = new ConsolePresentationAdapter();
        return this;
    }
    
    /// <summary>
    /// Use WebSocket for presentation.
    /// </summary>
    public TerminalPipelineBuilder WithWebSocketPresentation(WebSocket ws)
    {
        _presentation = new WebSocketPresentationAdapter(ws);
        return this;
    }
    
    // ... layer methods ...
    
    public Hex1bTerminal Build(int width = 80, int height = 24)
    {
        ArgumentNullException.ThrowIfNull(_workload, nameof(_workload));
        return new Hex1bTerminal(_workload, _presentation, _layers, width, height);
    }
}
```

### Usage Examples (Revised)

#### Testing Hex1b App

```csharp
// Build terminal with direct workload adapter
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload(out var appTerminal)  // Returns IHex1bTerminal for app
    .WithVirtualPresentation()                 // Virtual display for inspection
    .UseAnsiParser()
    .UseStateUpdates()
    .Build(80, 24);

// App uses the adapter's terminal interface
var app = new Hex1bApp(builder, new Hex1bAppOptions { Terminal = appTerminal });

// Start both
var appTask = app.RunAsync();

// Interact via virtual devices
await terminal.Input.SendKeyAsync(Hex1bKey.Tab);
await terminal.Display.WaitForTextAsync("Expected");
Assert.True(terminal.Display.ContainsText("Expected"));

// Cleanup
app.RequestStop();
await appTask;
```

#### Console Application

```csharp
var terminal = new TerminalPipelineBuilder()
    .WithDirectWorkload(out var appTerminal)
    .WithConsolePresentation()  // Real console I/O
    .UseAnsiParser()
    .UseCapabilityResponses(TerminalCapabilities.Modern)
    .UseStateUpdates()
    .Build(Console.WindowWidth, Console.WindowHeight);

var app = new Hex1bApp(builder, new Hex1bAppOptions { Terminal = appTerminal });
await app.RunAsync();
```

#### Testing External Process

```csharp
var terminal = new TerminalPipelineBuilder()
    .WithPtyWorkload("vim", "test.txt")  // Launch vim
    .WithVirtualPresentation()            // Capture output
    .UseAnsiParser()
    .UseCapabilityResponses(TerminalCapabilities.Modern)
    .UseStateUpdates()
    .Build(80, 24);

await terminal.StartAsync();

// Wait for vim to start
await terminal.Display.WaitForTextAsync("VIM");

// Type some text
await terminal.Input.SendTextAsync("iHello, World!");
await terminal.Input.SendKeyAsync(Hex1bKey.Escape);

// Verify
Assert.True(terminal.Display.ContainsText("Hello, World!"));
```

### Advantages of Uniform Access

| Benefit | Description |
|---------|-------------|
| **Composable** | Mix and match layers for different scenarios |
| **Testable** | Each layer can be unit tested in isolation |
| **Extensible** | Add new capabilities without modifying core |
| **Observable** | Insert logging/metrics layers anywhere |
| **Flexible** | Same core supports testing, proxying, optimization |
| **Bidirectional** | Input and output handling in same layer abstraction |
| **Uniform** | Testing isn't special - just uses virtual devices |

### Layer Ordering Considerations

```
Workload side (closest to app/process):
  1. Workload Pump (async I/O)
  2. ANSI Parser (raw → structured)
  3. Capability Response (intercept queries)
  4. State Update (apply to buffer)
  5. Delta Transform (compute changes)
  6. Presentation Pump (async I/O)
Presentation side (closest to user):
```

Some layers only make sense in certain positions:
- **AnsiParser** must come before **StateUpdate**
- **DeltaTransform** must come after **StateUpdate**
- **CapabilityResponse** should come before **StateUpdate** (so queries don't modify state)

## Open Questions

1. **Naming**: Should we rename `Hex1bTerminal` to something like `VirtualTerminal` or keep the name?

2. **Sync vs Async**: Current `Write()` is sync. Should adapter methods be all async, with sync wrappers?

3. **Threading Model**: Should the terminal pump data on background threads, or let the caller control the loop?

4. **Buffer History**: Should we support scrollback buffer for test inspection?

5. **Event Model**: Should capabilities be queryable via terminal escape sequences (DA1), or just adapter properties?

6. **Layer vs Middleware**: Is the ASP.NET-style `next()` delegate pattern right, or should layers be simpler transforms?

7. **Reverse Flow**: How should layers send data in the reverse direction (e.g., capability responses back to workload)?

## References

- [XTerm Control Sequences](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html)
- [ANSI Escape Codes](https://en.wikipedia.org/wiki/ANSI_escape_code)
- [ConPTY Documentation](https://docs.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [PTY Programming (Linux)](https://man7.org/linux/man-pages/man7/pty.7.html)
