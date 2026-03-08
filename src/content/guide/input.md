# Input Handling

Hex1b provides a comprehensive input system with focus management, keyboard routing, and declarative input bindings. Every built-in widget exposes named actions that you can remap, extend, or disable — making it straightforward to implement custom keybinding schemes like Vim or Emacs without forking widget code.

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

The naming convention is `"WidgetName.ActionName"`:

```csharp
// Widget definition (library code):
public record ListWidget : Hex1bWidget
{
    public static readonly ActionId MoveUp = new(nameof(MoveUp));
    public static readonly ActionId MoveDown = new(nameof(MoveDown));
    public static readonly ActionId Activate = new(nameof(Activate));
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
| `ListWidget.MoveUp` | `"MoveUp"` | ↑ |
| `ListWidget.MoveDown` | `"MoveDown"` | ↓ |
| `ListWidget.Activate` | `"Activate"` | Enter, Space, Mouse Left |
| `ListWidget.ScrollUp` | `"ScrollUp"` | Mouse ScrollUp |
| `ListWidget.ScrollDown` | `"ScrollDown"` | Mouse ScrollDown |

### Button

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ButtonWidget.Activate` | `"Activate"` | Enter, Space, Mouse Left |

### TextBox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TextBoxWidget.MoveLeft` | `"MoveLeft"` | ← |
| `TextBoxWidget.MoveRight` | `"MoveRight"` | → |
| `TextBoxWidget.MoveHome` | `"MoveHome"` | Home |
| `TextBoxWidget.MoveEnd` | `"MoveEnd"` | End |
| `TextBoxWidget.MoveWordLeft` | `"MoveWordLeft"` | Ctrl+← |
| `TextBoxWidget.MoveWordRight` | `"MoveWordRight"` | Ctrl+→ |
| `TextBoxWidget.SelectLeft` | `"SelectLeft"` | Shift+← |
| `TextBoxWidget.SelectRight` | `"SelectRight"` | Shift+→ |
| `TextBoxWidget.SelectToStart` | `"SelectToStart"` | Shift+Home |
| `TextBoxWidget.SelectToEnd` | `"SelectToEnd"` | Shift+End |
| `TextBoxWidget.SelectAll` | `"SelectAll"` | Ctrl+A, Mouse DoubleClick |
| `TextBoxWidget.DeleteForward` | `"DeleteForward"` | Delete |
| `TextBoxWidget.DeleteBackward` | `"DeleteBackward"` | Backspace |
| `TextBoxWidget.DeleteWordForward` | `"DeleteWordForward"` | Ctrl+Delete |
| `TextBoxWidget.DeleteWordBackward` | `"DeleteWordBackward"` | Ctrl+Backspace |
| `TextBoxWidget.InsertText` | `"InsertText"` | Any character |
| `TextBoxWidget.Submit` | `"Submit"` | Enter |

### Checkbox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `CheckboxWidget.ToggleActionId` | `"Toggle"` | Enter, Space, Mouse Left |

### ToggleSwitch

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ToggleSwitchWidget.PreviousOptionActionId` | `"PreviousOption"` | ← |
| `ToggleSwitchWidget.NextOptionActionId` | `"NextOption"` | → |
| `ToggleSwitchWidget.SelectOptionActionId` | `"SelectOption"` | Mouse Left |

### Slider

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SliderWidget.IncreaseSmallActionId` | `"IncreaseSmall"` | →, ↑, Mouse ScrollUp |
| `SliderWidget.DecreaseSmallActionId` | `"DecreaseSmall"` | ←, ↓, Mouse ScrollDown |
| `SliderWidget.IncreaseLargeActionId` | `"IncreaseLarge"` | PageUp |
| `SliderWidget.DecreaseLargeActionId` | `"DecreaseLarge"` | PageDown |
| `SliderWidget.JumpToMinimumActionId` | `"JumpToMinimum"` | Home |
| `SliderWidget.JumpToMaximumActionId` | `"JumpToMaximum"` | End |
| `SliderWidget.SetValueActionId` | `"SetValue"` | Mouse Left |

