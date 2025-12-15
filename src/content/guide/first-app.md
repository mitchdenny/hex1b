# Your First App

Let's build a simple todo list application to learn Hex1b's key concepts.

## The Goal

We'll create an app that:
- Displays a list of todos
- Lets you add new items
- Lets you toggle items complete
- Shows a count of remaining items

## Step 1: Define Your State

First, define the data your app needs:

```csharp
public record TodoItem(string Text, bool IsComplete);

public record AppState(
    List<TodoItem> Items,
    string NewItemText
);
```

## Step 2: Create the App

```csharp
using Hex1b;

var initialState = new AppState(
    Items: [
        new TodoItem("Learn Hex1b", false),
        new TodoItem("Build something cool", false)
    ],
    NewItemText: ""
);

var app = new Hex1bApp<AppState>(
    initialState,
    buildWidget: (ctx, ct) => BuildTodoApp(ctx)
);

await app.RunAsync();
```

## Step 3: Build the UI

```csharp
Hex1bWidget BuildTodoApp(WidgetContext<AppState> ctx)
{
    var state = ctx.State;
    var remaining = state.Items.Count(i => !i.IsComplete);
    
    return new BorderWidget(
        new VStackWidget([
            // Header
            new TextBlockWidget($"📝 Todo List ({remaining} remaining)")
                .Bold(),
            
            // Input for new items
            new HStackWidget([
                new TextBoxWidget(
                    value: state.NewItemText,
                    onChange: text => ctx.SetState(state with { NewItemText = text })
                ).Fill(),
                new ButtonWidget("Add", () => AddItem(ctx))
            ]),
            
            // Todo list
            new ListWidget(
                items: state.Items.Select((item, i) => 
                    new HStackWidget([
                        new TextBlockWidget(item.IsComplete ? "☑" : "☐"),
                        new TextBlockWidget(item.Text)
                            .Strikethrough(item.IsComplete)
                    ])
                ).ToArray(),
                onSelect: index => ToggleItem(ctx, index)
            ).Fill()
        ])
    ).Title("My Todos");
}

void AddItem(WidgetContext<AppState> ctx)
{
    var state = ctx.State;
    if (string.IsNullOrWhiteSpace(state.NewItemText)) return;
    
    ctx.SetState(state with {
        Items = [..state.Items, new TodoItem(state.NewItemText, false)],
        NewItemText = ""
    });
}

void ToggleItem(WidgetContext<AppState> ctx, int index)
{
    var state = ctx.State;
    var items = state.Items.ToList();
    var item = items[index];
    items[index] = item with { IsComplete = !item.IsComplete };
    ctx.SetState(state with { Items = items });
}
```

## Step 4: Run It

```bash
dotnet run
```

You should see a fully interactive todo list!

## Key Concepts Demonstrated

| Concept | Example |
|---------|---------|
| **State** | `AppState` record holds all app data |
| **SetState** | Calling `ctx.SetState()` triggers a re-render |
| **Composition** | Widgets nested inside widgets |
| **Layout** | `VStackWidget` and `HStackWidget` for arrangement |
| **Input** | `TextBoxWidget` for text, `ButtonWidget` for actions |
| **Lists** | `ListWidget` for scrollable, selectable lists |

## Live Demo

<TerminalDemo exhibit="responsive-todo" title="Todo App" />

## Next Steps

- [Widgets & Nodes](/guide/widgets-and-nodes) - Understand how rendering works
- [Layout System](/guide/layout) - Master the constraint-based layout
- [Input Handling](/guide/input) - Learn about keyboard and focus
