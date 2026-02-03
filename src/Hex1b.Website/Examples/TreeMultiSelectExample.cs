using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Tree Widget Documentation: Multi-Select
/// Demonstrates multi-select with cascade selection behavior.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the multiSelectCode sample in:
/// src/content/guide/widgets/tree.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TreeMultiSelectExample(ILogger<TreeMultiSelectExample> logger) : Hex1bExample
{
    private readonly ILogger<TreeMultiSelectExample> _logger = logger;

    public override string Id => "tree-multi-select";
    public override string Title => "Tree Widget - Multi-Select";
    public override string Description => "Demonstrates multi-select with cascade selection behavior";

    private class SelectionState
    {
        public string SelectedItems { get; set; } = "(none)";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating tree multi-select example widget builder");

        var state = new SelectionState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text($"Selected: {state.SelectedItems}"),
                v.Text(""),
                v.Tree(t => [
                    t.Item("Frontend", fe => [
                        fe.Item("React"),
                        fe.Item("Vue"),
                        fe.Item("Angular")
                    ]).Expanded(),
                    t.Item("Backend", be => [
                        be.Item("Node.js"),
                        be.Item("Python"),
                        be.Item("Go")
                    ]).Expanded()
                ])
                .MultiSelect()
                .OnSelectionChanged(e =>
                {
                    var selected = e.SelectedItems.Select(i => i.Label);
                    state.SelectedItems = selected.Any() 
                        ? string.Join(", ", selected) 
                        : "(none)";
                })
            ]);
        };
    }
}
