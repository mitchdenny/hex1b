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
    public static readonly ActionId MoveUp = new("List.MoveUp");
    public static readonly ActionId MoveDown = new("List.MoveDown");
    public static readonly ActionId Activate = new("List.Activate");
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
| `ListWidget.MoveUp` | `"List.MoveUp"` | ↑ |
| `ListWidget.MoveDown` | `"List.MoveDown"` | ↓ |
| `ListWidget.Activate` | `"List.Activate"` | Enter, Space, Mouse Left |
| `ListWidget.ScrollUp` | `"List.ScrollUp"` | Mouse ScrollUp |
| `ListWidget.ScrollDown` | `"List.ScrollDown"` | Mouse ScrollDown |

### Button

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ButtonWidget.Activate` | `"Button.Activate"` | Enter, Space, Mouse Left |

### TextBox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TextBoxWidget.MoveLeft` | `"TextBox.MoveLeft"` | ← |
| `TextBoxWidget.MoveRight` | `"TextBox.MoveRight"` | → |
| `TextBoxWidget.MoveHome` | `"TextBox.MoveHome"` | Home |
| `TextBoxWidget.MoveEnd` | `"TextBox.MoveEnd"` | End |
| `TextBoxWidget.MoveWordLeft` | `"TextBox.MoveWordLeft"` | Ctrl+← |
| `TextBoxWidget.MoveWordRight` | `"TextBox.MoveWordRight"` | Ctrl+→ |
| `TextBoxWidget.SelectLeft` | `"TextBox.SelectLeft"` | Shift+← |
| `TextBoxWidget.SelectRight` | `"TextBox.SelectRight"` | Shift+→ |
| `TextBoxWidget.SelectToStart` | `"TextBox.SelectToStart"` | Shift+Home |
| `TextBoxWidget.SelectToEnd` | `"TextBox.SelectToEnd"` | Shift+End |
| `TextBoxWidget.SelectAll` | `"TextBox.SelectAll"` | Ctrl+A, Mouse DoubleClick |
| `TextBoxWidget.DeleteForward` | `"TextBox.DeleteForward"` | Delete |
| `TextBoxWidget.DeleteBackward` | `"TextBox.DeleteBackward"` | Backspace |
| `TextBoxWidget.DeleteWordForward` | `"TextBox.DeleteWordForward"` | Ctrl+Delete |
| `TextBoxWidget.DeleteWordBackward` | `"TextBox.DeleteWordBackward"` | Ctrl+Backspace |
| `TextBoxWidget.InsertText` | `"TextBox.InsertText"` | Any character |
| `TextBoxWidget.Submit` | `"TextBox.Submit"` | Enter |

### Checkbox

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `CheckboxWidget.ToggleActionId` | `"Checkbox.Toggle"` | Enter, Space, Mouse Left |

### ToggleSwitch

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ToggleSwitchWidget.PreviousOptionActionId` | `"ToggleSwitch.PreviousOption"` | ← |
| `ToggleSwitchWidget.NextOptionActionId` | `"ToggleSwitch.NextOption"` | → |
| `ToggleSwitchWidget.SelectOptionActionId` | `"ToggleSwitch.SelectOption"` | Mouse Left |

### Slider

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SliderWidget.IncreaseSmallActionId` | `"Slider.IncreaseSmall"` | →, ↑, Mouse ScrollUp |
| `SliderWidget.DecreaseSmallActionId` | `"Slider.DecreaseSmall"` | ←, ↓, Mouse ScrollDown |
| `SliderWidget.IncreaseLargeActionId` | `"Slider.IncreaseLarge"` | PageUp |
| `SliderWidget.DecreaseLargeActionId` | `"Slider.DecreaseLarge"` | PageDown |
| `SliderWidget.JumpToMinimumActionId` | `"Slider.JumpToMinimum"` | Home |
| `SliderWidget.JumpToMaximumActionId` | `"Slider.JumpToMaximum"` | End |
| `SliderWidget.SetValueActionId` | `"Slider.SetValue"` | Mouse Left |

### Tree

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeWidget.MoveUpActionId` | `"Tree.MoveUp"` | ↑, Mouse ScrollUp |
| `TreeWidget.MoveDownActionId` | `"Tree.MoveDown"` | ↓, Mouse ScrollDown |
| `TreeWidget.ExpandOrChildActionId` | `"Tree.ExpandOrChild"` | → |
| `TreeWidget.CollapseOrParentActionId` | `"Tree.CollapseOrParent"` | ← |
| `TreeWidget.ToggleActionId` | `"Tree.Toggle"` | Space |
| `TreeWidget.ActivateActionId` | `"Tree.Activate"` | Enter |
| `TreeWidget.DoubleClickActivateActionId` | `"Tree.DoubleClickActivate"` | Mouse DoubleClick |
| `TreeWidget.SelectItemActionId` | `"Tree.SelectItem"` | Mouse Left |

### TreeItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TreeItemWidget.ExpandActionId` | `"TreeItem.Expand"` | → |
| `TreeItemWidget.CollapseActionId` | `"TreeItem.Collapse"` | ← |
| `TreeItemWidget.ToggleActionId` | `"TreeItem.Toggle"` | Space |
| `TreeItemWidget.ActivateActionId` | `"TreeItem.Activate"` | Enter |

