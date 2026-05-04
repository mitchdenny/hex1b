# Input Handling

Hex1b provides a comprehensive input system with focus management, keyboard routing, and declarative input bindings. Every built-in widget exposes named actions that you can remap, extend, or disable — making it straightforward to implement custom keybinding schemes like Vim or Emacs without forking widget code.

::: tip Picking portable bindings
Different terminals intercept different combos before they reach Hex1b
(Windows Terminal eats `Ctrl+Shift+↑/↓` for scroll, GNOME Terminal eats
`Ctrl+Shift+T/N/W` for tab/window management, kitty owns the entire
`Ctrl+Shift+*` keyspace by default, and so on). See
**[Keybinding Portability](./keybinding-portability)** for the per-terminal
interception matrix and recommendations on choosing combos that work
across the terminals you target.
:::

## Focus System

Only focusable widgets — such as `TextBox`, `Button`, `List`, `Table`, `Editor`, `Tree`, `Checkbox`, `Slider`, and `ToggleSwitch` — can receive keyboard input. Hex1b manages focus order automatically through a **focus ring** that tracks all focusable widgets in the tree.

| Key | Action |
|-----|--------|
| `Tab` | Move focus to the next focusable widget |
| `Shift+Tab` | Move focus to the previous focusable widget |
| `Escape` | Move focus up to the parent container |
| `Enter` / `Space` | Activate the focused widget |

The focus ring is built from the widget tree's layout order. When a new widget becomes focusable, it is automatically inserted into the ring. Container widgets like `ScrollPanel` and `Accordion` participate in focus management, delegating focus to their children.

## Input Routing

Input flows through a well-defined pipeline:

```
Terminal Input → InputRouter → Focused Node → Handle or Bubble Up
```

1. The terminal captures raw keyboard and mouse events.
2. The `InputRouter` delivers the event to the currently focused node.
3. The focused node processes the event through its input bindings, then its `HandleInput` method.
4. If the node returns `InputResult.Unhandled`, the event **bubbles up** through parent nodes until it is handled or reaches the root.

This bubbling behavior lets you attach bindings at any level. A binding on a parent container will catch any unhandled keys from its children.

## Input Bindings

The `WithInputBindings` API lets you attach keyboard, mouse, and character bindings to any widget using a fluent builder:

```csharp
new ButtonWidget("Save")
    .WithInputBindings(b =>
    {
        b.Key(Hex1bKey.S).Ctrl().Action(() => Save(), "Save file");
        b.Key(Hex1bKey.Escape).Action(() => Cancel(), "Cancel");
    })
```

### Key Bindings with Modifiers

Chain modifier methods before `.Action()` or `.Triggers()`:

```csharp
b.Key(Hex1bKey.S).Ctrl().Action(() => Save(), "Save");            // Ctrl+S
b.Key(Hex1bKey.S).Ctrl().Shift().Action(() => SaveAs(), "Save as"); // Ctrl+Shift+S
b.Key(Hex1bKey.F1).Alt().Action(() => ShowHelp(), "Help");         // Alt+F1
```

