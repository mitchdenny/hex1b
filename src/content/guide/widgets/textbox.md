# TextBox Widget

An editable single-line text input.

## Basic Usage

```csharp
new TextBoxWidget(
    value: state.Name,
    onChange: text => ctx.SetState(state with { Name = text })
)
```

## With Placeholder

```csharp
new TextBoxWidget(
    value: state.Search,
    onChange: text => ctx.SetState(state with { Search = text }),
    placeholder: "Search..."
)
```

## Keyboard Support

| Key | Action |
|-----|--------|
| Characters | Insert at cursor |
| `Backspace` | Delete before cursor |
| `Delete` | Delete at cursor |
| `Left` / `Right` | Move cursor |
| `Home` | Move to start |
| `End` | Move to end |
| `Ctrl+A` | Select all |

## Styling

```csharp
new TextBoxWidget(value, onChange)
    .Width(30)
    .Placeholder("Enter text...")
```

## Live Demo

<TerminalDemo exhibit="text-input" title="TextBox Demo" />
