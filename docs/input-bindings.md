# Input Bindings

Hex1b widgets handle keyboard and mouse input through a declarative **input binding** system. Each widget ships with sensible default keybindings, but you can remap, extend, or completely replace them — without forking the widget code.

This guide covers everything from basic remapping to building reusable keybinding presets.

## How Input Bindings Work

Every interactive widget in Hex1b defines its input behavior through `InputBinding` objects. Under the hood, each widget's `ConfigureDefaultBindings` method registers a set of key (and sometimes mouse) bindings using an `InputBindingsBuilder`. When a key event arrives, Hex1b walks the binding list and invokes the first match.

You customize bindings with the `.WithInputBindings()` extension method:

```csharp
var list = ctx.List(items)
    .WithInputBindings(b =>
    {
        // The builder `b` is pre-populated with the widget's defaults.
        // Add, remove, or remap bindings here.
    });
```

The builder you receive already contains every default binding for the widget, so you can inspect, modify, or remove them before Hex1b uses them.

## ActionId — Named Actions

An `ActionId` is a lightweight, strongly typed identifier for a bindable action:

```csharp
public readonly record struct ActionId(string Value);
```

Each widget exposes its available actions as `static readonly ActionId` fields. The naming convention is `"WidgetName.ActionName"`:

```csharp
// On ListWidget
public static readonly ActionId MoveUp   = new("List.MoveUp");
public static readonly ActionId MoveDown = new("List.MoveDown");
public static readonly ActionId Activate = new("List.Activate");
```

Because they are static fields, you reference them by name — no magic strings, no typo risk:

```csharp
b.Remove(ListWidget.MoveUp);  // strongly typed ✓
```

## The `Triggers` Method

`Triggers` is the bridge between a key and a named action.

### Registering an action (widget authors)

Inside `ConfigureDefaultBindings`, a widget registers its actions with a handler:

```csharp
b.Key(Hex1bKey.UpArrow).Triggers(MoveUp, MoveSelectionUp, "Move selection up");
```

This tells Hex1b: *"When ↑ is pressed, run `MoveSelectionUp` and call that action `MoveUp`."*

The three handler overloads are:

```csharp
void Triggers(ActionId actionId, Action handler, string? description = null);
void Triggers(ActionId actionId, Action<InputBindingActionContext> handler, string? description = null);
void Triggers(ActionId actionId, Func<InputBindingActionContext, Task> handler, string? description = null);
```

### Rebinding to an existing action (users)

When you only pass an `ActionId`, Hex1b looks up the handler that was already registered for that action and wires it to the new key:

```csharp
b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);  // K now does the same thing as ↑
```

```csharp
void Triggers(ActionId actionId);  // handler resolved from registry
```

Multiple keys can trigger the same action — they all share the same handler.

## Customizing Keybindings

### Remap a key

Replace the default trigger with a different key:

```csharp
list.WithInputBindings(b =>
{
    b.Remove(ListWidget.MoveUp);                      // remove the default ↑ binding
    b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);    // bind K instead
});
```

### Add an alias

Keep the default key *and* add another one:

```csharp
list.WithInputBindings(b =>
{
    b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);    // K + ↑ both work
});
```

### Disable an action

Remove all bindings for an action so no key triggers it:

```csharp
list.WithInputBindings(b =>
{
    b.Remove(ListWidget.Activate);  // Enter no longer activates
});
```

### Start from a blank slate

Clear every default binding and build your own set from scratch:

```csharp
list.WithInputBindings(b =>
{
    b.RemoveAll();  // wipe all defaults
    b.Key(Hex1bKey.K).Action(customUpHandler, "Custom up");
    b.Key(Hex1bKey.J).Action(customDownHandler, "Custom down");
});
```

> **Tip:** `RemoveAll()` clears bindings but keeps registered action handlers in the internal registry.
> You can still call `Triggers(actionId)` after `RemoveAll()` to re-wire actions to new keys.

## Walkthrough: Vim-Style Keybindings

A common request is to navigate lists with `j`/`k` instead of the arrow keys. Here's the complete recipe:

```csharp
var list = ctx.List(items)
    .WithInputBindings(b =>
    {
        // Remove the default arrow-key bindings
        b.Remove(ListWidget.MoveUp);
        b.Remove(ListWidget.MoveDown);

        // Rebind to Vim keys
        b.Key(Hex1bKey.K).Triggers(ListWidget.MoveUp);
        b.Key(Hex1bKey.J).Triggers(ListWidget.MoveDown);

        // Enter already triggers Activate by default — no change needed
    });
```

