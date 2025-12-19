using System.Runtime.Versioning;
using BrowserWasm;
using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

// ═══════════════════════════════════════════════════════════════════════════
// This is a standard Hex1b application!
// The only WASM-specific code is the BrowserTerminal passed to Hex1bAppOptions.
// All the JS interop complexity is hidden in BrowserTerminal.cs
// ═══════════════════════════════════════════════════════════════════════════

// Application state - just like any other Hex1b app
var tasks = new List<TodoItem>
{
    new("Review pull request", true),
    new("Write documentation", false),
    new("Deploy to staging", false),
    new("Team standup meeting", true),
    new("Fix critical bug", false),
    new("Update dependencies", false),
};

var newTaskText = "";
var selectedIndex = 0;

void OnSelectionChanged(ListSelectionChangedEventArgs args) =>
    selectedIndex = args.SelectedIndex;

void ToggleSelected()
{
    if (selectedIndex >= 0 && selectedIndex < tasks.Count)
    {
        tasks[selectedIndex] = tasks[selectedIndex] with
        {
            IsComplete = !tasks[selectedIndex].IsComplete
        };
    }
}

void AddTask()
{
    if (!string.IsNullOrWhiteSpace(newTaskText))
    {
        tasks.Add(new TodoItem(newTaskText, false));
        newTaskText = "";
    }
}

void DeleteSelected()
{
    if (selectedIndex >= 0 && selectedIndex < tasks.Count)
    {
        tasks.RemoveAt(selectedIndex);
        if (selectedIndex >= tasks.Count && tasks.Count > 0)
        {
            selectedIndex = tasks.Count - 1;
        }
    }
}

void ResetAll()
{
    for (int i = 0; i < tasks.Count; i++)
        tasks[i] = tasks[i] with { IsComplete = false };
}

string FormatTask(TodoItem item)
{
    var check = item.IsComplete ? "✓" : "○";
    return $" [{check}] {item.Title}";
}

string GetProgressBar(int width)
{
    if (tasks.Count == 0) return "[          ] 0%";

    var percent = tasks.Count(i => i.IsComplete) * 100 / tasks.Count;
    var barWidth = Math.Max(10, width - 10);
    var filled = barWidth * percent / 100;
    var bar = new string('█', filled) + new string('░', barWidth - filled);
    return $"[{bar}] {percent}%";
}

// Create the browser terminal - this is the only WASM-specific part!
var terminal = new BrowserTerminal();

