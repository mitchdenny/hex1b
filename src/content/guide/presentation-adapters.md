# Presentation Adapters

Presentation adapters control how terminal content is rendered and displayed. They implement the `IHex1bTerminalPresentationAdapter` interface and handle all visual output.

## Built-in Adapters

### ConsolePresentationAdapter

The default adapter for displaying content in a real terminal:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithConsolePresentation()
    .WithAppWorkload(BuildUI)
    .Build();
```

This adapter:
- Writes directly to `System.Console`
- Uses ANSI escape sequences for colors and formatting
- Manages the alternate screen buffer
- Handles cursor positioning and visibility

### HeadlessPresentationAdapter

For testing and automation without a display:

```csharp
var terminal = new Hex1bTerminalBuilder()
    .WithHeadlessPresentation(80, 24)  // Width x Height
    .WithAppWorkload(BuildUI)
    .Build();
```

This adapter:
- Maintains an in-memory buffer of the screen
- Allows programmatic inspection of rendered content
- Perfect for unit and integration testing
- No actual console output

## The IHex1bTerminalPresentationAdapter Interface

```csharp
public interface IHex1bTerminalPresentationAdapter
{
    /// <summary>
    /// Initialize the adapter with the parent terminal.
    /// </summary>
    void Initialize(Hex1bTerminal terminal);
    
    /// <summary>
    /// Render the cell buffer to the display.
    /// </summary>
    void Render(ReadOnlySpan<CellAttributes> buffer, int width, int height);
    
    /// <summary>
    /// Set the cursor position.
    /// </summary>
    void SetCursorPosition(int x, int y);
    
    /// <summary>
    /// Show or hide the cursor.
    /// </summary>
    void SetCursorVisible(bool visible);
    
    /// <summary>
    /// Set the cursor shape.
    /// </summary>
    void SetCursorShape(CursorShape shape);
    
    /// <summary>
    /// Get the current terminal size.
    /// </summary>
    (int Width, int Height) GetSize();
    
    /// <summary>
    /// Clean up resources.
    /// </summary>
    void Dispose();
}
```

## Creating a Custom Adapter

You can create custom presentation adapters for specialized rendering needs:

```csharp
public class WebSocketPresentationAdapter : IHex1bTerminalPresentationAdapter
{
    private readonly WebSocket _socket;
    private Hex1bTerminal? _terminal;
    
    public WebSocketPresentationAdapter(WebSocket socket)
    {
        _socket = socket;
    }
    
    public void Initialize(Hex1bTerminal terminal)
    {
        _terminal = terminal;
    }
    
    public void Render(ReadOnlySpan<CellAttributes> buffer, int width, int height)
    {
        // Convert buffer to JSON and send over WebSocket
        var data = SerializeBuffer(buffer, width, height);
        _socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    public void SetCursorPosition(int x, int y)
    {
        // Send cursor position update
    }
    
    // ... implement other methods
}
```

## Presentation Filters

You can also apply filters to transform the presentation output:

```csharp
public interface IHex1bTerminalPresentationFilter
{
    void Filter(Span<CellAttributes> buffer, int width, int height);
}
```

Filters are useful for:
- Recording terminal sessions (e.g., Asciinema)
- Applying visual effects
- Capturing screenshots
- Accessibility transformations

## Use Cases

| Adapter | Use Case |
|---------|----------|
| `ConsolePresentationAdapter` | Real terminal applications |
| `HeadlessPresentationAdapter` | Unit tests, CI/CD |
| Custom WebSocket adapter | Web-based terminals |
| Custom recording adapter | Session recording |

## Terminal Reflow

Presentation adapters can opt into terminal reflow by implementing the `ITerminalReflowProvider` interface. When enabled, soft-wrapped lines are re-wrapped on resize instead of being cropped.

### Enabling Reflow

Reflow is disabled by default on all adapters. Call `WithReflow()` to enable it:

```csharp
// Console adapter: auto-detects the appropriate strategy
var adapter = new ConsolePresentationAdapter().WithReflow();

// Console adapter: override with a specific strategy
var adapter = new ConsolePresentationAdapter()
    .WithReflow(KittyReflowStrategy.Instance);

// Headless adapter: VTE strategy (includes saved cursor reflow)
var adapter = new HeadlessPresentationAdapter(80, 24)
    .WithReflow(VteReflowStrategy.Instance);

// Headless adapter: Xterm strategy
var adapter = new HeadlessPresentationAdapter(80, 24)
    .WithReflow(XtermReflowStrategy.Instance);
```

### Auto-Detection (Console)

`ConsolePresentationAdapter.WithReflow()` (no arguments) auto-detects the strategy based on `TERM_PROGRAM`:

| `TERM_PROGRAM` / Detection | Strategy |
|---------------------------|----------|
| `kitty` | `KittyReflowStrategy` |
| `gnome-terminal`, `tilix`, `xfce4-terminal` | `VteReflowStrategy` |
| `VTE_VERSION` env var set | `VteReflowStrategy` |
| `ghostty` | `GhosttyReflowStrategy` |
| `wezterm`, `iterm.app` | `KittyReflowStrategy` |
| `xterm`, `alacritty` | `XtermReflowStrategy` |
| `foot` | `NoReflowStrategy` |
| `WT_SESSION` env var set | `KittyReflowStrategy` |
| Other / unset | `NoReflowStrategy` |

### The ITerminalReflowProvider Interface

Custom presentation adapters can implement reflow by adding the `ITerminalReflowProvider` interface:

```csharp
public interface ITerminalReflowProvider
{
    bool ReflowEnabled => true;
    ReflowResult Reflow(ReflowContext context);
    bool ShouldClearSoftWrapOnAbsolutePosition { get; }
}
```

The `ReflowEnabled` property controls whether the terminal uses the reflow path or the standard crop path during resize. Pre-built strategies (`XtermReflowStrategy`, `KittyReflowStrategy`, `VteReflowStrategy`, `GhosttyReflowStrategy`, `NoReflowStrategy`) are available as singletons that custom adapters can delegate to.

## Next Steps

- Learn about [Workload Adapters](./workload-adapters) for different workload types
- See [Using the Emulator](./using-the-emulator) for a step-by-step tutorial
