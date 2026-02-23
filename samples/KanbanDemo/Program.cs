using Hex1b;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: Kanban Board with Drag and Drop
//
// A 3-column Kanban board where task cards can be dragged between columns.
// Demonstrates: Draggable sources, Droppable targets, Accept predicates,
// context-driven rendering, and OnDrop event handling.
// One card has a smart matter particle animation on its drag ghost.

// Mini smart matter particle state for the animated ghost
var ghostParticles = new GhostParticle[60];
var ghostRandom = new Random(42);
for (int i = 0; i < ghostParticles.Length; i++)
{
    ghostParticles[i] = new GhostParticle
    {
        X = ghostRandom.NextDouble() * 30,
        Y = 12 - 1 - ghostRandom.NextDouble() * 2,
        Vx = 0,
        Vy = -ghostRandom.NextDouble() * 0.6 - 0.2,
        Brightness = 0.5 + ghostRandom.NextDouble() * 0.5,
    };
}

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
                    ])
                )
            );
        }

        return dc.ThemePanel(
            t => t
                .Set(BorderTheme.BorderColor, Hex1bColor.DarkGray)
                .Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
            dc.Border(
                dc.VStack(v => [
                    v.Text($" {task.Title}"),
                    v.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, task.CategoryColor),
                        v.Text($"   [{task.Category}]")),
                ])
            )
        );
    })
    .DragOverlay(dc =>
    {
        if (task.Id == "task-1")
        {
            // Smart matter animated ghost for the first task card
            return dc.Surface(s =>
            {
                UpdateGhostParticles(ghostParticles, ghostRandom, s.Width, s.Height);
                return [s.Layer(surface => RenderGhostParticles(surface, ghostParticles, task.Title))];
            })
            .Width(SizeHint.Fixed(20))
            .Height(SizeHint.Fixed(4))
            .RedrawAfter(50);
        }

        // Lightweight ghost that follows the cursor
        return dc.ThemePanel(
            t => t
                .Set(BorderTheme.BorderColor, Hex1bColor.Cyan)
                .Set(GlobalTheme.ForegroundColor, Hex1bColor.White),
            dc.Border(
                dc.Text($" 📋 {task.Title} ")
            )
        );
    });
}

// Mini smart matter particle functions (top-level, before type declarations)
void UpdateGhostParticles(GhostParticle[] particles, Random rng, int width, int height)
{
    int dotW = width * 2;   // 2 dots per cell horizontally (braille)
    int dotH = height * 4;  // 4 dots per cell vertically (braille)

    for (int i = 0; i < particles.Length; i++)
    {
        ref var p = ref particles[i];

        // Rise upward with turbulence
        p.Vy += -0.05 + rng.NextDouble() * 0.02;
        p.Vx += (rng.NextDouble() - 0.5) * 0.3;

        // Dampen
        p.Vx *= 0.9;
        p.Vy *= 0.95;

        p.X += p.Vx;
        p.Y += p.Vy;

        // Wrap around when particles go off screen
        if (p.Y < 0)
        {
            p.Y = dotH - 1;
            p.X = rng.NextDouble() * dotW;
            p.Vy = -rng.NextDouble() * 0.6 - 0.2;
        }
        if (p.X < 0) p.X += dotW;
        if (p.X >= dotW) p.X -= dotW;

        // Pulse brightness
        p.Brightness = 0.5 + 0.5 * Math.Sin(p.Y / 3.0 + p.X / 5.0);
    }
}

void RenderGhostParticles(Surface surface, GhostParticle[] particles, string title)
{
    int dotW = surface.Width * 2;
    int dotH = surface.Height * 4;

    // Dark background
    for (int y = 0; y < surface.Height; y++)
        for (int x = 0; x < surface.Width; x++)
            surface[x, y] = new SurfaceCell(" ", null, Hex1bColor.FromRgb(10, 15, 25));

    // Group particles by cell for braille rendering
    var cells = new Dictionary<(int, int), (int bits, double bright)>();

    foreach (var p in particles)
    {
        int dotX = (int)Math.Round(p.X);
        int dotY = (int)Math.Round(p.Y);
        if (dotX < 0 || dotX >= dotW || dotY < 0 || dotY >= dotH) continue;

        int cellX = dotX / 2;
        int cellY = dotY / 4;
        if (cellX < 0 || cellX >= surface.Width || cellY < 0 || cellY >= surface.Height) continue;

        int localX = dotX % 2;
        int localY = dotY % 4;
        int bit = localY switch
        {
            0 => localX == 0 ? 0x01 : 0x08,
            1 => localX == 0 ? 0x02 : 0x10,
            2 => localX == 0 ? 0x04 : 0x20,
            3 => localX == 0 ? 0x40 : 0x80,
            _ => 0
        };

        var key = (cellX, cellY);
        var cur = cells.GetValueOrDefault(key, (0, 0.0));
        cells[key] = (cur.Item1 | bit, Math.Max(cur.Item2, p.Brightness));
    }

    foreach (var kvp in cells)
    {
        var (cx, cy) = kvp.Key;
        int bits = kvp.Value.Item1;
        double bright = kvp.Value.Item2;
        var ch = (char)(0x2800 + bits);

        byte r = (byte)(80 + bright * 175);
        byte g = (byte)(180 + bright * 75);
        byte b = (byte)(220 + bright * 35);

        var existing = surface[cx, cy];
        surface[cx, cy] = new SurfaceCell(ch.ToString(), Hex1bColor.FromRgb(r, g, b), existing.Background);
    }

    // Overlay title text in the middle row
    int midY = surface.Height / 2;
    var titleText = $" 📋 {title} ";
    for (int i = 0; i < titleText.Length && i < surface.Width; i++)
    {
        var bg = surface[i, midY].Background ?? Hex1bColor.FromRgb(10, 15, 25);
        surface[i, midY] = new SurfaceCell(titleText[i].ToString(), Hex1bColor.White, bg);
    }
}

record KanbanTask(string Id, string Title, string Category, Hex1bColor CategoryColor);

struct GhostParticle
{
    public double X, Y, Vx, Vy, Brightness;
}