### Table

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TableWidget.MoveUp` | `"Table.MoveUp"` | ↑ |
| `TableWidget.MoveDown` | `"Table.MoveDown"` | ↓ |
| `TableWidget.MoveToFirst` | `"Table.MoveToFirst"` | Home |
| `TableWidget.MoveToLast` | `"Table.MoveToLast"` | End |
| `TableWidget.ExtendUp` | `"Table.ExtendUp"` | Shift+↑ |
| `TableWidget.ExtendDown` | `"Table.ExtendDown"` | Shift+↓ |
| `TableWidget.ExtendToFirst` | `"Table.ExtendToFirst"` | Shift+Home |
| `TableWidget.ExtendToLast` | `"Table.ExtendToLast"` | Shift+End |
| `TableWidget.ToggleSelection` | `"Table.ToggleSelection"` | Space |
| `TableWidget.SelectAll` | `"Table.SelectAll"` | Ctrl+A |
| `TableWidget.PageUp` | `"Table.PageUp"` | PageUp |
| `TableWidget.PageDown` | `"Table.PageDown"` | PageDown |
| `TableWidget.ScrollUp` | `"Table.ScrollUp"` | Mouse ScrollUp |
| `TableWidget.ScrollDown` | `"Table.ScrollDown"` | Mouse ScrollDown |
| `TableWidget.ClickRow` | `"Table.ClickRow"` | Mouse Left |

### Editor

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `EditorWidget.MoveLeft` | `"Editor.MoveLeft"` | ← |
| `EditorWidget.MoveRight` | `"Editor.MoveRight"` | → |
| `EditorWidget.MoveUp` | `"Editor.MoveUp"` | ↑ |
| `EditorWidget.MoveDown` | `"Editor.MoveDown"` | ↓ |
| `EditorWidget.MoveToLineStart` | `"Editor.MoveToLineStart"` | Home |
| `EditorWidget.MoveToLineEnd` | `"Editor.MoveToLineEnd"` | End |
| `EditorWidget.MoveToDocumentStart` | `"Editor.MoveToDocumentStart"` | Ctrl+Home |
| `EditorWidget.MoveToDocumentEnd` | `"Editor.MoveToDocumentEnd"` | Ctrl+End |
| `EditorWidget.MoveWordLeft` | `"Editor.MoveWordLeft"` | Ctrl+← |
| `EditorWidget.MoveWordRight` | `"Editor.MoveWordRight"` | Ctrl+→ |
| `EditorWidget.PageUp` | `"Editor.PageUp"` | PageUp |
| `EditorWidget.PageDown` | `"Editor.PageDown"` | PageDown |
| `EditorWidget.SelectLeft` | `"Editor.SelectLeft"` | Shift+← |
| `EditorWidget.SelectRight` | `"Editor.SelectRight"` | Shift+→ |
| `EditorWidget.SelectUp` | `"Editor.SelectUp"` | Shift+↑ |
| `EditorWidget.SelectDown` | `"Editor.SelectDown"` | Shift+↓ |
| `EditorWidget.SelectToLineStart` | `"Editor.SelectToLineStart"` | Shift+Home |
| `EditorWidget.SelectToLineEnd` | `"Editor.SelectToLineEnd"` | Shift+End |
| `EditorWidget.SelectToDocumentStart` | `"Editor.SelectToDocumentStart"` | Ctrl+Shift+Home |
| `EditorWidget.SelectToDocumentEnd` | `"Editor.SelectToDocumentEnd"` | Ctrl+Shift+End |
| `EditorWidget.SelectWordLeft` | `"Editor.SelectWordLeft"` | Ctrl+Shift+← |
| `EditorWidget.SelectWordRight` | `"Editor.SelectWordRight"` | Ctrl+Shift+→ |
| `EditorWidget.SelectPageUp` | `"Editor.SelectPageUp"` | Shift+PageUp |
| `EditorWidget.SelectPageDown` | `"Editor.SelectPageDown"` | Shift+PageDown |
| `EditorWidget.SelectAll` | `"Editor.SelectAll"` | Ctrl+A |
| `EditorWidget.AddCursorAtNextMatch` | `"Editor.AddCursorAtNextMatch"` | Ctrl+D |
| `EditorWidget.Undo` | `"Editor.Undo"` | Ctrl+Z |
| `EditorWidget.Redo` | `"Editor.Redo"` | Ctrl+Y |
| `EditorWidget.DeleteBackward` | `"Editor.DeleteBackward"` | Backspace |
| `EditorWidget.DeleteForward` | `"Editor.DeleteForward"` | Delete |
| `EditorWidget.DeleteWordBackward` | `"Editor.DeleteWordBackward"` | Ctrl+Backspace |
| `EditorWidget.DeleteWordForward` | `"Editor.DeleteWordForward"` | Ctrl+Delete |
| `EditorWidget.DeleteLine` | `"Editor.DeleteLine"` | Ctrl+Shift+K |
| `EditorWidget.InsertNewline` | `"Editor.InsertNewline"` | Enter |
| `EditorWidget.InsertTab` | `"Editor.InsertTab"` | Tab |
| `EditorWidget.Click` | `"Editor.Click"` | Mouse Left |
| `EditorWidget.CtrlClick` | `"Editor.CtrlClick"` | Ctrl+Mouse Left |
| `EditorWidget.DoubleClick` | `"Editor.DoubleClick"` | Mouse DoubleClick |
| `EditorWidget.TripleClick` | `"Editor.TripleClick"` | Mouse TripleClick |
| `EditorWidget.ScrollUp` | `"Editor.ScrollUp"` | Mouse ScrollUp |
| `EditorWidget.ScrollDown` | `"Editor.ScrollDown"` | Mouse ScrollDown |
| `EditorWidget.ScrollLeft` | `"Editor.ScrollLeft"` | Mouse ScrollLeft |
| `EditorWidget.ScrollRight` | `"Editor.ScrollRight"` | Mouse ScrollRight |

### Interactable

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `InteractableWidget.Activate` | `"Interactable.Activate"` | Enter, Space, Mouse Left |

### Hyperlink

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `HyperlinkWidget.ActivateActionId` | `"Hyperlink.Activate"` | Enter, Mouse Left |

### Icon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `IconWidget.ActivateActionId` | `"Icon.Activate"` | Mouse Left |

### ScrollPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `ScrollPanelWidget.ScrollUpAction` | `"ScrollPanel.ScrollUp"` | ↑ |
| `ScrollPanelWidget.ScrollDownAction` | `"ScrollPanel.ScrollDown"` | ↓ |
| `ScrollPanelWidget.ScrollLeftAction` | `"ScrollPanel.ScrollLeft"` | ← |
| `ScrollPanelWidget.ScrollRightAction` | `"ScrollPanel.ScrollRight"` | → |
| `ScrollPanelWidget.PageUpAction` | `"ScrollPanel.PageUp"` | PageUp |
| `ScrollPanelWidget.PageDownAction` | `"ScrollPanel.PageDown"` | PageDown |
| `ScrollPanelWidget.ScrollToStartAction` | `"ScrollPanel.ScrollToStart"` | Home |
| `ScrollPanelWidget.ScrollToEndAction` | `"ScrollPanel.ScrollToEnd"` | End |
| `ScrollPanelWidget.FocusNextAction` | `"ScrollPanel.FocusNext"` | Tab |
| `ScrollPanelWidget.FocusPreviousAction` | `"ScrollPanel.FocusPrevious"` | Shift+Tab |
| `ScrollPanelWidget.FocusFirstAction` | `"ScrollPanel.FocusFirst"` | Escape |
| `ScrollPanelWidget.MouseScrollUpAction` | `"ScrollPanel.MouseScrollUp"` | Mouse ScrollUp |
| `ScrollPanelWidget.MouseScrollDownAction` | `"ScrollPanel.MouseScrollDown"` | Mouse ScrollDown |

### SplitButton

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitButtonWidget.ActivateActionId` | `"SplitButton.Activate"` | Enter, Space, Mouse Left |
| `SplitButtonWidget.OpenMenuActionId` | `"SplitButton.OpenMenu"` | ↓ |

