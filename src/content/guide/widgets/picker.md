<!--
  MIRROR WARNING: The code samples below must stay in sync with their WebSocket example counterparts:
  - basicCode        → src/Hex1b.Website/Examples/PickerBasicExample.cs
  - selectionCode    → src/Hex1b.Website/Examples/PickerSelectionExample.cs
  - initialCode      → src/Hex1b.Website/Examples/PickerInitialExample.cs
  When updating code here, update the corresponding Example file and vice versa.
-->
<script setup>
const basicCode = `using Hex1b;
using Hex1b.Widgets;

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.VStack(v => [
            v.Text("Select a fruit:"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Fruit: "),
                h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
            ])
        ])
    ], title: "Fruit Picker")
);

await app.RunAsync();`

const selectionCode = `using Hex1b;
using Hex1b.Widgets;

var state = new PickerState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Selected fruit: {state.SelectedFruit}"),
            v.Text(""),
            v.HStack(h => [
                h.Text("Choose: "),
                h.Picker(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                    .OnSelectionChanged(e => state.SelectedFruit = e.SelectedText)
            ])
        ])
    ], title: "Selection Demo")
);

await app.RunAsync();

class PickerState
{
    public string SelectedFruit { get; set; } = "Apple";
}`

const initialCode = `using Hex1b;
using Hex1b.Widgets;

var state = new FormState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.VStack(v => [
            v.HStack(h => [
                h.Text("Size:     "),
                h.Picker(["Small", "Medium", "Large", "X-Large"], initialSelectedIndex: 1)
                    .OnSelectionChanged(e => state.Size = e.SelectedText)
            ]),
            v.HStack(h => [
                h.Text("Priority: "),
                h.Picker(["Low", "Medium", "High", "Critical"], initialSelectedIndex: 2)
                    .OnSelectionChanged(e => state.Priority = e.SelectedText)
            ]),
            v.Text(""),
            v.Text($"Order: {state.Size} priority {state.Priority}")
        ])
    ], title: "Order Form")
);

await app.RunAsync();

class FormState
{
    public string Size { get; set; } = "Medium";
    public string Priority { get; set; } = "High";
}`

const themingCode = `using Hex1b;
using Hex1b.Widgets;
using Hex1b.Theming;

var state = new ThemeState();

var app = new Hex1bApp(ctx =>
    ctx.ThemePanel(
        theme => theme
            .Set(ButtonTheme.ForegroundColor, Hex1bColor.Yellow)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Cyan)
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(BorderTheme.BorderColor, Hex1bColor.Magenta),
        t => [
            t.Border(b => [
                b.VStack(v => [
                    v.Text("Themed Picker:"),
                    v.Text(""),
                    v.HStack(h => [
                        h.Text("Color: "),
                        h.Picker(["Red", "Green", "Blue", "Yellow", "Cyan"])
                            .OnSelectionChanged(e => state.Color = e.SelectedText)
                    ]),
                    v.Text(""),
                    v.Text($"Selected: {state.Color}")
                ])
            ], title: "Theme Demo")
        ]
    )
);

await app.RunAsync();

class ThemeState
{
    public string Color { get; set; } = "Red";
}`
</script>

# Picker

A dropdown picker widget that displays a selected value and opens a popup list when activated.

Pickers are ideal for selecting from a list of predefined options where displaying all options at once would take too much space. They're commonly used in forms for selecting categories, priorities, modes, or any enumerated value.

## Basic Usage

Create a picker using the fluent API with an array of strings:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="picker-basic" exampleTitle="Picker Widget - Basic Usage" />

The picker displays as a button showing the current selection with a dropdown indicator (`▼`). When focused and activated (Enter, Space, or click), a bordered popup appears with all available options.

::: tip Navigation
- **Enter** or **Space** - Open the picker popup
- **Up/Down arrows** - Navigate items in the popup
- **Enter** - Select the highlighted item and close
- **Escape** - Close the popup without changing selection
:::

## Selection Changed Events

The `OnSelectionChanged` event fires when the user selects a different item from the popup:

<CodeBlock lang="csharp" :code="selectionCode" command="dotnet run" example="picker-selection" exampleTitle="Picker Widget - Selection Changed" />

The `PickerSelectionChangedEventArgs` provides:
- `SelectedIndex` - The index of the newly selected item
- `SelectedText` - The text of the newly selected item  
- `Widget` - The source PickerWidget
- `Node` - The underlying PickerNode
- `Context` - Access to the application context

