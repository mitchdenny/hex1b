# List Widget

A scrollable, selectable list of items.

## Basic Usage

```csharp
new ListWidget(
    items: ["Apple", "Banana", "Cherry"],
    onSelect: index => Console.WriteLine($"Selected: {index}")
)
```

## With Custom Rendering

```csharp
new ListWidget(
    items: todos.Select(todo => 
        new HStackWidget([
            new TextBlockWidget(todo.IsComplete ? "☑" : "☐"),
            new TextBlockWidget(todo.Title)
        ])
    ).ToArray(),
    selectedIndex: state.SelectedIndex,
    onSelect: i => ctx.SetState(state with { SelectedIndex = i })
)
```

## Keyboard Navigation

| Key | Action |
|-----|--------|
| `Up` | Previous item |
| `Down` | Next item |
| `Home` | First item |
| `End` | Last item |
| `PageUp` | Jump up |
| `PageDown` | Jump down |
| `Enter` | Select current item |

## Sizing

```csharp
// Fixed height list
new ListWidget(items, onSelect).Height(10)

// Fill available space
new ListWidget(items, onSelect).Fill()
```

## Live Demo

<TerminalDemo exhibit="responsive-todo" title="List Demo" />
