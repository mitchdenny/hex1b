<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode         → src/Hex1b.Website/Examples/WindowBasicExample.cs
  - modalCode         → src/Hex1b.Website/Examples/WindowModalExample.cs
  - titleActionsCode  → src/Hex1b.Website/Examples/WindowTitleActionsExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Button("Open Window").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  Hello from a floating window!"),
                        v.Text(""),
                        v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
                    ]))
                    .Title("My Window")
                    .Size(40, 8)
                    .Open(e.Windows);
                }),
                v.Text(""),
                v.Text("  Press the button to open a window...")
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();`

const modalCode = `using Hex1b;

var lastResult = "No dialog shown yet";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text($"Last Result: {lastResult}"),
                v.Text(""),
                v.Button("Confirm Delete").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  🗑️  Delete this item?"),
                        v.Text("  This action cannot be undone."),
                        v.Text(""),
                        v.HStack(h => [
                            h.Text("  "),
                            h.Button("Delete").OnClick(_ => w.Window.CloseWithResult(true)),
                            h.Text(" "),
                            h.Button("Cancel").OnClick(_ => w.Window.CloseWithResult(false))
                        ])
                    ]))
                    .Title("Confirm Delete")
                    .Size(40, 9)
                    .Modal()
                    .OnResult<bool>(result => {
                        if (result.IsCancelled || !result.Value)
                            lastResult = "Cancelled";
                        else
                            lastResult = "Deleted!";
                    })
                    .Open(e.Windows);
                })
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();`

const titleActionsCode = `using Hex1b;

var pinned = false;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Text("  Click the button below to open a window"),
                v.Text(""),
                v.Button("Open Window with Actions").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text(pinned ? "  📌 This window is pinned!" : "  Click the pin icon above"),
                        v.Text("")
                    ]))
                    .Title("Custom Actions")
                    .Size(40, 7)
                    .LeftTitleActions(t => [
                        t.Action("📌", _ => pinned = !pinned),
                        t.Action("📋", _ => { /* copy logic */ })
                    ])
                    .RightTitleActions(t => [
                        t.Action("?", _ => { /* help logic */ }),
                        t.Close()
                    ])
                    .Open(e.Windows);
                })
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();`

const resizableCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Button("Open Resizable Window").OnClick(e => {
                    e.Windows.Window(w => w.VStack(v => [
                        v.Text(""),
                        v.Text("  Drag edges or corners to resize!"),
                        v.Text(""),
                        v.Text("  Constraints:"),
                        v.Text("  • Min: 30×8"),
                        v.Text("  • Max: 80×20"),
                        v.Text("")
                    ]).Fill())
                    .Title("Resizable Window")
                    .Size(50, 12)
                    .Resizable(minWidth: 30, minHeight: 8, maxWidth: 80, maxHeight: 20)
                    .Open(e.Windows);
                }),
                v.Text(""),
                v.Text("  Drag window edges to resize")
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();`

const positionCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        z.WindowPanel()
            .Background(b => b.VStack(v => [
                v.Text(""),
                v.Text("  Click buttons to open windows at different positions:"),
                v.Text(""),
                v.HStack(h => [
                    h.Text("  "),
                    h.Button("Top-Left").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Top-Left  "))
                            .Title("TL")
                            .Size(15, 5)
                            .Position(WindowPositionSpec.TopLeft)
                            .Open(e.Windows);
                    }),
                    h.Text(" "),
                    h.Button("Center").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Center  "))
                            .Title("C")
                            .Size(15, 5)
                            .Position(WindowPositionSpec.Center)
                            .Open(e.Windows);
                    }),
                    h.Text(" "),
                    h.Button("Bottom-Right").OnClick(e => {
                        e.Windows.Window(w => w.Text("  Bottom-Right  "))
                            .Title("BR")
                            .Size(18, 5)
                            .Position(WindowPositionSpec.BottomRight)
                            .Open(e.Windows);
                    })
                ]),
                v.Text(""),
                v.Button("Close All").OnClick(e => e.Windows.CloseAll())
            ]))
            .Fill()
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# Windows