> **Terminal reachability caveat (`Ctrl+Shift+letter`).** Most terminals
> cannot distinguish `Ctrl+Shift+A` from `Ctrl+A`: the `Ctrl` modifier
> strips ASCII bit 6 of a letter and `Shift` is dropped. Bindings on
> letter keys with the `Ctrl+Shift` combination will therefore not fire
> on most platforms unless the terminal opts into a richer key reporting
> mode (e.g. xterm's `modifyOtherKeys`). **Special keys** — arrows,
> function keys, `Home`/`End`/`PageUp`/`PageDown`, `Tab`, `Insert`,
> `Delete` — and **mouse buttons** carry an explicit modifier code in
> their CSI sequences and deliver `Ctrl+Shift` reliably across terminals.
> Run `samples/KeyBindingTester` to see which combinations your target
> terminal supports.

### Available Keys

```csharp
// Navigation
Hex1bKey.UpArrow, Hex1bKey.DownArrow, Hex1bKey.LeftArrow, Hex1bKey.RightArrow
Hex1bKey.Home, Hex1bKey.End, Hex1bKey.PageUp, Hex1bKey.PageDown

// Editing
Hex1bKey.Enter, Hex1bKey.Escape, Hex1bKey.Tab, Hex1bKey.Backspace
Hex1bKey.Spacebar, Hex1bKey.Insert, Hex1bKey.Delete

// Letters and digits
Hex1bKey.A through Hex1bKey.Z

// Function keys
Hex1bKey.F1 through Hex1bKey.F12
```

### Mouse Bindings

```csharp
b.Mouse(MouseButton.Left).Action(ctx => HandleClick(ctx), "Click");
b.Mouse(MouseButton.Left).DoubleClick().Action(ctx => SelectWord(ctx), "Double-click");
b.Mouse(MouseButton.ScrollUp).Action(ctx => ScrollUp(), "Scroll up");
b.Mouse(MouseButton.ScrollDown).Action(ctx => ScrollDown(), "Scroll down");
b.Drag(MouseButton.Left).Action(ctx => HandleDrag(ctx), "Drag");
```

### Character Bindings

Use `AnyCharacter` to capture arbitrary text input:

```csharp
b.AnyCharacter().Action(ctx => InsertCharacter(ctx.Character), "Type character");
```

### Multi-Key Chords

Chain `.Then()` to create multi-key sequences:

```csharp
b.Key(Hex1bKey.K).Ctrl().Then().Key(Hex1bKey.C).Action(() => CommentLine(), "Toggle comment");
b.Key(Hex1bKey.K).Ctrl().Then().Key(Hex1bKey.U).Action(() => UncommentLine(), "Uncomment");
```

### Global Bindings

Attach bindings to a non-focusable container to make them apply globally. Because unhandled input bubbles up, a binding on the root widget catches everything:

```csharp
ctx.Border(
    ctx.VStack([content])
)
.WithInputBindings(b =>
{
    b.Key(Hex1bKey.Q).Ctrl().Action(() => app.Quit(), "Quit");
    b.Key(Hex1bKey.F5).Action(() => Refresh(), "Refresh");
})
```

## Named Actions with ActionId

Every built-in widget exposes its bindable actions as `static readonly ActionId` fields. `ActionId` is a `readonly record struct` with a string value — strongly typed, no risk of typos.

The naming convention is `"WidgetNameWidget.ActionName"`:

```csharp
// Widget definition (library code):
public record ListWidget : Hex1bWidget
{
    public static readonly ActionId MoveUp = new($"{nameof(ListWidget)}.{nameof(MoveUp)}");
    public static readonly ActionId MoveDown = new($"{nameof(ListWidget)}.{nameof(MoveDown)}");
    public static readonly ActionId Activate = new($"{nameof(ListWidget)}.{nameof(Activate)}");
    // ...
}
```

Inside each widget's node, `ConfigureDefaultBindings` uses the `Triggers()` method to register the default key for each action:

```csharp
// Inside ListNode (library code):
protected override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
{
    bindings.Key(Hex1bKey.UpArrow).Triggers(ListWidget.MoveUp, MoveUpWithEvent, "Move up");
    bindings.Key(Hex1bKey.DownArrow).Triggers(ListWidget.MoveDown, MoveDownWithEvent, "Move down");
    bindings.Key(Hex1bKey.Enter).Triggers(ListWidget.Activate, ActivateItemWithEvent, "Activate item");
}
```

This separation — actions declared on widgets, defaults wired in nodes — is what makes the rebinding system work. You reference `ListWidget.MoveUp` to remap it; you never need to know what key it was originally bound to.

## Customizing Keybindings

The `WithInputBindings` API combined with `ActionId` gives you four patterns for customizing widget behavior.

### Remap — Change a Key

Remove the default binding and re-bind the same action to a new key:

```csharp
list.WithInputBindings(b =>
{
    b.Remove(ListWidget.MoveUp);
    b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
})
```

The `K` key now moves up. The original `UpArrow` binding is removed.

### Alias — Add an Extra Key

Add a new key without removing the default. Both keys will trigger the action:

```csharp
list.WithInputBindings(b =>
{
    b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp); // K and UpArrow both work
})
```

### Disable — Remove an Action

Remove a binding without replacing it:

```csharp
list.WithInputBindings(b =>
{
    b.Remove(ListWidget.Activate); // Enter no longer activates items
})
```

### Blank Slate — Start Fresh

Remove all default bindings and define your own from scratch:

```csharp
list.WithInputBindings(b =>
{
    b.RemoveAll();
    b.Key(Hex1bKey.K).Action(() => MoveUp(), "Custom up");
    b.Key(Hex1bKey.J).Action(() => MoveDown(), "Custom down");
})
```

## Centralized Overrides with InputOverride

`InputOverrideWidget` lets you apply keybinding changes to **all widgets of a given type** within a subtree — no need to attach `WithInputBindings` to every instance.

```csharp
ctx.InputOverride(
    ctx.VStack([list1, list2, textbox1])
)
.Override<ListWidget>(b =>
{
    b.Remove(ListWidget.MoveUp);
    b.Remove(ListWidget.MoveDown);
    b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
    b.Key(Hex1bKey.J).Triggers(ListWidget.MoveDown);
})
.Override<TextBoxWidget>(b =>
{
    b.Remove(TextBoxWidget.MoveUp);
    b.Key(Hex1bKey.K).Ctrl().Triggers(TextBoxWidget.MoveUp);
})
```

### Root-Level Override

Wrap your entire app content to apply overrides globally:

```csharp
ctx.InputOverride(appContent)
    .Override<ListWidget>(VimBindings.ApplyToList)
    .Override<TableWidget>(VimBindings.ApplyToTable);
```

### Scoped Overrides

Different sections of your UI can have different bindings:

```csharp
ctx.VStack([
    ctx.InputOverride(editorPanel)
        .Override<EditorWidget>(EmacsBindings.Apply),

    ctx.InputOverride(filePanel)
        .Override<ListWidget>(VimBindings.ApplyToList),
])
```

### Nesting Behavior

When `InputOverride` widgets are nested, the **innermost override wins** for a given widget type. This lets you set global defaults at the root and override them in specific regions.

### Interaction with Per-Instance Bindings

When both `WithInputBindings` (per-instance) and `InputOverride` (centralized) apply to the same widget, the per-instance bindings run **first**, then the override is applied. This means a per-instance binding takes priority for any specific action it configures.

## Walkthrough: Vim-Style Keybindings

Here's a complete example that sets up Vim-style navigation across your entire application using a reusable helper:

```csharp
public static class VimBindings
{
    public static void ApplyToList(InputBindingsBuilder b)
    {
        b.Remove(ListWidget.MoveUp);
        b.Remove(ListWidget.MoveDown);
        b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
        b.Key(Hex1bKey.J).Triggers(ListWidget.MoveDown);
    }

    public static void ApplyToTable(InputBindingsBuilder b)
    {
        b.Remove(TableWidget.MoveUp);
        b.Remove(TableWidget.MoveDown);
        b.Key(Hex1bKey.K).Triggers(TableWidget.MoveUp);
        b.Key(Hex1bKey.J).Triggers(TableWidget.MoveDown);
    }

    public static void ApplyToTree(InputBindingsBuilder b)
    {
        b.Remove(TreeWidget.MoveUpActionId);
        b.Remove(TreeWidget.MoveDownActionId);
        b.Remove(TreeWidget.ExpandOrChildActionId);
        b.Remove(TreeWidget.CollapseOrParentActionId);
        b.Key(Hex1bKey.K).Triggers(TreeWidget.MoveUpActionId);
        b.Key(Hex1bKey.J).Triggers(TreeWidget.MoveDownActionId);
        b.Key(Hex1bKey.L).Triggers(TreeWidget.ExpandOrChildActionId);
        b.Key(Hex1bKey.H).Triggers(TreeWidget.CollapseOrParentActionId);
    }
}

// Apply at the root of your app:
ctx.InputOverride(appContent)
    .Override<ListWidget>(VimBindings.ApplyToList)
    .Override<TableWidget>(VimBindings.ApplyToTable)
    .Override<TreeWidget>(VimBindings.ApplyToTree);
```

## Custom Input Handling

When building custom nodes, override `HandleInput` to process keyboard events directly:

```csharp
public class MyCustomNode : Hex1bNode
{
    public override InputResult HandleInput(Hex1bKeyEvent keyEvent)
    {
        if (keyEvent.Key == Hex1bKey.Enter)
        {
            DoSomething();
            return InputResult.Handled;
        }

        return InputResult.Unhandled; // Let the event bubble up
    }
}
```

Return `InputResult.Handled` to consume the event. Return `InputResult.Unhandled` to let it propagate to parent nodes.

## Focus Events

Nodes are notified when they gain or lose focus. Use these to update visual state:

```csharp
public class MyCustomNode : Hex1bNode
{
    public bool IsFocused { get; set; }

    public override void OnFocusGained()
    {
        IsFocused = true;
        // Start cursor blink, show highlight, etc.
    }

    public override void OnFocusLost()
    {
        IsFocused = false;
        // Stop cursor blink, remove highlight
    }
}
```

## Discovering Available Actions

You can inspect a widget's registered actions and their current bindings at runtime:

```csharp
widget.WithInputBindings(b =>
{
    var allActions = b.GetAllActionIds();    // All registered ActionIds
    var bindings = b.GetBindings(ListWidget.MoveUp); // Keys bound to MoveUp
});
```

This is useful for building keybinding configuration UIs or debugging which keys are mapped to which actions.

## Action ID Reference

Every built-in widget's named actions, their `ActionId` values, and default key bindings are listed below.

### List

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ListWidget.MoveUp` | `"ListWidget.MoveUp"` | ↑ |
| `ListWidget.MoveDown` | `"ListWidget.MoveDown"` | ↓ |
| `ListWidget.Activate` | `"ListWidget.Activate"` | Enter, Space, Mouse Left |
| `ListWidget.ScrollUp` | `"ListWidget.ScrollUp"` | Mouse ScrollUp |
| `ListWidget.ScrollDown` | `"ListWidget.ScrollDown"` | Mouse ScrollDown |

### Button

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ButtonWidget.Activate` | `"ButtonWidget.Activate"` | Enter, Space, Mouse Left |

### TextBox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TextBoxWidget.MoveLeft` | `"TextBoxWidget.MoveLeft"` | ← |
| `TextBoxWidget.MoveRight` | `"TextBoxWidget.MoveRight"` | → |
| `TextBoxWidget.MoveHome` | `"TextBoxWidget.MoveHome"` | Home |
| `TextBoxWidget.MoveEnd` | `"TextBoxWidget.MoveEnd"` | End |
| `TextBoxWidget.MoveWordLeft` | `"TextBoxWidget.MoveWordLeft"` | Ctrl+← |
| `TextBoxWidget.MoveWordRight` | `"TextBoxWidget.MoveWordRight"` | Ctrl+→ |
| `TextBoxWidget.SelectLeft` | `"TextBoxWidget.SelectLeft"` | Shift+← |
| `TextBoxWidget.SelectRight` | `"TextBoxWidget.SelectRight"` | Shift+→ |
| `TextBoxWidget.SelectWordLeft` | `"TextBoxWidget.SelectWordLeft"` | Ctrl+Shift+← |
| `TextBoxWidget.SelectWordRight` | `"TextBoxWidget.SelectWordRight"` | Ctrl+Shift+→ |
| `TextBoxWidget.SelectToStart` | `"TextBoxWidget.SelectToStart"` | Shift+Home |
| `TextBoxWidget.SelectToEnd` | `"TextBoxWidget.SelectToEnd"` | Shift+End |
| `TextBoxWidget.SelectAll` | `"TextBoxWidget.SelectAll"` | Ctrl+A, Mouse DoubleClick |
| `TextBoxWidget.DeleteForward` | `"TextBoxWidget.DeleteForward"` | Delete |
| `TextBoxWidget.DeleteBackward` | `"TextBoxWidget.DeleteBackward"` | Backspace |
| `TextBoxWidget.DeleteWordForward` | `"TextBoxWidget.DeleteWordForward"` | Ctrl+Delete |
| `TextBoxWidget.DeleteWordBackward` | `"TextBoxWidget.DeleteWordBackward"` | Ctrl+Backspace |
| `TextBoxWidget.InsertText` | `"TextBoxWidget.InsertText"` | Any character |
| `TextBoxWidget.Submit` | `"TextBoxWidget.Submit"` | Enter |

### Checkbox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `CheckboxWidget.ToggleActionId` | `"CheckboxWidget.Toggle"` | Enter, Space, Mouse Left |

### ToggleSwitch

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ToggleSwitchWidget.PreviousOptionActionId` | `"ToggleSwitchWidget.PreviousOption"` | ← |
| `ToggleSwitchWidget.NextOptionActionId` | `"ToggleSwitchWidget.NextOption"` | → |
| `ToggleSwitchWidget.SelectOptionActionId` | `"ToggleSwitchWidget.SelectOption"` | Mouse Left |

### Slider

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SliderWidget.IncreaseSmallActionId` | `"SliderWidget.IncreaseSmall"` | →, ↑, Mouse ScrollUp |
| `SliderWidget.DecreaseSmallActionId` | `"SliderWidget.DecreaseSmall"` | ←, ↓, Mouse ScrollDown |
| `SliderWidget.IncreaseLargeActionId` | `"SliderWidget.IncreaseLarge"` | PageUp |
| `SliderWidget.DecreaseLargeActionId` | `"SliderWidget.DecreaseLarge"` | PageDown |
| `SliderWidget.JumpToMinimumActionId` | `"SliderWidget.JumpToMinimum"` | Home |
| `SliderWidget.JumpToMaximumActionId` | `"SliderWidget.JumpToMaximum"` | End |
| `SliderWidget.SetValueActionId` | `"SliderWidget.SetValue"` | Mouse Left |

### Tree

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeWidget.MoveUpActionId` | `"TreeWidget.MoveUp"` | ↑, Mouse ScrollUp |
| `TreeWidget.MoveDownActionId` | `"TreeWidget.MoveDown"` | ↓, Mouse ScrollDown |
| `TreeWidget.ExpandOrChildActionId` | `"TreeWidget.ExpandOrChild"` | → |
| `TreeWidget.CollapseOrParentActionId` | `"TreeWidget.CollapseOrParent"` | ← |
| `TreeWidget.ToggleActionId` | `"TreeWidget.Toggle"` | Space |
| `TreeWidget.ActivateActionId` | `"TreeWidget.Activate"` | Enter |
| `TreeWidget.DoubleClickActivateActionId` | `"TreeWidget.DoubleClickActivate"` | Mouse DoubleClick |
| `TreeWidget.SelectItemActionId` | `"TreeWidget.SelectItem"` | Mouse Left |

### TreeItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeItemWidget.ExpandActionId` | `"TreeItemWidget.Expand"` | → |
| `TreeItemWidget.CollapseActionId` | `"TreeItemWidget.Collapse"` | ← |
| `TreeItemWidget.ToggleActionId` | `"TreeItemWidget.Toggle"` | Space |
| `TreeItemWidget.ActivateActionId` | `"TreeItemWidget.Activate"` | Enter |

### Table

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TableWidget.MoveUp` | `"TableWidget.MoveUp"` | ↑ |
| `TableWidget.MoveDown` | `"TableWidget.MoveDown"` | ↓ |
| `TableWidget.MoveToFirst` | `"TableWidget.MoveToFirst"` | Home |
| `TableWidget.MoveToLast` | `"TableWidget.MoveToLast"` | End |
| `TableWidget.ExtendUp` | `"TableWidget.ExtendUp"` | Shift+↑ |
| `TableWidget.ExtendDown` | `"TableWidget.ExtendDown"` | Shift+↓ |
| `TableWidget.ExtendToFirst` | `"TableWidget.ExtendToFirst"` | Shift+Home |
| `TableWidget.ExtendToLast` | `"TableWidget.ExtendToLast"` | Shift+End |
| `TableWidget.ToggleSelection` | `"TableWidget.ToggleSelection"` | Space |
| `TableWidget.SelectAll` | `"TableWidget.SelectAll"` | Ctrl+A |
| `TableWidget.PageUp` | `"TableWidget.PageUp"` | PageUp |
| `TableWidget.PageDown` | `"TableWidget.PageDown"` | PageDown |
| `TableWidget.ScrollUp` | `"TableWidget.ScrollUp"` | Mouse ScrollUp |
| `TableWidget.ScrollDown` | `"TableWidget.ScrollDown"` | Mouse ScrollDown |
| `TableWidget.ClickRow` | `"TableWidget.ClickRow"` | Mouse Left |

### Editor

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `EditorWidget.MoveLeft` | `"EditorWidget.MoveLeft"` | ← |
| `EditorWidget.MoveRight` | `"EditorWidget.MoveRight"` | → |
| `EditorWidget.MoveUp` | `"EditorWidget.MoveUp"` | ↑ |
| `EditorWidget.MoveDown` | `"EditorWidget.MoveDown"` | ↓ |
| `EditorWidget.MoveToLineStart` | `"EditorWidget.MoveToLineStart"` | Home |
| `EditorWidget.MoveToLineEnd` | `"EditorWidget.MoveToLineEnd"` | End |
| `EditorWidget.MoveToDocumentStart` | `"EditorWidget.MoveToDocumentStart"` | Ctrl+Home |
| `EditorWidget.MoveToDocumentEnd` | `"EditorWidget.MoveToDocumentEnd"` | Ctrl+End |
| `EditorWidget.MoveWordLeft` | `"EditorWidget.MoveWordLeft"` | Ctrl+← |
| `EditorWidget.MoveWordRight` | `"EditorWidget.MoveWordRight"` | Ctrl+→ |
| `EditorWidget.PageUp` | `"EditorWidget.PageUp"` | PageUp |
| `EditorWidget.PageDown` | `"EditorWidget.PageDown"` | PageDown |
| `EditorWidget.SelectLeft` | `"EditorWidget.SelectLeft"` | Shift+← |
| `EditorWidget.SelectRight` | `"EditorWidget.SelectRight"` | Shift+→ |
| `EditorWidget.SelectUp` | `"EditorWidget.SelectUp"` | Shift+↑ |
| `EditorWidget.SelectDown` | `"EditorWidget.SelectDown"` | Shift+↓ |
| `EditorWidget.SelectToLineStart` | `"EditorWidget.SelectToLineStart"` | Shift+Home |
| `EditorWidget.SelectToLineEnd` | `"EditorWidget.SelectToLineEnd"` | Shift+End |
| `EditorWidget.SelectToDocumentStart` | `"EditorWidget.SelectToDocumentStart"` | Ctrl+Shift+Home |
| `EditorWidget.SelectToDocumentEnd` | `"EditorWidget.SelectToDocumentEnd"` | Ctrl+Shift+End |
| `EditorWidget.SelectWordLeft` | `"EditorWidget.SelectWordLeft"` | Ctrl+Shift+← |
| `EditorWidget.SelectWordRight` | `"EditorWidget.SelectWordRight"` | Ctrl+Shift+→ |
| `EditorWidget.SelectPageUp` | `"EditorWidget.SelectPageUp"` | Shift+PageUp |
| `EditorWidget.SelectPageDown` | `"EditorWidget.SelectPageDown"` | Shift+PageDown |
| `EditorWidget.SelectAll` | `"EditorWidget.SelectAll"` | Ctrl+A |
| `EditorWidget.AddCursorAtNextMatch` | `"EditorWidget.AddCursorAtNextMatch"` | Ctrl+D |
| `EditorWidget.Undo` | `"EditorWidget.Undo"` | Ctrl+Z |
| `EditorWidget.Redo` | `"EditorWidget.Redo"` | Ctrl+Y |
| `EditorWidget.DeleteBackward` | `"EditorWidget.DeleteBackward"` | Backspace |
| `EditorWidget.DeleteForward` | `"EditorWidget.DeleteForward"` | Delete |
| `EditorWidget.DeleteWordBackward` | `"EditorWidget.DeleteWordBackward"` | Ctrl+Backspace |
| `EditorWidget.DeleteWordForward` | `"EditorWidget.DeleteWordForward"` | Ctrl+Delete |
| `EditorWidget.DeleteLine` | `"EditorWidget.DeleteLine"` | Ctrl+Shift+K |
| `EditorWidget.InsertNewline` | `"EditorWidget.InsertNewline"` | Enter |
| `EditorWidget.InsertTab` | `"EditorWidget.InsertTab"` | Tab |
| `EditorWidget.Click` | `"EditorWidget.Click"` | Mouse Left |
| `EditorWidget.CtrlClick` | `"EditorWidget.CtrlClick"` | Ctrl+Mouse Left |
| `EditorWidget.DoubleClick` | `"EditorWidget.DoubleClick"` | Mouse DoubleClick |
| `EditorWidget.TripleClick` | `"EditorWidget.TripleClick"` | Mouse TripleClick |
| `EditorWidget.ScrollUp` | `"EditorWidget.ScrollUp"` | Mouse ScrollUp |
| `EditorWidget.ScrollDown` | `"EditorWidget.ScrollDown"` | Mouse ScrollDown |
| `EditorWidget.ScrollLeft` | `"EditorWidget.ScrollLeft"` | Mouse ScrollLeft |
| `EditorWidget.ScrollRight` | `"EditorWidget.ScrollRight"` | Mouse ScrollRight |

### Interactable

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `InteractableWidget.Activate` | `"InteractableWidget.Activate"` | Enter, Space, Mouse Left |

### Hyperlink

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `HyperlinkWidget.ActivateActionId` | `"HyperlinkWidget.Activate"` | Enter, Mouse Left |

### Icon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `IconWidget.ActivateActionId` | `"IconWidget.Activate"` | Mouse Left |

### ScrollPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ScrollPanelWidget.ScrollUpAction` | `"ScrollPanelWidget.ScrollUp"` | ↑ |
| `ScrollPanelWidget.ScrollDownAction` | `"ScrollPanelWidget.ScrollDown"` | ↓ |
| `ScrollPanelWidget.ScrollLeftAction` | `"ScrollPanelWidget.ScrollLeft"` | ← |
| `ScrollPanelWidget.ScrollRightAction` | `"ScrollPanelWidget.ScrollRight"` | → |
| `ScrollPanelWidget.PageUpAction` | `"ScrollPanelWidget.PageUp"` | PageUp |
| `ScrollPanelWidget.PageDownAction` | `"ScrollPanelWidget.PageDown"` | PageDown |
| `ScrollPanelWidget.ScrollToStartAction` | `"ScrollPanelWidget.ScrollToStart"` | Home |
| `ScrollPanelWidget.ScrollToEndAction` | `"ScrollPanelWidget.ScrollToEnd"` | End |
| `ScrollPanelWidget.FocusNextAction` | `"ScrollPanelWidget.FocusNext"` | Tab |
| `ScrollPanelWidget.FocusPreviousAction` | `"ScrollPanelWidget.FocusPrevious"` | Shift+Tab |
| `ScrollPanelWidget.FocusFirstAction` | `"ScrollPanelWidget.FocusFirst"` | Escape |
| `ScrollPanelWidget.MouseScrollUpAction` | `"ScrollPanelWidget.MouseScrollUp"` | Mouse ScrollUp |
| `ScrollPanelWidget.MouseScrollDownAction` | `"ScrollPanelWidget.MouseScrollDown"` | Mouse ScrollDown |

### SplitButton

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitButtonWidget.ActivateActionId` | `"SplitButtonWidget.Activate"` | Enter, Space, Mouse Left |
| `SplitButtonWidget.OpenMenuActionId` | `"SplitButtonWidget.OpenMenu"` | ↓ |

### Menu

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuWidget.Open` | `"MenuWidget.Open"` | Enter, Space, Mouse Left |
| `MenuWidget.NextItem` | `"MenuWidget.NextItem"` | ↓ (context-dependent) |
| `MenuWidget.PreviousItem` | `"MenuWidget.PreviousItem"` | ↑ (context-dependent) |
| `MenuWidget.Close` | `"MenuWidget.Close"` | ← (context-dependent) |
| `MenuWidget.NavigatePrevious` | `"MenuWidget.NavigatePrevious"` | ← (context-dependent) |
| `MenuWidget.FocusPreviousInBar` | `"MenuWidget.FocusPreviousInBar"` | ← (in menu bar) |
| `MenuWidget.FocusNextInBar` | `"MenuWidget.FocusNextInBar"` | → (in menu bar) |

### MenuBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuBarWidget.PreviousMenu` | `"MenuBarWidget.PreviousMenu"` | ← |
| `MenuBarWidget.NextMenu` | `"MenuBarWidget.NextMenu"` | → |
| `MenuBarWidget.NextFocusable` | `"MenuBarWidget.NextFocusable"` | Tab |
| `MenuBarWidget.PreviousFocusable` | `"MenuBarWidget.PreviousFocusable"` | Shift+Tab |

### MenuItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuItemWidget.MoveDown` | `"MenuItemWidget.MoveDown"` | ↓ |
| `MenuItemWidget.MoveUp` | `"MenuItemWidget.MoveUp"` | ↑ |
| `MenuItemWidget.Close` | `"MenuItemWidget.Close"` | Escape |
| `MenuItemWidget.NavigateLeft` | `"MenuItemWidget.NavigateLeft"` | ← |
| `MenuItemWidget.NavigateRight` | `"MenuItemWidget.NavigateRight"` | → |
| `MenuItemWidget.Activate` | `"MenuItemWidget.Activate"` | Enter, Space, Mouse Left |

### TabBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TabBarWidget.ScrollLeft` | `"TabBarWidget.ScrollLeft"` | ← |
| `TabBarWidget.ScrollRight` | `"TabBarWidget.ScrollRight"` | → |
| `TabBarWidget.Click` | `"TabBarWidget.Click"` | Mouse Left |

### Accordion

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `AccordionWidget.ToggleSectionAction` | `"AccordionWidget.ToggleSection"` | Enter, Space |
| `AccordionWidget.PreviousSectionAction` | `"AccordionWidget.PreviousSection"` | ↑ |
| `AccordionWidget.NextSectionAction` | `"AccordionWidget.NextSection"` | ↓ |
| `AccordionWidget.FocusNextAction` | `"AccordionWidget.FocusNext"` | Tab |
| `AccordionWidget.FocusPreviousAction` | `"AccordionWidget.FocusPrevious"` | Shift+Tab |
| `AccordionWidget.ClickAction` | `"AccordionWidget.Click"` | Mouse Left |

### Drawer

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DrawerWidget.ToggleAction` | `"DrawerWidget.Toggle"` | Mouse Left |
| `DrawerWidget.OpenAction` | `"DrawerWidget.Open"` | Enter, Space |

### Splitter

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitterWidget.ResizeLeftAction` | `"SplitterWidget.ResizeLeft"` | ← |
| `SplitterWidget.ResizeRightAction` | `"SplitterWidget.ResizeRight"` | → |
| `SplitterWidget.ResizeUpAction` | `"SplitterWidget.ResizeUp"` | ↑ |
| `SplitterWidget.ResizeDownAction` | `"SplitterWidget.ResizeDown"` | ↓ |
| `SplitterWidget.FocusNextAction` | `"SplitterWidget.FocusNext"` | Tab |
| `SplitterWidget.FocusPreviousAction` | `"SplitterWidget.FocusPrevious"` | Shift+Tab |
| `SplitterWidget.FocusFirstAction` | `"SplitterWidget.FocusFirst"` | Escape |

### DragBarPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DragBarPanelWidget.ResizeLeftAction` | `"DragBarPanelWidget.ResizeLeft"` | ← |
| `DragBarPanelWidget.ResizeRightAction` | `"DragBarPanelWidget.ResizeRight"` | → |
| `DragBarPanelWidget.ResizeUpAction` | `"DragBarPanelWidget.ResizeUp"` | ↑ |
| `DragBarPanelWidget.ResizeDownAction` | `"DragBarPanelWidget.ResizeDown"` | ↓ |
| `DragBarPanelWidget.FocusNextAction` | `"DragBarPanelWidget.FocusNext"` | Tab |
| `DragBarPanelWidget.FocusPreviousAction` | `"DragBarPanelWidget.FocusPrevious"` | Shift+Tab |

### Window

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `WindowNode.CloseAction` | `"WindowNode.Close"` | Escape |
| `WindowNode.ClickAction` | `"WindowNode.Click"` | Mouse Left |
| `WindowNode.TitleBarIconClickAction` | `"WindowNode.TitleBarIconClick"` | — |

### NotificationCard

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationCardWidget.DismissAction` | `"NotificationCardWidget.Dismiss"` | Escape |

### NotificationPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationPanelWidget.ToggleDrawerAction` | `"NotificationPanelWidget.ToggleDrawer"` | Alt+N |

### NotificationIcon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationIconWidget.TogglePanelAction` | `"NotificationIconWidget.TogglePanel"` | Enter, Space, Mouse Left |

### Backdrop

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `BackdropWidget.DismissAction` | `"BackdropWidget.Dismiss"` | Escape |
| `BackdropWidget.ClickAwayAction` | `"BackdropWidget.ClickAway"` | Mouse Left |

## Next Steps

- [Theming](/guide/theming) — Customize colors and visual styles
- [Widgets & Nodes](/guide/widgets-and-nodes) — Learn the widget/node architecture
