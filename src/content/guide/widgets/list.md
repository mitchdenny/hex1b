<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode     → src/Hex1b.Website/Examples/ListBasicExample.cs
  - selectionCode → src/Hex1b.Website/Examples/ListSelectionExample.cs
  - activateCode  → src/Hex1b.Website/Examples/ListActivateExample.cs
  - longListCode  → src/Hex1b.Website/Examples/ListLongExample.cs
  - templateCode  → src/Hex1b.Website/Examples/ListItemTemplateExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
import focusSnippet from './snippets/list-focus.cs?raw'

const basicCode = `using Hex1b;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Select a fruit:"),
            v.Text(""),
            v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
        ])
    ], title: "Fruit List"))
    .Build();

await terminal.RunAsync();`

const selectionCode = `using Hex1b;

var state = new ListSelectionState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Selected: {state.SelectedItem ?? "None"}"),
            v.Text(""),
            v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                .OnSelectionChanged(e => state.SelectedItem = e.SelectedText)
        ])
    ], title: "Selection Demo"))
    .Build();

await terminal.RunAsync();

class ListSelectionState
{
    public string? SelectedItem { get; set; }
}`

const activateCode = `using Hex1b;

var state = new TodoState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Press Enter or Space to toggle items:"),
            v.Text(""),
            v.List(state.GetFormattedItems())
                .OnItemActivated(e => state.ToggleItem(e.ActivatedIndex))
        ])
    ], title: "Todo List"))
    .Build();

await terminal.RunAsync();

class TodoState
{
    private readonly List<(string Text, bool Done)> _items =
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false),
        ("Deploy to production", false)
    ];

    public IReadOnlyList<string> GetFormattedItems() =>
        _items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void ToggleItem(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            var item = _items[index];
            _items[index] = (item.Text, !item.Done);
        }
    }
}`

const longListCode = `using Hex1b;

// Generate a list of 50 countries
var countries = new List<string>
{
    "Argentina", "Australia", "Austria", "Belgium", "Brazil",
    "Canada", "Chile", "China", "Colombia", "Czech Republic",
    "Denmark", "Egypt", "Finland", "France", "Germany",
    "Greece", "Hungary", "India", "Indonesia", "Ireland",
    "Israel", "Italy", "Japan", "Kenya", "Malaysia",
    "Mexico", "Netherlands", "New Zealand", "Nigeria", "Norway",
    "Pakistan", "Peru", "Philippines", "Poland", "Portugal",
    "Romania", "Russia", "Saudi Arabia", "Singapore", "South Africa",
    "South Korea", "Spain", "Sweden", "Switzerland", "Thailand",
    "Turkey", "Ukraine", "United Kingdom", "United States", "Vietnam"
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text("Select a country (scroll with arrow keys or mouse wheel):"),
            v.Text(""),
            v.List(countries).FixedHeight(10)
        ])
    ], title: "Country Selector"))
    .Build();

await terminal.RunAsync();`

const templateCode = `using Hex1b;

var countries = new[]
{
    new Country("Australia", "Canberra", "🇦🇺"),
    new Country("Brazil", "Brasilia", "🇧🇷"),
    new Country("Japan", "Tokyo", "🇯🇵"),
    new Country("Norway", "Oslo", "🇳🇴"),
    new Country("Portugal", "Lisbon", "🇵🇹"),
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.TypedList(countries)
            .ItemHeight(2)
            .ItemKey(c => c.Name)
            .ItemTemplate(context =>
            {
                var prefix = context.IsSelected ? "▶ " : "  ";
                return context.VStack(v => [
                    v.Text($"{prefix}{context.Item.Flag}  {context.Item.Name}"),
                    v.Text($"     {context.Item.Capital}")
                ]);
            })
    ], title: "Pick a Country"))
    .Build();

await terminal.RunAsync();

record Country(string Name, string Capital, string Flag);`
</script>

# ListWidget

A selectable list of text items with keyboard and mouse navigation.

Lists are interactive, focusable widgets that allow users to navigate through items and select or activate them. They're ideal for menus, file browsers, todo lists, and any UI requiring item selection.

## Basic Usage

Create a list using the fluent API with an array of strings:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="list-basic" exampleTitle="List Widget - Basic Usage" />

