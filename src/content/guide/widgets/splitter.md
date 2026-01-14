<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode → src/Hex1b.Website/Examples/SplitterBasicExample.cs
  - verticalCode → src/Hex1b.Website/Examples/SplitterVerticalExample.cs
  - nestedCode → src/Hex1b.Website/Examples/SplitterNestedExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HSplitter(
        ctx.Panel(left => [
            left.VStack(v => [
                v.Text("Left Pane"),
                v.Text(""),
                v.Text("This is the left side").Wrap(),
                v.Text("of a horizontal split.").Wrap(),
                v.Text(""),
                v.Text("Tab to focus the splitter,").Wrap(),
                v.Text("then use ← → to resize.").Wrap()
            ])
        ]),
        ctx.Panel(right => [
            right.VStack(v => [
                v.Text("Right Pane"),
                v.Text(""),
                v.Text("This is the right side").Wrap(),
                v.Text("of the horizontal split.").Wrap(),
                v.Text(""),
                v.Text("Both panes share the").Wrap(),
                v.Text("full height.").Wrap()
            ])
        ]),
        leftWidth: 25
    ))
    .Build();

await terminal.RunAsync();`

const verticalCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VSplitter(
        ctx.Panel(top => [
            top.VStack(v => [
                v.Text("Top Pane"),
                v.Text(""),
                v.Text("This is the top section of a vertical split.").Wrap()
            ])
        ]),
        ctx.Panel(bottom => [
            bottom.VStack(v => [
                v.Text("Bottom Pane"),
                v.Text(""),
                v.Text("This is the bottom section. Tab to the splitter,").Wrap(),
                v.Text("then use ↑ ↓ to resize the top/bottom panes.").Wrap(),
                v.Text(""),
                v.Text("Great for editor + terminal layouts.").Wrap()
            ])
        ]),
        topHeight: 5
    ))
    .Build();

await terminal.RunAsync();`

const nestedCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VSplitter(
        // Top: horizontal splitter
        ctx.HSplitter(
            ctx.Panel(tl => [
                tl.VStack(v => [
                    v.Text("Top-Left"),
                    v.Text(""),
                    v.Text("Horizontal split").Wrap(),
                    v.Text("in top pane").Wrap()
                ])
            ]),
            ctx.Panel(tr => [
                tr.VStack(v => [
                    v.Text("Top-Right"),
                    v.Text(""),
                    v.Text("Both panes share").Wrap(),
                    v.Text("the same height").Wrap()
                ])
            ]),
            leftWidth: 20
        ),
        // Bottom: single panel
        ctx.Panel(bottom => [
            bottom.VStack(v => [
                v.Text("Bottom Pane"),
                v.Text(""),
                v.Text("This demonstrates nesting a horizontal splitter").Wrap(),
                v.Text("inside the top pane of a vertical splitter.").Wrap(),
                v.Text("Great for IDE-style layouts!").Wrap()
            ])
        ]),
        topHeight: 6
    ))
    .Build();

await terminal.RunAsync();`
</script>

# SplitterWidget

A resizable splitter that divides the available space into two panes, either horizontally (left/right) or vertically (top/bottom).

The splitter widget is perfect for creating multi-pane layouts like editor interfaces, file browsers with preview panes, or terminal applications with side panels. Users can resize panes using keyboard navigation or mouse dragging.

## Basic Usage

Create a horizontal splitter using the `HSplitter` extension method:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="splitter-basic" exampleTitle="Splitter Widget - Horizontal Split" />

The splitter displays a visual divider (`│` for horizontal, `─` for vertical) between the two panes. Each pane's content is automatically clipped to its allocated space.

::: tip Navigation
- **Tab** to focus the splitter divider
- **←→** arrows to resize horizontal splitters
- **↑↓** arrows to resize vertical splitters
- **Mouse drag** on the divider to resize (when mouse support is enabled)
:::

## Vertical Splitter

Use `VSplitter` to create a vertical split with top and bottom panes:

<CodeBlock lang="csharp" :code="verticalCode" command="dotnet run" example="splitter-vertical" exampleTitle="Splitter Widget - Vertical Split" />

## Nested Splitters

Splitters can be nested to create complex multi-pane layouts:

<CodeBlock lang="csharp" :code="nestedCode" command="dotnet run" example="splitter-nested" exampleTitle="Splitter Widget - Nested Splitters" />

You can nest splitters in any combination:
- Horizontal inside vertical (editor layout with sidebar)
- Vertical inside horizontal (file tree + editor + terminal)
- Multiple levels deep for quad-split layouts

## Resizing Behavior

### Keyboard Resizing

When the splitter divider is focused:

| Key | Action (Horizontal) | Action (Vertical) |
|-----|-------------------|------------------|
| **←** | Decrease first pane width | _(not used)_ |
| **→** | Increase first pane width | _(not used)_ |
| **↑** | _(not used)_ | Decrease first pane height |
| **↓** | _(not used)_ | Increase first pane height |

The default resize step is 2 characters. Resizing respects minimum pane sizes to prevent either pane from becoming unusable.

### Mouse Resizing

Click and drag the divider to resize panes interactively. The divider area has a hit test zone that captures mouse events only on the divider itself, allowing clicks through to child widgets.

### Initial Size and Constraints

Set the initial size of the first pane using constructor parameters:

```csharp
// Horizontal: leftWidth parameter (default: 30)
ctx.HSplitter(leftPane, rightPane, leftWidth: 40)

