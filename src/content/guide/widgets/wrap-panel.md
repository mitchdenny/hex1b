<script setup>
import basicSnippet from './snippets/wrap-panel-basic.cs?raw'
import verticalSnippet from './snippets/wrap-panel-vertical.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.WrapPanel(w => [
        w.Border(b => [b.Text("Item 1")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 2")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 3")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 4")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 5")]).FixedWidth(15),
        w.Border(b => [b.Text("Item 6")]).FixedWidth(15),
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# WrapPanelWidget

Arrange child widgets sequentially, wrapping to the next row or column when available space is exceeded.

WrapPanelWidget is a layout container that flows children left-to-right (horizontal) or top-to-bottom (vertical), automatically wrapping to the next row or column when a child would exceed the available extent. It works best with uniformly-sized children and is ideal for card grids, tag lists, and responsive item layouts.

## Basic Usage

Create a horizontal wrap panel using the fluent API with collection expression syntax:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" />

::: tip Focus Management
WrapPanelWidget manages focus for all its descendant widgets. Use **Tab** to move focus forward and **Shift+Tab** to move backward through focusable children.
:::

## Horizontal Wrapping

Children flow left-to-right and wrap to the next row when the available width is exceeded:

<StaticTerminalPreview svgPath="/svg/wrap-panel-basic.svg" :code="basicSnippet" />

Items that don't fit on the current row are moved to the next row. The row height is determined by the tallest child in that row.

## Vertical Wrapping

Use `VWrapPanel` to flow children top-to-bottom, wrapping to the next column:

<StaticTerminalPreview svgPath="/svg/wrap-panel-vertical.svg" :code="verticalSnippet" />

Items that don't fit in the current column are moved to the next column. The column width is determined by the widest child in that column.

## Layout Algorithm

WrapPanelWidget lays out children in a single pass:

1. **Measure**: Each child is measured with unbounded extent in the wrap direction (height for horizontal, width for vertical) and the panel's max extent in the primary direction
2. **Place**: Children are placed sequentially. When a child would exceed the primary extent, a new row/column is started
3. **Row/Column sizing**: Each row's height (or column's width) is the maximum of its children's measured sizes

## Sizing Children

WrapPanel children should generally use fixed or content sizing:

### Fixed Size Items (Recommended)

```csharp
ctx.WrapPanel(w => items.Select(item =>
    w.Border(b => [
        b.Text(item.Name)
    ]).FixedWidth(20).FixedHeight(5)
).ToArray())
```

### Content-Sized Items

```csharp
ctx.WrapPanel(w => tags.Select(tag =>
    w.Text($" {tag} ")
).ToArray())
```

::: warning
Avoid using `.Fill()` on WrapPanel children. Fill sizing requires a known container extent, but WrapPanel measures children with unbounded constraints in the wrap direction, which can produce unexpected results.
:::

## Focus Navigation

WrapPanelWidget provides default keyboard bindings:

| Key | Action |
|-----|--------|
| Tab | Move focus to next focusable widget |
| Shift+Tab | Move focus to previous focusable widget |

Focus order follows the sequential order of children in the array, not the visual row/column position.

## Clipping

Content that extends beyond the WrapPanel's bounds is clipped by default. For scrollable wrap layouts, wrap the WrapPanel in a `VScrollPanel`:

```csharp
ctx.VScrollPanel(
    ctx.WrapPanel(w => items.Select(item =>
        w.Border(b => [
            b.Text(item.Name),
            b.Text(item.Description)
        ]).FixedWidth(25)
    ).ToArray())
)
```

## Common Patterns

### Card Grid

Display a collection of items in a responsive card layout:

```csharp
ctx.WrapPanel(w => templates.Select(t =>
    w.Border(b => [
        b.Text(t.Name),
        b.Text(t.Language),
        b.Text($"⭐ {t.Stars}")
    ]).FixedWidth(30)
).ToArray())
```

### Tag Cloud

Arrange tags or labels that wrap naturally:

```csharp
ctx.WrapPanel(w => tags.Select(tag =>
    w.Text($" [{tag}] ")
).ToArray())
```

### Toolbar with Overflow

Create a toolbar that wraps buttons to the next row when the terminal is narrow:

```csharp
ctx.WrapPanel(w => [
    w.Button("New").OnClick(_ => New()),
    w.Button("Open").OnClick(_ => Open()),
    w.Button("Save").OnClick(_ => Save()),
    w.Button("Export").OnClick(_ => Export()),
    w.Button("Settings").OnClick(_ => Settings()),
])
```

## Performance Considerations

- WrapPanel measures all children during layout
- Children are measured with unbounded constraints in the wrap direction
- For large collections, consider virtualizing with `ScrollPanel` or paginating manually
- Fixed-size children are faster to lay out than content-sized children

## Related Widgets

- [HStackWidget](/guide/widgets/hstack) — For single-row horizontal layouts without wrapping
- [VStackWidget](/guide/widgets/vstack) — For single-column vertical layouts without wrapping
- [GridWidget](/guide/widgets/grid) — For explicit two-dimensional grid positioning
- [Scroll](/guide/widgets/scroll) — For scrollable content when WrapPanel overflows
