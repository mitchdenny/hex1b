<script setup>
const basicCode = `using Hex1b;

var state = new EditorState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.VStack(v => [
        v.Text("Split Button Demo"),
        v.Text(""),
        v.Text($"Last action: {state.LastAction}"),
        v.Text(""),
        v.SplitButton()
           .PrimaryAction("Save", _ => state.LastAction = "Saved file")
           .SecondaryAction("Save As...", _ => state.LastAction = "Save As dialog")
           .SecondaryAction("Save All", _ => state.LastAction = "Saved all files")
           .SecondaryAction("Save Copy", _ => state.LastAction = "Saved copy"),
        v.Text(""),
        v.Text("Click the button or press ▼ for more options")
    ]))
    .Build();

await terminal.RunAsync();

class EditorState
{
    public string LastAction { get; set; } = "(none)";
}`

const multipleCode = `using Hex1b;

var state = new TaskState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Text($"Task: {state.TaskName}"),
            v.Text($"Priority: {state.Priority}"),
            v.Text(""),
            v.HStack(h => [
                h.SplitButton()
                   .PrimaryAction("Create Task", _ => state.TaskName = "New Task")
                   .SecondaryAction("From Template", _ => state.TaskName = "Template Task")
                   .SecondaryAction("Duplicate Last", _ => state.TaskName = "Duplicated Task"),
                h.Text(" "),
                h.SplitButton()
                   .PrimaryAction("Set Priority", _ => state.Priority = "Normal")
                   .SecondaryAction("Low", _ => state.Priority = "Low")
                   .SecondaryAction("High", _ => state.Priority = "High")
                   .SecondaryAction("Urgent", _ => state.Priority = "Urgent")
            ])
        ])
    ], title: "Task Manager"))
    .Build();

await terminal.RunAsync();

class TaskState
{
    public string TaskName { get; set; } = "(none)";
    public string Priority { get; set; } = "Normal";
}`
</script>

# SplitButton

A split button combines a primary action with a dropdown menu of secondary actions. The main button area triggers the default action, while a dropdown arrow reveals additional options.

Split buttons are ideal when you have a common default action but need to expose related alternatives without cluttering your UI with multiple buttons.

## Basic Usage

Create split buttons using `SplitButton()`, then chain `PrimaryAction()` and `SecondaryAction()` methods:

<CodeBlock lang="csharp" :code="basicCode" command="dotnet run" example="split-button-basic" exampleTitle="Split Button - Basic Usage" />

::: tip Interaction
- **Click the label** or press **Enter** to trigger the primary action
- **Click the arrow (▼)** or press **Down** to open the dropdown menu
- **Escape** closes the dropdown without selecting
:::

## How It Works

The split button renders as `[ Label ▼ ]` with two distinct click regions:

| Region | Mouse Action | Keyboard Action |
|--------|--------------|-----------------|
| Label area | Triggers primary action | Enter or Space |
| Arrow (▼) | Opens dropdown menu | Down arrow |

When the dropdown is open:
- **Up/Down arrows** navigate between options
- **Enter** selects the highlighted option
- **Escape** closes without selecting

## Event Handlers

### Primary Action

Use `PrimaryAction()` to set the label and handler for the main button:

```csharp
ctx.SplitButton()
   .PrimaryAction("Run", e => RunDefaultCommand())
```

The handler receives `SplitButtonClickedEventArgs` with:
- `Widget` - The source SplitButtonWidget
- `Node` - The underlying SplitButtonNode  
- `Context` - Access to notifications, focus, popups, and app services

### Secondary Actions

Add dropdown menu items with `SecondaryAction()`:

```csharp
ctx.SplitButton()
   .PrimaryAction("Run", _ => RunDefault())
   .SecondaryAction("Run with Debugger", _ => RunDebug())
   .SecondaryAction("Run Tests", _ => RunTests())
   .SecondaryAction("Run Benchmarks", _ => RunBenchmarks())
```

Actions appear in the dropdown in the order they're added.

### Async Handlers

Both primary and secondary actions support async handlers:

```csharp
ctx.SplitButton()
   .PrimaryAction("Deploy", async e => {
       await DeployToProductionAsync();
       e.Context.Notifications.Post(
           new Notification("Deployed", "Successfully deployed to production"));
   })
   .SecondaryAction("Deploy to Staging", async e => {
       await DeployToStagingAsync();
   })
```

### Dropdown Opened Callback

Use `OnDropdownOpened()` to react when the dropdown menu opens:

```csharp
ctx.SplitButton()
   .PrimaryAction("Options", _ => { })
   .OnDropdownOpened(() => Analytics.Track("dropdown_opened"))
   .SecondaryAction("Option A", _ => { })
   .SecondaryAction("Option B", _ => { })
```

This is useful for:
- Analytics tracking
- Canceling timeouts (e.g., on notification cards)
- Lazy-loading menu content

## Multiple Split Buttons

You can use multiple split buttons together for complex toolbars:

<CodeBlock lang="csharp" :code="multipleCode" command="dotnet run" example="split-button-multiple" exampleTitle="Split Button - Multiple Buttons" />

## Use Cases

Split buttons work well for:

| Scenario | Example |
|----------|---------|
| File operations | Save / Save As / Save All |
| Deployment | Deploy / Deploy to Staging / Rollback |
| Creation actions | New File / From Template / Duplicate |
| Run commands | Run / Debug / Profile |
| Export options | Export / Export as PDF / Export as CSV |

## Keyboard Navigation

| Key | Action |
|-----|--------|
| Tab | Focus the split button |
| Enter / Space | Trigger primary action |
| Down | Open dropdown menu |
| Up / Down (in menu) | Navigate options |
| Enter (in menu) | Select option |
| Escape | Close dropdown |

## Related Widgets

- [Button](/guide/widgets/button) - Simple single-action buttons
- [Picker](/guide/widgets/picker) - Dropdown selection without primary action
- [List](/guide/widgets/list) - Scrollable item selection
