<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode   → src/Hex1b.Website/Examples/FloatBasicExample.cs
  - overlayCode → src/Hex1b.Website/Examples/FloatOverlayExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import basicSnippet from './snippets/float-basic.cs?raw'
import overlapSnippet from './snippets/float-overlap.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Float(v.Text("📍 Marker at (2, 1)")).Absolute(2, 1),
        v.Float(v.Text("📍 Marker at (30, 5)")).Absolute(30, 5),
        v.Float(v.Text("📍 Marker at (10, 9)")).Absolute(10, 9),
        v.Float(v.Border(b => [
            b.Text("Boxed content")
        ]).Title("Info")).Absolute(45, 3),
    ]))
    .Build();

await terminal.RunAsync();`

const overlayCode = `using Hex1b;

var state = new OverlayState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("═══════════════════════════════════════"),
        v.Text("         Main Application Area         "),
        v.Text("═══════════════════════════════════════"),
        v.Text(""),
        v.Text("  Content goes here..."),
        // Float overlay with score and controls
        v.Float(v.Text(\`Score: \${state.Score}\`)).Absolute(2, 0),
        v.Float(v.Button("+1 Point").OnClick(_ => state.Score++)).Absolute(2, 8),
        v.Float(v.Button("Reset").OnClick(_ => state.Score = 0)).Absolute(20, 8),
    ]))
    .Build();

await terminal.RunAsync();

class OverlayState
{
    public int Score { get; set; }
}`

const alignmentCode = `using Hex1b;
using Hex1b.Widgets;

var state = new AlignmentState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v =>
    {
        var anchor = v.Border(b => [
            b.Text("  Anchor Widget  ")
        ]).Title("Anchor");

        var floated = v.Float(
            v.Border(b => [ b.Text("Float") ]).Title("Float")
        );

        floated = state.Horizontal switch
        {
            "AlignLeft" => floated.AlignLeft(anchor, state.Offset),
            "AlignRight" => floated.AlignRight(anchor, state.Offset),
            "ExtendLeft" => floated.ExtendLeft(anchor, state.Offset),
            "ExtendRight" => floated.ExtendRight(anchor, state.Offset),
            _ => floated,
        };
        floated = state.Vertical switch
        {
            "AlignTop" => floated.AlignTop(anchor, state.Offset),
            "AlignBottom" => floated.AlignBottom(anchor, state.Offset),
            "ExtendTop" => floated.ExtendTop(anchor, state.Offset),
            "ExtendBottom" => floated.ExtendBottom(anchor, state.Offset),
            _ => floated,
        };
        if (state.Horizontal == "(none)" && state.Vertical == "(none)")
            floated = floated.Absolute(25, 6);

        return [
            v.Text(""),
            v.HStack(h => [
                h.Text(" Horizontal: "),
                h.Picker("(none)", "AlignLeft", "AlignRight", "ExtendLeft", "ExtendRight")
                    .OnSelectionChanged(e => state.Horizontal = e.SelectedText),
                h.Text("  Vertical: "),
                h.Picker("(none)", "AlignTop", "AlignBottom", "ExtendTop", "ExtendBottom")
                    .OnSelectionChanged(e => state.Vertical = e.SelectedText),
            ]),
            v.Text(""),
            anchor,
            floated,
        ];
    }))
    .Build();

await terminal.RunAsync();

class AlignmentState
{
    public string Horizontal { get; set; } = "(none)";
    public string Vertical { get; set; } = "(none)";
    public int Offset { get; set; }
}`
</script>

# FloatWidget

Removes a child widget from the container's normal layout flow and positions it at absolute coordinates or relative to an anchor sibling. Any container that implements `IFloatWidgetContainer` supports floating—currently **VStack**, **HStack**, and **ZStack**.

FloatWidget is useful for overlays, HUDs, tooltips, map markers, and any scenario where widgets need to be placed at arbitrary positions over flowing content. It works like CSS `position: absolute`—the float is positioned within the container's bounds but does not participate in flow layout.

## Basic Usage

Use the `Float` extension method to wrap a widget, then call `Absolute(x, y)` to position it:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="float-basic" exampleTitle="Float - Basic Usage" />

Each floated child is placed at the specified `(x, y)` character position relative to the container's origin. The child can be any widget—text, borders, buttons, or even nested layout containers.

## How It Works

### Coordinate System

Coordinates are character-based, measured from the container's top-left corner:

```
(0,0) ───── X increases →
  │
  │   (5,2) = column 5, row 2
  │
  Y increases ↓
```

- **X** — Column offset (characters from left edge)
- **Y** — Row offset (lines from top edge)

Children are offset from the container's own position, not from the terminal origin. If the container is arranged at `(10, 5)`, a float at `Absolute(3, 2)` renders at terminal position `(13, 7)`.

### Z-Order

Float children render **after** all normal flow children, in their array order. When floats overlap, the last one wins:

<StaticTerminalPreview svgPath="/svg/float-overlap.svg" :code="overlapSnippet" />

### Flow vs Float

Normal children participate in the container's layout flow (stacking vertically in VStack, horizontally in HStack, etc.). Floated children are removed from flow and positioned independently:

```csharp
ctx.VStack(v => [
    v.Text("Normal flow item 1"),      // Flows normally
    v.Text("Normal flow item 2"),      // Flows normally
    v.Float(v.Text("Overlay")).Absolute(10, 5),  // Positioned independently
])
```

## Anchor-Relative Positioning

Instead of absolute coordinates, floats can be positioned relative to another widget in the container using alignment methods. The anchor can be a flow sibling, a widget nested inside a flow child, or even another float. This is useful for tooltips, dropdowns, and contextual overlays.

### Alignment Methods

| Method | Effect |
|--------|--------|
| `AlignLeft(anchor, offset?)` | Float's left edge = anchor's left edge |
| `AlignRight(anchor, offset?)` | Float's right edge = anchor's right edge |
| `AlignTop(anchor, offset?)` | Float's top edge = anchor's top edge |
| `AlignBottom(anchor, offset?)` | Float's bottom edge = anchor's bottom edge |
| `ExtendRight(anchor, offset?)` | Float's left edge = anchor's right edge (place beside) |
| `ExtendLeft(anchor, offset?)` | Float's right edge = anchor's left edge (place beside) |
| `ExtendBottom(anchor, offset?)` | Float's top edge = anchor's bottom edge (place below) |
| `ExtendTop(anchor, offset?)` | Float's bottom edge = anchor's top edge (place above) |

**Align** methods align the same edge of both widgets. **Extend** methods align opposing edges to place the float adjacent to the anchor.

### Example: Tooltip Below a Button

```csharp
ctx.VStack(v =>
{
    var header = v.Text("Dashboard");
    return [
        header,
        v.Text("Content goes here..."),
        v.Float(v.Border(b => [
            b.Text("Welcome! Click any item to begin.")
        ]).Title("Tip"))
            .AlignLeft(header)
            .ExtendBottom(header, offset: 1),
    ];
})
```

Alignment methods are composable—chain a horizontal and a vertical method to position in both axes. The `offset` parameter shifts the float from the computed position.

### Try It: Alignment Explorer

Use the dropdowns to see how each alignment option positions the floated border relative to the anchor:

<CodeBlock lang="csharp" :code="alignmentCode" command="dotnet run" example="float-alignment" exampleTitle="Float - Alignment Explorer" />

::: info Anchor Scope
The anchor widget can be any widget within the same container—either a direct flow sibling or a widget nested inside a flow child (e.g., a border wrapped in `Center` or `Padding`). Floats can also anchor to other floated widgets in the same container.
:::

### Anchoring to Nested Widgets

The anchor widget doesn't have to be a direct flow child—it can be nested inside layout wrappers like `Center`, `Padding`, or other containers:

```csharp
ctx.VStack(v =>
{
    var innerBorder = v.Border(b => [b.Text("Target")]).Title("Anchor");
    return [
        v.Center(v.Padding(4, 4, 2, 2, innerBorder)),
        v.Float(v.Text("←")).ExtendLeft(innerBorder).AlignTop(innerBorder),
    ];
})
```

The float positions relative to the inner border's actual rendered bounds, not the `Center`/`Padding` wrapper. This is useful when you want visual padding around an anchor but need floats to align precisely with the inner content.

### Float-to-Float Anchoring

Floats can anchor to other floated widgets in the same container. The float declared first is arranged first, so subsequent floats can reference its position:

```csharp
ctx.VStack(v =>
{
    var menu = v.Border(b => [b.Text("Menu")]).Title("Menu");
    var tooltip = v.Text("Tooltip text");
    return [
        v.Float(menu).Absolute(5, 3),
        v.Float(tooltip).ExtendRight(menu).AlignTop(menu),
    ];
})
```

The tooltip float chains to the menu float—it appears immediately to the right, top-aligned. This enables building complex floating UIs like cascading menus or multi-part HUDs.

::: warning Declaration Order Matters
When a float anchors to another float, the anchor must be declared **before** the dependent float in the children array. Floats are arranged in declaration order.
:::

## Use as an Overlay

Floats render on top of flow content, making them ideal for HUD-style overlays:

<CodeBlock lang="csharp" :code="overlayCode" command="dotnet run" example="float-overlay" exampleTitle="Float - Interactive Overlay" />

The score text and buttons are floated over the background text. Flow children lay out normally while floats are positioned independently on top.

## Supported Containers

Float is available in any container implementing `IFloatWidgetContainer`:

| Container | Use Case |
|-----------|----------|
| **VStack** | Float overlays within vertical layouts |
| **HStack** | Float overlays within horizontal layouts |
| **ZStack** | Layered overlays with precise positioning |

## Clipping

Floats are clipped to the container's bounds. Children placed at coordinates outside the container's area (or whose content overflows) are truncated at the container edges.

## Focus Management

Focus order follows the **declaration order** in the children array, not render order. A float declared between two flow children participates in focus at that position:

```csharp
ctx.VStack(v => [
    v.Button("First"),                               // Focus order: 1
    v.Float(v.Button("Overlay")).Absolute(10, 5),    // Focus order: 2
    v.Button("Third"),                               // Focus order: 3
])
```

- **Tab** cycles focus forward through all children (flow and float) in declaration order
- **Shift+Tab** cycles backward

## API Reference

### FloatWidget

| Property | Type | Description |
|----------|------|-------------|
| `Child` | `Hex1bWidget` | The widget removed from flow and positioned independently |

### Extension Methods

| Method | Description |
|--------|-------------|
| `v.Float(widget)` | Wraps a widget in a FloatWidget (available on VStack, HStack, ZStack contexts) |

### Positioning Methods

| Method | Description |
|--------|-------------|
| `.Absolute(x, y)` | Position at absolute coordinates within the container |
| `.AlignLeft(anchor, offset?)` | Align left edges |
| `.AlignRight(anchor, offset?)` | Align right edges |
| `.AlignTop(anchor, offset?)` | Align top edges |
| `.AlignBottom(anchor, offset?)` | Align bottom edges |
| `.ExtendRight(anchor, offset?)` | Place to the right of anchor |
| `.ExtendLeft(anchor, offset?)` | Place to the left of anchor |
| `.ExtendBottom(anchor, offset?)` | Place below anchor |
| `.ExtendTop(anchor, offset?)` | Place above anchor |

## Related Widgets

- [Stacks](/guide/widgets/stacks) — VStack, HStack, ZStack containers that support floating
- [Align](/guide/widgets/align) — Position content relative to available space
- [Border](/guide/widgets/border) — Add decorative borders around content
- [Windows](/guide/widgets/windows) — Floating, draggable window panels with title bars
