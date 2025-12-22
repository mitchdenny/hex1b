# Hex1b Terminal Architecture

> **Status**: Complete  
> **Date**: December 2025

## Overview

Hex1b's terminal architecture separates concerns into three distinct components:

1. **Presentation Adapter** - The physical I/O layer (console, WebSocket, etc.)
2. **Workload Adapter** - The application-side interface (what Hex1bApp uses)
3. **Hex1bTerminal** - The bridge that connects them

This design allows the same application code to work across different environments (console, web browser, tests) without modification.

## Architecture Diagram

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
│                   │ IHex1bAppTerminal   │  ◄── App uses THIS interface     │
│                   │ WorkloadAdapter     │                                   │
│                   └──────────┬──────────┘                                   │
│                              │                                              │
└──────────────────────────────┼──────────────────────────────────────────────┘
                               │
                               │  Write() → output channel
                               │  InputEvents ← parsed events
                               │
┌──────────────────────────────┼──────────────────────────────────────────────┐
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │ Hex1bAppWorkload    │  ◄── Dual-faced adapter          │
│                   │ Adapter             │                                   │
│                   └──────────┬──────────┘                                   │
│                              │                                              │
│                              │  ReadOutputAsync() ← output channel          │
│                              │  WriteInputEventAsync() → input channel      │
│                              │                                              │
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │   Hex1bTerminal     │  ◄── Terminal emulator/bridge    │
│                   │   [I/O pumps,       │                                   │
│                   │    screen buffer,   │                                   │
│                   │    input parsing]   │                                   │
│                   └──────────┬──────────┘                                   │
│                              │                                              │
│                              │  WriteOutputAsync() → ANSI to display        │
│                              │  ReadInputAsync() ← raw bytes from user      │
│                              │                                              │
│                              ▼                                              │
│                   ┌─────────────────────┐                                   │
│                   │ IHex1bTerminal      │  ◄── Physical I/O                │
│                   │ PresentationAdapter │                                   │
│                   └─────────────────────┘                                   │
│                              │                                              │
│                              ▼                                              │
│                   Console / WebSocket / null (testing)                      │
│                                                              (Hex1b infra)  │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Key Interfaces

### IHex1bTerminalPresentationAdapter

The presentation adapter handles raw I/O with the physical terminal device.

```csharp
public interface IHex1bTerminalPresentationAdapter : IAsyncDisposable
{
    // Send rendered output TO the display
    ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    // Receive raw input FROM the user
    ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default);
    
    // Terminal dimensions
    int Width { get; }
    int Height { get; }
    
    // Capability hints
    TerminalCapabilities Capabilities { get; }
    
    // Events
    event Action<int, int>? Resized;
    event Action? Disconnected;
    
    // Lifecycle
    ValueTask EnterTuiModeAsync(CancellationToken ct = default);
    ValueTask ExitTuiModeAsync(CancellationToken ct = default);
    ValueTask FlushAsync(CancellationToken ct = default);
}
```

**Implementations:**
- `ConsolePresentationAdapter` - Real console I/O with raw mode support
- `WebSocketPresentationAdapter` - Browser-based terminal via WebSocket

### IHex1bTerminalWorkloadAdapter

The base interface for workload communication (raw bytes).

```csharp
public interface IHex1bTerminalWorkloadAdapter : IAsyncDisposable
{
    // Terminal reads output FROM the workload
    ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default);
    
    // Terminal writes input TO the workload
    ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    // Notify workload of resize
    ValueTask ResizeAsync(int width, int height, CancellationToken ct = default);
    
    // Workload disconnected
    event Action? Disconnected;
}
```

### IHex1bAppTerminalWorkloadAdapter

The app-side interface that extends the base with higher-level APIs.