### Tree

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeWidget.MoveUpActionId` | `"MoveUp"` | ↑, Mouse ScrollUp |
| `TreeWidget.MoveDownActionId` | `"MoveDown"` | ↓, Mouse ScrollDown |
| `TreeWidget.ExpandOrChildActionId` | `"ExpandOrChild"` | → |
| `TreeWidget.CollapseOrParentActionId` | `"CollapseOrParent"` | ← |
| `TreeWidget.ToggleActionId` | `"Toggle"` | Space |
| `TreeWidget.ActivateActionId` | `"Activate"` | Enter |
| `TreeWidget.DoubleClickActivateActionId` | `"DoubleClickActivate"` | Mouse DoubleClick |
| `TreeWidget.SelectItemActionId` | `"SelectItem"` | Mouse Left |

### TreeItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeItemWidget.ExpandActionId` | `"Expand"` | → |
| `TreeItemWidget.CollapseActionId` | `"Collapse"` | ← |
| `TreeItemWidget.ToggleActionId` | `"Toggle"` | Space |
| `TreeItemWidget.ActivateActionId` | `"Activate"` | Enter |

### Table

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TableWidget.MoveUp` | `"MoveUp"` | ↑ |
| `TableWidget.MoveDown` | `"MoveDown"` | ↓ |
| `TableWidget.MoveToFirst` | `"MoveToFirst"` | Home |
| `TableWidget.MoveToLast` | `"MoveToLast"` | End |
| `TableWidget.ExtendUp` | `"ExtendUp"` | Shift+↑ |
| `TableWidget.ExtendDown` | `"ExtendDown"` | Shift+↓ |
| `TableWidget.ExtendToFirst` | `"ExtendToFirst"` | Shift+Home |
| `TableWidget.ExtendToLast` | `"ExtendToLast"` | Shift+End |
| `TableWidget.ToggleSelection` | `"ToggleSelection"` | Space |
| `TableWidget.SelectAll` | `"SelectAll"` | Ctrl+A |
| `TableWidget.PageUp` | `"PageUp"` | PageUp |
| `TableWidget.PageDown` | `"PageDown"` | PageDown |
| `TableWidget.ScrollUp` | `"ScrollUp"` | Mouse ScrollUp |
| `TableWidget.ScrollDown` | `"ScrollDown"` | Mouse ScrollDown |
| `TableWidget.ClickRow` | `"ClickRow"` | Mouse Left |

### Editor

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `EditorWidget.MoveLeft` | `"MoveLeft"` | ← |
| `EditorWidget.MoveRight` | `"MoveRight"` | → |
| `EditorWidget.MoveUp` | `"MoveUp"` | ↑ |
| `EditorWidget.MoveDown` | `"MoveDown"` | ↓ |
| `EditorWidget.MoveToLineStart` | `"MoveToLineStart"` | Home |
| `EditorWidget.MoveToLineEnd` | `"MoveToLineEnd"` | End |
| `EditorWidget.MoveToDocumentStart` | `"MoveToDocumentStart"` | Ctrl+Home |
| `EditorWidget.MoveToDocumentEnd` | `"MoveToDocumentEnd"` | Ctrl+End |
| `EditorWidget.MoveWordLeft` | `"MoveWordLeft"` | Ctrl+← |
| `EditorWidget.MoveWordRight` | `"MoveWordRight"` | Ctrl+→ |
| `EditorWidget.PageUp` | `"PageUp"` | PageUp |
| `EditorWidget.PageDown` | `"PageDown"` | PageDown |
| `EditorWidget.SelectLeft` | `"SelectLeft"` | Shift+← |
| `EditorWidget.SelectRight` | `"SelectRight"` | Shift+→ |
| `EditorWidget.SelectUp` | `"SelectUp"` | Shift+↑ |
| `EditorWidget.SelectDown` | `"SelectDown"` | Shift+↓ |
| `EditorWidget.SelectToLineStart` | `"SelectToLineStart"` | Shift+Home |
| `EditorWidget.SelectToLineEnd` | `"SelectToLineEnd"` | Shift+End |
| `EditorWidget.SelectToDocumentStart` | `"SelectToDocumentStart"` | Ctrl+Shift+Home |
| `EditorWidget.SelectToDocumentEnd` | `"SelectToDocumentEnd"` | Ctrl+Shift+End |
| `EditorWidget.SelectWordLeft` | `"SelectWordLeft"` | Ctrl+Shift+← |
| `EditorWidget.SelectWordRight` | `"SelectWordRight"` | Ctrl+Shift+→ |
| `EditorWidget.SelectPageUp` | `"SelectPageUp"` | Shift+PageUp |
| `EditorWidget.SelectPageDown` | `"SelectPageDown"` | Shift+PageDown |
| `EditorWidget.SelectAll` | `"SelectAll"` | Ctrl+A |
| `EditorWidget.AddCursorAtNextMatch` | `"AddCursorAtNextMatch"` | Ctrl+D |
| `EditorWidget.Undo` | `"Undo"` | Ctrl+Z |
| `EditorWidget.Redo` | `"Redo"` | Ctrl+Y |
| `EditorWidget.DeleteBackward` | `"DeleteBackward"` | Backspace |
| `EditorWidget.DeleteForward` | `"DeleteForward"` | Delete |
| `EditorWidget.DeleteWordBackward` | `"DeleteWordBackward"` | Ctrl+Backspace |
| `EditorWidget.DeleteWordForward` | `"DeleteWordForward"` | Ctrl+Delete |
| `EditorWidget.DeleteLine` | `"DeleteLine"` | Ctrl+Shift+K |
| `EditorWidget.InsertNewline` | `"InsertNewline"` | Enter |
| `EditorWidget.InsertTab` | `"InsertTab"` | Tab |
| `EditorWidget.Click` | `"Click"` | Mouse Left |
| `EditorWidget.CtrlClick` | `"CtrlClick"` | Ctrl+Mouse Left |
| `EditorWidget.DoubleClick` | `"DoubleClick"` | Mouse DoubleClick |
| `EditorWidget.TripleClick` | `"TripleClick"` | Mouse TripleClick |
| `EditorWidget.ScrollUp` | `"ScrollUp"` | Mouse ScrollUp |
| `EditorWidget.ScrollDown` | `"ScrollDown"` | Mouse ScrollDown |
| `EditorWidget.ScrollLeft` | `"ScrollLeft"` | Mouse ScrollLeft |
| `EditorWidget.ScrollRight` | `"ScrollRight"` | Mouse ScrollRight |

### Interactable

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `InteractableWidget.Activate` | `"Activate"` | Enter, Space, Mouse Left |

### Hyperlink

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `HyperlinkWidget.ActivateActionId` | `"Activate"` | Enter, Mouse Left |

### Icon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `IconWidget.ActivateActionId` | `"Activate"` | Mouse Left |

### ScrollPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ScrollPanelWidget.ScrollUpAction` | `"ScrollUp"` | ↑ |
| `ScrollPanelWidget.ScrollDownAction` | `"ScrollDown"` | ↓ |
| `ScrollPanelWidget.ScrollLeftAction` | `"ScrollLeft"` | ← |
| `ScrollPanelWidget.ScrollRightAction` | `"ScrollRight"` | → |
| `ScrollPanelWidget.PageUpAction` | `"PageUp"` | PageUp |
| `ScrollPanelWidget.PageDownAction` | `"PageDown"` | PageDown |
| `ScrollPanelWidget.ScrollToStartAction` | `"ScrollToStart"` | Home |
| `ScrollPanelWidget.ScrollToEndAction` | `"ScrollToEnd"` | End |
| `ScrollPanelWidget.FocusNextAction` | `"FocusNext"` | Tab |
| `ScrollPanelWidget.FocusPreviousAction` | `"FocusPrevious"` | Shift+Tab |
| `ScrollPanelWidget.FocusFirstAction` | `"FocusFirst"` | Escape |
| `ScrollPanelWidget.MouseScrollUpAction` | `"MouseScrollUp"` | Mouse ScrollUp |
| `ScrollPanelWidget.MouseScrollDownAction` | `"MouseScrollDown"` | Mouse ScrollDown |

