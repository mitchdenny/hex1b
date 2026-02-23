using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Drag &amp; Drop Documentation: Drop Targets
/// Demonstrates positional insertion with drop targets.
/// </summary>
public class DragDropTargetExample(ILogger<DragDropTargetExample> logger) : Hex1bExample
{
    private readonly ILogger<DragDropTargetExample> _logger = logger;

    public override string Id => "drag-drop-target";
    public override string Title => "Drag & Drop - Drop Targets";
    public override string Description => "Demonstrates positional insertion with drop targets";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating drag-drop target example widget builder");

        var items = new List<string> { "First", "Second", "Third" };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text(" Drop Targets — Positional Insertion"),
                v.Separator(),

                // Draggable source
                v.Draggable("New Item", dc =>
                    dc.Text(dc.IsDragging ? " ┄┄┄" : " ⊕ New Item"))
                    .DragOverlay(dc => dc.Border(dc.Text(" ⊕ New Item"))),
                v.Text(""),

                // Droppable list with insertion points
                v.Droppable(dc => dc.Border(b => [
                    b.VStack(sv =>
                    {
                        var widgets = new List<Hex1bWidget>();

                        // Drop target before first item
                        widgets.Add(dc.DropTarget("pos-0", dt =>
                            dt.IsActive
                                ? dt.ThemePanel(
                                    t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                                    dt.Text(" ─── insert here ───"))
                                : dt.Text("").Height(SizeHint.Fixed(0))));

                        // Items interleaved with drop targets
                        for (int i = 0; i < items.Count; i++)
                        {
                            widgets.Add(sv.Text($" • {items[i]}"));
                            widgets.Add(dc.DropTarget($"pos-{i + 1}", dt =>
                                dt.IsActive
                                    ? dt.ThemePanel(
                                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Green),
                                        dt.Text(" ─── insert here ───"))
                                    : dt.Text("").Height(SizeHint.Fixed(0))));
                        }

                        return [.. widgets];
                    })
                ]))
                .OnDropTarget(e =>
                {
                    var pos = int.Parse(e.TargetId.Split('-')[1]);
                    pos = Math.Min(pos, items.Count);
                    items.Insert(pos, $"Item {items.Count + 1}");
                })
                .OnDrop(e => items.Add($"Item {items.Count + 1}"))
                .Fill(),
            ]);
        };
    }
}
