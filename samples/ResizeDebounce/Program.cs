using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hex1b;
using Hex1b.Terminal;
using Hex1b.Widgets;

// Responsive TODO app demonstrating the ResizeDebounceFilter
// which suppresses output during rapid resize to prevent lag

Console.OutputEncoding = Encoding.UTF8;

var presentation = new ConsolePresentationAdapter();
var workload = new Hex1bAppWorkloadAdapter(presentation.Capabilities);

// Create terminal with the resize debounce filter
var options = new Hex1bTerminalOptions
{
    PresentationAdapter = presentation,
    WorkloadAdapter = workload
};

// Add the resize debounce filter - this suppresses output during rapid resize
// and only allows output through once the resize settles (after 50ms)
options.PresentationFilters.Add(new ResizeDebounceFilter(debounceMs: 50));

using var terminal = new Hex1bTerminal(options);

var state = new TodoState();

await using var app = new Hex1bApp(
    ctx =>
    {
        var listItems = state.GetListItems();

        // Build responsive layout that adapts to available width
        return ctx.Responsive(r => [
            // Extra wide layout (150+ cols): Three columns with stats
            r.WhenMinWidth(150, r => BuildExtraWideLayout(r, state, listItems)),
            
            // Wide layout (110+ cols): Two columns with details sidebar
            r.WhenMinWidth(110, r => BuildWideLayout(r, state, listItems)),
            
            // Medium layout (70+ cols): Single column with full details
            r.WhenMinWidth(70, r => BuildMediumLayout(r, state, listItems)),
            
            // Compact layout (< 70 cols): Minimal single column
            r.Otherwise(r => BuildCompactLayout(r, state, listItems))
        ]);
    },
    new Hex1bAppOptions
    {
        WorkloadAdapter = workload
    }
);

await app.RunAsync();

// === Layout builders ===

static Hex1bWidget BuildExtraWideLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
{
    var completedCount = state.Items.Count(i => i.IsComplete);
    var totalCount = state.Items.Count;
    var todoCount = totalCount - completedCount;
    
    return ctx.HStack(h => [
        // Left: Todo list
        h.Border(b => [
            b.Text("📋 Todo Items"),
            b.Text(""),
            b.List(listItems)
                .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                .OnItemActivated(_ => state.ToggleSelected()),
            b.Text(""),
            b.Text("↑↓ Navigate  Space: Toggle")
        ], title: "Tasks").FillWidth(2),
        
        // Middle: Add new item
        h.Border(b => [
            b.Text("➕ Add New Task"),
            b.Text(""),
            b.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
            b.Text(""),
            b.Button("Add Task").OnClick(_ => state.AddItem()),
            b.Text(""),
            b.Text("Type and click Add")
        ], title: "New Task").FillWidth(1),
        
        // Right: Statistics
        h.Border(b => [
            b.Text("📊 Statistics"),
            b.Text(""),
            b.Text($"Total: {totalCount} items"),
            b.Text($"Done:  {completedCount} ✓"),
            b.Text($"Todo:  {todoCount} ○"),
            b.Text(""),
            b.Text($"Progress: {GetProgressBar(state)}")
        ], title: "Stats").FillWidth(1)
    ]);
}

static Hex1bWidget BuildWideLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
{
    var completedCount = state.Items.Count(i => i.IsComplete);
    var totalCount = state.Items.Count;
    
    return ctx.HStack(h => [
        // Left: Todo list
        h.Border(b => [
            b.Text("📋 Todo Items"),
            b.Text(""),
            b.List(listItems)
                .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                .OnItemActivated(_ => state.ToggleSelected()),
            b.Text(""),
            b.Text("↑↓ Nav  Space: Toggle")
        ], title: "Tasks").FillWidth(2),
        
        // Right: Add + Stats combined
        h.VStack(v => [
            v.Border(b => [
                b.Text("➕ Add Task"),
                b.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
                b.Button("Add").OnClick(_ => state.AddItem())
            ], title: "New"),
            v.Border(b => [
                b.Text($"Done: {completedCount}/{totalCount}"),
                b.Text(GetProgressBar(state))
            ], title: "Progress").FillHeight()
        ]).FillWidth(1)
    ]);
}

static Hex1bWidget BuildMediumLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
{
    var completedCount = state.Items.Count(i => i.IsComplete);
    var totalCount = state.Items.Count;
    
    return ctx.VStack(v => [
        v.Border(b => [
            b.Text("📋 Responsive Todo List"),
            b.Text($"[{completedCount}/{totalCount} complete]")
        ], title: "Todo"),
        
        v.Border(b => [
            b.List(listItems)
                .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
                .OnItemActivated(_ => state.ToggleSelected())
        ], title: "Items").FillHeight(),
        
        v.HStack(h => [
            h.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText).FillWidth(),
            h.Button("[+]").OnClick(_ => state.AddItem())
        ]),
        
        v.Text("↑↓:Move  Space:Toggle  Tab:Focus")
    ]);
}

static Hex1bWidget BuildCompactLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
{
    var completedCount = state.Items.Count(i => i.IsComplete);
    var totalCount = state.Items.Count;
    
    return ctx.VStack(v => [
        v.Text($"Todo [{completedCount}/{totalCount}]"),
        v.Text("────────────────────"),
        v.List(listItems)
            .OnSelectionChanged(e => state.SelectedIndex = e.SelectedIndex)
            .OnItemActivated(_ => state.ToggleSelected())
            .FillHeight(),
        v.Text("────────────────────"),
        v.TextBox(state.NewItemText).OnTextChanged(args => state.NewItemText = args.NewText),
        v.Button("+ Add").OnClick(_ => state.AddItem())
    ]);
}

static string GetProgressBar(TodoState state)
{
    if (state.Items.Count == 0) return "[          ] 0%";
    
    var percent = state.Items.Count(i => i.IsComplete) * 100 / state.Items.Count;
    var filled = percent / 10;
    var bar = new string('█', filled) + new string('░', 10 - filled);
    return $"[{bar}] {percent}%";
}

// === State classes ===

record TodoItem(string Title, bool IsComplete);

class TodoState
{
    public List<TodoItem> Items { get; } =
    [
        new("Buy groceries", true),
        new("Review pull request", false),
        new("Write documentation", false),
        new("Fix bug #42", true),
        new("Deploy to staging", false),
        new("Team standup meeting", true),
        new("Update dependencies", false),
        new("Code review", false),
    ];

    public int SelectedIndex { get; set; }
    public string NewItemText { get; set; } = "";

    public void AddItem()
    {
        if (!string.IsNullOrWhiteSpace(NewItemText))
        {
            Items.Add(new TodoItem(NewItemText, false));
            NewItemText = "";
        }
    }

    public void ToggleSelected()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
        {
            Items[SelectedIndex] = Items[SelectedIndex] with 
            { 
                IsComplete = !Items[SelectedIndex].IsComplete 
            };
        }
    }

    public IReadOnlyList<string> GetListItems() =>
        Items.Select(item => FormatTodoItem(item)).ToList();

    private static string FormatTodoItem(TodoItem item)
    {
        var check = item.IsComplete ? "✓" : "○";
        return $" [{check}] {item.Title}";
    }
}