### Menu

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuWidget.Open` | `"Menu.Open"` | Enter, Space, Mouse Left |
| `MenuWidget.NextItem` | `"Menu.NextItem"` | ↓ (context-dependent) |
| `MenuWidget.PreviousItem` | `"Menu.PreviousItem"` | ↑ (context-dependent) |
| `MenuWidget.Close` | `"Menu.Close"` | ← (context-dependent) |
| `MenuWidget.NavigatePrevious` | `"Menu.NavigatePrevious"` | ← (context-dependent) |
| `MenuWidget.FocusPreviousInBar` | `"Menu.FocusPreviousInBar"` | ← (in menu bar) |
| `MenuWidget.FocusNextInBar` | `"Menu.FocusNextInBar"` | → (in menu bar) |

### MenuBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuBarWidget.PreviousMenu` | `"MenuBar.PreviousMenu"` | ← |
| `MenuBarWidget.NextMenu` | `"MenuBar.NextMenu"` | → |
| `MenuBarWidget.NextFocusable` | `"MenuBar.NextFocusable"` | Tab |
| `MenuBarWidget.PreviousFocusable` | `"MenuBar.PreviousFocusable"` | Shift+Tab |

### MenuItem

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `MenuItemWidget.MoveDown` | `"MenuItem.MoveDown"` | ↓ |
| `MenuItemWidget.MoveUp` | `"MenuItem.MoveUp"` | ↑ |
| `MenuItemWidget.Close` | `"MenuItem.Close"` | Escape |
| `MenuItemWidget.NavigateLeft` | `"MenuItem.NavigateLeft"` | ← |
| `MenuItemWidget.NavigateRight` | `"MenuItem.NavigateRight"` | → |
| `MenuItemWidget.Activate` | `"MenuItem.Activate"` | Enter, Space, Mouse Left |

