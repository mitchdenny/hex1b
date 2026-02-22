<script setup>
import basicSnippet from './snippets/grid-basic.cs?raw'
import sidebarSnippet from './snippets/grid-sidebar.cs?raw'
import columnsSnippet from './snippets/grid-columns.cs?raw'

const basicCode = `using Hex1b;
using Hex1b.Layout;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
        ctx.Grid(g =>
        {
            g.Columns.Add(SizeHint.Fixed(22));
            g.Columns.Add(SizeHint.Fill);

            g.Rows.Add(SizeHint.Fixed(3));
            g.Rows.Add(SizeHint.Fill);
            g.Rows.Add(SizeHint.Fixed(1));

            return [
                g.Cell(c => c.Border(b => [
                    b.VStack(v => [
                        v.Text("📂 Files"),
                        v.Text("📊 Dashboard"),
                        v.Text("⚙️ Settings"),
                    ])
                ]).Title("Navigation")).RowSpan(0, 3).Column(0),

                g.Cell(c => c.Border(b => [
                    b.Text("Grid Layout Demo"),
                ]).Title("Header")).Row(0).Column(1),

                g.Cell(c => c.Border(b => [
                    b.Text("Main content area")
                ]).Title("Content")).Row(1).Column(1),

                g.Cell(c => c.Text(" Status: Ready")).Row(2).Column(1),
            ];
        }))
    .Build();

await terminal.RunAsync();`
</script>

<!--
⚠️ MIRROR WARNING: The basic code sample mirrors the WebSocket example in:
   src/Hex1b.Website/Examples/GridBasicExample.cs
   Keep both files in sync when making changes.
-->

# GridWidget

Arrange child widgets in a two-dimensional grid with row and column spanning.

GridWidget is a layout container that positions children explicitly by row and column index. It supports fixed, content-based, and fill sizing for both columns and rows, with optional spanning across multiple rows or columns.

## Basic Usage

Create a grid layout using the fluent API. Each cell is positioned with `.Row()` and `.Column()`:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="grid-basic" exampleTitle="Grid - Basic Layout" />

## Simple Grid

Position cells by row and column index:

<StaticTerminalPreview svgPath="/svg/grid-basic.svg" :code="basicSnippet" />

Cells default to row 0, column 0 with a span of 1. Use `.Row(n)` and `.Column(n)` to place them explicitly.

## Row and Column Spanning

Cells can span multiple rows or columns using `.RowSpan(row, span)` and `.ColumnSpan(col, span)`:

```csharp
ctx.Grid(g => [
    // Spans rows 0 and 1 in column 0
    g.Cell(c => c.Text("Sidebar")).RowSpan(0, 2).Column(0).Width(20),

    // Spans columns 0 and 1 in row 0
    g.Cell(c => c.Text("Header")).Row(0).ColumnSpan(0, 2),
])
```

- `.RowSpan(row, span)` — Start at `row`, occupy `span` rows
- `.ColumnSpan(col, span)` — Start at `col`, occupy `span` columns
- `.Row(n)` is shorthand for `.RowSpan(n, 1)`
- `.Column(n)` is shorthand for `.ColumnSpan(n, 1)`

## Sidebar Layout

A classic application layout with sidebar, header, and content area:

<StaticTerminalPreview svgPath="/svg/grid-sidebar.svg" :code="sidebarSnippet" />

The sidebar spans both rows while the header and content each occupy one row in the second column.

## Sizing Columns and Rows

### Column Width

Set column widths directly on cells or through explicit definitions:

<StaticTerminalPreview svgPath="/svg/grid-columns.svg" :code="columnsSnippet" />

- `.Width(n)` — Fixed width of `n` characters
- `.FillWidth()` — Fill remaining horizontal space
- `.FillWidth(weight)` — Fill with proportional weight

### Row Height

Similarly for row heights:

- `.Height(n)` — Fixed height of `n` rows
- `.FillHeight()` — Fill remaining vertical space
- `.FillHeight(weight)` — Fill with proportional weight

### Explicit Definitions

For more control, define columns and rows explicitly via `GridContext`:

```csharp
ctx.Grid(g => {
    g.Columns.Add(SizeHint.Fixed(20));    // Column 0: 20 chars wide
    g.Columns.Add(SizeHint.Fill);          // Column 1: fill remaining

    g.Rows.Add(SizeHint.Fixed(3));         // Row 0: 3 rows tall
    g.Rows.Add(SizeHint.Fill);             // Row 1: fill remaining

    return [
        g.Cell(c => c.Text("Nav")).RowSpan(0, 2).Column(0),
        g.Cell(c => c.Text("Header")).Row(0).Column(1),
        g.Cell(c => c.Text("Content")).Row(1).Column(1),
    ];
})
```

::: tip Auto-Created Definitions
Columns and rows not covered by explicit definitions are auto-created from cell positions with **Content** sizing (shrink to fit). You only need explicit definitions when you want Fill or Fixed sizing.
:::

## Layout Algorithm

GridWidget distributes space in two passes per axis:

1. **Fixed Pass**: Fixed-size columns/rows get their exact size. Content-sized columns/rows are measured from their non-spanning cells, taking the maximum.
2. **Fill Pass**: Remaining space is distributed among fill columns/rows proportionally by weight.

All children receive the full width and height of their cell (accounting for any spans).

## Common Patterns

### Dashboard Layout

```csharp
ctx.Grid(g => {
    g.Columns.Add(SizeHint.Fill);
    g.Columns.Add(SizeHint.Fill);

    g.Rows.Add(SizeHint.Fixed(1));
    g.Rows.Add(SizeHint.Fill);
    g.Rows.Add(SizeHint.Fill);

    return [
        g.Cell(c => c.Text("Dashboard")).Row(0).ColumnSpan(0, 2),
        g.Cell(c => c.Border(b => [b.Text("Chart 1")]).Title("Sales"))
            .Row(1).Column(0),
        g.Cell(c => c.Border(b => [b.Text("Chart 2")]).Title("Users"))
            .Row(1).Column(1),
        g.Cell(c => c.Border(b => [b.Text("Details")]).Title("Activity"))
            .Row(2).ColumnSpan(0, 2),
    ];
})
```

### Form with Labels

```csharp
ctx.Grid(g => [
    g.Cell(c => c.Text("Name:")).Row(0).Column(0).Width(12),
    g.Cell(c => c.TextBox(nameState)).Row(0).Column(1).FillWidth(),

    g.Cell(c => c.Text("Email:")).Row(1).Column(0),
    g.Cell(c => c.TextBox(emailState)).Row(1).Column(1),

    g.Cell(c => c.Text("Notes:")).Row(2).Column(0),
    g.Cell(c => c.TextBox(notesState)).Row(2).Column(1),
])
```

## Related Widgets

- [VStackWidget](/guide/widgets/vstack) — For simple vertical layouts
- [HStackWidget](/guide/widgets/hstack) — For simple horizontal layouts
- [BorderWidget](/guide/widgets/border) — For adding borders to grid cells
- [Splitter](/guide/widgets/splitter) — For resizable split views
