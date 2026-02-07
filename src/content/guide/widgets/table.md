<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode      → src/Hex1b.Website/Examples/TableBasicExample.cs
  - selectionCode  → src/Hex1b.Website/Examples/TableSelectionExample.cs
  - focusCode      → src/Hex1b.Website/Examples/TableFocusExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;

// Sample product data
var products = new List<Product>
{
    new("Widget Pro", "Electronics", 299.99m, 42),
    new("Gadget X", "Electronics", 149.50m, 128),
    new("Tool Kit", "Hardware", 89.00m, 56),
    new("Cable Pack", "Accessories", 24.99m, 200),
    new("Power Bank", "Electronics", 79.99m, 85)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.Table(products)
            .Header(h => [
                h.Cell("Product").Width(SizeHint.Fill),
                h.Cell("Category").Width(SizeHint.Content),
                h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right),
                h.Cell("Stock").Width(SizeHint.Fixed(8)).Align(Alignment.Right)
            ])
            .Row((r, product, state) => [
                r.Cell(product.Name),
                r.Cell(product.Category),
                r.Cell($"\${product.Price:F2}"),
                r.Cell(product.Stock.ToString())
            ])
    ], title: "Product Inventory"))
    .Build();

await terminal.RunAsync();

record Product(string Name, string Category, decimal Price, int Stock);`

const selectionCode = `using Hex1b;

// Sample data with selection state
var items = new List<SelectableItem>
{
    new("Task 1", false),
    new("Task 2", true),
    new("Task 3", false),
    new("Task 4", false),
    new("Task 5", true)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Table(items)
                .Header(h => [
                    h.Cell("Task").Width(SizeHint.Fill),
                    h.Cell("Status").Width(SizeHint.Fixed(12))
                ])
                .Row((r, item, state) => [
                    r.Cell(item.Name),
                    r.Cell(item.IsComplete ? "✓ Done" : "Pending")
                ])
                .SelectionColumn(
                    item => item.IsComplete,
                    (item, selected) => item.IsComplete = selected
                )
                .OnSelectAll(() => items.ForEach(i => i.IsComplete = true))
                .OnDeselectAll(() => items.ForEach(i => i.IsComplete = false)),
            v.Text(""),
            v.Text($"Completed: {items.Count(i => i.IsComplete)} / {items.Count}")
        ])
    ], title: "Task List with Selection"))
    .Build();

await terminal.RunAsync();

class SelectableItem(string name, bool isComplete)
{
    public string Name { get; } = name;
    public bool IsComplete { get; set; } = isComplete;
}`

const focusCode = `using Hex1b;

object? focusedKey = null;
Product? focusedProduct = null;

var products = new List<Product>
{
    new("Widget Pro", "High-end widget with premium features", 299.99m),
    new("Gadget X", "Compact gadget for everyday use", 149.50m),
    new("Tool Kit", "Complete toolkit for professionals", 89.00m),
    new("Cable Pack", "Assorted cables and adapters", 24.99m),
    new("Power Bank", "Portable 20000mAh battery pack", 79.99m)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Border(b => [
            b.Table(products)
                .RowKey(p => p.Name)
                .Header(h => [
                    h.Cell("Product").Width(SizeHint.Fill),
                    h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right)
                ])
                .Row((r, product, state) => [
                    r.Cell(product.Name),
                    r.Cell($"\${product.Price:F2}")
                ])
                .Focus(focusedKey)
                .OnFocusChanged(key =>
                {
                    focusedKey = key;
                    focusedProduct = products.FirstOrDefault(p => p.Name.Equals(key));
                })
        ], title: "Products"),
        v.Text(""),
        v.Border(b => [
            b.Text(focusedProduct?.Description ?? "Select a product to see details")
        ], title: "Details")
    ]))
    .Build();

await terminal.RunAsync();

record Product(string Name, string Description, decimal Price);`

const renderModesCode = `using Hex1b;

