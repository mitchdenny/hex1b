<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode   → src/Hex1b.Website/Examples/FloatPanelBasicExample.cs
  - overlayCode → src/Hex1b.Website/Examples/FloatPanelOverlayExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/float-panel-basic.cs?raw'
import overlapSnippet from './snippets/float-panel-overlap.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.FloatPanel(f => [
        f.Place(2, 1, f.Text("📍 Marker at (2, 1)")),
        f.Place(30, 5, f.Text("📍 Marker at (30, 5)")),
        f.Place(10, 9, f.Text("📍 Marker at (10, 9)")),
        f.Place(45, 3, f.Border(b => [
            b.Text("Boxed content")
        ]).Title("Info")),
    ]))
    .Build();

await terminal.RunAsync();`

const overlayCode = `using Hex1b;

var state = new OverlayState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.ZStack(z => [
        // Background content
        z.VStack(v => [
            v.Text("═══════════════════════════════════════"),
            v.Text("         Main Application Area         "),
            v.Text("═══════════════════════════════════════"),
            v.Text(""),
            v.Text("  Content goes here..."),
        ]),
        // FloatPanel overlay with score and controls
        z.FloatPanel(f => [
            f.Place(2, 0, f.Text($"Score: {state.Score}")),
            f.Place(2, 8, f.Button("+1 Point").OnClick(_ => state.Score++)),
            f.Place(20, 8, f.Button("Reset").OnClick(_ => state.Score = 0)),
        ]),
    ]))
    .Build();

await terminal.RunAsync();

class OverlayState
{
    public int Score { get; set; }
}`
</script>

# FloatPanelWidget

A container that positions children at absolute (x, y) character coordinates. Widget order implies z-order—first child renders at the bottom, last child renders on top.

FloatPanel is useful for overlays, HUDs, map markers, tooltips, and any scenario where widgets need to be placed at arbitrary positions rather than flowing in a layout.

## Basic Usage

Use the `FloatPanel` extension method with `Place` to position children at specific coordinates:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="float-panel-basic" exampleTitle="FloatPanel - Basic Usage" />

Each child is placed at the specified `(x, y)` character position relative to the panel's origin. Children can be any widget—text, borders, buttons, or even nested layout containers.

## How It Works

### Coordinate System

Coordinates are character-based, measured from the panel's top-left corner:

```
(0,0) ───── X increases →
  │
  │   (5,2) = column 5, row 2
  │
  Y increases ↓
```

- **X** — Column offset (characters from left edge)
- **Y** — Row offset (lines from top edge)

Children are offset from the panel's own position, not from the terminal origin. If the panel is arranged at `(10, 5)`, a child placed at `(3, 2)` renders at terminal position `(13, 7)`.

### Z-Order

Children render in array order—earlier children are at the bottom, later children are on top. When children overlap, the last one wins:

<StaticTerminalPreview svgPath="/svg/float-panel-overlap.svg" :code="overlapSnippet" />

## Use as an Overlay

FloatPanel works well inside a `ZStack` to create HUD-style overlays on top of other content:

<CodeBlock lang="csharp" :code="overlayCode" command="dotnet run" example="float-panel-overlay" exampleTitle="FloatPanel - Interactive Overlay" />

::: tip Combining with ZStack
Place your main content as the first child of a `ZStack`, then add a `FloatPanel` as the second child. The FloatPanel renders on top, letting you position controls, status displays, or markers at exact coordinates over the background.
:::

## Clipping

By default, FloatPanel clips children that extend beyond its bounds. Children placed at coordinates outside the panel's area (or whose content overflows) will be truncated at the panel edges.

## Focus Management

FloatPanel manages focus for its children. If the panel contains focusable widgets (buttons, text boxes, etc.):

- **Tab** cycles focus forward through children
- **Shift+Tab** cycles focus backward
- Focus order follows the array order of children

## API Reference

### FloatPanelWidget

| Property | Type | Description |
|----------|------|-------------|
| `Children` | `IReadOnlyList<FloatChild>` | The positioned child widgets |

### FloatChild

| Property | Type | Description |
|----------|------|-------------|
| `X` | `int` | Column offset within the panel |
| `Y` | `int` | Row offset within the panel |
| `Widget` | `Hex1bWidget` | The child widget to render |

### Extension Methods

| Method | Description |
|--------|-------------|
| `ctx.FloatPanel(builder)` | Creates a FloatPanel with positioned children |
| `ctx.Place(x, y, widget)` | Positions a widget at absolute coordinates within a FloatPanel |

## Related Widgets

- [ZStack](/guide/widgets/stacks) — Layer widgets on top of each other (FloatPanel works well as a ZStack child)
- [Align](/guide/widgets/align) — Position content relative to available space
- [Border](/guide/widgets/border) — Add decorative borders around content
- [Windows](/guide/widgets/windows) — Floating, draggable window panels with title bars