The `Triggers(ActionId)` overload resolves each action's handler from the widget's default registration, so you don't need to know the implementation details.

## Walkthrough: Building a Reusable Keybinding Helper

If you apply the same remappings across your application, extract them into a helper:

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
}
```

Because `WithInputBindings` accepts an `Action<InputBindingsBuilder>`, your helper plugs right in:

```csharp
var list = ctx.List(items)
    .WithInputBindings(VimBindings.ApplyToList);
```

You can compose multiple helpers by chaining calls:

```csharp
list.WithInputBindings(b =>
{
    VimBindings.ApplyToList(b);
    // Additional per-instance tweaks here
});
```

## Discovery — Finding Available Actions

The builder exposes introspection methods that let you query the binding state at configuration time.

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllActionIds()` | `IReadOnlyList<ActionId>` | Every registered ActionId across all binding types |
| `GetBindings(actionId)` | `IReadOnlyList<InputBinding>` | Key bindings currently wired to the given action |
| `GetRegisteredAction(actionId)` | `(handler, description)` | The handler and description for an action (throws if not found) |

Example — listing all actions available on a widget:

```csharp
list.WithInputBindings(b =>
{
    foreach (var id in b.GetAllActionIds())
    {
        var (handler, desc) = b.GetRegisteredAction(id);
        Console.WriteLine($"{id}: {desc}");
    }
});
```

## Action ID Reference

The tables below list every `ActionId` exposed by Hex1b's built-in widgets. Use these identifiers with `Remove()` and `Triggers()` to remap or disable actions.

### AccordionWidget

| Field | ActionId String |
|-------|----------------|
| `ToggleSectionAction` | `Accordion.ToggleSection` |
| `ClickAction` | `Accordion.Click` |
| `NextSectionAction` | `Accordion.NextSection` |
| `PreviousSectionAction` | `Accordion.PreviousSection` |
| `FocusNextAction` | `Accordion.FocusNext` |
| `FocusPreviousAction` | `Accordion.FocusPrevious` |

### BackdropWidget

| Field | ActionId String |
|-------|----------------|
| `ClickAwayAction` | `Backdrop.ClickAway` |
| `DismissAction` | `Backdrop.Dismiss` |

### ButtonWidget

| Field | ActionId String |
|-------|----------------|
| `Activate` | `Button.Activate` |

### CheckboxWidget

| Field | ActionId String |
|-------|----------------|
| `ToggleActionId` | `Checkbox.Toggle` |

### DrawerWidget

| Field | ActionId String |
|-------|----------------|
| `OpenAction` | `Drawer.Open` |
| `ToggleAction` | `Drawer.Toggle` |

### EditorWidget

| Field | ActionId String |
|-------|----------------|
| `MoveLeft` | `Editor.MoveLeft` |
| `MoveRight` | `Editor.MoveRight` |
| `MoveUp` | `Editor.MoveUp` |
| `MoveDown` | `Editor.MoveDown` |
| `MoveWordLeft` | `Editor.MoveWordLeft` |
| `MoveWordRight` | `Editor.MoveWordRight` |
| `MoveToLineStart` | `Editor.MoveToLineStart` |
| `MoveToLineEnd` | `Editor.MoveToLineEnd` |
| `MoveToDocumentStart` | `Editor.MoveToDocumentStart` |
| `MoveToDocumentEnd` | `Editor.MoveToDocumentEnd` |
| `SelectLeft` | `Editor.SelectLeft` |
| `SelectRight` | `Editor.SelectRight` |
| `SelectUp` | `Editor.SelectUp` |
| `SelectDown` | `Editor.SelectDown` |
| `SelectWordLeft` | `Editor.SelectWordLeft` |
| `SelectWordRight` | `Editor.SelectWordRight` |
| `SelectToLineStart` | `Editor.SelectToLineStart` |
| `SelectToLineEnd` | `Editor.SelectToLineEnd` |
| `SelectToDocumentStart` | `Editor.SelectToDocumentStart` |
| `SelectToDocumentEnd` | `Editor.SelectToDocumentEnd` |
| `SelectAll` | `Editor.SelectAll` |
| `SelectPageUp` | `Editor.SelectPageUp` |
| `SelectPageDown` | `Editor.SelectPageDown` |
| `DeleteBackward` | `Editor.DeleteBackward` |
| `DeleteForward` | `Editor.DeleteForward` |
| `DeleteWordBackward` | `Editor.DeleteWordBackward` |
| `DeleteWordForward` | `Editor.DeleteWordForward` |
| `DeleteLine` | `Editor.DeleteLine` |
| `InsertNewline` | `Editor.InsertNewline` |
| `InsertTab` | `Editor.InsertTab` |
| `Undo` | `Editor.Undo` |
| `Redo` | `Editor.Redo` |
| `PageUp` | `Editor.PageUp` |
| `PageDown` | `Editor.PageDown` |
| `ScrollUp` | `Editor.ScrollUp` |
| `ScrollDown` | `Editor.ScrollDown` |
| `ScrollLeft` | `Editor.ScrollLeft` |
| `ScrollRight` | `Editor.ScrollRight` |
| `AddCursorAtNextMatch` | `Editor.AddCursorAtNextMatch` |
| `Click` | `Editor.Click` |
| `CtrlClick` | `Editor.CtrlClick` |
| `DoubleClick` | `Editor.DoubleClick` |
| `TripleClick` | `Editor.TripleClick` |

