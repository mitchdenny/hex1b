<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode     → src/Hex1b.Website/Examples/ListBasicExample.cs
  - selectionCode → src/Hex1b.Website/Examples/ListSelectionExample.cs
  - activateCode  → src/Hex1b.Website/Examples/ListActivateExample.cs
  - longListCode  → src/Hex1b.Website/Examples/ListLongExample.cs
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

## Related Widgets

- [ButtonWidget](/guide/widgets/button) - For single-action controls
- [TextBoxWidget](/guide/widgets/textbox) - For text input with selection
- [ScrollWidget](/guide/widgets/scroll) - For scrollable content areas
- [VStackWidget](/guide/widgets/stacks) - For vertical layout of widgets
