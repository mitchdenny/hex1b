<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/DragBarBasicExample.cs
  - verticalCode → src/Hex1b.Website/Examples/DragBarVerticalExample.cs
  - multiPanelCode → src/Hex1b.Website/Examples/DragBarMultiPanelExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

var currentSize = 0;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
        h.DragBarPanel(
            h.VStack(panel => [
                panel.Text(" Sidebar"),
                panel.Text(" ───────"),
                panel.Text(" Drag the handle →"),
                panel.Text(" or Tab to it and"),
                panel.Text(" use ← → arrow keys"),
                panel.Text(""),
                panel.Text($" Width: {currentSize}")
            ])
        )
        .InitialSize(30)
        .MinSize(15)
        .MaxSize(50)
        .OnSizeChanged(size => currentSize = size),

        h.Border(
            h.VStack(main => [
                main.Text(""),
                main.Text("  Main Content"),
                main.Text(""),
                main.Text("  This area fills the remaining space.").Wrap(),
                main.Text("  Resize the sidebar by dragging the").Wrap(),
                main.Text("  handle or using arrow keys.").Wrap()
            ]),
            title: "Content"
        ).Fill()
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const verticalCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Border(
            v.VStack(main => [
                main.Text(""),
                main.Text("  Main Content Area"),
                main.Text(""),
                main.Text("  The panel below can be resized").Wrap(),
                main.Text("  by dragging its top handle.").Wrap()
            ]),
            title: "Editor"
        ).Fill(),

        v.DragBarPanel(
            v.VStack(panel => [
                panel.Text(" Output Panel"),
                panel.Text(" [INFO] Build started..."),
                panel.Text(" [INFO] Compilation successful"),
                panel.Text(" [INFO] 0 warnings, 0 errors")
            ])
        )
        .InitialSize(8)
        .MinSize(4)
        .MaxSize(20)
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`

const multiPanelCode = `using Hex1b;
using Hex1b.Widgets;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(outer => [
        outer.HStack(main => [
            // Left panel — handle auto-detected on right edge
            main.DragBarPanel(
                main.VStack(panel => [
                    panel.Text(" Explorer"),
                    panel.Text(" ────────"),
                    panel.Text(" 📁 src/"),
                    panel.Text("   📄 main.cs"),
                    panel.Text("   📄 app.cs"),
                    panel.Text(" 📁 tests/")
                ])
            )
            .InitialSize(25)
            .MinSize(15)
            .MaxSize(40),

            // Center content fills remaining space
            main.Border(
                main.VStack(center => [
                    center.Text("  Editor"),
                    center.Text(""),
                    center.Text("  Both sidebars are independently").Wrap(),
                    center.Text("  resizable. The center fills the").Wrap(),
                    center.Text("  remaining space.").Wrap()
                ]),
                title: "main.cs"
            ).Fill(),

            // Right panel — handle auto-detected on left edge
            main.DragBarPanel(
                main.VStack(panel => [
                    panel.Text(" Properties"),
                    panel.Text(" ──────────"),
                    panel.Text(" Name: main.cs"),
                    panel.Text(" Size: 2.4 KB"),
                    panel.Text(" Modified: Today")
                ])
            )
            .InitialSize(22)
            .MinSize(12)
            .MaxSize(35)
        ]).Fill(),

        // Bottom panel — handle auto-detected on top edge
        outer.DragBarPanel(
            outer.VStack(panel => [
                panel.Text(" Terminal"),
                panel.Text(" $ dotnet build"),
                panel.Text(" Build succeeded."),
                panel.Text(" 0 Warning(s), 0 Error(s)")
            ])
        )
        .InitialSize(7)
        .MinSize(4)
        .MaxSize(15)
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();`
</script>

# DragBarPanel

A panel with a built-in resize handle on one edge that can be dragged to change the panel's size.

DragBarPanel is ideal for creating resizable sidebars, output panels, and tool windows. Unlike [Splitter](/guide/widgets/splitter), which manages two fixed panes, DragBarPanel wraps a single piece of content and controls its own size—making it composable inside any layout.

## Basic Usage

Place a DragBarPanel inside an HStack to create a resizable sidebar:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="dragbar-basic" exampleTitle="DragBarPanel - Basic Usage" />

The panel displays a thin handle on one edge (`│` for vertical handles, `─` for horizontal). When hovered or focused, a braille thumb indicator appears on the handle to signal interactivity.

::: tip Navigation
- **Mouse drag** on the handle to resize
- **Tab** to focus the handle, then **←→** or **↑↓** arrow keys to resize
- Arrow keys resize by 2 characters per press
:::

## Vertical Resizing

DragBarPanel auto-detects the handle edge from its parent layout. Inside a VStack, the last child gets a handle on the top edge:

<CodeBlock lang="csharp" :code="verticalCode" command="dotnet run" example="dragbar-vertical" exampleTitle="DragBarPanel - Vertical Resizing" />

This pattern is great for resizable output panels, terminal panes, or log viewers at the bottom of your layout.

## Multi-Panel Layout

Combine multiple DragBarPanels to build IDE-style layouts with independently resizable regions:

<CodeBlock lang="csharp" :code="multiPanelCode" command="dotnet run" example="dragbar-multi-panel" exampleTitle="DragBarPanel - Multi-Panel Layout" />

Each DragBarPanel manages its own size state independently. The center content uses `.Fill()` to take the remaining space, and all panels are freely resizable.

## Edge Auto-Detection

When you don't specify a handle edge, DragBarPanel infers it from the parent layout:

| Parent Layout | Position | Auto-Detected Edge |
|---------------|----------|-------------------|
| **HStack** | First child | Right |
| **HStack** | Last child | Left |
| **VStack** | First child | Bottom |
| **VStack** | Last child | Top |

Override the auto-detection with `.HandleEdge()` when you need a specific edge:

```csharp
h.DragBarPanel(content)
    .HandleEdge(DragBarEdge.Left)
    .InitialSize(30)
```

## Size Constraints

Control the resizing range with `.MinSize()` and `.MaxSize()`:

```csharp
h.DragBarPanel(content)
    .InitialSize(30)   // Starting size in characters
    .MinSize(10)       // Cannot shrink below 10
    .MaxSize(60)       // Cannot grow beyond 60
```

| Method | Default | Description |
|--------|---------|-------------|
| `.InitialSize(int)` | Content intrinsic size | Starting size along the drag axis |
| `.MinSize(int)` | `5` | Minimum allowed size in characters |
| `.MaxSize(int)` | _(unlimited)_ | Maximum allowed size in characters |

::: warning State Preservation
After a user resizes the panel, the new size is preserved across re-renders through reconciliation. The `.InitialSize()` value only applies on first creation—subsequent user resizing takes precedence.
:::

## Size Change Callback

React to size changes with `.OnSizeChanged()`:

```csharp
var panelWidth = 30;

h.DragBarPanel(content)
    .InitialSize(30)
    .OnSizeChanged(size => panelWidth = size)
```

The callback receives the new size as an `int` after clamping to min/max bounds. Both synchronous and async overloads are available:

```csharp
// Synchronous
.OnSizeChanged(size => state.Width = size)

// Asynchronous
.OnSizeChanged(async size => await SaveLayoutAsync(size))
```

## Theming

Customize the handle appearance using theme elements:

```csharp
using Hex1b.Theming;

var theme = new Hex1bTheme("Custom")
    .Set(DragBarPanelTheme.HandleColor, Hex1bColor.DarkGray)
    .Set(DragBarPanelTheme.HandleHoverColor, Hex1bColor.Cyan)
    .Set(DragBarPanelTheme.HandleFocusedColor, Hex1bColor.Cyan)
    .Set(DragBarPanelTheme.ThumbColor, Hex1bColor.Yellow);
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `VerticalHandleChar` | `string` | `"│"` | Character for Left/Right edge handles |
| `HorizontalHandleChar` | `string` | `"─"` | Character for Top/Bottom edge handles |
| `VerticalThumbChar` | `string` | `"⣿"` | Braille thumb on vertical handles (shown on hover/focus) |
| `HorizontalThumbChar` | `string` | `"⠶"` | Braille thumb on horizontal handles (shown on hover/focus) |
| `HandleColor` | `Hex1bColor` | Gray | Handle color in default state |
| `HandleHoverColor` | `Hex1bColor` | White | Handle color when hovered |
| `HandleFocusedColor` | `Hex1bColor` | White | Handle color when focused |
| `ThumbColor` | `Hex1bColor` | White | Color of the braille thumb indicator |

## DragBarPanel vs Splitter

Both widgets provide resizable layouts, but they serve different purposes:

| Feature | DragBarPanel | Splitter |
|---------|-------------|----------|
| **Panes** | Wraps one child | Manages two children |
| **Composability** | Drop into any layout | Replaces the parent layout |
| **Handle** | On one edge of the panel | Between the two panes |
| **Use case** | Resizable sidebars, tool panels | Fixed two-pane splits |
| **Multiple resizable areas** | Stack several in an HStack/VStack | Nest splitters |

Use DragBarPanel when you want individual panels that own their resize behavior. Use Splitter when you want a fixed two-pane layout with a divider.

## Related Widgets

- [Splitter](/guide/widgets/splitter) - Two-pane resizable splits
- [Stacks (HStack/VStack)](/guide/widgets/stacks) - Layout containers for DragBarPanels
- [Border](/guide/widgets/containers) - Add borders around panel content
- [Scroll](/guide/widgets/scroll) - Scrollable content inside panels
