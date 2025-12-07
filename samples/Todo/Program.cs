using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

// State stored OUTSIDE the builder so it persists across renders
var items = new List<TodoItem>
{
    new("Buy groceries", true),
    new("Review pull request", false),
    new("Write documentation", false),
    new("Fix bug #42", true),
    new("Deploy to staging", false),
    new("Team standup meeting", true),
};

var newItemInput = new TextBoxState();

ListState listState = null!;
listState = new ListState
{
    OnItemActivated = _ => ToggleSelected()
};

void ToggleSelected()
{
    if (listState.SelectedIndex >= 0 && listState.SelectedIndex < items.Count)
    {
        items[listState.SelectedIndex] = items[listState.SelectedIndex] with 
        { 
            IsComplete = !items[listState.SelectedIndex].IsComplete 
        };
    }
}

void AddItem()
{
    if (!string.IsNullOrWhiteSpace(newItemInput.Text))
    {
        items.Add(new TodoItem(newItemInput.Text, false));
        newItemInput.Text = "";
        newItemInput.CursorPosition = 0;
    }
}

void DeleteSelected()
{
    if (listState.SelectedIndex >= 0 && listState.SelectedIndex < items.Count)
    {
        items.RemoveAt(listState.SelectedIndex);
        if (listState.SelectedIndex >= items.Count && items.Count > 0)
        {
            listState.SelectedIndex = items.Count - 1;
        }
    }
}

string FormatTodoItem(TodoItem item)
{
    var check = item.IsComplete ? "✓" : "○";
    return $" [{check}] {item.Title}";
}

string GetProgressBar()
{
    if (items.Count == 0) return "[          ] 0%";
    
    var percent = items.Count(i => i.IsComplete) * 100 / items.Count;
    var filled = percent / 10;
    var bar = new string('█', filled) + new string('░', 10 - filled);
    return $"[{bar}] {percent}%";
}

using var cts = new CancellationTokenSource();

// Responsive todo list demonstrating Hex1b basics
using var app = new Hex1bApp<object>(
    state: new object(),
    builder: ctx =>
    {
        // Update list items each render
        listState.Items = items.Select((item, idx) => 
            new ListItem(idx.ToString(), FormatTodoItem(item))).ToList();

        var terminalSize = $"{Console.WindowWidth}x{Console.WindowHeight}";
        var completedCount = items.Count(i => i.IsComplete);
        var totalCount = items.Count;
        var todoCount = totalCount - completedCount;

        // Build responsive layout that adapts to available width
        return new BorderWidget(
            new ResponsiveWidget([
                // Wide layout (100+ cols): Two columns with details sidebar
                new ConditionalWidget((w, h) => w >= 100,
                    new HStackWidget([
                        // Left: Todo list
                        new BorderWidget(new VStackWidget([
                            new TextBlockWidget("📋 Todo Items"),
                            new TextBlockWidget(""),
                            new ListWidget(listState),
                            new TextBlockWidget(""),
                            new TextBlockWidget("↑↓ Nav  Space: Toggle  Del: Remove")
                        ]), "Tasks"),
                        
                        // Right: Add + Stats combined
                        new VStackWidget([
                            new BorderWidget(new VStackWidget([
                                new TextBlockWidget("➕ Add Task"),
                                new TextBoxWidget(newItemInput),
                                new ButtonWidget("Add", AddItem)
                            ]), "New"),
                            new BorderWidget(new VStackWidget([
                                new TextBlockWidget($"Total: {totalCount} items"),
                                new TextBlockWidget($"Done:  {completedCount} ✓"),
                                new TextBlockWidget($"Todo:  {todoCount} ○"),
                                new TextBlockWidget(""),
                                new TextBlockWidget(GetProgressBar())
                            ]), "Stats"),
                            new ButtonWidget("Quit", () => cts.Cancel())
                        ], [SizeHint.Content, SizeHint.Fill, SizeHint.Content])
                    ], [SizeHint.Weighted(2), SizeHint.Weighted(1)])),

                // Compact layout (< 100 cols): Single column
                new ConditionalWidget((w, h) => true,
                    new VStackWidget([
                        new TextBlockWidget($"📋 Todo [{completedCount}/{totalCount}]"),
                        new TextBlockWidget(GetProgressBar()),
                        new TextBlockWidget(""),
                        new ListWidget(listState),
                        new TextBlockWidget(""),
                        new HStackWidget([
                            new TextBoxWidget(newItemInput),
                            new ButtonWidget("[+]", AddItem)
                        ], [SizeHint.Fill, SizeHint.Content]),
                        new TextBlockWidget(""),
                        new ButtonWidget("Quit", () => cts.Cancel()),
                        new TextBlockWidget(""),
                        new TextBlockWidget("↑↓:Move  Space:Toggle  Del:Remove  Tab:Focus")
                    ], [SizeHint.Content, SizeHint.Content, SizeHint.Content, SizeHint.Fill, 
                        SizeHint.Content, SizeHint.Content, SizeHint.Content, SizeHint.Content, 
                        SizeHint.Content, SizeHint.Content]))
            ]),
            $"Hex1b Todo ({terminalSize})"
        ) with { Shortcuts = [new Shortcut(KeyBinding.Plain(ConsoleKey.Delete), DeleteSelected, "Delete selected item")] };
    }
);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
await app.RunAsync(cts.Token);

// Record must be at the end for top-level statements
record TodoItem(string Title, bool IsComplete);