### HyperlinkWidget

| Field | ActionId String |
|-------|----------------|
| `ActivateActionId` | `Hyperlink.Activate` |

### IconWidget

| Field | ActionId String |
|-------|----------------|
| `ActivateActionId` | `Icon.Activate` |

### InteractableWidget

| Field | ActionId String |
|-------|----------------|
| `Activate` | `Interactable.Activate` |

### ListWidget

| Field | ActionId String |
|-------|----------------|
| `MoveUp` | `List.MoveUp` |
| `MoveDown` | `List.MoveDown` |
| `Activate` | `List.Activate` |
| `ScrollUp` | `List.ScrollUp` |
| `ScrollDown` | `List.ScrollDown` |

### MenuWidget

| Field | ActionId String |
|-------|----------------|
| `Open` | `Menu.Open` |
| `Close` | `Menu.Close` |
| `NextItem` | `Menu.NextItem` |
| `PreviousItem` | `Menu.PreviousItem` |
| `FocusNextInBar` | `Menu.FocusNextInBar` |
| `FocusPreviousInBar` | `Menu.FocusPreviousInBar` |
| `NavigatePrevious` | `Menu.NavigatePrevious` |

### MenuBarWidget

| Field | ActionId String |
|-------|----------------|
| `NextMenu` | `MenuBar.NextMenu` |
| `PreviousMenu` | `MenuBar.PreviousMenu` |
| `NextFocusable` | `MenuBar.NextFocusable` |
| `PreviousFocusable` | `MenuBar.PreviousFocusable` |

### MenuItemWidget

| Field | ActionId String |
|-------|----------------|
| `Activate` | `MenuItem.Activate` |
| `MoveUp` | `MenuItem.MoveUp` |
| `MoveDown` | `MenuItem.MoveDown` |
| `NavigateLeft` | `MenuItem.NavigateLeft` |
| `NavigateRight` | `MenuItem.NavigateRight` |
| `Close` | `MenuItem.Close` |

### MenuSeparatorWidget

| Field | ActionId String |
|-------|----------------|
| `NextMenu` | `MenuSeparator.NextMenu` |
| `PreviousMenu` | `MenuSeparator.PreviousMenu` |
| `Close` | `MenuSeparator.Close` |

### NotificationCardWidget

| Field | ActionId String |
|-------|----------------|
| `DismissAction` | `NotificationCard.Dismiss` |

### NotificationIconWidget

| Field | ActionId String |
|-------|----------------|
| `TogglePanelAction` | `NotificationIcon.TogglePanel` |

### NotificationPanelWidget

| Field | ActionId String |
|-------|----------------|
| `ToggleDrawerAction` | `NotificationPanel.ToggleDrawer` |

### PickerWidget

*No public ActionId fields — input is delegated to its inner TextBox and List widgets.*

### ScrollbarWidget

