using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: Kanban Board with Drag and Drop
//
// A 3-column Kanban board where task cards can be dragged between columns.
// Demonstrates: Draggable sources, Droppable targets, Drop targets for
// positional insertion, context-driven rendering, and animated Surface widgets.

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
var dropTargetMode = DropTargetMode.ActiveOnly;
var rainbowTimer = System.Diagnostics.Stopwatch.StartNew();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            // Header
            v.HStack(h => [
                h.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                    h.Text(" ◆ Kanban Board — Drag & Drop Demo  ")),
                h.Text(" Drop Targets: "),
                h.ToggleSwitch(["Off", "Active Only", "Always Visible"], (int)dropTargetMode)
                    .OnSelectionChanged(e => { dropTargetMode = (DropTargetMode)e.SelectedIndex; }),
                h.Text("").Fill(),
            ]),
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
                dc.VStack(v =>
                {
                    var items = new List<Hex1bWidget>();

                    // Column header
                    items.Add(v.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, columnColor),
                        v.Text(headerText)));
                    items.Add(v.Separator());

                    // Drop target before first card
                    if (dropTargetMode != DropTargetMode.Off)
                    {
                        items.Add(dc.DropTarget("pos-0", dt =>
                            BuildDropTargetIndicator(dt)));
                    }

                    // Cards interleaved with drop targets
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        items.Add(BuildTaskCard(v, tasks[i]));
                        if (dropTargetMode != DropTargetMode.Off)
                        {
                            items.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                                BuildDropTargetIndicator(dt)));
                        }
                    }

                    // Empty space filler
                    items.Add(v.Text("").Fill());

                    return [.. items];
                })
            )
        );
    })
    .Accept(data => data is KanbanTask)
    .OnDropTarget(e =>
    {
        if (e.DragData is KanbanTask task)
        {
            // Remove from source column
            foreach (var col in columns.Values)
                col.Remove(task);

            // Insert at the position indicated by the drop target
            var posIndex = int.Parse(e.TargetId.Split('-')[1]);
            posIndex = Math.Min(posIndex, tasks.Count);
            tasks.Insert(posIndex, task);
            lastAction = $" Moved \"{task.Title}\" to {columnName} at position {posIndex}";
        }
    })
    .OnDrop(e =>
    {
        // Fallback: append to end when no drop target is active
        if (e.DragData is KanbanTask task)
        {
            foreach (var col in columns.Values)
                col.Remove(task);
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
        // When dragging, show an empty placeholder to give the sense of physical movement
        if (dc.IsDragging)
        {
            return dc.ThemePanel(
                t => t
                    .Set(BorderTheme.BorderColor, Hex1bColor.FromRgb(60, 60, 60))
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                dc.Border(
                    dc.VStack(v => [
                        v.Text($" ┄┄┄┄┄┄┄┄┄┄┄┄┄┄"),
                        v.Text($"   "),
                        v.Text($"   "),
                    ])
                )
            );
        }

        return BuildCardContent(dc, task);
    })
    .DragOverlay(dc => BuildCardContent(dc, task));
}

Hex1bWidget BuildCardContent<T>(WidgetContext<T> ctx, KanbanTask task) where T : Hex1bWidget
{
    return ctx.ThemePanel(
        t => t
            .Set(BorderTheme.BorderColor, Hex1bColor.DarkGray)
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(20, 20, 30)),
        ctx.Border(
            ctx.VStack(v => [
                v.Text($" {task.Title}"),
                v.ThemePanel(
                    t => t.Set(GlobalTheme.ForegroundColor, task.CategoryColor),
                    v.Text($"   [{task.Category}]")),
                v.Surface(s =>
                {
                    var phase = rainbowTimer.Elapsed.TotalSeconds * 0.15;
                    var tint = GetTintRgb(task.Category);
                    var angle = GetWaveAngle(task.Category);
                    return [s.Layer(surface => RenderTintedWave(surface, phase % 1.0, tint.r, tint.g, tint.b, angle))];
                })
                .Height(SizeHint.Fixed(6))
                .RedrawAfter(100),
            ])
        )
    );
}

void RenderTintedWave(Surface surface, double phase, double tr, double tg, double tb, double angle)
{
    var cosA = Math.Cos(angle);
    var sinA = Math.Sin(angle);

    for (int y = 0; y < surface.Height; y++)
    {
        for (int x = 0; x < surface.Width; x++)
        {
            // Project position along the angle direction
            var proj = x * cosA + y * sinA;
            var denom = Math.Max(1.0, surface.Width * Math.Abs(cosA) + surface.Height * Math.Abs(sinA));
            var t = proj / denom;

            // Animated grayscale intensity via overlapping sine waves
            var wave1 = Math.Sin((t + phase) * Math.PI * 4) * 0.5 + 0.5;
            var wave2 = Math.Sin((t * 1.5 - phase * 2) * Math.PI * 3) * 0.3 + 0.5;
            var intensity = Math.Clamp(wave1 * 0.6 + wave2 * 0.4, 0, 1);

            // Raise the floor so darks aren't too strong
            intensity = 0.35 + intensity * 0.65;

            byte r = (byte)(intensity * tr);
            byte g = (byte)(intensity * tg);
            byte b = (byte)(intensity * tb);
            surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(r, g, b));
        }
    }
}

(double r, double g, double b) GetTintRgb(string category) => category switch
{
    "UI" => (80, 220, 255),
    "DevOps" => (255, 220, 80),
    "Testing" => (80, 255, 120),
    "Backend" => (255, 100, 220),
    _ => (180, 180, 220),
};

double GetWaveAngle(string category) => category switch
{
    "UI" => 0.6,        // ~34° diagonal down-right
    "DevOps" => -0.4,   // ~-23° diagonal up-right
    "Testing" => 1.2,   // ~69° mostly vertical
    "Backend" => -0.8,  // ~-46° steep diagonal
    _ => 0.0,           // horizontal
};

Hex1bWidget BuildDropTargetIndicator(DropTargetContext dt)
{
    if (dt.IsActive)
        return dt.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
            dt.Text(" ─── insert here ───"));

    if (dropTargetMode == DropTargetMode.AlwaysVisible)
        return dt.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(60, 60, 60)),
            dt.Text(" ─── ─── ─── ───"));

    return dt.Text("");
}

record KanbanTask(string Id, string Title, string Category, Hex1bColor CategoryColor);

enum DropTargetMode { Off, ActiveOnly, AlwaysVisible }
