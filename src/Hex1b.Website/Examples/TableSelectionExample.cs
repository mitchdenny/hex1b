using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Table Widget Documentation: Selection Column
/// Demonstrates multi-select with checkboxes and select all/deselect all.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the selectionCode sample in:
/// src/content/guide/widgets/table.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TableSelectionExample(ILogger<TableSelectionExample> logger) : Hex1bExample
{
    private readonly ILogger<TableSelectionExample> _logger = logger;

    public override string Id => "table-selection";
    public override string Title => "Table Widget - Selection Column";
    public override string Description => "Demonstrates multi-select with checkboxes";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating table selection example widget builder");

        var items = new List<SelectableItem>
        {
            new("Task 1", false),
            new("Task 2", true),
            new("Task 3", false),
            new("Task 4", false),
            new("Task 5", true)
        };

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
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
            ], title: "Task List with Selection");
        };
    }

    private class SelectableItem(string name, bool isComplete)
    {
        public string Name { get; } = name;
        public bool IsComplete { get; set; } = isComplete;
    }
}