// Build the app with our terminal
using var app = new Hex1bApp(
    builder: ctx =>
    {
        var width = terminal.Width;
        var height = terminal.Height;
        var listItems = tasks.Select(FormatTask).ToList();
        var completedCount = tasks.Count(t => t.IsComplete);
        var totalCount = tasks.Count;

        // Build responsive layout - identical pattern to console apps
        return new BorderWidget(
            new ResponsiveWidget([
                // Wide layout (100+ cols): Three columns
                new ConditionalWidget((w, h) => w >= 100,
                    new HStackWidget([
                        // Left: Task list
                        new BorderWidget(new VStackWidget([
                            new TextBlockWidget("📋 Tasks"),
                            new TextBlockWidget(""),
                            new ListWidget(listItems)
                            {
                                HeightHint = SizeHint.Fill,
                                OnSelectionChanged = args => { OnSelectionChanged(args); return Task.CompletedTask; },
                                OnItemActivated = _ => { ToggleSelected(); return Task.CompletedTask; }
                            },
                            new TextBlockWidget(""),
                            new TextBlockWidget("↑↓ Nav  Space: Toggle  Del: Remove")
                        ]), "Tasks") { WidthHint = SizeHint.Weighted(2) },

                        // Middle: Add task
                        new BorderWidget(new VStackWidget([
                            new TextBlockWidget("➕ New Task"),
                            new TextBlockWidget(""),
                            new TextBoxWidget(newTaskText)
                            {
                                OnTextChanged = args => { newTaskText = args.NewText; return Task.CompletedTask; }
                            },
                            new TextBlockWidget(""),
                            new ButtonWidget("[ Add Task ]")
                            {
                                OnClick = _ => { AddTask(); return Task.CompletedTask; }
                            },
                            new TextBlockWidget(""),
                            new TextBlockWidget("Enter: Add task")
                        ]), "Add") { WidthHint = SizeHint.Weighted(1) },

                        // Right: Stats
                        new BorderWidget(new VStackWidget([
                            new TextBlockWidget("📊 Statistics"),
                            new TextBlockWidget(""),
                            new TextBlockWidget($"✓ Done:    {completedCount}"),
                            new TextBlockWidget($"○ Pending: {totalCount - completedCount}"),
                            new TextBlockWidget($"∑ Total:   {totalCount}"),
                            new TextBlockWidget(""),
                            new TextBlockWidget(GetProgressBar(25)),
                            new TextBlockWidget(""),
                            new ButtonWidget("[ Reset All ]")
                            {
                                OnClick = _ => { ResetAll(); return Task.CompletedTask; }
                            }
                        ]), "Stats") { WidthHint = SizeHint.Weighted(1) }
                    ])),

                // Medium layout (60-99 cols): Two columns
                new ConditionalWidget((w, h) => w >= 60,
                    new HStackWidget([
                        new BorderWidget(new VStackWidget([
                            new TextBlockWidget($"📋 Tasks [{completedCount}/{totalCount}]"),
                            new ListWidget(listItems)
                            {
                                HeightHint = SizeHint.Fill,
                                OnSelectionChanged = args => { OnSelectionChanged(args); return Task.CompletedTask; },
                                OnItemActivated = _ => { ToggleSelected(); return Task.CompletedTask; }
                            }
                        ]), "Tasks") { WidthHint = SizeHint.Weighted(2) },
                        new VStackWidget([
                            new BorderWidget(new VStackWidget([
                                new TextBoxWidget(newTaskText)
                                {
                                    OnTextChanged = args => { newTaskText = args.NewText; return Task.CompletedTask; }
                                },
                                new ButtonWidget("[+]")
                                {
                                    OnClick = _ => { AddTask(); return Task.CompletedTask; }
                                }
                            ]), "Add"),
                            new TextBlockWidget(GetProgressBar(20))
                        ]) { WidthHint = SizeHint.Weighted(1) }
                    ])),

                // Narrow layout (< 60 cols): Single column
                new ConditionalWidget((w, h) => true,
                    new VStackWidget([
                        new TextBlockWidget($"📋 Todo [{completedCount}/{totalCount}]"),
                        new TextBlockWidget(GetProgressBar(width - 4)),
                        new ListWidget(listItems)
                        {
                            HeightHint = SizeHint.Fill,
                            OnSelectionChanged = args => { OnSelectionChanged(args); return Task.CompletedTask; },
                            OnItemActivated = _ => { ToggleSelected(); return Task.CompletedTask; }
                        },
                        new HStackWidget([
                            new TextBoxWidget(newTaskText)
                            {
                                WidthHint = SizeHint.Fill,
                                OnTextChanged = args => { newTaskText = args.NewText; return Task.CompletedTask; }
                            },
                            new ButtonWidget("[+]")
                            {
                                OnClick = _ => { AddTask(); return Task.CompletedTask; }
                            }
                        ]),
                        new TextBlockWidget("Tab:Focus ↑↓:Nav Space:Toggle")
                    ]))
            ]),
            $"🚀 Hex1b WASM ({width}×{height})"
        ).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Delete).Action(_ => DeleteSelected(), "Delete selected");
        });
    },
    options: new Hex1bAppOptions { Terminal = terminal }
);

// Run the app - this is the standard Hex1b entry point!
await app.RunAsync();

record TodoItem(string Title, bool IsComplete);