::: warning Selection Only Changes on Confirmation
Unlike List widgets where `OnSelectionChanged` fires during navigation, Picker's `OnSelectionChanged` only fires when the user confirms a selection by pressing Enter or clicking an item. Pressing Escape cancels without firing the event.
:::

## Initial Selection

By default, pickers select the first item (index 0). Use the `initialSelectedIndex` parameter to start with a different selection:

<CodeBlock lang="csharp" :code="initialCode" command="dotnet run" example="picker-initial" exampleTitle="Picker Widget - Initial Selection" />

The initial selection is clamped to valid bounds—if you specify an index beyond the item count, the last item is selected.

## Keyboard and Mouse Navigation

Pickers support comprehensive interaction:

### Button State (Picker Closed)

| Input | Action |
|-------|--------|
| **Enter** | Open the picker popup |
| **Space** | Open the picker popup |
| **Click** | Open the picker popup |
| **Tab** | Move focus to next widget |
| **Shift+Tab** | Move focus to previous widget |

### Popup State (Picker Open)

| Input | Action |
|-------|--------|
| **Up Arrow** | Move to previous item |
| **Down Arrow** | Move to next item |
| **Enter** | Select current item and close |
| **Click** | Select clicked item and close |
| **Escape** | Close without changing selection |

::: tip Mouse Wheel
In terminals with mouse support, the mouse wheel scrolls through items in the popup when open.
:::

## Theming

Customize picker appearance using theme elements. Since Picker is a composite widget made of Button and List, you can theme both components:

```csharp
var theme = Hex1bTheme.Create()
    // Button appearance (the picker display)
    .Set(ButtonTheme.ForegroundColor, Hex1bColor.Yellow)
    .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.Yellow)
    .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
    // List appearance (the popup)
    .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.Cyan)
    .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
    // Border appearance (around the popup)
    .Set(BorderTheme.BorderColor, Hex1bColor.Magenta);

var app = new Hex1bApp(options => {
    options.Theme = theme;
}, ctx => /* ... */);
```

### Picker-Specific Theme Elements

| Element | Type | Default | Description |
|---------|------|---------|-------------|
| `ForegroundColor` | `Hex1bColor` | Default | Text color for the button |
| `BackgroundColor` | `Hex1bColor` | Default | Background for the button |
| `FocusedForegroundColor` | `Hex1bColor` | Black | Text when focused |
| `FocusedBackgroundColor` | `Hex1bColor` | White | Background when focused |
| `LeftBracket` | `string` | `"[ "` | Left bracket text |
| `RightBracket` | `string` | `" ▼]"` | Right bracket with indicator |
| `MinimumWidth` | `int` | `10` | Minimum button width |

::: tip Theme Propagation
When a picker is inside a `ThemePanel`, the theme automatically propagates to the popup. This ensures consistent styling even though the popup renders as a separate layer.
:::

## State Management

Picker selection state is owned by the underlying `PickerNode` and preserved across reconciliation. This means:

- The selection persists when the widget tree rebuilds
- You don't need to manage selection state yourself unless you want to track it externally
- The `OnSelectionChanged` callback lets you synchronize external state when needed

When the items list changes:
- If the selected index is beyond the new list length, it's clamped to the last item
- If the list becomes empty, the selection resets to index 0

## Popup Behavior

The picker popup is rendered as an anchored popup positioned below the picker button:

- The popup appears immediately when the picker is activated
- It's automatically dismissed when an item is selected or Escape is pressed
- The popup list matches the button's width for visual consistency
- Long lists scroll automatically within the popup

::: warning Modal Behavior
While the popup is open, focus is trapped within it. The user must select an item or press Escape to return to the main UI.
:::

## Composite Widget Pattern

Picker is implemented as a **composite widget**, meaning it's built from other Hex1b widgets (Button and List) rather than rendering directly. This provides:

- **Consistent behavior**: Uses the same Button and List that you'd use directly
- **Theme integration**: Button and List theme elements apply naturally
- **Focus management**: Focus flows correctly between button and popup list

For developers interested in creating custom composite widgets, see the [Composite Widgets](/guide/composite-widgets) guide.

## Related Widgets

- [ButtonWidget](/guide/widgets/button) - For single actions without selection
- [ListWidget](/guide/widgets/list) - For always-visible selection lists
- [ToggleSwitchWidget](/guide/widgets/toggle-switch) - For binary or small-set choices
- [TextBoxWidget](/guide/widgets/textbox) - For free-form text input
