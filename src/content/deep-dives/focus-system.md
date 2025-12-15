# Focus System

The focus system determines which widget receives keyboard input. Understanding focus is essential for building keyboard-navigable interfaces.

## Focus Basics

Only certain widgets are **focusable**:
- `ButtonNode`
- `TextBoxNode`
- `ListNode`

Container widgets like `VStackNode` and `BorderNode` are not directly focusable but participate in focus routing.

## Focus Navigation

| Key | Action |
|-----|--------|
| `Tab` | Move to next focusable widget |
| `Shift+Tab` | Move to previous focusable widget |
| `Escape` | Move focus to parent container |
| Arrow keys | Widget-specific (list navigation, etc.) |

## How Focus Works

### Focus State

Each node tracks its focus state:

```csharp
public class ButtonNode : Hex1bNode
{
    public bool IsFocused { get; private set; }
    
    public override void OnFocusGained()
    {
        IsFocused = true;
        // Trigger visual update
    }
    
    public override void OnFocusLost()
    {
        IsFocused = false;
    }
}
```

### Focus Traversal

The focus manager maintains a traversal order:

```
VStack
├─ TextBlock      (not focusable)
├─ TextBox ←──────── Focus #1
├─ HStack
│  ├─ Button ←────── Focus #2  
│  └─ Button ←────── Focus #3
└─ List ←─────────── Focus #4
```

Pressing `Tab` cycles: #1 → #2 → #3 → #4 → #1 → ...

### Focus Scope

Containers can create focus scopes:

```csharp
new BorderWidget(
    new VStackWidget([
        new ButtonWidget("A"),  // Focus scope 1
        new ButtonWidget("B")
    ])
).FocusScope()  // Tab cycles within this border

new BorderWidget(
    new VStackWidget([
        new ButtonWidget("X"),  // Focus scope 2
        new ButtonWidget("Y")
    ])
).FocusScope()
```

`Escape` moves focus up to the containing scope.

## Input Routing

When a key is pressed:

```
1. InputRouter receives key event
       ↓
2. Find focused node in tree
       ↓
3. Check input bindings on focused node
       ↓
4. Call HandleInput on focused node
       ↓
5. If Unhandled, bubble to parent
       ↓
6. If still Unhandled, check global bindings
```

### Example Flow

```csharp
// User presses 'x' while button is focused
var result = focusedButton.HandleInput(keyEvent);  // Unhandled
result = parentVStack.HandleInput(keyEvent);       // Unhandled
result = rootBorder.HandleInput(keyEvent);         // Handled (has binding)
```

## Focus Restoration

When the widget tree changes, Hex1b tries to preserve focus:

```
Before                      After
VStack                      VStack
├─ Button "Save" ← focused  ├─ TextBlock "Saved!"  (new)
└─ Button "Cancel"          ├─ Button "Save" ← focus restored
                            └─ Button "Cancel"
```

The reconciler matches by type and position, preserving `IsFocused` state.

## Programmatic Focus

Set initial focus:

```csharp
new TextBoxWidget(value, onChange)
    .Autofocus()  // Focused on first render
```

Focus after an action:

```csharp
new ButtonWidget("Add Item", () => {
    AddItem();
    ctx.Focus("new-item-textbox");  // Focus by ID
})

new TextBoxWidget(value, onChange)
    .Id("new-item-textbox")
```

## Focus and Lists

Lists have internal focus (selection):

```csharp
new ListWidget(
    items: items.Select(RenderItem).ToArray(),
    selectedIndex: state.SelectedIndex,
    onSelect: index => ctx.SetState(state with { SelectedIndex = index })
)
```

Arrow keys change selection within the list:
- `Up` / `Down` - Move selection
- `Enter` - Activate selected item
- `Tab` - Move focus to next widget (out of list)

## Focus Events

Widgets receive focus lifecycle events:

```csharp
public class TextBoxNode : Hex1bNode
{
    private int _cursorPosition;
    
    public override void OnFocusGained()
    {
        IsFocused = true;
        StartCursorBlink();
    }
    
    public override void OnFocusLost()
    {
        IsFocused = false;
        StopCursorBlink();
        // Optionally commit changes
    }
}
```

## Debugging Focus

Enable focus debugging:

```csharp
var options = new Hex1bAppOptions
{
    DebugFocus = true
};
```

Output:
```
[Focus] Tab pressed
[Focus] Current: ButtonNode#2 "Save"
[Focus] Next: ButtonNode#3 "Cancel"
[Focus] FocusLost: ButtonNode#2
[Focus] FocusGained: ButtonNode#3
```

## Related

- [Input Handling](/guide/input) - Keyboard bindings
- [Render Loop](/deep-dives/render-loop) - Where focus fits in the loop
