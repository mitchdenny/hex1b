using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// A responsive todo list that adapts its layout based on available terminal width.
/// Demonstrates the ResponsiveWidget with different layouts for different sizes.
/// </summary>
public class ResponsiveTodoExhibit(ILogger<ResponsiveTodoExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<ResponsiveTodoExhibit> _logger = logger;

    public override string Id => "responsive-todo";
    public override string Title => "Responsive Todo";
    public override string Description => "Todo list that adapts layout based on terminal size.";

    /// <summary>
    /// State for the todo list exhibit.
    /// </summary>
    private class TodoState
    {
        public List<TodoItem> Items { get; } =
        [
            new("Buy groceries", true),
            new("Review pull request", false),
            new("Write documentation", false),
            new("Fix bug #42", true),
            new("Deploy to staging", false),
            new("Team standup meeting", true),
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

        public void DeleteSelected()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Items.Count)
            {
                Items.RemoveAt(SelectedIndex);
                if (SelectedIndex >= Items.Count && Items.Count > 0)
                {
                    SelectedIndex = Items.Count - 1;
                }
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

    private record TodoItem(string Title, bool IsComplete);

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating responsive todo widget builder");

        var state = new TodoState();

        return () =>
        {
            var ctx = new RootContext();
            var listItems = state.GetListItems();

            // Build responsive layout that adapts to available width
            var widget = ctx.Responsive(r => [
                // Extra wide layout (150+ cols): Three columns with stats
                r.WhenMinWidth(150, r => BuildExtraWideLayout(r, state, listItems)),
                
                // Wide layout (110+ cols): Two columns with details sidebar
                r.WhenMinWidth(110, r => BuildWideLayout(r, state, listItems)),
                
                // Medium layout (70+ cols): Single column with full details
                r.WhenMinWidth(70, r => BuildMediumLayout(r, state, listItems)),
                
                // Compact layout (< 70 cols): Minimal single column
                r.Otherwise(r => BuildCompactLayout(r, state, listItems))
            ]);

            return widget;
        };
    }

    /// <summary>
    /// Extra wide layout: Three columns - list, details, and statistics.
    /// </summary>
    private static Hex1bWidget BuildExtraWideLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
    {
        var completedCount = state.Items.Count(i => i.IsComplete);
        var totalCount = state.Items.Count;
        var todoCount = totalCount - completedCount;
        
        return ctx.HStack(h => [
            // Left: Todo list
            h.Border(b => [
                b.Text("📋 Todo Items"),
                b.Text(""),
                b.List(listItems, e => state.SelectedIndex = e.SelectedIndex, _ => state.ToggleSelected()),
                b.Text(""),
                b.Text("↑↓ Navigate  Space: Toggle")
            ], title: "Tasks").FillWidth(2),
            
            // Middle: Add new item
            h.Border(b => [
                b.Text("➕ Add New Task"),
                b.Text(""),
                b.TextBox(state.NewItemText, args => state.NewItemText = args.NewText),
                b.Text(""),
                b.Button("Add Task", _ => state.AddItem()),
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

    /// <summary>
    /// Wide layout: Two columns - list and details/add panel.
    /// </summary>
    private static Hex1bWidget BuildWideLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
    {
        var completedCount = state.Items.Count(i => i.IsComplete);
        var totalCount = state.Items.Count;
        
        return ctx.HStack(h => [
            // Left: Todo list
            h.Border(b => [
                b.Text("📋 Todo Items"),
                b.Text(""),
                b.List(listItems, e => state.SelectedIndex = e.SelectedIndex, _ => state.ToggleSelected()),
                b.Text(""),
                b.Text("↑↓ Nav  Space: Toggle")
            ], title: "Tasks").FillWidth(2),
            
            // Right: Add + Stats combined
            h.VStack(v => [
                v.Border(b => [
                    b.Text("➕ Add Task"),
                    b.TextBox(state.NewItemText, args => state.NewItemText = args.NewText),
                    b.Button("Add", _ => state.AddItem())
                ], title: "New"),
                v.Border(b => [
                    b.Text($"Done: {completedCount}/{totalCount}"),
                    b.Text(GetProgressBar(state))
                ], title: "Progress").FillHeight()
            ]).FillWidth(1)
        ]);
    }

    /// <summary>
    /// Medium layout: Single column with all features visible.
    /// </summary>
    private static Hex1bWidget BuildMediumLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
    {
        var completedCount = state.Items.Count(i => i.IsComplete);
        var totalCount = state.Items.Count;
        
        return ctx.VStack(v => [
            v.Border(b => [
                b.Text("📋 Responsive Todo List"),
                b.Text($"[{completedCount}/{totalCount} complete]")
            ], title: "Todo"),
            
            v.Border(b => [
                b.List(listItems, e => state.SelectedIndex = e.SelectedIndex, _ => state.ToggleSelected())
            ], title: "Items").FillHeight(),
            
            v.HStack(h => [
                h.TextBox(state.NewItemText, args => state.NewItemText = args.NewText).FillWidth(),
                h.Button("[+]", _ => state.AddItem())
            ]),
            
            v.Text("↑↓:Move  Space:Toggle  Tab:Focus")
        ]);
    }

    /// <summary>
    /// Compact layout: Minimal display for narrow terminals.
    /// </summary>
    private static Hex1bWidget BuildCompactLayout(WidgetContext<ConditionalWidget> ctx, TodoState state, IReadOnlyList<string> listItems)
    {
        var completedCount = state.Items.Count(i => i.IsComplete);
        var totalCount = state.Items.Count;
        
        return ctx.VStack(v => [
            v.Text($"Todo [{completedCount}/{totalCount}]"),
            v.Text("────────────────────"),
            v.List(listItems, e => state.SelectedIndex = e.SelectedIndex, _ => state.ToggleSelected()).FillHeight(),
            v.Text("────────────────────"),
            v.TextBox(state.NewItemText, args => state.NewItemText = args.NewText),
            v.Button("+ Add", _ => state.AddItem())
        ]);
    }

    private static string GetProgressBar(TodoState state)
    {
        if (state.Items.Count == 0) return "[          ] 0%";
        
        var percent = state.Items.Count(i => i.IsComplete) * 100 / state.Items.Count;
        var filled = percent / 10;
        var bar = new string('█', filled) + new string('░', 10 - filled);
        return $"[{bar}] {percent}%";
    }
}
