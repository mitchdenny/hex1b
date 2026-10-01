using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

// Sample data with selection state
var items = new List<SelectableItem>
{
    new("Task 1", false),
    new("Task 2", true),
    new("Task 3", false),
    new("Task 4", false),
    new("Task 5", true)
};

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.Border(b => [
        b.VStack(v => [
            v.Table(items)
                .Header(h => [
                    h.Cell("Task").Width(SizeHint.Fill),
                    h.Cell("Status").Width(SizeHint.Fixed(12))
                ])
                .Row((r, item, state) => [
                    r.Cell(item.Name),
                    r.Cell(item.IsComplete ? "✓ Done" : "Pending")
                ])
                .SelectionColumn(
                    item => item.IsComplete,
                    (item, selected) => item.IsComplete = selected
                )
                .OnSelectAll(() => items.ForEach(i => i.IsComplete = true))
                .OnDeselectAll(() => items.ForEach(i => i.IsComplete = false)),
            v.Text(""),
            v.Text($"Completed: {items.Count(i => i.IsComplete)} / {items.Count}")
        ])
    ]).Title("Task List with Selection"))
    .Build();

await terminal.RunAsync();

class SelectableItem(string name, bool isComplete)
{
    public string Name { get; } = name;
    public bool IsComplete { get; set; } = isComplete;
}
