using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: Kanban Board with Drag and Drop
//
// A 3-column Kanban board where task cards can be dragged between columns.
// Demonstrates: Draggable sources, Droppable targets, Accept predicates,
// context-driven rendering, and OnDrop event handling.

var columns = new Dictionary<string, List<KanbanTask>>
{
    ["To Do"] = [
        new("task-1", "Design login page", "UI", Hex1bColor.Cyan),
        new("task-2", "Set up CI pipeline", "DevOps", Hex1bColor.Yellow),
        new("task-3", "Write unit tests", "Testing", Hex1bColor.Green),
    ],
    ["In Progress"] = [
        new("task-4", "Implement auth API", "Backend", Hex1bColor.Magenta),
    ],
    ["Done"] = [
        new("task-5", "Project setup", "DevOps", Hex1bColor.Yellow),
    ],
};

string? lastAction = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            // Header
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text(" ◆ Kanban Board — Drag & Drop Demo")),
            v.Text(" Drag task cards between columns with the mouse"),
            v.Separator(),

            // Board: 3 columns side by side
            v.HStack(h => [
                ..columns.Select(kvp => BuildColumn(h, kvp.Key, kvp.Value))
            ]).Fill(),

            // Footer showing last action
            v.Separator(),
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                v.Text(lastAction ?? " Drag a task card to another column")),
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

Hex1bWidget BuildColumn(
    WidgetContext<HStackWidget> parent,
    string columnName,
    List<KanbanTask> tasks)
{
    var columnColor = columnName switch
    {
        "To Do" => Hex1bColor.Blue,
        "In Progress" => Hex1bColor.Yellow,
        "Done" => Hex1bColor.Green,
        _ => Hex1bColor.White,
    };

    return parent.Droppable(dc =>
    {
        var borderColor = dc.IsHoveredByDrag
            ? (dc.CanAcceptDrag ? Hex1bColor.Green : Hex1bColor.Red)
            : columnColor;

        var headerText = dc.IsHoveredByDrag && dc.CanAcceptDrag
            ? $" {columnName} ← Drop here! "
            : $" {columnName} ({tasks.Count}) ";

        return dc.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, borderColor),
            dc.Border(
                dc.VStack(v => [
                    // Column header
                    v.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, columnColor),
                        v.Text(headerText)),
                    v.Separator(),

                    // Task cards
                    ..tasks.Select(task => BuildTaskCard(v, task)),

                    // Empty space filler
                    v.Text("").Fill(),
                ])
            )
        );
    })
    .Accept(data => data is KanbanTask)
    .OnDrop(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            // Remove from source column
            foreach (var col in columns.Values)
                col.Remove(task);

            // Add to target column
            tasks.Add(task);
            lastAction = $" Moved \"{task.Title}\" to {columnName}";
        }
    })
    .Fill();
}

Hex1bWidget BuildTaskCard(
    WidgetContext<VStackWidget> parent,
    KanbanTask task)
{
    return parent.Draggable(task, dc =>
    {
        var borderColor = dc.IsDragging ? Hex1bColor.Cyan : Hex1bColor.DarkGray;
        var textColor = dc.IsDragging ? Hex1bColor.DarkGray : Hex1bColor.White;

        return dc.ThemePanel(
            t => t
                .Set(BorderTheme.BorderColor, borderColor)
                .Set(GlobalTheme.ForegroundColor, textColor),
            dc.Border(
                dc.VStack(v => [
                    v.Text(dc.IsDragging ? $" ↕ {task.Title}" : $" {task.Title}"),
                    v.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, task.CategoryColor),
                        v.Text($"   [{task.Category}]")),
                ])
            )
        );
    });
}

record KanbanTask(string Id, string Title, string Category, Hex1bColor CategoryColor);
