using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drag &amp; Drop Documentation: Basic Usage
/// Demonstrates basic draggable sources and droppable targets.
/// </summary>
public class DragDropBasicExample(ILogger<DragDropBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<DragDropBasicExample> _logger = logger;

    public override string Id => "drag-drop-basic";
    public override string Title => "Drag & Drop - Basic Usage";
    public override string Description => "Demonstrates basic draggable sources and droppable targets";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drag-drop basic example widget builder");

        var items = new List<string> { "Apple", "Banana", "Cherry", "Date" };
        string? lastAction = null;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
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
            ]);
        };
    }
}
