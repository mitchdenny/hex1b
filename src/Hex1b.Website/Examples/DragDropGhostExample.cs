using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drag &amp; Drop Documentation: Ghost Overlay
/// Demonstrates drag ghost overlays that follow the cursor.
/// </summary>
public class DragDropGhostExample(ILogger<DragDropGhostExample> logger) : Hex1bExample
{
    private readonly ILogger<DragDropGhostExample> _logger = logger;

    public override string Id => "drag-drop-ghost";
    public override string Title => "Drag & Drop - Ghost Overlay";
    public override string Description => "Demonstrates drag ghost overlays that follow the cursor";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drag-drop ghost example widget builder");

        var tasks = new List<string> { "Design UI", "Write tests", "Deploy" };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
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
                    .DragOverlay(dc =>
                        dc.ThemePanel(
                            t => t.Set(BorderTheme.BorderColor, Hex1bColor.Cyan),
                            dc.Border(dc.Text($" 📋 {task}"))))
                )
            ]);
        };
    }
}