var data = new[] { ("Alice", 28), ("Bob", 34), ("Charlie", 22) };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.HStack(h => [
        h.Border(b => [
            b.Table(data)
                .Header(hdr => [hdr.Cell("Name"), hdr.Cell("Age")])
                .Row((r, item, _) => [r.Cell(item.Item1), r.Cell(item.Item2.ToString())])
                .Compact()  // No separators between rows (default)
        ], title: "Compact Mode"),
        h.Border(b => [
            b.Table(data)
                .Header(hdr => [hdr.Cell("Name"), hdr.Cell("Age")])
                .Row((r, item, _) => [r.Cell(item.Item1), r.Cell(item.Item2.ToString())])
                .Full()  // Horizontal separators between rows
        ], title: "Full Mode")
    ]))
    .Build();

await terminal.RunAsync();`
</script>

# TableWidget

Display tabular data with columns, headers, row navigation, and optional selection.

TableWidget is a powerful widget for presenting structured data in rows and columns. It supports keyboard navigation, row focus tracking, multi-select with checkboxes, virtualization for large datasets, and theming.

## Basic Usage

Create a table by defining header cells and row cells using the fluent API:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="table-basic" exampleTitle="Table Widget - Basic Usage" />

The table requires:
- **Data source**: A list of objects to display
- **Header**: Column definitions with names and sizing
- **Row builder**: A function that creates cells for each data row

::: tip Navigation
Use **Up/Down arrows** to navigate between rows. Press **Tab** to move focus to other widgets.
:::

## Column Sizing

Control column widths using the `Width()` method on header cells:

| SizeHint | Description |
|----------|-------------|
| `SizeHint.Fill` | Column expands to fill available space |
| `SizeHint.Content` | Column fits its content |
| `SizeHint.Fixed(n)` | Column has fixed width of `n` characters |

```csharp
.Header(h => [
    h.Cell("Name").Width(SizeHint.Fill),           // Expands to fill
    h.Cell("Category").Width(SizeHint.Content),   // Fits content
    h.Cell("Price").Width(SizeHint.Fixed(10))     // Fixed 10 chars
])
```

## Cell Alignment

Align cell content using the `Align()` method:

```csharp
h.Cell("Price").Width(SizeHint.Fixed(10)).Align(Alignment.Right)
h.Cell("Status").Align(Alignment.Center)
```

## Focus and Navigation

Track which row has keyboard focus using `RowKey()`, `Focus()`, and `OnFocusChanged()`:

<CodeBlock lang="csharp" :code="focusCode" command="dotnet run" example="table-focus" exampleTitle="Table Widget - Focus Tracking" />

### Key Concepts

- **RowKey**: A function that returns a unique identifier for each row. Required for stable focus tracking across data changes.
- **Focus**: The currently focused row key. Pass `null` for no focus.
- **OnFocusChanged**: Called when the user navigates to a different row.

::: warning Row Keys Are Important
Without `RowKey()`, the table uses row indices which can cause focus to jump unexpectedly when data changes. Always provide a row key for dynamic data.
:::

## Selection Column

Add a checkbox column for multi-select functionality:

<CodeBlock lang="csharp" :code="selectionCode" command="dotnet run" example="table-selection" exampleTitle="Table Widget - Selection Column" />

### Selection API

```csharp
.SelectionColumn()  // Basic selection (checkbox only)

.SelectionColumn(
    item => item.IsSelected,           // Read selection state
    (item, selected) => item.IsSelected = selected  // Write selection state
)

.OnSelectAll(() => { /* Select all items */ })
.OnDeselectAll(() => { /* Deselect all items */ })
```

The header checkbox shows:
- `[ ]` when no items are selected
- `[-]` when some items are selected
- `[x]` when all items are selected

::: tip Keyboard Shortcuts
Press **Space** to toggle selection on the focused row. Press **Space** on the header to select/deselect all.
:::

## Render Modes

Tables support two render modes:

```csharp
.Compact()  // No horizontal separators (default)
.Full()     // Horizontal separators between rows
```

**Compact mode** is more space-efficient and suitable for dense data. **Full mode** provides better visual separation for complex tables.

## Row Activation

Handle double-click or Enter key on rows:

```csharp
.OnRowActivated((key, item) => {
    Console.WriteLine($"Activated: {item.Name}");
})

