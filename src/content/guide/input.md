# Input Handling

Hex1b provides a comprehensive input system with focus management, keyboard routing, and input bindings.

## Focus System

Focusable widgets (TextBox, Button, List) can receive keyboard input. Focus is managed automatically:

| Key | Action |
|-----|--------|
| `Tab` | Move to next focusable widget |
| `Shift+Tab` | Move to previous focusable widget |
| `Escape` | Move focus up to parent container |
| `Enter` / `Space` | Activate focused widget |

## Input Routing

Input flows through a router:

```
Terminal Input → InputRouter → Focused Node → Handle or Bubble
```

Each node can handle input and return:
- `InputResult.Handled` - Input was consumed
- `InputResult.Unhandled` - Pass to parent

## Input Bindings

Add custom keyboard shortcuts to any widget:

```csharp
new ButtonWidget("Save", () => Save())
    .OnKey(Hex1bKey.S, Hex1bModifiers.Control, () => Save())
    .OnKey(Hex1bKey.Escape, () => Cancel())
```

### Available Keys

```csharp
// Special keys
Hex1bKey.Enter, Hex1bKey.Escape, Hex1bKey.Tab, Hex1bKey.Backspace
Hex1bKey.Up, Hex1bKey.Down, Hex1bKey.Left, Hex1bKey.Right
Hex1bKey.Home, Hex1bKey.End, Hex1bKey.PageUp, Hex1bKey.PageDown
Hex1bKey.Insert, Hex1bKey.Delete
Hex1bKey.F1 through Hex1bKey.F12

// Letter keys (for shortcuts)
Hex1bKey.A through Hex1bKey.Z
```

### Modifiers

```csharp
Hex1bModifiers.None
Hex1bModifiers.Control
Hex1bModifiers.Alt
Hex1bModifiers.Shift
Hex1bModifiers.Control | Hex1bModifiers.Shift  // Combine with |
```

## Handling Input in Custom Nodes

Override `HandleInput` in your node:

```csharp
public class MyNode : Hex1bNode
{
    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        if (keyEvent.Key == Hex1bKey.Enter)
        {
            DoSomething();
            return InputResult.Handled;
        }
        
        return InputResult.Unhandled;
    }
}
```

## Focus Events

Nodes receive focus notifications:

```csharp
public class MyNode : Hex1bNode
{
    public bool IsFocused { get; set; }
    
    public override void OnFocusGained()
    {
        IsFocused = true;
        // Start cursor blink, highlight, etc.
    }
    
    public override void OnFocusLost()
    {
        IsFocused = false;
        // Stop cursor blink, remove highlight
    }
}
```

## Global Key Bindings

Handle keys at the app level:

```csharp
var app = new Hex1bApp<AppState>(
    initialState,
    buildWidget: (ctx, ct) => 
        new BorderWidget(content)
            .OnKey(Hex1bKey.Q, Hex1bModifiers.Control, () => Environment.Exit(0))
            .OnKey(Hex1bKey.F5, () => ctx.SetState(RefreshData(ctx.State)))
);
```

## Text Input

`TextBoxWidget` handles text input automatically:

```csharp
new TextBoxWidget(
    value: state.SearchText,
    onChange: text => ctx.SetState(state with { SearchText = text }),
    placeholder: "Search..."
)
```

Supported editing:
- Character input
- Backspace / Delete
- Home / End
- Ctrl+A (select all)
- Left / Right arrows

## Live Demo

Try the input handling:

<TerminalDemo exhibit="text-input" title="Input Demo" />

## Next Steps

- [Theming](/guide/theming) - Customize colors and styles
- [Focus System Deep Dive](/deep-dives/focus-system) - How focus really works
