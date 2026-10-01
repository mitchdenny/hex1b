using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

var items = new List<string> { "Apple", "Banana", "Cherry", "Date" };
string? lastAction = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v => [
        v.Text(" Drag & Drop Demo"),
        v.Separator(),

        v.HStack(h => [
            // Source list
            h.Border(b => [
                b.VStack(sv => [
                    sv.Text(" Fruits"),
                    sv.Separator(),
                    ..items.Select(item =>
                        sv.Draggable(item, dc =>
                            dc.Text(dc.IsDragging ? " ┄┄┄┄┄" : $" {item}"))
                    )
                ])
            ]).Fill(),

            // Drop target
            h.Droppable(dc => dc.Border(b => [
                b.VStack(dv => [
                    dv.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            dc.IsHoveredByDrag ? Hex1bColor.Green : Hex1bColor.White),
                        dv.Text(dc.IsHoveredByDrag ? " ← Drop here!" : " Drop Zone")),
                    dv.Separator(),
                    dv.Text(lastAction ?? " Drag a fruit here"),
                ])
            ]))
            .OnDrop(e => lastAction = $" Received: {e.DragData}")
            .Fill(),
        ]).Fill(),
    ]))
    .WithMouse()
    .Build();

await terminal.RunAsync();