```csharp
public interface IHex1bAppTerminalWorkloadAdapter : IHex1bTerminalWorkloadAdapter
{
    // === Output (App → Terminal) ===
    void Write(string text);
    void Write(ReadOnlySpan<byte> data);
    void Flush();
    
    // === Input (Terminal → App) ===
    ChannelReader<Hex1bEvent> InputEvents { get; }
    
    // === Terminal Info ===
    int Width { get; }
    int Height { get; }
    TerminalCapabilities Capabilities { get; }
    
    // === TUI Mode ===
    void EnterTuiMode();
    void ExitTuiMode();
    void Clear();
    void SetCursorPosition(int left, int top);
}
```

**Implementation:**
- `Hex1bAppWorkloadAdapter` - Dual-faced adapter connecting Hex1bApp to Hex1bTerminal

## Data Flow

### Production Flow (Console)

```
User Input                                                    Screen Display
     │                                                              ▲
     ▼                                                              │
┌─────────────────────────────────────────────────────────────────────────────┐
│                     ConsolePresentationAdapter                               │
│  - Raw console read/write                                                   │
│  - Platform-specific driver (Unix termios / Windows ConPTY)                 │
│  - Enter/exit raw mode                                                      │
└─────────────────────────────────────────────────────────────────────────────┘
     │ ReadInputAsync()                              WriteOutputAsync() ▲
     ▼                                                                  │
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Hex1bTerminal                                       │
│  - Input parsing (ANSI sequences → Hex1bEvent)                              │
│  - Output forwarding                                                        │
│  - Screen buffer capture (optional)                                         │
│  - I/O pump tasks                                                           │
└─────────────────────────────────────────────────────────────────────────────┘
     │ WriteInputEventAsync()                        ReadOutputAsync() ▲
     ▼                                                                  │
┌─────────────────────────────────────────────────────────────────────────────┐
│                     Hex1bAppWorkloadAdapter                                  │
│  - Input events channel                                                     │
│  - Output bytes channel                                                     │
│  - TUI mode escape sequences                                                │
└─────────────────────────────────────────────────────────────────────────────┘
     │ InputEvents                                           Write() ▲
     ▼                                                               │
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Hex1bApp                                          │
│  - Widget tree                                                              │
│  - Input routing                                                            │
│  - Rendering                                                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Testing Flow (Headless)

In testing mode, there's no presentation adapter. The terminal operates in headless mode with screen buffer capture:

```
Test Code
     │
     │  SendKey(), TypeText()
     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     Hex1bAppWorkloadAdapter                                  │
│  - SendKey() writes to input channel                                        │
│  - Processes input events asynchronously                                    │
└─────────────────────────────────────────────────────────────────────────────┘
     │                                                               ▲
     ▼ InputEvents                                           Write() │
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Hex1bApp                                          │
└─────────────────────────────────────────────────────────────────────────────┘
     │                                                               ▲
     ▼ Write()                                         InputEvents │
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Hex1bTerminal                                       │
│  - Screen buffer enabled (captures all output)                              │
│  - No presentation adapter (null)                                           │
│  - Screen reads auto-flush pending output                                   │
└─────────────────────────────────────────────────────────────────────────────┘
     │
     │  GetScreenText(), ContainsText(), RawOutput
     ▼
