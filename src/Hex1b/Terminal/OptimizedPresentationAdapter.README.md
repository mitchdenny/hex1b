# OptimizedPresentationAdapter

The `OptimizedPresentationAdapter` is a wrapper for `IHex1bTerminalPresentationAdapter` that dramatically reduces the amount of ANSI data sent to the presentation layer by suppressing redundant updates.

## How It Works

The adapter maintains a snapshot of the terminal cell state that was last sent to the presentation layer. Before forwarding any output:

1. It parses the ANSI sequences to simulate what terminal cells would be affected
2. Compares the resulting state with its cached snapshot
3. Only forwards output if any cells would actually change
4. For non-cell-modifying sequences (like cursor movements or color changes without text), it forwards the first occurrence but suppresses duplicates

This optimization is particularly beneficial for:
- Reducing flicker in fast-updating displays
- Minimizing network traffic for remote terminals (WebSocket, SSH)
- Improving performance for high-latency connections
- Reducing battery consumption on mobile devices

## Usage

### Basic Setup

```csharp
// Create your actual presentation adapter
var consoleAdapter = new ConsolePresentationAdapter();

// Wrap it with the optimizer
var optimizedAdapter = new OptimizedPresentationAdapter(consoleAdapter);

// Use the optimized adapter with your workload
var workload = new Hex1bAppWorkloadAdapter(optimizedAdapter.Capabilities);
using var terminal = new Hex1bTerminal(optimizedAdapter, workload);

// Create and run your app
await using var app = new Hex1bApp(..., new Hex1bAppOptions 
{ 
    WorkloadAdapter = workload 
});
await app.RunAsync();
```

### With WebSocket (for remote terminals)

```csharp
// Wrap WebSocket adapter to minimize network traffic
var wsAdapter = new WebSocketPresentationAdapter(webSocket);
var optimizedAdapter = new OptimizedPresentationAdapter(wsAdapter);
```

## Example: Sample Application

See `samples/OptimizedPresentation/Program.cs` for a complete example that demonstrates:
- Wrapping a console adapter
- Tracking bytes written to see the optimization in action
- Running a continuously updating UI that benefits from suppression

## Performance Characteristics

The optimizer adds minimal overhead:
- **Memory**: Stores a 2D array of `TerminalCell` (width × height)
- **CPU**: Parses ANSI sequences twice (once for simulation, once for execution if forwarded)
- **Benefit**: Can reduce output by 90%+ in scenarios with many redundant updates

## Caveats

- The internal ANSI emulator supports common sequences but may not handle all edge cases
- The first write is always forwarded to establish a baseline state
- Entering TUI mode resets the cached state
- Best suited for applications with frequent re-renders of mostly static content

## Implementation Details

The `AnsiEmulator` class (internal to OptimizedPresentationAdapter) provides a lightweight ANSI parser that tracks:
- Character cell content
- Foreground and background colors
- Text attributes (bold, italic, underline, etc.)
- Cursor position
- Clear operations

This allows the adapter to predict the visual outcome of ANSI sequences without actually sending them to the presentation layer.