Create floating, draggable windows for dialog boxes, tool palettes, document windows, and more. Windows can be modal (blocking interaction with other content) or non-modal, resizable or fixed-size, and fully customizable.

## Basic Usage

Use `WindowPanel` to create an area that can host floating windows, then open windows from event handlers:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="window-basic" exampleTitle="Windows - Basic Usage" />

Key concepts:
- **WindowPanel** provides the container where windows can be displayed
- **WindowManager** (accessed via `e.Windows`) creates and manages windows
- **WindowHandle** is the fluent builder returned by `Window(...)` that configures the window

## Opening Windows

To open a window, create a `WindowHandle` using the fluent API and call `Open()`:

```csharp
e.Windows.Window(w => w.VStack(v => [
    v.Text("Window content here"),
    v.Button("Close").OnClick(ev => ev.Windows.Close(w.Window))
]))
.Title("My Window")
.Size(40, 15)
.Open(e.Windows);
```

### Window Content Context

Inside the window content builder, you receive a `WindowContentContext` (`w` in the example above). This provides:

- All standard widget methods (`VStack`, `Text`, `Button`, etc.)
- `w.Window` - the window handle, used for closing or returning results

## Modal Dialogs

Modal windows block interaction with all other windows until closed. Use `.Modal()` to create a modal, and `OnResult<T>` + `CloseWithResult<T>()` to return values:

<CodeBlock lang="csharp" :code="modalCode" command="dotnet run" example="window-modal" exampleTitle="Modal Dialog with Result" />

### Result Handling

The `OnResult<T>` callback receives a `WindowResultContext<T>` with:

| Property | Description |
|----------|-------------|
| `Value` | The result value (default of `T` if cancelled) |
| `IsCancelled` | `true` if closed via Escape, close button, or `Cancel()` |
| `Window` | The window entry that was closed |
| `Windows` | The window manager for opening follow-up windows |

### Cancellation

Close without a result using `Cancel()` or simply close the window:

```csharp
// Explicit cancellation
h.Button("Cancel").OnClick(_ => w.Window.Cancel())

// Or just close (also triggers cancellation)
ev.Windows.Close(w.Window)
```

## Title Bar Actions

Customize the title bar with custom action buttons using `LeftTitleActions` and `RightTitleActions`:

<CodeBlock lang="csharp" :code="titleActionsCode" command="dotnet run" example="window-title-actions" exampleTitle="Custom Title Bar Actions" />

### Action Builder Methods

| Method | Description |
|--------|-------------|
| `t.Action(icon, handler)` | Custom action with an icon (emoji or character) |
| `t.Close(icon?)` | Standard close action (defaults to "×") |

By default, windows include a close button on the right. Use `RightTitleActions` to override this.

## Window Size and Position

### Size

Set the initial window size with `.Size(width, height)`:

```csharp
.Size(60, 20)  // 60 columns, 20 rows
```

### Position

Control initial placement with `.Position()`:

<CodeBlock lang="csharp" :code="positionCode" command="dotnet run" example="window-position" exampleTitle="Window Positioning" />

Available positions:

| Position | Description |
|----------|-------------|
| `WindowPositionSpec.Center` | Center of the panel (default) |
| `WindowPositionSpec.TopLeft` | Top-left corner |
| `WindowPositionSpec.TopRight` | Top-right corner |
| `WindowPositionSpec.BottomLeft` | Bottom-left corner |
| `WindowPositionSpec.BottomRight` | Bottom-right corner |
| `WindowPositionSpec.CenterTop` | Centered horizontally, at top |
| `WindowPositionSpec.CenterBottom` | Centered horizontally, at bottom |

For offset positioning:

```csharp
// Center with offset (cascade windows)
.Position(new WindowPositionSpec(WindowPosition.Center, OffsetX: 3, OffsetY: 2))
```

## Resizable Windows

Allow users to resize windows by dragging edges and corners:

<CodeBlock lang="csharp" :code="resizableCode" command="dotnet run" example="window-resizable" exampleTitle="Resizable Window" />

### Resize Constraints

