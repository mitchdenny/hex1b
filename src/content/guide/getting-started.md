# Getting Started

Hex1b is a .NET library for building terminal user interfaces (TUI) with a React-inspired declarative API.

## Prerequisites

- .NET 8.0 or later
- A terminal that supports ANSI escape sequences (most modern terminals do)

## Create a New Console Application

First, create a new console application:

```bash
dotnet new console -n MyTodoApp
cd MyTodoApp
```

## Install Hex1b

Add the Hex1b package to your project:

```bash
dotnet add package Hex1b
```

## Step 1: Hello World

Let's start with a simple "Hello World" example. Replace the contents of `Program.cs`:

```csharp
using Hex1b;

var app = new Hex1bApp(ctx => ctx.Text("Hello, Hex1b!"));
await app.RunAsync();
```

Run your app:

```bash
dotnet run
```

Press `Ctrl+C` to exit.

<TerminalDemo example="getting-started-step1" title="Step 1: Hello World" />

The fluent API uses the `ctx` (context) parameter to create widgets. In this case, `ctx.Text()` creates a text widget.

## Step 2: Adding State and Interactivity

Now let's add a button and some state to track clicks:

```csharp
using Hex1b;

// Define a simple state class to hold our counter
class CounterState
{
    public int Count { get; set; }
}

var state = new CounterState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text($"Button pressed {state.Count} times"),
        b.Text(""),
        b.Button("Click me!", _ => state.Count++)
    ], title: "Counter Demo")
);

await app.RunAsync();
```

<TerminalDemo example="getting-started-step2" title="Step 2: Interactive Counter" />

Key concepts:
- **State**: We created a `CounterState` class to hold mutable data
- **Border**: The `ctx.Border()` creates a widget with a border and optional title
- **Collection expressions**: `[...]` syntax creates an array of child widgets
- **Button**: The `ctx.Button()` creates an interactive button with a click handler

## Step 3: Building a Todo List

Let's build a simple todo list that displays items and lets you toggle them:

```csharp
using Hex1b;

class TodoState
{
    public List<(string Text, bool Done)> Items { get; } = 
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false),
        ("Deploy to production", false)
    ];

    public IReadOnlyList<string> FormatItems() =>
        Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void ToggleItem(int index)
    {
        var item = Items[index];
        Items[index] = (item.Text, !item.Done);
    }
}

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text("📋 My Todos"),
        b.Text(""),
        b.List(state.FormatItems(), e => state.ToggleItem(e.ActivatedIndex)),
        b.Text(""),
        b.Text("↑↓ Navigate  Space: Toggle")
    ], title: "Todo List")
);

await app.RunAsync();
```

<TerminalDemo example="getting-started-step3" title="Step 3: Simple Todo List" />

New concepts:
- **List widget**: `ctx.List()` creates a scrollable, selectable list
- **Event handlers**: The second parameter to `List()` is called when an item is activated (Space/Enter)
- **Navigation**: Use arrow keys to navigate, Space or Enter to toggle items

## Step 4: Adding New Items

Let's add the ability to create new todo items with a text input:

```csharp
using Hex1b;
using Hex1b.Widgets;

class TodoState
{
    public List<(string Text, bool Done)> Items { get; } = 
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false)
    ];

    public string NewItemText { get; set; } = "";

    public IReadOnlyList<string> FormatItems() =>
        Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void AddItem()
    {
        if (!string.IsNullOrWhiteSpace(NewItemText))
        {
            Items.Add((NewItemText, false));
            NewItemText = "";
        }
    }

    public void ToggleItem(int index)
    {
        var item = Items[index];
        Items[index] = (item.Text, !item.Done);
    }
}

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.HStack(h => [
            h.Text("New task: "),
            h.TextBox(state.NewItemText, e => state.NewItemText = e.NewText),
            h.Button("Add", _ => state.AddItem())
        ]),
        new SeparatorWidget(),
        b.List(state.FormatItems(), e => state.ToggleItem(e.ActivatedIndex)),
        b.Text(""),
        b.Text("Tab: Focus next  Space: Toggle")
    ], title: "📋 Todo")
);

await app.RunAsync();
```

<TerminalDemo example="getting-started-step4" title="Step 4: Todo with Input" />

New concepts:
- **HStack**: `ctx.HStack()` arranges children horizontally
- **TextBox**: `ctx.TextBox()` creates an editable text input
- **Separator**: `new SeparatorWidget()` creates a horizontal line divider
- **Focus**: Use Tab to move focus between interactive widgets

## Step 5: Complete Todo Application

Let's add a few more features to complete our todo app:

```csharp
using Hex1b;
using Hex1b.Widgets;

class TodoState
{
    public List<(string Text, bool Done)> Items { get; } = 
    [
        ("Learn Hex1b", true),
        ("Build a TUI", false),
        ("Deploy to production", false)
    ];

    public string NewItemText { get; set; } = "";
    public int SelectedIndex { get; set; }

    public int RemainingCount => Items.Count(i => !i.Done);

    public IReadOnlyList<string> FormatItems() =>
        Items.Select(i => $"[{(i.Done ? "✓" : " ")}] {i.Text}").ToList();

    public void AddItem()
    {
        if (!string.IsNullOrWhiteSpace(NewItemText))
        {
            Items.Add((NewItemText, false));
            NewItemText = "";
        }
    }

    public void ToggleItem(int index)
    {
        var item = Items[index];
        Items[index] = (item.Text, !item.Done);
    }

    public void DeleteItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items.RemoveAt(index);
            if (SelectedIndex >= Items.Count && Items.Count > 0)
            {
                SelectedIndex = Items.Count - 1;
            }
        }
    }
}

var state = new TodoState();

var app = new Hex1bApp(ctx =>
    ctx.Border(b => [
        b.Text($"📋 Todo List ({state.RemainingCount} remaining)"),
        b.Text(""),
        b.HStack(h => [
            h.Text("New: "),
            h.TextBox(state.NewItemText, e => state.NewItemText = e.NewText),
            h.Button("Add", _ => state.AddItem())
        ]),
        new SeparatorWidget(),
        b.List(
            state.FormatItems(),
            e => state.SelectedIndex = e.SelectedIndex,
            e => state.ToggleItem(e.ActivatedIndex)
        ),
        b.Text(""),
        b.Button("Delete Selected", _ => state.DeleteItem(state.SelectedIndex)),
        b.Text(""),
        b.Text("↑↓: Navigate  Space: Toggle  Tab: Focus  Del: Delete")
    ], title: "My Todos")
);

await app.RunAsync();
```

<TerminalDemo example="getting-started-step5" title="Step 5: Complete Todo App" />

Final features:
- **Remaining count**: Shows how many items are not yet complete
- **Selection tracking**: Tracks which list item is selected
- **Delete functionality**: Removes the selected item
- **Two event handlers**: List widget uses both selection change and item activation callbacks

## What You've Learned

Through this tutorial, you've learned:

✅ How to create a console application and install Hex1b  
✅ The fluent API using context (`ctx`) to build widgets  
✅ State management with simple classes  
✅ Layout with `Border`, `VStack`, and `HStack`  
✅ Interactive widgets: `Button`, `TextBox`, `List`  
✅ Event handling for user input  
✅ Collection expressions for composing UIs  

## Next Steps

- [Widgets & Nodes](/guide/widgets-and-nodes) - Understand the core architecture
- [Layout System](/guide/layout) - Master the constraint-based layout
- [Input Handling](/guide/input) - Learn about keyboard shortcuts and focus
- [Theming](/guide/theming) - Customize the appearance of your app
