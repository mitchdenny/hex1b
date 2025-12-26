using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// List Widget Documentation: Selection Changed
/// Demonstrates handling selection changes with OnSelectionChanged.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the selectionCode sample in:
/// src/content/guide/widgets/list.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ListSelectionExample(ILogger<ListSelectionExample> logger) : Hex1bExample
{
    private readonly ILogger<ListSelectionExample> _logger = logger;

    public override string Id => "list-selection";
    public override string Title => "List Widget - Selection Changed";
    public override string Description => "Demonstrates handling selection changes with OnSelectionChanged";

    private class ListSelectionState
    {
        public string? SelectedItem { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating list selection example widget builder");

        var state = new ListSelectionState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text($"Selected: {state.SelectedItem ?? "None"}"),
                    v.Text(""),
                    v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                        .OnSelectionChanged(e => state.SelectedItem = e.SelectedText)
                ])
            ], title: "Selection Demo");
        };
    }
}