```csharp
.Resizable(
    minWidth: 30,    // Minimum width
    minHeight: 10,   // Minimum height
    maxWidth: 100,   // Maximum width (null = unbounded)
    maxHeight: 40    // Maximum height (null = unbounded)
)
```

## Frameless Windows

Create windows without a title bar using `.NoTitleBar()`:

```csharp
e.Windows.Window(w => w.Border(b => [
    b.Text("  Floating tooltip  ")
]))
.Size(25, 5)
.NoTitleBar()
.Open(e.Windows);
```

## Lifecycle Events

Handle window lifecycle events:

```csharp
.OnClose(() => {
    // Window is closing
    CleanupResources();
})
.OnActivated(() => {
    // Window brought to front
    RefreshData();
})
.OnDeactivated(() => {
    // Window lost focus to another window
})
```

## Escape Key Behavior

Control how the Escape key behaves:

```csharp
.EscapeBehavior(WindowEscapeBehavior.Close)      // Default: close on Escape
.EscapeBehavior(WindowEscapeBehavior.Ignore)     // Ignore Escape key
.EscapeBehavior(WindowEscapeBehavior.CloseNonModal)  // Close only non-modal windows
```

## Singleton Windows

To ensure only one instance of a window (like "About" or "Settings"), store the handle and check if it's open:

```csharp
WindowHandle? settingsWindow = null;

// In your click handler:
if (settingsWindow != null && e.Windows.IsOpen(settingsWindow)) {
    e.Windows.BringToFront(settingsWindow);
    return;
}

settingsWindow = e.Windows.Window(w => w.Text("Settings..."))
    .Title("Settings")
    .Size(50, 20)
    .OnClose(() => settingsWindow = null);

e.Windows.Open(settingsWindow);
```

## WindowPanel Background

Add a decorative background to the WindowPanel that renders behind all windows:

```csharp
ctx.WindowPanel()
    .Background(b => b.Surface(s => BuildBackground(s)))
    .Fill()
```

The background is purely decorative—it doesn't receive focus or input.

## API Reference

### WindowHandle Methods

| Method | Description |
|--------|-------------|
| `.Title(string)` | Window title in the title bar |
| `.Size(width, height)` | Initial window size |
| `.Position(x, y)` | Absolute position |
| `.Position(WindowPositionSpec)` | Position strategy with optional offset |
| `.Modal()` | Make modal (blocks other windows) |
| `.Resizable(...)` | Enable resize with constraints |
| `.NoTitleBar()` | Hide the title bar |
| `.AllowOutOfBounds()` | Allow moving outside panel bounds |
| `.LeftTitleActions(builder)` | Custom left title bar buttons |
| `.RightTitleActions(builder)` | Custom right title bar buttons |
| `.EscapeBehavior(behavior)` | How Escape key is handled |
| `.OnClose(action)` | Callback when window closes |
| `.OnActivated(action)` | Callback when window gains focus |
| `.OnDeactivated(action)` | Callback when window loses focus |
| `.OnResult<T>(callback)` | Callback with typed result on close |
| `.CloseWithResult<T>(value)` | Close and return a result |
| `.Cancel()` | Close without result (cancelled) |
| `.Open(WindowManager)` | Open the window |

### WindowManager Methods

| Method | Description |
|--------|-------------|
| `.Window(builder)` | Create a new window handle |
| `.Open(handle)` | Open (or bring to front) a window |
| `.Close(handle)` | Close a window by handle |
| `.Close(entry)` | Close a window by entry |
| `.CloseAll()` | Close all windows |
| `.BringToFront(handle)` | Bring window to top |
| `.Get(handle)` | Get the WindowEntry for a handle |
| `.IsOpen(handle)` | Check if window is open |
| `.All` | All open windows (z-ordered) |
| `.Count` | Number of open windows |
| `.ActiveWindow` | Currently active window |

## Related Widgets

- [ZStack](/guide/widgets/stacks) - For layering windows over content
- [Notifications](/guide/widgets/notifications) - For toast-style messages
- [Navigator](/guide/widgets/navigator) - For stack-based page navigation