### SplitButton

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitButtonWidget.ActivateActionId` | `"Activate"` | Enter, Space, Mouse Left |
| `SplitButtonWidget.OpenMenuActionId` | `"OpenMenu"` | ↓ |

### Menu

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuWidget.Open` | `"Open"` | Enter, Space, Mouse Left |
| `MenuWidget.NextItem` | `"NextItem"` | ↓ (context-dependent) |
| `MenuWidget.PreviousItem` | `"PreviousItem"` | ↑ (context-dependent) |
| `MenuWidget.Close` | `"Close"` | ← (context-dependent) |
| `MenuWidget.NavigatePrevious` | `"NavigatePrevious"` | ← (context-dependent) |
| `MenuWidget.FocusPreviousInBar` | `"FocusPreviousInBar"` | ← (in menu bar) |
| `MenuWidget.FocusNextInBar` | `"FocusNextInBar"` | → (in menu bar) |

### MenuBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuBarWidget.PreviousMenu` | `"PreviousMenu"` | ← |
| `MenuBarWidget.NextMenu` | `"NextMenu"` | → |
| `MenuBarWidget.NextFocusable` | `"NextFocusable"` | Tab |
| `MenuBarWidget.PreviousFocusable` | `"PreviousFocusable"` | Shift+Tab |

### MenuItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuItemWidget.MoveDown` | `"MoveDown"` | ↓ |
| `MenuItemWidget.MoveUp` | `"MoveUp"` | ↑ |
| `MenuItemWidget.Close` | `"Close"` | Escape |
| `MenuItemWidget.NavigateLeft` | `"NavigateLeft"` | ← |
| `MenuItemWidget.NavigateRight` | `"NavigateRight"` | → |
| `MenuItemWidget.Activate` | `"Activate"` | Enter, Space, Mouse Left |

### TabBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TabBarWidget.ScrollLeft` | `"ScrollLeft"` | ← |
| `TabBarWidget.ScrollRight` | `"ScrollRight"` | → |
| `TabBarWidget.Click` | `"Click"` | Mouse Left |

### Accordion

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `AccordionWidget.ToggleSectionAction` | `"ToggleSection"` | Enter, Space |
| `AccordionWidget.PreviousSectionAction` | `"PreviousSection"` | ↑ |
| `AccordionWidget.NextSectionAction` | `"NextSection"` | ↓ |
| `AccordionWidget.FocusNextAction` | `"FocusNext"` | Tab |
| `AccordionWidget.FocusPreviousAction` | `"FocusPrevious"` | Shift+Tab |
| `AccordionWidget.ClickAction` | `"Click"` | Mouse Left |

### Drawer

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DrawerWidget.ToggleAction` | `"Toggle"` | Mouse Left |
| `DrawerWidget.OpenAction` | `"Open"` | Enter, Space |

### Splitter

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitterWidget.ResizeLeftAction` | `"ResizeLeft"` | ← |
| `SplitterWidget.ResizeRightAction` | `"ResizeRight"` | → |
| `SplitterWidget.ResizeUpAction` | `"ResizeUp"` | ↑ |
| `SplitterWidget.ResizeDownAction` | `"ResizeDown"` | ↓ |
| `SplitterWidget.FocusNextAction` | `"FocusNext"` | Tab |
| `SplitterWidget.FocusPreviousAction` | `"FocusPrevious"` | Shift+Tab |
| `SplitterWidget.FocusFirstAction` | `"FocusFirst"` | Escape |

### DragBarPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DragBarPanelWidget.ResizeLeftAction` | `"ResizeLeft"` | ← |
| `DragBarPanelWidget.ResizeRightAction` | `"ResizeRight"` | → |
| `DragBarPanelWidget.ResizeUpAction` | `"ResizeUp"` | ↑ |
| `DragBarPanelWidget.ResizeDownAction` | `"ResizeDown"` | ↓ |
| `DragBarPanelWidget.FocusNextAction` | `"FocusNext"` | Tab |
| `DragBarPanelWidget.FocusPreviousAction` | `"FocusPrevious"` | Shift+Tab |

### Window

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `WindowNode.CloseAction` | `"Close"` | Escape |
| `WindowNode.ClickAction` | `"Click"` | Mouse Left |
| `WindowNode.TitleBarIconClickAction` | `"TitleBarIconClick"` | — |

### NotificationCard

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationCardWidget.DismissAction` | `"Dismiss"` | Escape |

### NotificationPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationPanelWidget.ToggleDrawerAction` | `"ToggleDrawer"` | Alt+N |

### NotificationIcon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationIconWidget.TogglePanelAction` | `"TogglePanel"` | Enter, Space, Mouse Left |

### Backdrop

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `BackdropWidget.DismissAction` | `"Dismiss"` | Escape |
| `BackdropWidget.ClickAwayAction` | `"ClickAway"` | Mouse Left |

## Next Steps

- [Theming](/guide/theming) — Customize colors and visual styles
- [Widgets & Nodes](/guide/widgets-and-nodes) — Learn the widget/node architecture