### TabBar

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `TabBarWidget.ScrollLeft` | `"TabBar.ScrollLeft"` | ← |
| `TabBarWidget.ScrollRight` | `"TabBar.ScrollRight"` | → |
| `TabBarWidget.Click` | `"TabBar.Click"` | Mouse Left |

### Accordion

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `AccordionWidget.ToggleSectionAction` | `"Accordion.ToggleSection"` | Enter, Space |
| `AccordionWidget.PreviousSectionAction` | `"Accordion.PreviousSection"` | ↑ |
| `AccordionWidget.NextSectionAction` | `"Accordion.NextSection"` | ↓ |
| `AccordionWidget.FocusNextAction` | `"Accordion.FocusNext"` | Tab |
| `AccordionWidget.FocusPreviousAction` | `"Accordion.FocusPrevious"` | Shift+Tab |
| `AccordionWidget.ClickAction` | `"Accordion.Click"` | Mouse Left |

### Drawer

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DrawerWidget.ToggleAction` | `"Drawer.Toggle"` | Mouse Left |
| `DrawerWidget.OpenAction` | `"Drawer.Open"` | Enter, Space |

### Splitter

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `SplitterWidget.ResizeLeftAction` | `"Splitter.ResizeLeft"` | ← |
| `SplitterWidget.ResizeRightAction` | `"Splitter.ResizeRight"` | → |
| `SplitterWidget.ResizeUpAction` | `"Splitter.ResizeUp"` | ↑ |
| `SplitterWidget.ResizeDownAction` | `"Splitter.ResizeDown"` | ↓ |
| `SplitterWidget.FocusNextAction` | `"Splitter.FocusNext"` | Tab |
| `SplitterWidget.FocusPreviousAction` | `"Splitter.FocusPrevious"` | Shift+Tab |
| `SplitterWidget.FocusFirstAction` | `"Splitter.FocusFirst"` | Escape |

### DragBarPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `DragBarPanelWidget.ResizeLeftAction` | `"DragBarPanel.ResizeLeft"` | ← |
| `DragBarPanelWidget.ResizeRightAction` | `"DragBarPanel.ResizeRight"` | → |
| `DragBarPanelWidget.ResizeUpAction` | `"DragBarPanel.ResizeUp"` | ↑ |
| `DragBarPanelWidget.ResizeDownAction` | `"DragBarPanel.ResizeDown"` | ↓ |
| `DragBarPanelWidget.FocusNextAction` | `"DragBarPanel.FocusNext"` | Tab |
| `DragBarPanelWidget.FocusPreviousAction` | `"DragBarPanel.FocusPrevious"` | Shift+Tab |

### Window

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `WindowNode.CloseAction` | `"Window.Close"` | Escape |
| `WindowNode.ClickAction` | `"Window.Click"` | Mouse Left |
| `WindowNode.TitleBarIconClickAction` | `"Window.TitleBarIconClick"` | — |

### NotificationCard

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationCardWidget.DismissAction` | `"NotificationCard.Dismiss"` | Escape |

### NotificationPanel

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationPanelWidget.ToggleDrawerAction` | `"NotificationPanel.ToggleDrawer"` | Alt+N |

### NotificationIcon

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `NotificationIconWidget.TogglePanelAction` | `"NotificationIcon.TogglePanel"` | Enter, Space, Mouse Left |

### Backdrop

| Field | ActionId | Default Key |
|-------|----------|-------------|
| `BackdropWidget.DismissAction` | `"Backdrop.Dismiss"` | Escape |
| `BackdropWidget.ClickAwayAction` | `"Backdrop.ClickAway"` | Mouse Left |

## Next Steps

- [Theming](/guide/theming) — Customize colors and visual styles
- [Widgets & Nodes](/guide/widgets-and-nodes) — Learn the widget/node architecture
