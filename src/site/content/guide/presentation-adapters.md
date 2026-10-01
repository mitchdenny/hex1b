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

Reflow is disabled by default on all adapters. The simplest way to enable it is via the terminal builder:

```csharp
// Auto-detect strategy from environment
var terminal = Hex1bTerminal.CreateBuilder()
    .WithPtyProcess("bash")
    .WithReflow()
    .Build();

// Explicit strategy for testing
var terminal = Hex1bTerminal.CreateBuilder()
    .WithHeadless()
    .WithDimensions(80, 24)
    .WithReflow(KittyReflowStrategy.Instance)
    .Build();
```

You can also configure reflow directly on the adapter if you need more control:

```csharp
// Console adapter: auto-detects the appropriate strategy
var adapter = new ConsolePresentationAdapter().WithReflow();

// Console adapter: override with a specific strategy
var adapter = new ConsolePresentationAdapter()
    .WithReflow(KittyReflowStrategy.Instance);

// Headless adapter: VTE strategy (includes saved cursor reflow)
var adapter = new HeadlessPresentationAdapter(80, 24)
    .WithReflow(VteReflowStrategy.Instance);
```

### Auto-Detection (Console)

`ConsolePresentationAdapter.WithReflow()` (no arguments) auto-detects the strategy based on `TERM_PROGRAM`:

::: warning Use auto-detection when possible
When using `ConsolePresentationAdapter`, prefer `WithReflow()` without arguments. Selecting the wrong strategy will cause `Hex1bTerminal`'s internal buffer state to diverge from what the upstream terminal emulator is displaying. This leads to cursor misplacement, garbled output, or visual artifacts — because each strategy makes different assumptions about how the terminal repositions content and cursors during a resize.
:::

| `TERM_PROGRAM` / Detection | Strategy |
|---------------------------|----------|
| `kitty` | `KittyReflowStrategy` |
| `ghostty` | `GhosttyReflowStrategy` |
| `foot` | `FootReflowStrategy` |
| `gnome-terminal`, `tilix`, `xfce4-terminal` | `VteReflowStrategy` |
| `VTE_VERSION` env var set | `VteReflowStrategy` |
| `wezterm` | `WezTermReflowStrategy` |
| `alacritty` | `AlacrittyReflowStrategy` |
| `WT_SESSION` env var set | `WindowsTerminalReflowStrategy` |
| `xterm` | `XtermReflowStrategy` |
| `iterm.app` | `ITerm2ReflowStrategy` |
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

The `ReflowEnabled` property controls whether the terminal uses the reflow path or the standard crop path during resize. Each terminal emulator has its own strategy class (e.g., `AlacrittyReflowStrategy`, `KittyReflowStrategy`, `VteReflowStrategy`) so behavior can evolve independently as terminals are updated. The generic `NoReflowStrategy` is available for unknown terminals.

### Headless Adapter and Testing

The builder's `WithReflow(strategy)` overload makes it easy to test different terminal resize semantics:

```csharp
// Test that your app handles Kitty-style cursor-anchored reflow
var terminal = Hex1bTerminal.CreateBuilder()
    .WithHeadless()
    .WithDimensions(80, 24)
    .WithReflow(KittyReflowStrategy.Instance)
    .Build();

// Test that your app handles VTE-style reflow with saved cursor
var terminal = Hex1bTerminal.CreateBuilder()
    .WithHeadless()
    .WithDimensions(80, 24)
    .WithReflow(VteReflowStrategy.Instance)
    .Build();
```

### Best-Effort Strategies

Detailed information about terminal reflow behavior is hard to come by — terminal emulators rarely document their resize logic formally, and behavior can change between versions. These strategies are **best effort**, based on upstream source code, test suites, and observed behavior.

If you encounter a mismatch between `Hex1bTerminal`'s internal state and what your terminal emulator displays — especially after a recent emulator update — please [file an issue](https://github.com/mitchdenny/hex1b/issues) with evidence. A screen recording or video showing the before/after state is often the most helpful artifact.

## Next Steps

- Learn about [Workload Adapters](./workload-adapters) for different workload types
- See [Using the Emulator](./using-the-emulator) for a step-by-step tutorial
