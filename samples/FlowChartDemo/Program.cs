using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

// Demo: Flow Chart Builder with Drag and Drop
//
// A canvas where flow chart nodes can be reordered by dragging.
// Demonstrates the "reposition within container" pattern:
// items are both Draggable AND inside a Droppable surface.
// The OnDrop handler reorders nodes based on drop position.

var nodes = new List<FlowNode>
{
    new("start", "Start", Hex1bColor.Green, "○"),
    new("validate", "Validate Input", Hex1bColor.Cyan, "▢"),
    new("decision", "Is Valid?", Hex1bColor.Yellow, "◇"),
    new("process", "Process Data", Hex1bColor.Cyan, "▢"),
    new("error", "Show Error", Hex1bColor.Red, "▢"),
    new("end", "End", Hex1bColor.Green, "○"),
};

string? lastAction = null;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            // Header
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.Cyan),
                v.Text(" ◆ Flow Chart Builder — Drag & Drop Demo")),
            v.Text(" Drag nodes to reorder them in the flow"),
            v.Separator(),

            // Canvas: single Droppable surface containing Draggable nodes
            v.Droppable(dc =>
            {
                var borderColor = dc.IsHoveredByDrag
                    ? (dc.CanAcceptDrag ? Hex1bColor.Blue : Hex1bColor.Red)
                    : Hex1bColor.DarkGray;

                return dc.ThemePanel(
                    t => t.Set(BorderTheme.BorderColor, borderColor),
                    dc.Border(
                        dc.VStack(col => [
                            // Render flow nodes in order with arrows between them
                            ..nodes.SelectMany((node, index) =>
                            {
                                var items = new List<Hex1bWidget>();
                                if (index > 0)
                                {
                                    items.Add(col.ThemePanel(
                                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                                        col.Text("       │")));
                                    items.Add(col.ThemePanel(
                                        t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                                        col.Text("       ▼")));
                                }
                                items.Add(BuildFlowNode(col, node));
                                return items;
                            }),
                            col.Text("").Fill(),
                        ])
                    )
                );
            })
            .Accept(data => data is FlowNode)
            .OnDrop(e =>
            {
                if (e.DragData is FlowNode draggedNode)
                {
                    // Calculate target index from drop Y position
                    // Each node takes 3 rows (arrow + arrow + node), first node is 1 row
                    var targetIndex = Math.Max(0, Math.Min(nodes.Count - 1, e.LocalY / 3));
                    var currentIndex = nodes.IndexOf(draggedNode);

                    if (currentIndex >= 0 && currentIndex != targetIndex)
                    {
                        nodes.RemoveAt(currentIndex);
                        nodes.Insert(Math.Min(targetIndex, nodes.Count), draggedNode);
                        lastAction = $" Moved \"{draggedNode.Label}\" to position {targetIndex + 1}";
                    }
                }
            })
            .Fill(),

            // Footer
            v.Separator(),
            v.ThemePanel(
                t => t.Set(GlobalTheme.ForegroundColor, Hex1bColor.DarkGray),
                v.Text(lastAction ?? " Drag a flow node to reorder it in the flow")),
        ]);
    })
    .WithMouse()
    .Build();

await terminal.RunAsync();

Hex1bWidget BuildFlowNode(
    WidgetContext<VStackWidget> parent,
    FlowNode node)
{
    return parent.Draggable(node, dc =>
    {
        var borderColor = dc.IsDragging ? Hex1bColor.White : node.Color;
        var textColor = dc.IsDragging ? Hex1bColor.DarkGray : Hex1bColor.White;
        var indicator = dc.IsDragging ? "↕" : node.ShapeIndicator;

        return dc.ThemePanel(
            t => t
                .Set(GlobalTheme.ForegroundColor, textColor)
                .Set(BorderTheme.BorderColor, borderColor),
            dc.Border(
                dc.HStack(h => [
                    h.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor, node.Color),
                        h.Text($" {indicator} ")),
                    h.Text(node.Label),
                    h.Text(" "),
                ])
            )
        );
    });
}

record FlowNode(string Id, string Label, Hex1bColor Color, string ShapeIndicator);