::: tip Navigation
Use **Up/Down arrows** to move between items. The selected item is highlighted when the list has focus. Press **Tab** to move focus to other widgets.
:::

## Selection Changed Events

The `OnSelectionChanged` event fires whenever the user navigates to a different item:

<CodeBlock lang="csharp" :code="selectionCode" command="dotnet run" example="list-selection" exampleTitle="List Widget - Selection Changed" />

The `ListSelectionChangedEventArgs` provides:
- `SelectedIndex` - The index of the newly selected item
- `SelectedText` - The text of the newly selected item
- `Widget` - The source ListWidget
- `Node` - The underlying ListNode
- `Context` - Access to the application context

::: warning Selection vs Activation
`OnSelectionChanged` fires when navigating with arrow keys. Use `OnItemActivated` (shown below) to respond to Enter/Space/Click actions.
:::

## Item Activated Events

The `OnItemActivated` event fires when the user activates an item with Enter, Space, or mouse click:

<CodeBlock lang="csharp" :code="activateCode" command="dotnet run" example="list-activate" exampleTitle="List Widget - Item Activation" />

The `ListItemActivatedEventArgs` provides:
- `ActivatedIndex` - The index of the activated item
- `ActivatedText` - The text of the activated item
- `Widget` - The source ListWidget
- `Node` - The underlying ListNode
- `Context` - Access to the application context

::: tip Common Pattern
Use `OnItemActivated` for actions like toggling checkboxes, opening details, or navigating to a new screen. Use `OnSelectionChanged` for updating dependent UI like preview panes.
:::

## Event Handlers

Both `OnSelectionChanged` and `OnItemActivated` accept synchronous and asynchronous handlers:

### Synchronous Handler
```csharp
v.List(items).OnItemActivated(e => {
    Console.WriteLine($"Activated: {e.ActivatedText}");
})
```

### Asynchronous Handler
```csharp
v.List(items).OnItemActivated(async e => {
    await SaveSelectionAsync(e.ActivatedIndex);
})
```

