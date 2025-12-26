# OptimizedPresentationFilter

The `OptimizedPresentationFilter` is a presentation filter that dramatically optimizes ANSI output by comparing terminal screen buffer snapshots and generating minimal ANSI sequences for only the cells that changed.

## How It Works

Instead of re-parsing ANSI streams (CPU intensive), this filter works directly with the terminal's screen buffer:

1. **Takes snapshots** of the terminal's screen buffer before and after each output
2. **Compares snapshots** to detect which cells actually changed
3. **Generates optimized ANSI** sequences to update only the changed cells
4. **Suppresses output** entirely if no cells changed

This approach trades CPU/memory for dramatically improved render performance by minimizing the amount of data sent to the presentation layer.

## Benefits

- **90%+ reduction** in output for applications with frequent re-renders
- **Reduced flicker** on high-latency connections
- **Minimal network traffic** for remote terminals (WebSocket, SSH)
- **Better battery life** on mobile devices
- **No ANSI parsing overhead** - works directly with the terminal's screen buffer

## Usage

### Basic Setup

```csharp
var presentation = new ConsolePresentationAdapter();
var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

var options = new Hex1bTerminalOptions
{
    PresentationAdapter = presentation,
    WorkloadAdapter = workload
};

// Add the optimization filter
options.PresentationFilters.Add(new OptimizedPresentationFilter());

using var terminal = new Hex1bTerminal(options);
```

### With WebSocket (Remote Terminals)

```csharp
var wsAdapter = new WebSocketPresentationAdapter(webSocket);
var workload = new Hex1bAppWorkloadAdapter(wsAdapter.Capabilities);

var options = new Hex1bTerminalOptions
{
    PresentationAdapter = wsAdapter,
    WorkloadAdapter = workload
};

// Dramatically reduce network traffic
options.PresentationFilters.Add(new OptimizedPresentationFilter());

using var terminal = new Hex1bTerminal(options);
```

## Example: Sample Application

See `samples/OptimizedPresentationFilter/Program.cs` for a complete example that demonstrates:
- How to configure the filter
- Tracking bytes written to see the optimization in action
- Running a continuously updating UI that benefits from the optimization

Run the sample:
```bash
dotnet run --project samples/OptimizedPresentationFilter
```

## Performance Characteristics

**Memory:**
- Stores a 2D array of `TerminalCell` (width × height) for the snapshot
- ~1-2KB for typical 80×24 terminal

**CPU:**
- Compares screen buffers cell-by-cell on each output
- Generates optimized ANSI only for changed cells
- **No ANSI parsing** - works directly with the terminal's screen buffer

**Benefit:**
- Can reduce output by 90%+ in scenarios with frequent re-renders of mostly static content
- Most effective when updates are localized (e.g., a counter or status line)

## How It Differs from OptimizedPresentationAdapter

The previous `OptimizedPresentationAdapter` approach:
- Was a wrapper adapter (not a filter)
- Re-parsed ANSI sequences to simulate terminal state (CPU intensive)
- Couldn't access the terminal's actual screen buffer

The new `OptimizedPresentationFilter` approach:
- Is a proper presentation filter (can be combined with any adapter)
- Works directly with the terminal's screen buffer (no parsing needed)
- Generates optimized ANSI sequences from the ground up
- More efficient and accurate

## Implementation Details

The filter implements `IHex1bTerminalPresentationTransformFilter`, an extended filter interface that:
- Provides access to the terminal's screen buffer
- Allows filters to transform output before it's sent to the presentation layer
- Enables sophisticated optimizations based on actual terminal state

The optimization algorithm:
1. Maintains a snapshot of the last screen buffer state
2. On each output, compares current buffer with snapshot
3. Identifies cells that changed (character, color, or attributes)
4. Generates minimal ANSI sequences to update only those cells
5. Tracks cursor position and attributes to minimize escape sequences
6. Returns empty output if nothing changed

## Caveats

- Best suited for applications with frequent re-renders of mostly static content
- The first write is always forwarded to establish baseline state
- Resize operations reset the snapshot
- Combines well with other presentation filters in the pipeline
