using Hex1b;
using Hex1b.Events;
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

var newItemText = "";

// Track selection via callback
int selectedIndex = 0;

IReadOnlyList<string> GetListItems() =>
    items.Select(item => FormatTodoItem(item)).ToList();

void OnSelectionChanged(ListSelectionChangedEventArgs args)
{
    selectedIndex = args.SelectedIndex;
}

void ToggleSelected()
{
    if (selectedIndex >= 0 && selectedIndex < items.Count)
    {
        items[selectedIndex] = items[selectedIndex] with 
        { 
            IsComplete = !items[selectedIndex].IsComplete 
        };
    }
}

void AddItem()
{
    if (!string.IsNullOrWhiteSpace(newItemText))
    {
        items.Add(new TodoItem(newItemText, false));
        newItemText = "";
    }
}

void DeleteSelected()
{
    if (selectedIndex >= 0 && selectedIndex < items.Count)
    {
        items.RemoveAt(selectedIndex);
        if (selectedIndex >= items.Count && items.Count > 0)
        {
            selectedIndex = items.Count - 1;
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
using var app = new Hex1bApp(
    builder: ctx =>
    {
        var listItems = GetListItems();
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
                            new ListWidget(listItems) { OnSelectionChanged = args => { OnSelectionChanged(args); return Task.CompletedTask; }, OnItemActivated = _ => { ToggleSelected(); return Task.CompletedTask; } },
                            new TextBlockWidget(""),
                            new TextBlockWidget("↑↓ Nav  Space: Toggle  Del: Remove")
                        ]), "Tasks") { WidthHint = SizeHint.Weighted(2) },
                        
                        // Right: Add + Stats combined
                        new VStackWidget([
                            new BorderWidget(new VStackWidget([
                                new TextBlockWidget("➕ Add Task"),
                                new TextBoxWidget(newItemText) { OnTextChanged = args => { newItemText = args.NewText; return Task.CompletedTask; } },
                                new ButtonWidget("Add") { OnClick = _ => { AddItem(); return Task.CompletedTask; } }
                            ]), "New"),
                            new BorderWidget(new VStackWidget([
                                new TextBlockWidget($"Total: {totalCount} items"),
                                new TextBlockWidget($"Done:  {completedCount} ✓"),
                                new TextBlockWidget($"Todo:  {todoCount} ○"),
                                new TextBlockWidget(""),
                                new TextBlockWidget(GetProgressBar())
                            ]), "Stats") { HeightHint = SizeHint.Fill },
                            new ButtonWidget("Quit") { OnClick = _ => { cts.Cancel(); return Task.CompletedTask; } }
                        ]) { WidthHint = SizeHint.Weighted(1) }
                    ])),

                // Compact layout (< 100 cols): Single column
                new ConditionalWidget((w, h) => true,
                    new VStackWidget([
                        new TextBlockWidget($"📋 Todo [{completedCount}/{totalCount}]"),
                        new TextBlockWidget(GetProgressBar()),
                        new TextBlockWidget(""),
                        new ListWidget(listItems) { HeightHint = SizeHint.Fill, OnSelectionChanged = args => { OnSelectionChanged(args); return Task.CompletedTask; }, OnItemActivated = _ => { ToggleSelected(); return Task.CompletedTask; } },
                        new TextBlockWidget(""),
                        new HStackWidget([
                            new TextBoxWidget(newItemText) { WidthHint = SizeHint.Fill, OnTextChanged = args => { newItemText = args.NewText; return Task.CompletedTask; } },
                            new ButtonWidget("[+]") { OnClick = _ => { AddItem(); return Task.CompletedTask; } }
                        ]),
                        new TextBlockWidget(""),
                        new ButtonWidget("Quit") { OnClick = _ => { cts.Cancel(); return Task.CompletedTask; } },
                        new TextBlockWidget(""),
                        new TextBlockWidget("↑↓:Move  Space:Toggle  Del:Remove  Tab:Focus")
                    ]))
            ]),
            $"Hex1b Todo ({terminalSize})"
        ).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Delete).Action(_ => DeleteSelected(), "Delete selected item");
        });
    }
);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
await app.RunAsync(cts.Token);

// Record must be at the end for top-level statements
record TodoItem(string Title, bool IsComplete);