| Field | ActionId String |
|-------|----------------|
| `ScrollUpAction` | `Scrollbar.ScrollUp` |
| `ScrollDownAction` | `Scrollbar.ScrollDown` |
| `ScrollLeftAction` | `Scrollbar.ScrollLeft` |
| `ScrollRightAction` | `Scrollbar.ScrollRight` |
| `PageUpAction` | `Scrollbar.PageUp` |
| `PageDownAction` | `Scrollbar.PageDown` |
| `ScrollToTopAction` | `Scrollbar.ScrollToTop` |
| `ScrollToBottomAction` | `Scrollbar.ScrollToBottom` |
| `ScrollToStartAction` | `Scrollbar.ScrollToStart` |
| `ScrollToEndAction` | `Scrollbar.ScrollToEnd` |

### ScrollPanelWidget

| Field | ActionId String |
|-------|----------------|
| `ScrollUpAction` | `ScrollPanel.ScrollUp` |
| `ScrollDownAction` | `ScrollPanel.ScrollDown` |
| `ScrollLeftAction` | `ScrollPanel.ScrollLeft` |
| `ScrollRightAction` | `ScrollPanel.ScrollRight` |
| `PageUpAction` | `ScrollPanel.PageUp` |
| `PageDownAction` | `ScrollPanel.PageDown` |
| `ScrollToStartAction` | `ScrollPanel.ScrollToStart` |
| `ScrollToEndAction` | `ScrollPanel.ScrollToEnd` |
| `MouseScrollUpAction` | `ScrollPanel.MouseScrollUp` |
| `MouseScrollDownAction` | `ScrollPanel.MouseScrollDown` |
| `FocusNextAction` | `ScrollPanel.FocusNext` |
| `FocusPreviousAction` | `ScrollPanel.FocusPrevious` |
| `FocusFirstAction` | `ScrollPanel.FocusFirst` |

### SliderWidget

| Field | ActionId String |
|-------|----------------|
| `IncreaseSmallActionId` | `Slider.IncreaseSmall` |
| `DecreaseSmallActionId` | `Slider.DecreaseSmall` |
| `IncreaseLargeActionId` | `Slider.IncreaseLarge` |
| `DecreaseLargeActionId` | `Slider.DecreaseLarge` |
| `JumpToMinimumActionId` | `Slider.JumpToMinimum` |
| `JumpToMaximumActionId` | `Slider.JumpToMaximum` |
| `SetValueActionId` | `Slider.SetValue` |

### SplitButtonWidget

| Field | ActionId String |
|-------|----------------|
| `ActivateActionId` | `SplitButton.Activate` |
| `OpenMenuActionId` | `SplitButton.OpenMenu` |

### SplitterWidget

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `Splitter.FocusNext` |
| `FocusPreviousAction` | `Splitter.FocusPrevious` |
| `FocusFirstAction` | `Splitter.FocusFirst` |
| `ResizeUpAction` | `Splitter.ResizeUp` |
| `ResizeDownAction` | `Splitter.ResizeDown` |
| `ResizeLeftAction` | `Splitter.ResizeLeft` |
| `ResizeRightAction` | `Splitter.ResizeRight` |

### TabBarWidget

| Field | ActionId String |
|-------|----------------|
| `Click` | `TabBar.Click` |
| `ScrollLeft` | `TabBar.ScrollLeft` |
| `ScrollRight` | `TabBar.ScrollRight` |

### TabPanelWidget

| Field | ActionId String |
|-------|----------------|
| `NextTab` | `TabPanel.NextTab` |
| `PreviousTab` | `TabPanel.PreviousTab` |
| `Click` | `TabPanel.Click` |
| `NextFocusable` | `TabPanel.NextFocusable` |
| `PreviousFocusable` | `TabPanel.PreviousFocusable` |
| `ScrollTabsLeft` | `TabPanel.ScrollTabsLeft` |
| `ScrollTabsRight` | `TabPanel.ScrollTabsRight` |

### TableWidget

| Field | ActionId String |
|-------|----------------|
| `MoveUp` | `Table.MoveUp` |
| `MoveDown` | `Table.MoveDown` |
| `MoveToFirst` | `Table.MoveToFirst` |
| `MoveToLast` | `Table.MoveToLast` |
| `ExtendUp` | `Table.ExtendUp` |
| `ExtendDown` | `Table.ExtendDown` |
| `ExtendToFirst` | `Table.ExtendToFirst` |
| `ExtendToLast` | `Table.ExtendToLast` |
| `SelectAll` | `Table.SelectAll` |
| `ToggleSelection` | `Table.ToggleSelection` |
| `ClickRow` | `Table.ClickRow` |
| `PageUp` | `Table.PageUp` |
| `PageDown` | `Table.PageDown` |
| `ScrollUp` | `Table.ScrollUp` |
| `ScrollDown` | `Table.ScrollDown` |