::: warning Render Loop Blocking
Async handlers block the render loop while awaiting. For long operations, use the background work pattern shown in the [ButtonWidget documentation](/guide/widgets/button#background-work-pattern).
:::

## Keyboard Navigation

Lists support comprehensive keyboard navigation:

| Key | Action |
|-----|--------|
| **Up Arrow** | Move to previous item (wraps to last) |
| **Down Arrow** | Move to next item (wraps to first) |
| **Enter** | Activate current item |
| **Space** | Activate current item |
| **Tab** | Move focus to next widget |
| **Shift+Tab** | Move focus to previous widget |

::: tip Wrap-Around Navigation
When reaching the first item, pressing Up wraps to the last item. Similarly, pressing Down at the last item wraps to the first.
:::

## Mouse Support

Lists support mouse interaction in terminals that support mouse events:

- **Left Click** - Selects the clicked item (fires `OnSelectionChanged` if selection changed) and then activates it (fires `OnItemActivated`)
- **Mouse Wheel Up** - Moves selection to the previous item
- **Mouse Wheel Down** - Moves selection to the next item

## Long Lists with Scrolling

When a list has more items than can fit in its container, it automatically becomes scrollable. The viewport scrolls to keep the selected item visible as you navigate:

<CodeBlock lang="csharp" :code="longListCode" command="dotnet run" example="list-long" exampleTitle="List Widget - Long List with Scrolling" />

Use the `.FixedHeight()` extension to constrain the list to a specific number of rows. The list will:

- Show only the visible items within the viewport
- Automatically scroll when navigating beyond visible bounds
- Keep the selected item centered when possible
- Support both keyboard and mouse wheel scrolling

::: tip Performance
Long lists are efficient because only visible items are rendered. You can safely use lists with hundreds of items without performance concerns.
:::

## Focus Behavior

Lists visually indicate their focus and selection state:

| State | Appearance |
|-------|------------|
| Unfocused, item selected | `> Item` (indicator only) |
| Focused, item selected | `> Item` (highlighted with theme colors) |
| Unfocused, item not selected | `  Item` (no indicator) |

<StaticTerminalPreview svgPath="/svg/list-focus.svg" :code="focusSnippet" />

When a list loses focus, the selected item still shows the selection indicator but without the highlighting. This helps users track which item was last selected.

## Theming

Customize list appearance using theme elements:

```csharp
var theme = Hex1bTheme.Create()
    .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
    .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Cyan)
    .Set(ListTheme.SelectedIndicator, "▶ ")
    .Set(ListTheme.UnselectedIndicator, "  ");

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
| `ForegroundColor` | `Hex1bColor` | Default | Text color for unselected items |
| `BackgroundColor` | `Hex1bColor` | Default | Background for unselected items |
| `SelectedForegroundColor` | `Hex1bColor` | White | Text color when focused and selected |
| `SelectedBackgroundColor` | `Hex1bColor` | Blue | Background when focused and selected |
| `SelectedIndicator` | `string` | `"> "` | Indicator for selected items |
| `UnselectedIndicator` | `string` | `"  "` | Indicator for unselected items |

::: tip Custom Indicators
Change the selection indicators to match your app's style. Common alternatives include `"→ "`, `"● "`, `"▸ "`, or emoji like `"👉 "`.
:::

## State Management

Lists preserve their selection state across reconciliation. The selected index is managed internally by the ListNode and persists even when the widget tree is rebuilt.

When the items list changes, the selection is automatically clamped to valid bounds:
- If the selected index is beyond the new list length, it moves to the last item
- If the list becomes empty, the selection resets to index 0

## Typed Lists and Item Templates

For Spectre-style selection prompts and other cases where each row needs a
custom multi-line layout, use `TypedList<T>` with `ItemTemplate`. The list
items can be any type (records, classes, primitives), and each row is rendered
as a widget tree returned by your template.

<CodeBlock lang="csharp" :code="templateCode" command="dotnet run" example="list-item-template" exampleTitle="List Widget - Item Template" />

`TypedList<T>(items)` returns a `TypedListWidget<T>` that supports the same
keyboard navigation, mouse handling, and selection model as `List`, plus:

| Method | Purpose |
|--------|---------|
| `.ItemTemplate(context => …)` | Custom per-row widget tree. The list draws no selector or background — the template owns all chrome. |
| `.ItemHeight(rows)` | Fixed row height in terminal rows. Required when the template is multi-line. Defaults to 1. |
| `.ItemKey(item => …)` | Stable key per item so template subtrees survive reorder/filter. Recommended whenever items can move. |
| `.InitialSelectedIndex(n)` | Index selected when the list is first created. |
| `.OnSelectionChanged(args => …)` | Fires when the user navigates to a different row. `args.SelectedItem` is typed as `T`. |
| `.OnItemActivated(args => …)` | Fires on Enter/Space/Click. `args.ActivatedItem` is typed as `T`. |

Inside the template callback, the `context` parameter exposes:

| Member | Description |
|--------|-------------|
| `Item` | The typed item value (`T`). |
| `Index` | The item's zero-based position. |
| `IsSelected` | `true` for the currently selected row. |
| `IsFocused` | `true` when the list itself has focus. |
| `IsHovered` | `true` for the row currently under the mouse cursor. |

Because `ListItemContext<T>` derives from `WidgetContext<TypedListWidget<T>>`,
you can call any widget extension (`context.Text(...)`, `context.VStack(...)`,
`context.Border(...)`, etc.) from inside the template.

::: tip Default rendering
If you don't set `ItemTemplate`, `TypedList<T>` renders each row as a single
line of `item?.ToString()` — visually identical to the basic `List` widget.
This means you can switch any `List(strings)` callsite to
`TypedList(myObjects)` without writing a template, and override
`object.ToString()` (or use `record` types) to control the row text.
:::

::: warning Focusable widgets inside templates
In the current release, interactive widgets placed inside an item template
(buttons, text boxes, nested lists) are rendered but do not receive focus —
the list itself is the only focusable surface. Use `OnItemActivated` to drive
per-row actions.
:::

## Related Widgets

- [ButtonWidget](/guide/widgets/button) - For single-action controls
- [TextBoxWidget](/guide/widgets/textbox) - For text input with selection
- [ScrollPanelWidget](/guide/widgets/scroll) - For scrollable content areas
- [VStackWidget](/guide/widgets/stacks) - For vertical layout of widgets