// Vertical: topHeight parameter (default: 10)
ctx.VSplitter(topPane, bottomPane, topHeight: 8)
```

The splitter maintains minimum sizes for both panes to ensure usability. The second pane automatically takes the remaining space.

::: warning State Preservation
After a user resizes a splitter, the widget preserves the new size across re-renders through reconciliation. Setting `firstSize` in the widget only affects the initial creation—manual resizing takes precedence.
:::

## Focus Navigation

The splitter participates in the focus system:

1. **Tab navigation**: Tab moves focus through the first pane's widgets, then to the splitter divider, then through the second pane's widgets
2. **Splitter focus**: When the divider is focused, arrow keys resize the panes
3. **Escape to first**: Press Escape while focused on the divider to jump to the first focusable widget

Focus indicators:
- Unfocused divider: displays in the theme's divider color
- Focused divider: inverts colors (divider color becomes background, contrasting foreground)

## Extension Methods

The splitter widget provides several convenience extension methods:

### HSplitter (Horizontal)

```csharp
// Basic horizontal split
ctx.HSplitter(leftWidget, rightWidget, leftWidth: 30)

// With callbacks for VStack children
ctx.HSplitter(
    left => [
        left.Text("Item 1"),
        left.Text("Item 2")
    ],
    right => [
        right.Text("Content")
    ],
    leftWidth: 25
)
```

### VSplitter (Vertical)

```csharp
// Basic vertical split
ctx.VSplitter(topWidget, bottomWidget, topHeight: 10)

// With callbacks for VStack children
ctx.VSplitter(
    top => [
        top.Text("Header")
    ],
    bottom => [
        bottom.Text("Content")
    ],
    topHeight: 3
)
```

## Theming

Customize the splitter appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(SplitterTheme.DividerColor, Hex1bColor.Cyan)
    .Set(SplitterTheme.DividerCharacter, "┃")
    .Set(SplitterTheme.LeftArrowCharacter, "◀")
    .Set(SplitterTheme.RightArrowCharacter, "▶");

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => /* ... */;
    })
    .Build();

await terminal.RunAsync();
```

### Available Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `DividerColor` | `Hex1bColor` | Gray | Color of the divider line |
| `DividerCharacter` | `string` | `"│"` | Character for horizontal divider |
| `HorizontalDividerCharacter` | `string` | `"─"` | Character for vertical divider |
| `LeftArrowCharacter` | `string` | `"◀"` | Left arrow hint (horizontal) |
| `RightArrowCharacter` | `string` | `"▶"` | Right arrow hint (horizontal) |
| `UpArrowCharacter` | `string` | `"▲"` | Up arrow hint (vertical) |
| `DownArrowCharacter` | `string` | `"▼"` | Down arrow hint (vertical) |
| `LeftArrowColor` | `Hex1bColor` | Default (falls back to divider color at render) | Color for left arrow |
| `RightArrowColor` | `Hex1bColor` | Default (falls back to divider color at render) | Color for right arrow |
| `UpArrowColor` | `Hex1bColor` | Default (falls back to divider color at render) | Color for up arrow |
| `DownArrowColor` | `Hex1bColor` | Default (falls back to divider color at render) | Color for down arrow |

Arrow hints are displayed at the center of the divider when the splitter is tall/wide enough, indicating the resize direction.

## Common Use Cases

### Editor with Sidebar

```csharp
ctx.HSplitter(
    ctx.Panel(left => [
        left.VStack(v => [
            v.Text("Files"),
            v.List(fileList).Fill()
        ])
    ]),
    ctx.Panel(right => [
        right.VStack(v => [
            v.Text("Editor"),
            v.TextBox(editorState).Fill()
        ])
    ]),
    leftWidth: 30
)
```

### Terminal with Output Panel

```csharp
ctx.VSplitter(
    ctx.Panel(top => [
        top.Text("Main Content").Fill()
    ]),
    ctx.Panel(bottom => [
        bottom.VStack(v => [
            v.Text("Output"),
            v.Scroll(s => [
                s.Text(outputLog)
            ])
        ])
    ]),
    topHeight: 20
)
```

### Quad Layout (IDE-style)

```csharp
ctx.VSplitter(
    ctx.HSplitter(explorerPane, editorPane, leftWidth: 25),
    ctx.HSplitter(terminalPane, outputPane, leftWidth: 25),
    topHeight: 20
)
```

## Related Widgets

- [VStackWidget](/guide/widgets/stacks) - Vertical layout without resizing
- [HStackWidget](/guide/widgets/stacks) - Horizontal layout without resizing
- [BorderWidget](/guide/widgets/containers) - Add borders around splitter panes
- [PanelWidget](/guide/widgets/containers) - Add padding to splitter content