### TextBoxWidget

| Field | ActionId String |
|-------|----------------|
| `MoveLeft` | `TextBox.MoveLeft` |
| `MoveRight` | `TextBox.MoveRight` |
| `MoveWordLeft` | `TextBox.MoveWordLeft` |
| `MoveWordRight` | `TextBox.MoveWordRight` |
| `MoveHome` | `TextBox.MoveHome` |
| `MoveEnd` | `TextBox.MoveEnd` |
| `SelectLeft` | `TextBox.SelectLeft` |
| `SelectRight` | `TextBox.SelectRight` |
| `SelectAll` | `TextBox.SelectAll` |
| `SelectToStart` | `TextBox.SelectToStart` |
| `SelectToEnd` | `TextBox.SelectToEnd` |
| `DeleteBackward` | `TextBox.DeleteBackward` |
| `DeleteForward` | `TextBox.DeleteForward` |
| `DeleteWordBackward` | `TextBox.DeleteWordBackward` |
| `DeleteWordForward` | `TextBox.DeleteWordForward` |
| `InsertText` | `TextBox.InsertText` |
| `Submit` | `TextBox.Submit` |

### ToggleSwitchWidget

| Field | ActionId String |
|-------|----------------|
| `NextOptionActionId` | `ToggleSwitch.NextOption` |
| `PreviousOptionActionId` | `ToggleSwitch.PreviousOption` |
| `SelectOptionActionId` | `ToggleSwitch.SelectOption` |

### TreeWidget

| Field | ActionId String |
|-------|----------------|
| `MoveUpActionId` | `Tree.MoveUp` |
| `MoveDownActionId` | `Tree.MoveDown` |
| `ExpandOrChildActionId` | `Tree.ExpandOrChild` |
| `CollapseOrParentActionId` | `Tree.CollapseOrParent` |
| `ToggleActionId` | `Tree.Toggle` |
| `ActivateActionId` | `Tree.Activate` |
| `SelectItemActionId` | `Tree.SelectItem` |
| `DoubleClickActivateActionId` | `Tree.DoubleClickActivate` |

### TreeItemWidget

| Field | ActionId String |
|-------|----------------|
| `ExpandActionId` | `TreeItem.Expand` |
| `CollapseActionId` | `TreeItem.Collapse` |
| `ToggleActionId` | `TreeItem.Toggle` |
| `ActivateActionId` | `TreeItem.Activate` |

### Layout Containers

The following layout containers share a common set of focus-navigation actions:

**DragBarPanelWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `DragBarPanel.FocusNext` |
| `FocusPreviousAction` | `DragBarPanel.FocusPrevious` |
| `ResizeUpAction` | `DragBarPanel.ResizeUp` |
| `ResizeDownAction` | `DragBarPanel.ResizeDown` |
| `ResizeLeftAction` | `DragBarPanel.ResizeLeft` |
| `ResizeRightAction` | `DragBarPanel.ResizeRight` |

**HStackWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `HStack.FocusNext` |
| `FocusPreviousAction` | `HStack.FocusPrevious` |

**VStackWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `VStack.FocusNext` |
| `FocusPreviousAction` | `VStack.FocusPrevious` |

**WrapPanelWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `WrapPanel.FocusNext` |
| `FocusPreviousAction` | `WrapPanel.FocusPrevious` |

**ZStackWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `ZStack.FocusNext` |
| `FocusPreviousAction` | `ZStack.FocusPrevious` |

**WindowPanelWidget**

| Field | ActionId String |
|-------|----------------|
| `FocusNextAction` | `WindowPanel.FocusNext` |
| `FocusPreviousAction` | `WindowPanel.FocusPrevious` |
| `PanUpAction` | `WindowPanel.PanUp` |
| `PanDownAction` | `WindowPanel.PanDown` |

## Next Steps

- [Input Handling Guide](/guide/input) — broader look at focus, input routing, and event handling
- [Widgets & Nodes](/guide/widgets-and-nodes) — understand the architecture behind widgets and nodes
- [Theming](/guide/theming) — customize the visual appearance of your app
