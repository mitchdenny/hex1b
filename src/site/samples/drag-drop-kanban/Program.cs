using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

var columns = new Dictionary<string, List<KanbanTask>>
{
    ["To Do"] = [
        new("1", "Design login page", "UI"),
        new("2", "Set up CI pipeline", "DevOps"),
        new("3", "Write unit tests", "Testing"),
    ],
    ["In Progress"] = [
        new("4", "Implement auth API", "Backend"),
    ],
    ["Done"] = [
        new("5", "Project setup", "DevOps"),
    ],
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text(" ◆ Kanban Board"),
        v.Separator(),
        v.HStack(h => [
            ..columns.Select(kvp => BuildColumn(h, kvp.Key, kvp.Value))
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();

Hex1bWidget BuildColumn(
    WidgetContext<HStackWidget> parent,
    string name,
    List<KanbanTask> tasks)
{
    return parent.Droppable(dc =>
    {
        var color = dc.IsHoveredByDrag && dc.CanAcceptDrag
            ? Hex1bColor.Green : Hex1bColor.White;

        return dc.ThemePanel(
            t => t.Set(BorderTheme.BorderColor, color),
            dc.Border(dc.VStack(v =>
            {
                var items = new List<Hex1bWidget>();
                items.Add(v.Text($" {name} ({tasks.Count})"));
                items.Add(v.Separator());

                items.Add(dc.DropTarget("pos-0", dt =>
                    dt.IsActive
                        ? dt.ThemePanel(
                            t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                            dt.Text(" ─── insert here ───"))
                        : dt.Text("").Height(SizeHint.Fixed(0))));

                for (int i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    items.Add(v.Draggable(task, dc2 =>
                        dc2.IsDragging
                            ? dc2.ThemePanel(
                                t => t.Set(BorderTheme.BorderColor,
                                    Hex1bColor.FromRgb(60, 60, 60)),
                                dc2.Border(dc2.Text(" ┄┄┄")))
                            : dc2.Border(dc2.VStack(cv => [
                                cv.Text($" {task.Title}"),
                                cv.Text($"   [{task.Category}]"),
                            ])))
                        .DragOverlay(dc2 =>
                            dc2.Border(dc2.VStack(cv => [
                                cv.Text($" {task.Title}"),
                                cv.Text($"   [{task.Category}]"),
                            ]))));

                    items.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                        dt.IsActive
                            ? dt.ThemePanel(
                                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                                dt.Text(" ─── insert here ───"))
                            : dt.Text("").Height(SizeHint.Fixed(0))));
                }

                items.Add(v.Text("").Fill());
                return [.. items];
            })));
    })
    .Accept(data => data is KanbanTask)
    .OnDropTarget(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            var srcIdx = tasks.IndexOf(task);
            foreach (var col in columns.Values) col.Remove(task);
            var pos = int.Parse(e.TargetId.Split('-')[1]);
            if (srcIdx >= 0 && pos > srcIdx) pos--;
            pos = Math.Min(pos, tasks.Count);
            tasks.Insert(pos, task);
        }
    })
    .OnDrop(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            foreach (var col in columns.Values) col.Remove(task);
            tasks.Add(task);
        }
    })
    .Fill();
}

record KanbanTask(string Id, string Title, string Category);
