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

// Mini smart matter particle state for animated surface inside cards
var ghostParticles = new GhostParticle[40];
var ghostRandom = new Random(42);
for (int i = 0; i < ghostParticles.Length; i++)
{
    ghostParticles[i] = new GhostParticle
    {
        X = ghostRandom.NextDouble() * 40,
        Y = ghostRandom.NextDouble() * 8,
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
var rainbowTimer = System.Diagnostics.Stopwatch.StartNew();

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

(byte r, byte g, byte b) HsvToRgb(double h, double s, double v)
{
    var hi = (int)(h * 6) % 6;
    var f = h * 6 - Math.Floor(h * 6);
    var p = v * (1 - s);
    var q = v * (1 - f * s);
    var t = v * (1 - (1 - f) * s);

    var (rd, gd, bd) = hi switch
    {
        0 => (v, t, p),
        1 => (q, v, p),
        2 => (p, v, t),
        3 => (p, q, v),
        4 => (t, p, v),
        _ => (v, p, q),
    };
    return ((byte)(rd * 255), (byte)(gd * 255), (byte)(bd * 255));
}

// Mini smart matter particle functions
void UpdateGhostParticles(GhostParticle[] particles, Random rng, int width, int height)
{
    int dotW = width * 2;
    int dotH = height * 4;

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

void RenderGhostParticles(Surface surface, GhostParticle[] particles)
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
}

record KanbanTask(string Id, string Title, string Category, Hex1bColor CategoryColor);

struct GhostParticle
{
    public double X, Y, Vx, Vy, Brightness;
}