Test Assertions
```

## Usage Examples

### Console Application

```csharp
// Simple case - Hex1bApp creates everything internally
await using var app = new Hex1bApp(
    ctx => ctx.Text("Hello, World!"),
    new Hex1bAppOptions { EnableMouse = true }
);
await app.RunAsync();
```

Internally, Hex1bApp creates:
1. `ConsolePresentationAdapter` with mouse support
2. `Hex1bAppWorkloadAdapter` 
3. `Hex1bTerminal` bridging them

### Web Application (WebSocket)

```csharp
async Task HandleWebSocketAsync(WebSocket webSocket)
{
    // Create presentation adapter for WebSocket
    await using var presentation = new WebSocketPresentationAdapter(
        webSocket, 80, 24, enableMouse: true);
    
    // Create workload adapter
    var workload = new Hex1bAppWorkloadAdapter(
        presentation.Width, 
        presentation.Height, 
        presentation.Capabilities);
    
    // Create terminal bridge
    using var terminal = new Hex1bTerminal(presentation, workload);
    
    // Start I/O pumps
    terminal.Start();
    
    // Create and run app
    await using var app = new Hex1bApp(
        ctx => ctx.Text("Hello from the web!"),
        new Hex1bAppOptions { WorkloadAdapter = workload }
    );
    await app.RunAsync();
}
```

### Testing

```csharp
[Fact]
public async Task Button_Click_UpdatesCounter()
{
    // Create headless terminal (no presentation)
    using var terminal = new Hex1bTerminal(80, 24);
    
    var clicks = 0;
    await using var app = new Hex1bApp(
        ctx => ctx.Button($"Clicks: {clicks}").OnClick(_ => clicks++),
        new Hex1bAppOptions { WorkloadAdapter = terminal.WorkloadAdapter }
    );
    
    // Run app and send input
    var runTask = app.RunAsync();
    await new Hex1bTestSequenceBuilder()
        .WaitUntil(s => s.ContainsText("Clicks:"), TimeSpan.FromSeconds(2))
        .Enter()  // Click button
        .Ctrl().Key(Hex1bKey.C)  // Exit app
        .Build()
        .ApplyAsync(terminal);
    await runTask;
    
    // Assert
    Assert.Equal(1, clicks);
    Assert.True(terminal.ContainsText("Clicks: 1"));
}
```

## Platform Support

### Console Drivers

The `ConsolePresentationAdapter` uses platform-specific drivers:

| Platform | Driver | Features |
|----------|--------|----------|
| Linux/macOS | `UnixConsoleDriver` | termios raw mode, poll() for async input, SIGWINCH |
| Windows | `WindowsConsoleDriver` | ConPTY VT mode, ReadConsoleInput |

### Raw Mode

Raw mode is essential for:
- Capturing individual keystrokes (not line-buffered)
- Receiving escape sequences (arrows, function keys)
- Mouse event tracking
- Proper Ctrl+C handling

The presentation adapter enters raw mode when `Start()` is called on the terminal, and exits when disposed.

## Component Reference

### Files

| File | Description |
|------|-------------|
| `Terminal/IHex1bTerminalPresentationAdapter.cs` | Presentation interface |
| `Terminal/IHex1bTerminalWorkloadAdapter.cs` | Base workload interface |
| `Terminal/IHex1bAppTerminalWorkloadAdapter.cs` | App-side workload interface |
| `Terminal/ConsolePresentationAdapter.cs` | Console presentation implementation |
| `Terminal/WebSocketPresentationAdapter.cs` | WebSocket presentation implementation |
| `Terminal/Hex1bAppWorkloadAdapter.cs` | Workload adapter for Hex1bApp |
| `Terminal/Hex1bTerminal.cs` | Terminal bridge/emulator |
| `Terminal/IConsoleDriver.cs` | Platform driver interface |
| `Terminal/UnixConsoleDriver.cs` | Linux/macOS driver |
| `Terminal/WindowsConsoleDriver.cs` | Windows driver |
| `Terminal/TerminalCapabilities.cs` | Capability flags |

### Key Methods

**Hex1bTerminal:**
- `Start()` - Begins I/O pump tasks, enters TUI mode on presentation
- `ContainsText()` - Screen buffer inspection (auto-flushes)
- `GetScreenText()` - Get full screen content (auto-flushes)
- `RawOutput` - Get raw ANSI output (auto-flushes)
- `SendKey()` / `TypeText()` - Inject input for testing

**Hex1bAppWorkloadAdapter:**
- `Write()` - Queue ANSI output
- `InputEvents` - Channel of parsed input events
- `SendKey()` / `TypeText()` - Inject test input

## Design Principles

1. **Separation of Concerns**: Presentation handles I/O, workload handles app interface, terminal bridges them

2. **Testability**: Headless mode with screen buffer capture allows deterministic testing

3. **Flexibility**: Same app code works across console, web, and test environments

4. **Platform Abstraction**: Console drivers isolate platform-specific code

5. **Async-First**: All I/O operations are async-capable for proper cancellation support
