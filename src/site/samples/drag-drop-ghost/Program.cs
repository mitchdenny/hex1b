using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var tasks = new List<string> { "Design UI", "Write tests", "Deploy" };

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text(" Drag Ghost Demo"),
        v.Separator(),
        ..tasks.Select(task =>
            v.Draggable(task, dc =>
                dc.ThemePanel(
                    t => t.Set(BorderTheme.BorderColor,
                        dc.IsDragging
                            ? Hex1bColor.FromRgb(60, 60, 60)
                            : Hex1bColor.White),
                    dc.Border(dc.Text($" {task}"))))
            // Ghost overlay follows the cursor during drag
            .DragOverlay(dc =>
                dc.ThemePanel(
                    t => t.Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
                    dc.Border(dc.Text($" 📋 {task}"))))
        )
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