// Async version
.OnRowActivated(async (key, item) => {
    await OpenDetailsAsync(item);
})
```

## Footer Row

Add a footer row for summaries or actions:

```csharp
.Footer(f => [
    f.Cell($"Total: {products.Count} items"),
    f.Cell(""),
    f.Cell($"${products.Sum(p => p.Price):F2}").Align(Alignment.Right),
    f.Cell("")
])
```

## Empty State

Customize the display when data is empty:

```csharp
.Empty(ctx => ctx.VStack(v => [
    v.Text("No items found"),
    v.Button("Add Item").OnClick(_ => AddItem())
]))
```

## Virtualization

Tables automatically virtualize large datasets, rendering only visible rows. This enables smooth scrolling through thousands of items.

For async data sources with pagination, implement `ITableDataSource<T>`:

```csharp
public interface ITableDataSource<T>
{
    int TotalCount { get; }
    Task<IReadOnlyList<T>> GetItemsAsync(int start, int count, CancellationToken ct);
}
```

## Keyboard Navigation

| Key | Action |
|-----|--------|
| **Up Arrow** | Move to previous row |
| **Down Arrow** | Move to next row |
| **Page Up** | Move up one page |
| **Page Down** | Move down one page |
| **Home** | Move to first row |
| **End** | Move to last row |
| **Space** | Toggle selection (when selection column enabled) |
| **Enter** | Activate row (fires OnRowActivated) |
| **Tab** | Move focus to next widget |
| **Shift+Tab** | Move focus to previous widget |

## Theming

Customize table appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(TableTheme.BorderColor, Hex1bColor.DarkGray)
    .Set(TableTheme.TableFocusedBorderColor, Hex1bColor.Gray)
    .Set(TableTheme.FocusedRowBackground, Hex1bColor.FromRgb(50, 50, 50))
    .Set(TableTheme.HeaderForeground, Hex1bColor.Cyan);

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) =>
    {
        options.Theme = theme;
        return ctx => /* ... */;
    })
    .Build();
```

### Available Theme Elements

| Element | Type | Description |
|---------|------|-------------|
| `BorderColor` | `Hex1bColor` | Border color when unfocused |
| `TableFocusedBorderColor` | `Hex1bColor` | Border color when table has focus |
| `FocusedBorderColor` | `Hex1bColor` | Color of the thick focus indicator |
| `FocusedRowBackground` | `Hex1bColor` | Background of focused row |
| `HeaderForeground` | `Hex1bColor` | Header text color |
| `HeaderBackground` | `Hex1bColor` | Header background color |
| `CheckboxChecked` | `string` | Checked checkbox character (default: `[x]`) |
| `CheckboxUnchecked` | `string` | Unchecked checkbox character (default: `[ ]`) |
| `CheckboxIndeterminate` | `string` | Partial selection character (default: `[-]`) |
| `ShowFocusIndicator` | `bool` | Whether to show the thick focus bar |

## API Reference

### Configuration Methods

| Method | Description |
|--------|-------------|
| `Header(builder)` | Define column headers |
| `Row(builder)` | Define row cell content |
| `Footer(builder)` | Define footer cells |
| `Empty(builder)` | Define empty state widget |
| `RowKey(selector)` | Set row key selector for stable tracking |
| `Focus(key)` | Set the focused row by key |
| `SelectionColumn()` | Enable selection checkboxes |
| `SelectionColumn(getter, setter)` | Enable selection with view model binding |
| `Compact()` | Use compact render mode (default) |
| `Full()` | Use full render mode with separators |

### Event Handlers

| Method | Description |
|--------|-------------|
| `OnFocusChanged(handler)` | Called when focused row changes |
| `OnRowActivated(handler)` | Called when row is activated (Enter/double-click) |
| `OnSelectAll(handler)` | Called when header checkbox selects all |
| `OnDeselectAll(handler)` | Called when header checkbox deselects all |

### TableRowState Properties

The row builder receives a `TableRowState` object with these properties:

| Property | Type | Description |
|----------|------|-------------|
| `RowIndex` | `int` | Zero-based index of the row |
| `RowKey` | `object` | The row's unique key |
| `IsFocused` | `bool` | Whether this row has keyboard focus |
| `IsSelected` | `bool` | Whether this row is selected |
| `IsFirst` | `bool` | Whether this is the first row |
| `IsLast` | `bool` | Whether this is the last row |

## Related Widgets

- [ListWidget](/guide/widgets/list) - For simple string-based lists
- [ScrollPanelWidget](/guide/widgets/scroll) - For scrollable content
- [BorderWidget](/guide/widgets/border) - For framing tables
